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
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
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

    public List<TerritoryType> Territories;

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

        Territories = PlayerScopePlugin._dataManager.GetExcelSheet<TerritoryType>().ToList();

        _ = PostPlayerAndRetainerData();
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

    public static void AddRetainerUploadData(ulong CId, PostRetainerRequest request)
    {
        _ = _UploadRetainers.TryGetValue(CId, out var _GetUploadRetainer);
        _ = _UploadedRetainersCache.TryGetValue(CId, out var _GetUploadedCacheRetainer); //Check if uploaded before

        if (_GetUploadedCacheRetainer != null)
        {
            bool removed = false;
            if (Tools.UnixTime - _GetUploadedCacheRetainer.CreatedAt > 215) //3.30minutes
            {
                _UploadedRetainersCache.TryRemove(CId, out _);
                removed = true;
            }

            if (!removed)
            {
                bool changed = false;
                if (request.Name != _GetUploadedCacheRetainer.Name) changed = true;
                else if (request.WorldId != _GetUploadedCacheRetainer.WorldId) changed = true;

                if (!changed)
                    return;

                _UploadRetainers[CId] = request;
                _UploadedRetainersCache[CId] = request;
            }
            else
            {
                _UploadRetainers[CId] = request;
                _UploadedRetainersCache[CId] = request;
            }
        }
        else if (_GetUploadRetainer != null)
        {
            bool changed = false;
            if (request.Name != _GetUploadRetainer.Name) changed = true;
            else if (request.WorldId != _GetUploadRetainer.WorldId) changed = true;

            if (!changed)
                return;

            _UploadRetainers[CId] = request;
        }
        else if (_GetUploadRetainer == null && _GetUploadedCacheRetainer == null)
        {
            _UploadRetainers[CId] = request;
        }
    }
    public static void AddPlayerUploadData(ulong CId, PostPlayerRequest request)
    {
        _ = _UploadPlayers.TryGetValue(CId, out var UploadPlayer);
        _ = _UploadedPlayersCache.TryGetValue(CId, out var UploadedCachePlayer); //Check if uploaded before
        if (UploadedCachePlayer != null)
        {
            bool removed = false;
            if (Tools.UnixTime - UploadedCachePlayer.CreatedAt > 215) //3.30minutes
            {
                _UploadedRetainersCache.TryRemove(CId, out _);
                removed = true;
            }
            if (!removed)
            {
                bool changed = false;
                if (request.Name != UploadedCachePlayer.Name) changed = true;
                else if (request.AccountId != null && UploadedCachePlayer.AccountId == null) changed = true;
                else if (request.TerritoryId != null && UploadedCachePlayer.TerritoryId == null) changed = true;
                else if (request.HomeWorldId != null && UploadedCachePlayer.HomeWorldId == null) changed = true;
                else if (request.CurrentWorldId != null && UploadedCachePlayer.CurrentWorldId == null) changed = true;
                if (!changed)
                    return;

                _UploadPlayers[CId] = request;
                _UploadedPlayersCache[CId] = request;
            }
            else
            {
                _UploadPlayers[CId] = request;
                _UploadedPlayersCache[CId] = request;
            }
        }
        else if (UploadPlayer != null) 
        {
            bool changed = false;
            if (request.Name != UploadPlayer.Name) changed = true;
            else if (request.AccountId != null && UploadPlayer.AccountId == null) changed = true;
            else if (request.TerritoryId != null && UploadPlayer.TerritoryId == null) changed = true;
            else if (request.HomeWorldId != null && UploadPlayer.HomeWorldId == null) changed = true;
            else if (request.CurrentWorldId != null && UploadPlayer.CurrentWorldId == null) changed = true;
            if (!changed)
                return;

            _UploadPlayers[CId] = request;
        }
        else if (UploadPlayer == null && UploadedCachePlayer == null) 
        {
            _UploadPlayers[CId] = request;
        }
    }
    public static bool AnamnesisFound;
    public static async Task PostPlayerAndRetainerData()
    {
        int takeCount = 200;
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
            post:

                if (!PersistenceContext._UploadPlayers.IsEmpty)
                {
                    if (Process.GetProcessesByName("Anamnesis").Length != 0)
                        AnamnesisFound = true;

                    var TakeUploadPlayers = PersistenceContext._UploadPlayers.Take(takeCount).Select(p =>
                    new PostPlayerRequest
                    {
                        Name = p.Value.Name,
                        LocalContentId = p.Key,
                        AccountId = p.Value.AccountId,
                        HomeWorldId = p.Value.HomeWorldId,
                        CurrentWorldId = p.Value.CurrentWorldId,
                        Customization = AnamnesisFound ? null : p.Value.Customization,
                        PlayerPos = p.Value.PlayerPos,
                        TerritoryId = p.Value.TerritoryId,
                        CreatedAt = p.Value.CreatedAt,
                    }).ToList();

                    var request = ApiClient.Instance.PostPlayers(TakeUploadPlayers).ConfigureAwait(false).GetAwaiter().GetResult();
                    foreach (var uploadedPlayer in TakeUploadPlayers)
                    {
                        _UploadPlayers.Remove(uploadedPlayer.LocalContentId, out _);
                        _UploadedPlayersCache[uploadedPlayer.LocalContentId] = uploadedPlayer;
                    }
                    Thread.Sleep(3000);
                    goto post;
                }
                if (!_UploadRetainers.IsEmpty)
                {
                    var TakeUploadRetainers = PersistenceContext._UploadRetainers.Take(takeCount).Select(p =>
                    new PostRetainerRequest
                    {
                        LocalContentId = p.Value.LocalContentId,
                        Name = p.Value.Name,
                        OwnerLocalContentId = p.Value.OwnerLocalContentId,
                        WorldId = p.Value.WorldId,
                        CreatedAt = p.Value.CreatedAt,
                    }).ToList();

                    var request = ApiClient.Instance.PostRetainers(TakeUploadRetainers).ConfigureAwait(false).GetAwaiter().GetResult();
                    foreach (var uploadedRetainer in TakeUploadRetainers)
                    {
                        _UploadRetainers.Remove(uploadedRetainer.LocalContentId, out _);
                        _UploadedRetainersCache[uploadedRetainer.LocalContentId] = uploadedRetainer;
                    }
                    Thread.Sleep(3000);
                    goto post;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning("Could not post " + e.Message);
        }
    }

    public static uint? GetCurrentWorld()
    {
        uint currentWorld = _clientState.LocalPlayer?.CurrentWorld.Id ?? 0;
        if (currentWorld == 0)
            return null;
        return currentWorld;
    }

    public static string GetCharacterNameOnCurrentWorld(string retainerName)
    {
        uint currentWorld = _clientState.LocalPlayer?.CurrentWorld.Id ?? 0;
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

    public void HandleMarketBoardPage(List<Retainer> retainers)
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