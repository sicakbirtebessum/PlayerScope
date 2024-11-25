using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayerScope.API;
using PlayerScope.API.Models;
using PlayerScope.Database;
using PlayerScope.GUI;
using static FFXIVClientStructs.Havok.Animation.Deform.Skinning.hkaMeshBinding;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static PlayerScope.Handlers.PersistenceContext;

namespace PlayerScope.Handlers;

internal sealed class PersistenceContext
{
    public static ILogger<PersistenceContext> _logger;
    public static IClientState _clientState;
    public static IServiceProvider _serviceProvider;
    public static readonly ConcurrentDictionary<uint, ConcurrentDictionary<string, ulong>> _worldRetainerCache = new();
    public static readonly ConcurrentDictionary<ulong, CachedPlayer> _playerCache = new();
    public static readonly ConcurrentDictionary<ulong, List<ulong>> _AccountIdCache = new();

    public static ConcurrentDictionary<ulong, (CachedPlayer Player, List<Retainer> Retainers)> _playerWithRetainersCache = new();

    public static ConcurrentDictionary<ulong, Retainer> _retainerCache = new(); 

    public static ConcurrentDictionary<ulong, PostPlayerRequest> _UploadPlayers = new();  //will be uploaded
    public static ConcurrentDictionary<ulong, PostPlayerRequest> _UploadedPlayersCache = new(); //Already uploaded

    public static ConcurrentDictionary<ulong, PostRetainerRequest> _UploadRetainers = new();  //will be uploaded
    public static ConcurrentDictionary<ulong, PostRetainerRequest> _UploadedRetainersCache = new(); //Already uploaded


    private static PersistenceContext _instance = null;
    public static PersistenceContext Instance
    {
        get
        {
            return _instance;
        }
    }

    public PersistenceContext(ILogger<PersistenceContext> logger, IClientState clientState,
        IServiceProvider serviceProvider, IDataManager data)
    {
        if (_instance == null)
        {
            _instance = this;
        }

        _logger = logger;
        _clientState = clientState;
        _serviceProvider = serviceProvider;

        ReloadCache();

        _cancellationTokenSource = new CancellationTokenSource();
        _ = PostPlayerAndRetainerData(_cancellationTokenSource.Token);
    }
    public static void ReloadCache()
    {
        using (IServiceScope scope = _serviceProvider.CreateScope())
        {
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
            var retainersByWorld = dbContext.Retainers.GroupBy(retainer => retainer.WorldId);

            foreach (var retainers in retainersByWorld)
            {
                var world = _worldRetainerCache.GetOrAdd(retainers.Key, _ => new());
                foreach (var retainer in retainers)
                {
                    if (retainer.Name != null)
                    {
                        world[retainer.Name] = retainer.OwnerLocalContentId;
                        _retainerCache[retainer.LocalContentId] = retainer;
                    }
                }
            }

            foreach (var player in dbContext.Players)
            {
                _playerCache[player.LocalContentId] = new CachedPlayer
                {
                    AccountId = player.AccountId,
                    Name = player.Name ?? string.Empty,
                };
            }
        }
    }

   public static void UpdateRetainers()
    {
        foreach (var player in _playerCache)
        {
            if (_playerCache.TryGetValue(player.Key, out CachedPlayer _GetPlayer))
            {
                _playerWithRetainersCache.GetOrAdd(player.Key, _ => (_GetPlayer, new List<Retainer>() { }));
            }

            if (player.Value.AccountId != null)
            {
                var _GetAccountsCache = _AccountIdCache.TryGetValue((ulong)player.Value.AccountId, out var AccountContentIds);
                if (_GetAccountsCache )
                {
                    if (!AccountContentIds.Contains(player.Key))
                    {
                        AccountContentIds.Add(player.Key);
                    }
                }
                else
                {
                    _AccountIdCache[(ulong)player.Value.AccountId] = new List<ulong> { player.Key };
                }
            }
        }

        foreach (var retainer in _retainerCache.Values)
        {
            if (_playerCache.TryGetValue(retainer.OwnerLocalContentId, out CachedPlayer _GetPlayer))
            {
                var player = _playerWithRetainersCache.GetOrAdd(retainer.OwnerLocalContentId, _ => (_GetPlayer, new List<Retainer>() { retainer }));

                if (!player.Item2.Contains(retainer))
                {
                    player.Item2.Add(retainer);
                }
            }
            else
            {
                var player = _playerWithRetainersCache.GetOrAdd(retainer.OwnerLocalContentId, _ => (new CachedPlayer { Name = "-", AccountId = null}, new List<Retainer>() { retainer }));

                if (!player.Item2.Contains(retainer))
                {
                    player.Item2.Add(retainer);
                }
            }
        }
    }

