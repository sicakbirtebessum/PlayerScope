using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.Database;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData;

namespace PlayerScope.Handlers;

internal sealed unsafe class GameHooks : IDisposable
{
    private readonly ILogger<GameHooks> _logger;
    private readonly PersistenceContext _persistenceContext;

    /// <summary>
    /// Processes the content id to character name packet, seen e.g. when you hover an item to retrieve the
    /// crafter's signature.
    /// </summary>
    private delegate int CharacterNameResultDelegate(nint a1, ulong contentId, char* playerName);

    private delegate nint SocialListResultDelegate(nint a1, nint dataPtr);

#pragma warning disable CS0649
    [Signature("40 53 48 83 EC 20 48 8B  D9 33 C9 45 33 C9", DetourName = nameof(ProcessCharacterNameResult))]
    private Hook<CharacterNameResultDelegate> CharacterNameResultHook { get; init; } = null!;

    // Signature adapted from https://github.com/LittleNightmare/UsedName
    [Signature("48 89 5C 24 10 56 48 83 EC 20 48 ?? ?? ?? ?? ?? ?? 48 8B F2 E8 ?? ?? ?? ?? 48 8B D8",
        DetourName = nameof(ProcessSocialListResult))]
    private Hook<SocialListResultDelegate> SocialListResultHook { get; init; } = null!;

#pragma warning restore CS0649

    public GameHooks(ILogger<GameHooks> logger, PersistenceContext persistenceContext,
        IGameInteropProvider gameInteropProvider)
    {
        _logger = logger;
        _persistenceContext = persistenceContext;

        _logger.LogDebug("Initializing game hooks");
        gameInteropProvider.InitializeFromAttributes(this);
        CharacterNameResultHook.Enable();
        SocialListResultHook.Enable();
       
        _logger.LogDebug("Game hooks initialized");
    }

    private int ProcessCharacterNameResult(nint a1, ulong contentId, char* playerName)
    {
        try
        {
            var mapping = new PlayerMapping
            {
                ContentId = contentId,
                AccountId = null,
                PlayerName = MemoryHelper.ReadString(new nint(playerName), Encoding.ASCII, 32),
            };

            if (!string.IsNullOrEmpty(mapping.PlayerName))
            {
                _logger.LogTrace("Content id {ContentId} belongs to '{Name}'", mapping.ContentId,
                    mapping.PlayerName);
                if (mapping.PlayerName.IsValidCharacterName(true))
                {
                    var playerRequest = new PostPlayerRequest
                    {
                        LocalContentId = contentId,
                        Name = mapping.PlayerName,
                        AccountId = (int?)mapping.AccountId,
                        HomeWorldId = null,
                        CurrentWorldId = (ushort?)PersistenceContext.GetCurrentWorld(),
                        CreatedAt = Tools.UnixTime,
                    };

                    Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(new List<PlayerMapping> { mapping }));
                    PersistenceContext.AddPlayerUploadData(new List<PostPlayerRequest> { playerRequest });
                }
            }
            else
            {
                _logger.LogDebug("Content id {ContentId} didn't resolve to a player name, ignoring",
                    mapping.ContentId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not process character name result");
        }

        return CharacterNameResultHook.Original(a1, contentId, playerName);
    }

    private nint ProcessSocialListResult(nint a1, nint dataPtr)
    {
        try
        {
            var result = Marshal.PtrToStructure<SocialListResultPage>(dataPtr);
            List<PlayerMapping> mappings = new();
            List<PostPlayerRequest> playerRequests = new();
            foreach (SocialListPlayer player in result.PlayerSpan)
            {
                if (player.ContentId == 0)
                    continue;

                ushort? homeWorldId = player.HomeWorldID != 0 && player.HomeWorldID != 65535 ? player.HomeWorldID : null;

                var mapping = new PlayerMapping
                {
                    ContentId = player.ContentId,
                    AccountId = player.AccountId != 0 ? player.AccountId : null,
                    PlayerName = MemoryHelper.ReadString(new nint(player.CharacterName), Encoding.ASCII, 32),
                    WorldId = homeWorldId,
                };

                if (!string.IsNullOrEmpty(mapping.PlayerName))
                {
                    mappings.Add(mapping);
                    playerRequests.Add(new PostPlayerRequest
                    {
                        LocalContentId = mapping.ContentId,
                        Name = mapping.PlayerName,
                        AccountId = (int?)mapping.AccountId,
                        HomeWorldId = mapping.WorldId,
                        CurrentWorldId = (ushort?)PersistenceContext.GetCurrentWorld(),
                        TerritoryId = player.TerritoryId == 0 ? (short?)null : (short)player.TerritoryId,
                        CreatedAt = Tools.UnixTime,
                    });
                }
                else
                {
                    //_logger.LogDebug("Content id {ContentId} didn't resolve to a player name, ignoring", mapping.ContentId);
                }
            }

            if (mappings.Count > 0)
                Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(mappings));
            
            if (playerRequests.Count > 0)
                PersistenceContext.AddPlayerUploadData(playerRequests);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not process social list result");
        }

        return SocialListResultHook.Original(a1, dataPtr);
    }

    public void Dispose()
    {
        CharacterNameResultHook.Dispose();
        SocialListResultHook.Dispose();
    }

    /// <summary>
    /// There are some caveats here, the social list includes a LOT of things with different types
    /// (we don't care for the result type in this plugin), see sapphire for which field is the type.
    ///
    /// 1 = party
    /// 2 = friend list
    /// 3 = link shell
    /// 4 = player search
    /// 5 = fc short list (first tab, with company board + actions + online members)
    /// 6 = fc long list (members tab)
    ///
    /// Both 1 and 2 are sent to you on login, unprompted.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x420)]
    internal struct SocialListResultPage
    {
        [FieldOffset(0x10)] private fixed byte Players[10 * 0x70];

        public Span<SocialListPlayer> PlayerSpan => new(Unsafe.AsPointer(ref Players[0]), 10);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x70, Pack = 1)]
    internal struct SocialListPlayer
    {
        /// <summary>
        /// If this is set, it means there is a player present in this slot (even if no name can be retrieved),
        /// 0 if empty.
        /// </summary>
        [FieldOffset(0x00)] public readonly ulong ContentId;

        /// <summary>
        /// Only seems to be set for certain kind of social lists, e.g. friend list/FC members doesn't include any.
        /// </summary>
        [FieldOffset(0x18)] public readonly ulong AccountId;

        [FieldOffset(0x24)] public ushort TerritoryId;
        [FieldOffset(0x28)] public GrandCompany GrandCompanyId;
        [FieldOffset(0x29)] public Language ClientLanguage;
        [FieldOffset(0x2A)] public LanguageMask Languages;
        [FieldOffset(0x2B)] public byte HasSearchComment;
        [FieldOffset(0x30)] public ulong OnlineStatusBytes;
        [FieldOffset(0x38)] public byte CurrentJobId;
        [FieldOffset(0x3A)] public ushort CurrentJobLevel;
        [FieldOffset(0x42)] public ushort HomeWorldID;

        /// <summary>
        /// This *can* be empty, e.g. if you're querying your friend list, the names are ONLY set for characters on the same world.
        /// </summary>
        [FieldOffset(0x44)] public fixed byte CharacterName[32];
        [FieldOffset(0x64)] private fixed byte FcTagBytes[7];
    }
}