    private const int CacheExpirationTimeInSeconds = 215; // 3.30 minutes
    public static bool AnamnesisFound;

    private static bool HasDataChanged<T>(T request, T cachedRequest) where T : class
    {
        switch (request)
        {
            case PostPlayerRequest playerRequest when cachedRequest is PostPlayerRequest cachedPlayer:
                return playerRequest.Name != cachedPlayer.Name ||
                       (playerRequest.AccountId.HasValue && !cachedPlayer.AccountId.HasValue) ||
                       playerRequest.TerritoryId != cachedPlayer.TerritoryId ||
                       playerRequest.HomeWorldId != cachedPlayer.HomeWorldId ||
                       playerRequest.CurrentWorldId != cachedPlayer.CurrentWorldId;

            case PostRetainerRequest retainerRequest when cachedRequest is PostRetainerRequest cachedRetainer:
                return retainerRequest.Name != cachedRetainer.Name ||
                       retainerRequest.WorldId != cachedRetainer.WorldId;

            default:
                throw new InvalidOperationException("Unsupported type for data change check");
        }
    }

    private static void UpdateCacheIfNeeded<T>(
        ulong id,
        T request,
        ConcurrentDictionary<ulong, T> uploadList,
        ConcurrentDictionary<ulong, T> cache
    ) where T : class
    {
        if (cache.TryGetValue(id, out var cachedRequest))
        {
            if (Tools.UnixTime - GetCreatedAt(cachedRequest) > CacheExpirationTimeInSeconds)
            {
                cache.TryRemove(id, out _);
            }
            else if (!HasDataChanged(request, cachedRequest))
            {
                return;
            }
        }
        uploadList[id] = request;
        cache[id] = request;
    }
    private static int GetCreatedAt<T>(T request)
    {
        return request switch
        {
            PostPlayerRequest player => player.CreatedAt,
            PostRetainerRequest retainer => retainer.CreatedAt,
            _ => throw new InvalidOperationException("Unsupported type")
        };
    }

    public static void AddPlayerUploadData(IEnumerable<PostPlayerRequest> requests)
    {
        foreach (var request in requests)
        {
            UpdateCacheIfNeeded(request.LocalContentId, request, _UploadPlayers, _UploadedPlayersCache);
        }
    }

    public static void AddRetainerUploadData(IEnumerable<PostRetainerRequest> requests)
    {
        foreach (var request in requests)
        {
            UpdateCacheIfNeeded(request.LocalContentId, request, _UploadRetainers, _UploadedRetainersCache);
        }
    }

    public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    public static async Task PostPlayerAndRetainerData(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (!_UploadPlayers.IsEmpty)
                {
                    await ProcessPlayerUploadBatch(cancellationToken).ConfigureAwait(false);
                }
                while (!_UploadRetainers.IsEmpty)
                {
                    await ProcessRetainerUploadBatch(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PostPlayerAndRetainerData was canceled.");
        }
        catch (Exception e)
        {
            _logger.LogWarning("Could not post " + e.Message);
        }
    }
    public static void StopUploads()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _logger.LogInformation("Upload tasks have been canceled.");
        }
    }
    private static async Task ProcessPlayerUploadBatch(CancellationToken cancellationToken,
    int batchSize = 200,
    int maxRetries = 3)
    {
        if (_UploadPlayers.IsEmpty) return;
        if (cancellationToken.IsCancellationRequested)
            return;

        var itemsToUpload = _UploadPlayers.Take(batchSize).Select(kvp => kvp.Value).ToList();
        //_logger.LogInformation($"Uploading {itemsToUpload.Count} Player items. TotalCount: {_UploadPlayers.Count}");

        int retryCount = 0;
        bool uploadSuccess = false;

        while (!_cancellationTokenSource.IsCancellationRequested && !_UploadPlayers.IsEmpty && !uploadSuccess && retryCount < maxRetries)
        {
            if (await ApiClient.Instance.PostPlayers(itemsToUpload).ConfigureAwait(false))
            {
                foreach (var item in itemsToUpload)
                {
                    var key = GetKey(item);
                    _UploadPlayers.TryRemove(key, out _);
                    _UploadedPlayersCache[key] = item;
                }
                //_logger.LogInformation("Player upload successful, items added to cache.");
                uploadSuccess = true;
            }
            else
            {
                retryCount++;
                _logger.LogWarning($"Player upload attempt {retryCount} failed. Retrying...");

                try
                {
                    await Task.Delay(1500 * retryCount, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Upload process canceled during delay.");
                    return;
                }
            }
        }

        if (!uploadSuccess)
        {
            _logger.LogError("Player upload failed after multiple attempts, items could not be uploaded.");
        }

        //_logger.LogInformation("ProcessPlayerUploadBatch completed.");

        if (!_UploadPlayers.IsEmpty)
        {
            //_logger.LogInformation("Waiting before next Player upload attempt as there are still items in the list.");
            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                //_logger.LogInformation("Upload process canceled during final delay.");
                return;
            }
        }
    }

    private static async Task ProcessRetainerUploadBatch(CancellationToken cancellationToken,
    int batchSize = 200,
    int maxRetries = 3
)
    {
        if (_UploadRetainers.IsEmpty) return;
        if (cancellationToken.IsCancellationRequested)
            return;

        var itemsToUpload = _UploadRetainers.Take(batchSize).Select(kvp => kvp.Value).ToList();
        //_logger.LogInformation($"Uploading {itemsToUpload.Count} Retainer items. TotalCount: {_UploadRetainers.Count}");

        int retryCount = 0;
        bool uploadSuccess = false;

        while (!cancellationToken.IsCancellationRequested && !_UploadRetainers.IsEmpty && !uploadSuccess && retryCount < maxRetries)
        {
            if (await ApiClient.Instance.PostRetainers(itemsToUpload).ConfigureAwait(false))
            {
                foreach (var item in itemsToUpload)
                {
                    var key = GetKey(item);
                    _UploadRetainers.TryRemove(key, out _);
                    _UploadedRetainersCache[key] = item;
                }
                //_logger.LogInformation("Retainer upload successful, items added to cache.");
                uploadSuccess = true;
            }
            else
            {
                retryCount++;
                _logger.LogWarning($"Retainer upload attempt {retryCount} failed. Retrying...");

                try
                {
                    await Task.Delay(1500 * retryCount, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Upload process canceled during delay.");
                    return;
                }
            }
        }

        if (!uploadSuccess)
        {
            _logger.LogError("Retainer upload failed after multiple attempts, items could not be uploaded.");
        }

        //_logger.LogInformation("ProcessRetainerUploadBatch completed.");

        if (!_UploadRetainers.IsEmpty)
        {
            //_logger.LogInformation("Waiting before next Retainer upload attempt as there are still items in the list.");
            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                //_logger.LogInformation("Upload process canceled during final delay.");
                return;
            }
        }
    }

    private static ulong GetKey<T>(T request)
    {
        return request switch
        {
            PostPlayerRequest player => player.LocalContentId,
            PostRetainerRequest retainer => retainer.LocalContentId,
            _ => throw new InvalidOperationException("Unsupported type")
        };
    }

    public static uint? GetCurrentWorld()
    {
        uint currentWorld = _clientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (currentWorld == 0)
            return null;
        return currentWorld;
    }

    public static string GetCharacterNameOnCurrentWorld(string retainerName)
    {
        uint currentWorld = _clientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (currentWorld == 0)
            return string.Empty;
        
        var currentWorldCache = _worldRetainerCache.GetOrAdd(currentWorld, _ => new());
        if (!currentWorldCache.TryGetValue(retainerName, out ulong playerContentId))
            return string.Empty;

        return _playerCache.TryGetValue(playerContentId, out CachedPlayer? cachedPlayer)
            ? cachedPlayer.Name
            : string.Empty;
    }

    public IReadOnlyList<string> GetRetainerNamesForCharacter(string characterName, uint world)
    {
        using var scope = _serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        return dbContext.Players.Where(p => characterName == p.Name)
            .SelectMany(player =>
                dbContext.Retainers.Where(x => x.OwnerLocalContentId == player.LocalContentId && x.WorldId == world))
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<string> GetAllAccountNamesForCharacter(ulong playerContentId)
    {
        using var scope = _serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        return dbContext.Players.Where(p => playerContentId == p.LocalContentId)
            .SelectMany(player =>
                dbContext.Players.Where(x => x.AccountId == player.AccountId && player.AccountId != null))
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList()
            .AsReadOnly();
    }

    public async Task HandleMarketBoardPage(List<Retainer> retainers)
    {
        try
        {
            var updates = retainers
                    .DistinctBy(o => o.LocalContentId)
                    .Where(l => l.LocalContentId != 0)
                    .Where(l => l.OwnerLocalContentId != 0)
                    .Where(mapping =>
                    {
                        if (mapping.Name == null)
                            return true;

                        var currentWorldCache = _worldRetainerCache.GetOrAdd(mapping.WorldId, _ => new());
                        if (currentWorldCache.TryGetValue(mapping.Name, out ulong playerContentId))
                            return mapping.OwnerLocalContentId != playerContentId;

                        return true;
                    })
                    .DistinctBy(x => x.LocalContentId)
                    .ToList();

            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();

            foreach (var retainer in updates)
            {
                Retainer? dbRetainer = dbContext.Retainers.Find(retainer.LocalContentId);
                if (dbRetainer != null)
                {
                    _logger.LogDebug("Updating retainer {RetainerName} with {LocalContentId}", retainer.Name,
                        retainer.LocalContentId);
                    dbRetainer.Name = retainer.Name;
                    dbRetainer.WorldId = retainer.WorldId;
                    dbRetainer.OwnerLocalContentId = retainer.OwnerLocalContentId;
                    dbContext.Retainers.Update(dbRetainer);
                }
                else
                {
                    //_logger.LogDebug("Adding retainer {RetainerName} with {LocalContentId}", retainer.Name, retainer.LocalContentId);
                    dbContext.Retainers.Add(retainer);
                }

                string ownerName;
                if (_playerCache.TryGetValue(retainer.OwnerLocalContentId, out CachedPlayer? cachedPlayer))
                    ownerName = cachedPlayer.Name;
                else
                    ownerName = retainer.OwnerLocalContentId.ToString(CultureInfo.InvariantCulture);
                //_logger.LogDebug("  Retainer {RetainerName} belongs to {OwnerName}", retainer.Name, ownerName);

                if (retainer.Name != null)
                {
                    var world = _worldRetainerCache.GetOrAdd(retainer.WorldId, _ => new());
                    world[retainer.Name] = retainer.OwnerLocalContentId;
                    _retainerCache[retainer.LocalContentId] = retainer;
                }
            }

            int changeCount = dbContext.SaveChanges();
            if (changeCount > 0)
                _logger.LogDebug("Saved {Count} retainer mappings", changeCount);

            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not persist retainer info from market board page");
        }
    }

    private void HandleContentIdMappingFallback(PlayerMapping mapping)
    {
        try
        {
            if (mapping.ContentId == 0 || string.IsNullOrEmpty(mapping.PlayerName))
                return;

            if (_playerCache.TryGetValue(mapping.ContentId, out CachedPlayer? cachedPlayer))
            {
                if (mapping.PlayerName == cachedPlayer.Name && mapping.AccountId == cachedPlayer.AccountId)
                    return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
                var dbPlayer = dbContext.Players.Find(mapping.ContentId);
                if (dbPlayer == null)
                    dbContext.Players.Add(new Player
                    {
                        LocalContentId = mapping.ContentId,
                        Name = mapping.PlayerName,
                        AccountId = mapping.AccountId,
                    });
                else
                {
                    dbPlayer.Name = mapping.PlayerName;
                    dbPlayer.AccountId ??= mapping.AccountId;
                    dbContext.Entry(dbPlayer).State = EntityState.Modified;
                }

                int changeCount = dbContext.SaveChanges();
                if (changeCount > 0)
                {
                    //_logger.LogDebug("Saved fallback player mappings for {ContentId} / {Name} / {AccountId}", mapping.ContentId, mapping.PlayerName, mapping.AccountId);
                }

                _playerCache[mapping.ContentId] = new CachedPlayer
                {
                    AccountId = mapping.AccountId,
                    Name = mapping.PlayerName,
                };
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not persist singular mapping for {ContentId} / {Name} / {AccountId}",
                mapping.ContentId, mapping.PlayerName, mapping.AccountId);
        }
    }

    public static SemaphoreSlim processPlayers = new SemaphoreSlim(1, 999);
    public async Task HandleContentIdMappingAsync(IReadOnlyList<PlayerMapping> mappings)
    {
        
        var updates = mappings.DistinctBy(x => x.ContentId)
            .Where(mapping => mapping.ContentId != 0 && !string.IsNullOrEmpty(mapping.PlayerName))
            .Where(mapping =>
            {
                if (_playerCache.TryGetValue(mapping.ContentId, out CachedPlayer? cachedPlayer))
                {
                    if (mapping.PlayerName != cachedPlayer.Name)
                    {
                        _logger.LogInformation($"Player name updated: {cachedPlayer.Name} > {mapping.PlayerName} [{mapping.ContentId}]");
                        return true;
                    }

                    if (mapping.AccountId != null)
                    {
                        if (mapping.AccountId != cachedPlayer.AccountId)
                        {
                            _logger.LogInformation($"Player AccountId added: {mapping.PlayerName} - AccId:[{mapping.AccountId}] CId:[{mapping.ContentId}]");
                            return true;
                        }
                    }
                    return false;
                }

                return true;
            })
            .ToList();
        if (updates.Count == 0)
            return;

        await processPlayers.WaitAsync().ConfigureAwait(false);

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
                foreach (var update in updates)
                {
                    var dbPlayer = dbContext.Players.Find(update.ContentId);
                    if (dbPlayer == null)
                        dbContext.Players.Add(new Player
                        {
                            LocalContentId = update.ContentId,
                            Name = update.PlayerName,
                            AccountId = update.AccountId,
                        });
                    else
                    {
                        dbPlayer.Name = update.PlayerName;
                        dbPlayer.AccountId ??= update.AccountId;
                        dbContext.Entry(dbPlayer).State = EntityState.Modified;
                    }
                }

                int changeCount = await dbContext.SaveChangesAsync();
                if (changeCount > 0)
                {
                    // foreach (var update in updates)
                    //_logger.LogDebug("  {ContentId} = {Name} ({AccountId})", update.ContentId, update.PlayerName,  update.AccountId);

                    _logger.LogDebug("Saved {Count} player mappings", changeCount);
                }
            }

            foreach (var player in updates)
            {
                _playerCache[player.ContentId] = new CachedPlayer
                {
                    AccountId = player.AccountId,
                    Name = player.PlayerName,
                };
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not persist multiple mappings, attempting non-batch update");
            foreach (var update in updates)
            {
                HandleContentIdMappingFallback(update);
            }
        }

        processPlayers.Release();
    }

    public class CachedPlayer
    {
        public required ulong? AccountId { get; init; }
        public required string Name { get; init; }
    }
}