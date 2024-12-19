using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Collections.Concurrent;
using PlayerScope.Handlers;
using static PlayerScope.Handlers.PersistenceContext;
using PlayerScope.API;
using PlayerScope.API.Models;
using System.Threading;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using System.Text.RegularExpressions;
using PlayerScope.API.Query;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Xml.Linq;
using PlayerScope.Properties;
using Dalamud;
using System.Drawing;
using Dalamud.Interface.ImGuiNotification;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PlayerScope.Database;
using Microsoft.EntityFrameworkCore;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Microsoft.VisualBasic;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using PlayerScope.GUI.MainWindowTab;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Dalamud.Interface.Textures.TextureWraps;

namespace PlayerScope.GUI
{
    public class MainWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeMain";
        public MainWindow() : base(WindowId, ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            UpdateWindowTitle();
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(770, 545),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            TitleBarButtons.Add(new()
            {
                Click = (m) => { if (m == ImGuiMouseButton.Left) SettingsWindow.Instance.IsOpen = true; },
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 2),
                ShowTooltip = () => ImGui.SetTooltip(Loc.MnOpenSettingsMenu),
            });
        }

        private static MainWindow _instance = null;
        public static MainWindow Instance
        {
            get
            {
                return _instance;
            }
        }
        private void UpdateWindowTitle()
            => WindowName = $"{Loc.TitleMainMenu}{WindowId}";

        public void LanguageChanged()
        {
            UpdateWindowTitle();

            TableColumn = new string[]
            {
            Loc.MnName, Loc.MnAccountCount,Loc.MnRetainerCount, Loc.MnRetainers, Loc.MnContentId,Loc.MnAccountId
            };

            FromServerTableSearchPlayersColumn = new string[]
            {
            "Details", "##Avatar",Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAccountId, Loc.MnContentId
            };

            FromServerTableSearchRetainersColumn = new string[]
            {
            Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAddedAt, Loc.MnOwnerContentId, Loc.MnContentId
            };

            WorldsTableColumn = new string[]
            {
            Loc.MnWorld, "DataCenter", Loc.MnTotalRetainers
            };

            FavoritedPlayersColumn = new string[]
            {
           Loc.MnName, Loc.MnContentId, Loc.MnAccountId, Loc.MnAddNote, Loc.MnRemove
            };

            PlayerAndRetainersWorldStatsColumn = new string[]
            {
            Loc.StHomeWorldName, Loc.StCharacterCountColumn, Loc.StRetainerCountColumn
            };
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        public override void OnOpen()
        {
            base.OnOpen();
            ReloadMainWindowStats();
        }

        public static void ReloadMainWindowStats()
        {
            UpdateStatisticsValues();
            Task.Run(GetServerRetainersCount);
            Task.Run(UpdateRetainers);
        }

        private string[] TableColumn = new string[]
        {
            Loc.MnName, Loc.MnAccountCount,Loc.MnRetainerCount, Loc.MnRetainers, Loc.MnContentId,Loc.MnAccountId
        };

        private string[] FromServerTableSearchPlayersColumn = new string[]
        {
            "Details", "##Avatar",Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAccountId, Loc.MnContentId
        };

        private string[] FromServerTableSearchRetainersColumn = new string[]
        {
            Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAddedAt, Loc.MnOwnerContentId, Loc.MnContentId
        };

        private string[] WorldsTableColumn = new string[]
        {
            Loc.MnWorld, "DataCenter", Loc.MnTotalRetainers
        };

        private string[] FavoritedPlayersColumn = new string[]
        {
            Loc.MnName, Loc.MnContentId, Loc.MnAccountId, Loc.MnAddNote, Loc.MnRemove
        };

        public enum Tabs
        {
            General,
            SearchCharactersAndRetainers
        }

        public string _searchContent = "...";

        public static int _TotalPlayers_Value = 0;
        public static int _TotalRetainers_Value = 0;

        public long LastUnix = 0;

        public int TablePlayerMaxLimit = 100;

        public Tabs _CurrentTab = Tabs.General;

        public static void UpdateStatisticsValues()
        {
            _TotalPlayers_Value = PersistenceContext._playerCache.Count;
            _TotalRetainers_Value = PersistenceContext._retainerCache.Count;
        }

        unsafe public void OpenAdventurePlate(ulong ContentId)
        {
            try
            {
                AgentCharaCard.Instance()->OpenCharaCard(ContentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        string MyFavoritesTabTitle = string.Empty;

        public override void Draw()
        {
            using (var tabBar = ImRaii.TabBar("TabsMain"))
            {
                if (tabBar)
                {
                    using (var tabItem = ImRaii.TabItem(Loc.MnTabSearchCharacterAndRetainer))
                    {
                        if (tabItem)
                        {
                            _CurrentTab = Tabs.SearchCharactersAndRetainers;
                            DrawSearchPlayersAndRetainers_FromServerTab();
                        }
                    }
                    
                    using (var tabItem = ImRaii.TabItem(MyFavoritesTabTitle))
                    {
                        if (tabItem)
                        {
                            DrawMyFavoriesTab();
                        }
                        else
                        {
                            MyFavoritesTabTitle = Config.FavoritedPlayer.Count > 0
                               ? $"{Loc.MnTabMyFavorites} ({Config.FavoritedPlayer.Count})"
                               : Loc.MnTabMyFavorites;
                        }
                    }
                    using (var tabItem = ImRaii.TabItem(Loc.MnDatabaseStats))
                    {
                        if (tabItem)
                        {
                            DrawStatisticsTab();
                        }
                    }
                }
            }
        }

        void DrawStatisticsTab()
        {
            using (var tabBar = ImRaii.TabBar("TabStatistics"))
            {
                if (tabBar)
                {
                    using (var tabServer = ImRaii.TabItem(Loc.MnTabServerStats))
                    {
                        if (tabServer)
                        {
                            DrawServerStatsTab();
                        }
                    }
                    using (var tabLocal = ImRaii.TabItem(Loc.MnTabLocalStats))
                    {
                        if (tabLocal)
                        {
                            DrawLocalStatisticsTab();
                        }
                    }
                }
            }
        }

        private async void DrawServerStatsTab()
        {
            using (ImRaii.Disabled(!Config.LoggedIn))
            using (var tabBar = ImRaii.TabBar("ServerDbTabs"))
            {
                if (tabBar)
                {
                    using (var tabItem = ImRaii.TabItem(Loc.StGeneralStats))
                    {
                        if (tabItem)
                        {
                            DrawGeneralStats();
                        }
                    }
                    using (var tabItem = ImRaii.TabItem(Loc.StCharacterAndRetainerSummary))
                    {
                        if (tabItem)
                        {
                            DrawCharacterAndRetainerStats();
                        }
                    }
                }
            }
        }
        private string[] PlayerAndRetainersWorldStatsColumn = new string[]
        {
            Loc.StHomeWorldName, Loc.StCharacterCountColumn, Loc.StRetainerCountColumn
        };

        bool IsRefreshStatsRequestSent = false;
        bool IsRefreshStatsRequestSentForPlayerAndRetainer;
        public string _LastServerStatsMessage = string.Empty;
        private int _LastServerStatsRefreshTime = 0;
        public string LastPlayerAndRetainerWorldStatsMessage = string.Empty;
        private int _lastPlayerAndRetainerWorldRefreshTime = 0;
        private bool bPlayerAndRetainerWorldsConverted;

        public (ServerStatsDto ServerStats, string Message) CheckServerStats()
        {
            if (!bIsNetworkProcessing)
            {
                _ = Task.Run(() =>
                {
                    bIsNetworkProcessing = true;

                    var request = _client.CheckServerStats().ConfigureAwait(false).GetAwaiter().GetResult();
                    _LastServerStatsMessage = request.Message;
                    _LastServerStatsRefreshTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

                    bIsNetworkProcessing = false;
                    return request;
                });
            }
            return (null, string.Empty);
        }
        public (ServerPlayerAndRetainerStatsDto Stats, string Message) CheckPlayerAndRetainerStats()
        {
            if (!bIsNetworkProcessing)
            {
                _ = Task.Run(() =>
                {
                    bIsNetworkProcessing = true;

                    var request = _client.GetPlayerAndRetainerCountStats().ConfigureAwait(false).GetAwaiter().GetResult();
                    LastPlayerAndRetainerWorldStatsMessage = request.Message;
                    _lastPlayerAndRetainerWorldRefreshTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                    ConvertLastPlayerAndRetainerWorlds();

                    bIsNetworkProcessing = false;
                    return request;
                });
            }
            return (null, string.Empty);
        }

        private ConcurrentDictionary<World, (int PlayerCount, int RetainerCount, long LastUpdated)> ConvertedPlayerAndRetainerWorldCounts = new();

        public void ConvertLastPlayerAndRetainerWorlds()
        {
            if (_client.LastPlayerAndRetainerCountStats.Stats == null)
                return;

            var stats = _client.LastPlayerAndRetainerCountStats.Stats;
            foreach (var playerWorldStat in stats.PlayerWorldStats)
            {
                var world = Utils.GetWorld((uint)playerWorldStat.WorldId);
                if (world == null)
                    continue;

                ConvertedPlayerAndRetainerWorldCounts.AddOrUpdate((World)world,
                    _ => (playerWorldStat.Count, 0, stats.LastUpdate),
                    // If exists
                    (_, existing) => (playerWorldStat.Count, existing.RetainerCount, stats.LastUpdate));
            }

            foreach (var retainerWorldStat in stats.RetainerWorldStats)
            {
                var world = Utils.GetWorld((uint)retainerWorldStat.WorldId);
                if (world == null)
                    continue;

                ConvertedPlayerAndRetainerWorldCounts.AddOrUpdate((World)world,
                    _ => (0, retainerWorldStat.Count, stats.LastUpdate),
                    // If exists
                    (_, existing) => (existing.PlayerCount, retainerWorldStat.Count, stats.LastUpdate));
            }

            bPlayerAndRetainerWorldsConverted = true;
        }

        private void DrawCharacterAndRetainerStats()
        {
            if (_client.LastPlayerAndRetainerCountStats.Stats == null)
            {
                IsRefreshStatsRequestSentForPlayerAndRetainer = true;
                CheckPlayerAndRetainerStats();
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            using (ImRaii.Disabled(bIsNetworkProcessing))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.StRefreshDatabaseWorldCount))
                {
                    bPlayerAndRetainerWorldsConverted = false;
                    CheckPlayerAndRetainerStats();
                }
            }

            if (!string.IsNullOrWhiteSpace(LastPlayerAndRetainerWorldStatsMessage))
            {
                ImGui.SameLine();
                Utils.ColoredErrorTextWrapped($"{LastPlayerAndRetainerWorldStatsMessage} ({DateTimeOffset.FromUnixTimeSeconds(_lastPlayerAndRetainerWorldRefreshTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)_lastPlayerAndRetainerWorldRefreshTime)})");
            }

            ImGui.NewLine();

            Utils.TextWrapped(Loc.StCharacterAndRetainerWorldCountMsg);

            ImGui.NewLine();

            if (_client.LastPlayerAndRetainerCountStats.Stats == null)
                return;

            if (!bPlayerAndRetainerWorldsConverted)
                ConvertLastPlayerAndRetainerWorlds();

            if (ConvertedPlayerAndRetainerWorldCounts.IsEmpty)
                return;

            foreach (var playerRegionGroup in ConvertedPlayerAndRetainerWorldCounts
                .GroupBy(WorldCount => Utils.GetRegionLongName(WorldCount.Key))
                .OrderByDescending(group => group.Sum(playerWorld => playerWorld.Value.PlayerCount))) // Region
            {
                int regionPlayerCount = playerRegionGroup.Sum(playerWorld => playerWorld.Value.PlayerCount);
                string regionHeaderString = $"{playerRegionGroup.Key} ({regionPlayerCount.ToString("N0")})";

                using (var tabBar = ImRaii.TabBar("ServerDbTabs"))
                {
                    if (tabBar)
                    {
                        using (var regionTabItem = ImRaii.TabItem(regionHeaderString))
                        {
                            if (regionTabItem)
                            {
                                foreach (var dataCenter in playerRegionGroup
                                .GroupBy(r => r.Key.DataCenter.Value.Name)
                                .OrderBy(group => group.Key.ExtractText())) // DataCenter
                                {
                                    string dataCenterHeaderString = $"{dataCenter.Key} ({dataCenter.Sum(r => r.Value.PlayerCount).ToString("N0")} | {dataCenter.Sum(r => r.Value.RetainerCount).ToString("N0")})";
                                    if (ImGui.CollapsingHeader(dataCenterHeaderString))
                                    {
                                        using (var displaytable = ImRaii.Table("displaytable", PlayerAndRetainersWorldStatsColumn.Length, ImGuiTableFlags.BordersInner))
                                        {
                                            if (displaytable)
                                            {
                                                Utils.SetupTableColumns(PlayerAndRetainersWorldStatsColumn);

                                                var index = 0;

                                                foreach (var world in dataCenter
                                               .GroupBy(r => r.Key.Name)
                                               .OrderBy(group => group.Key.ExtractText())) // World
                                                {
                                                    ImGui.TableNextRow();
                                                    ImGui.TableNextColumn(); // World column

                                                    ImGui.Text(world.Key.ExtractText());

                                                    ImGui.TableNextColumn(); // PlayerCount column

                                                    ImGui.Text(world.Sum(r => r.Value.PlayerCount).ToString("N0"));

                                                    ImGui.TableNextColumn(); // RetainerCount column

                                                    ImGui.Text(world.Sum(r => r.Value.RetainerCount).ToString("N0"));

                                                    index++;
                                                }

                                                ImGui.TableNextRow();
                                                ImGui.TableNextColumn(); // World column

                                                ImGui.Text(Loc.StTotalCount);

                                                ImGui.TableNextColumn(); // PlayerCount column

                                                ImGui.Text(dataCenter.Sum(r => r.Value.PlayerCount).ToString("N0"));

                                                ImGui.TableNextColumn(); // RetainerCount column

                                                ImGui.Text(dataCenter.Sum(r => r.Value.RetainerCount).ToString("N0"));
                                            }
                                        }
                                    }
                                    ImGuiHelpers.ScaledDummy(10.0f);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void DrawGeneralStats()
        {
            if (_client._LastServerStats.ServerStats == null)
            {
                IsRefreshStatsRequestSent = true;
                CheckServerStats();
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            //ImGui.TextColored(ImGuiColors.ParsedGold, Loc.StServerDatabaseStats);

            long _refreshButtonCondition = _client._LastServerStats.ServerStats != null ? _client._LastServerStats.ServerStats.LastUpdate : 0;
            using (ImRaii.Disabled(bIsNetworkProcessing || Tools.UnixTime - _refreshButtonCondition < 20))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.SyncAlt, Loc.StRefreshServerStats))
                {
                    CheckServerStats();
                }
            }

            if (!string.IsNullOrWhiteSpace(_LastServerStatsMessage))
            {
                ImGui.SameLine();
                Utils.ColoredErrorTextWrapped($"{_LastServerStatsMessage} ({DateTimeOffset.FromUnixTimeSeconds(_LastServerStatsRefreshTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)_LastServerStatsRefreshTime)})");
            }

            ImGui.NewLine();

            ImGui.Text(Loc.StCharacterCount);
            ImGui.SameLine();

            if (_client._LastServerStats.ServerStats != null)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalPlayerCount.ToString()}");

                if (_client._LastServerStats.ServerStats.TotalPrivatePlayerCount > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $" (+{_client._LastServerStats.ServerStats.TotalPrivatePlayerCount} {Loc.StPrivateCharacters})");
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text(Loc.StRetainerCount);
            ImGui.SameLine();

            if (_client._LastServerStats.ServerStats != null)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalRetainerCount.ToString()}");

                if (_client._LastServerStats.ServerStats.TotalPrivateRetainerCount > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $" (+{_client._LastServerStats.ServerStats.TotalPrivateRetainerCount} {Loc.StPrivateRetainers})");
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text(Loc.StUserCount); ImGui.SameLine();
            if (_client._LastServerStats.ServerStats != null)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalUserCount.ToString()}");
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text(Loc.StLastUpdatedOn);
            ImGui.SameLine();

            if (_client._LastServerStats.ServerStats != null)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{Tools.UnixTimeConverter((int)_client._LastServerStats.ServerStats.LastUpdate)}");
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.NewLine();

            if (bEnableFetching)
            {
                bool _syncDatabaseButtonCondition = Config.LastSyncedTime != null ? Tools.UnixTime - Config.LastSyncedTime < 300 : true;
                using (ImRaii.Disabled(bIsNetworkProcessing || IsSyncingPlayers || IsSyncingRetainers || IsDbRefreshing || _client._LastServerStats.ServerStats == null || _syncDatabaseButtonCondition)) // 5 minutes
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.UserFriends, Loc.StSyncCharacterAndRetainerFromServer))
                    {
                        IsSyncingPlayers = true;
                        _cancellationToken = new CancellationTokenSource();
                        var syncPlayers = SyncPlayersWithLocalDb(_cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }

                Utils.SetHoverTooltip(Loc.StSyncCharacterAndRetainerFromServerTooltip);

                if (_syncDatabaseButtonCondition)
                {
                    var syncAgainTime = Config.LastSyncedTime + 300;
                    using (ImRaii.Disabled()) { Utils.TextWrapped($"{Loc.StCanSyncAgainTime} {Tools.TimeFromNow((int)syncAgainTime)}"); }
                }

                if (IsSyncingPlayers || IsSyncingRetainers)
                {
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, Loc.StStopFetching))
                    {
                        _SyncMessage = string.Empty;
                        _cancellationToken.Cancel();
                        _playersFetchedFromServer.Clear();
                        _retainersFetchedFromServer.Clear();
                        _LastCursor = 0;
                    }

                    Utils.CompletionProgressBar(_playersFetchedFromServer.Count + _retainersFetchedFromServer.Count,
                        (_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount)
                        + (_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount));
                }
            }

            Utils.ShowColoredMessage(_SyncMessage);
        }

        private CancellationTokenSource _cancellationToken;

        bool bEnableFetching = false;

        bool IsSyncingPlayers;
        bool IsSyncingRetainers;
        string _SyncMessage = string.Empty;
        public int _LastCursor = 0;
        public ConcurrentDictionary<long, PlayerDto> _playersFetchedFromServer = new ConcurrentDictionary<long, PlayerDto>();
        public ConcurrentDictionary<long, RetainerDto> _retainersFetchedFromServer = new ConcurrentDictionary<long, RetainerDto>();
        public async Task<bool> SyncPlayersWithLocalDb(CancellationTokenSource cts)
        {
            _ = Task.Run(async () =>
            {
                IsSyncingPlayers = true;

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var query = new PlayerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                        var request = await ApiClient.Instance.GetPlayers<PlayerDto>(query);

                        if (cts.Token.IsCancellationRequested || request.Page?.Data == null)
                        {
                            IsSyncingPlayers = false;
                            return;
                        }

                        foreach (var _data in request.Page.Data)
                        {
                            _playersFetchedFromServer[_data.LocalContentId] = _data;
                        }

                        _LastCursor = request.Page.LastCursor;

                        if (request.Page.NextCount > 0)
                        {
                            continue;
                        }
                        else
                        {
                            _LastCursor = 0;
                            IsSyncingPlayers = false;
                            IsSyncingRetainers = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsSyncingPlayers = false;
                }

                // Retainer senkronizasyonunu başlat
                await SyncRetainersWithLocalDb(cts);
            });

            return true;
        }

        public async Task<bool> SyncRetainersWithLocalDb(CancellationTokenSource cts)
        {
            _ = Task.Run(async () =>
            {
                IsSyncingRetainers = true;
                _LastCursor = 0;

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var query = new RetainerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                        var request = await ApiClient.Instance.GetRetainers<RetainerDto>(query);

                        if (cts.Token.IsCancellationRequested || request.Page?.Data == null)
                        {
                            IsSyncingRetainers = false;
                            return;
                        }

                        foreach (var _data in request.Page.Data)
                        {
                            _retainersFetchedFromServer[_data.LocalContentId] = _data;
                        }

                        _LastCursor = request.Page.LastCursor;

                        if (request.Page.NextCount > 0)
                        {
                            continue;
                        }
                        else
                        {
                            _LastCursor = 0;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsSyncingRetainers = false;
                }

                // Veritabanı senkronizasyonunu başlat
                IsDbRefreshing = true;
                await SyncWithLocalDB();
            });

            return true;
        }

        public bool IsDbRefreshing;

        private async Task SyncWithLocalDB()
        {
            if (!_playersFetchedFromServer.Any() && !_retainersFetchedFromServer.Any())
            {
                IsDbRefreshing = false;
                return;
            }

            var playerMappings = _playersFetchedFromServer.Select(p => new PlayerMapping
            {
                ContentId = (ulong)p.Key,
                PlayerName = p.Value.Name,
                AccountId = p.Value.AccountId.HasValue ? (ulong)p.Value.AccountId.Value : (ulong?)null,
            }).ToList();

            var retainerMappings = _retainersFetchedFromServer.Select(r => new Retainer
            {
                LocalContentId = (ulong)r.Key,
                Name = r.Value.Name,
                OwnerLocalContentId = (ulong)r.Value.OwnerLocalContentId,
                WorldId = (ushort)r.Value.WorldId,
            }).ToList();

            try
            {
                _SyncMessage = "\n" + Loc.StSavingToLocalDb;

                await Task.WhenAll(
                    PersistenceContext.Instance.HandleContentIdMappingAsync(playerMappings),
                    PersistenceContext.Instance.HandleMarketBoardPage(retainerMappings)
                ).ConfigureAwait(false);

                _SyncMessage = $"\n{Loc.StFetchingComplete}" +
                               $"\n{Loc.StCharacters}: {_playersFetchedFromServer.Count}" +
                               $"\n{Loc.StRetainers}: {_retainersFetchedFromServer.Count}";

                _playersFetchedFromServer.Clear();
                _retainersFetchedFromServer.Clear();
            }
            catch (Exception ex)
            {
                _SyncMessage = $"{Loc.ApiError} {Loc.StErrorWhileSavingToLocalDb}";
            }
            finally
            {
                IsDbRefreshing = false;
                //RefreshUserProfileInfo();
            }
        }

        private int lastSelectedPlayerOrRetainerValue = 0;
        private int selectedComboItem_ServerOrLocalDb = 0;
        private int selectedComboItem_PlayerOrRetainer = 0;
        private int selectedComboItem_NameorId = 0;

        private string[] selectedComboItems0 = [Loc.MnServer, Loc.MnLocalPC];
        private string[] selectedComboItems1 = [Loc.MnCharacter, "Retainer"];
        private string[] selectedComboItems2 = [Loc.MnByName, Loc.MnById];

        public Configuration Config = Plugin.Instance.Configuration;

        public ApiClient _client = ApiClient.Instance;
        public (Dictionary<long, PlayerSearchDto> Players, string Message) _LastPlayerSearchResult = new();
        public (Dictionary<long, API.Models.RetainerSearchDto> Retainers, string Message) _LastRetainerSearchResult = new();
        public bool bIsNetworkProcessing = false;
        bool bFilterMatchAnyPartOfName = false;
        bool bShowFilters = false;
        public void SetPlayerResult((Dictionary<long, PlayerSearchDto> Players, string Message) PlayerResult)
        {
            _LastRetainerSearchResult = (_LastRetainerSearchResult.Retainers, string.Empty);
            _LastPlayerSearchResult = (PlayerResult.Players, PlayerResult.Message);
        }

        public void SetRetainerResult((Dictionary<long, API.Models.RetainerSearchDto> Retainers, string Message) RetainerResult)
        {
            _LastPlayerSearchResult = (_LastPlayerSearchResult.Players, string.Empty);
            _LastRetainerSearchResult = (RetainerResult.Retainers, RetainerResult.Message);
        }

        public void DrawSearchPlayersAndRetainers_FromServerTab()
        {
            if (lastSelectedPlayerOrRetainerValue != selectedComboItem_PlayerOrRetainer)
            {
                LastTargetName = "###";
                _TestTempPlayerWithRetainers.Clear();
                lastSelectedPlayerOrRetainerValue = selectedComboItem_PlayerOrRetainer;
            }

            var bFiltersGuiArrow = ImGuiDir.Down;
            if (bShowFilters)
            {
                bFiltersGuiArrow = ImGuiDir.Up;
            }

            using (ImRaii.PushColor(ImGuiCol.Button, (bFilterMatchAnyPartOfName || WorldSelectorWindow.Instance.SelectedWorlds.Any()) ? ImGuiColors.TankBlue : ImGuiColors.DalamudGrey3))
            {
                if (ImGui.ArrowButton("filtersArrow", bFiltersGuiArrow))
                {
                    bShowFilters = !bShowFilters;
                }
                Utils.SetHoverTooltip(Loc.MnFilters);
            }

            ImGui.SameLine();

            ImGui.Text(Loc.MnSource);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            ImGui.Combo("##db1", ref selectedComboItem_ServerOrLocalDb, selectedComboItems0, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            ImGui.Combo("##serverDB1", ref selectedComboItem_PlayerOrRetainer, selectedComboItems1, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);

            if (selectedComboItem_PlayerOrRetainer == 0)
                selectedComboItems2 = [Loc.MnByName, Loc.MnById];
            else
            {
                selectedComboItems2 = [Loc.MnByName];
                selectedComboItem_NameorId = 0;
            }

            if (selectedComboItem_ServerOrLocalDb == 1)
                selectedComboItem_NameorId = 0;

            using (ImRaii.Disabled(selectedComboItem_PlayerOrRetainer == 1 || selectedComboItem_ServerOrLocalDb == 1))
            {
                ImGui.Combo("##serverDB2", ref selectedComboItem_NameorId, selectedComboItems2, 2);
            }

            string SearchByPlayerOrRetainer = selectedComboItem_PlayerOrRetainer == 0 ? Loc.MnCharacter : "Retainer";
            string SearchByNameorId = selectedComboItem_NameorId == 0 ? Loc.MnNameComboBox : Loc.MnId;
            ImGui.SameLine();
            ImGui.Text("->");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("##searchC", string.Format(Loc.MnTextInputEnterHere, SearchByPlayerOrRetainer, SearchByNameorId), ref _searchContent, 32, ImGuiInputTextFlags.AutoSelectAll);

            ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
            ImGui.SameLine();

            if (selectedComboItem_ServerOrLocalDb == 0)
            {
                string SearchButtonText = (bIsNetworkProcessing ? Loc.MnSearching : Loc.MnSearch);
                using (ImRaii.Disabled(bIsNetworkProcessing))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, SearchButtonText) || ImGui.IsKeyPressed(ImGuiKey.Enter))
                    {
                        if (selectedComboItem_PlayerOrRetainer == 0) //Search Player
                        {
                            if (selectedComboItem_NameorId == 0)
                            {
                                bool regex = Regex.IsMatch(_searchContent, @"^[A-Za-z]+(?:[ '-][A-Za-z]+)*[ '-]?$");
                                if (!regex)
                                {
                                    SetPlayerResult((_LastPlayerSearchResult.Players, Loc.MnErrorBadSearchQuery));
                                    return;
                                }
                                if (_searchContent.Length < 2)
                                {
                                    SetPlayerResult((_LastPlayerSearchResult.Players, Loc.MnErrorBadSearchQuery));
                                    return;
                                }

                                var selectedWorlds = WorldSelectorWindow.Instance.SelectedWorlds;
                                var query = new PlayerQueryObject() { Name = _searchContent };

                                if (bFilterMatchAnyPartOfName)
                                    query.F_MatchAnyPartOfName = true;

                                if (selectedWorlds.Any())
                                {
                                    query.F_WorldIds = selectedWorlds.Select(world => (short)world.WorldId).ToList();
                                }

                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;

                                    var request = _client.GetPlayers<PlayerSearchDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();
                                    if (request.Page == null)
                                    {
                                        SetPlayerResult((_LastPlayerSearchResult.Players, request.Message));
                                        bIsNetworkProcessing = false;
                                        return;
                                    }

                                    SetPlayerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                    bIsNetworkProcessing = false;
                                });
                            }
                            else if (selectedComboItem_NameorId == 1)
                            {
                                bool isParsedSuccessfully = Int64.TryParse(_searchContent, out long providedLocalContentId);
                                if (!isParsedSuccessfully || _searchContent.Length > 17)
                                {
                                    SetPlayerResult((_LastPlayerSearchResult.Players, Loc.MnErrorBadSearchQuery));
                                    return;
                                }
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;
                                    var query = new PlayerQueryObject() { LocalContentId = providedLocalContentId };
                                    if (bFilterMatchAnyPartOfName)
                                        query.F_MatchAnyPartOfName = true;

                                    var request = _client.GetPlayers<PlayerSearchDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();

                                    if (request.Page == null)
                                    {
                                        SetPlayerResult((_LastPlayerSearchResult.Players, request.Message));
                                        bIsNetworkProcessing = false;
                                        return;
                                    }

                                    SetPlayerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                    bIsNetworkProcessing = false;
                                });

                            }
                        }
                        else if (selectedComboItem_PlayerOrRetainer == 1) //Search Retainer
                        {
                            bool regex = Regex.IsMatch(_searchContent, @"^[a-zA-Z'-]+$");
                            if (!regex)
                            {
                                SetRetainerResult((_LastRetainerSearchResult.Retainers, Loc.MnErrorBadSearchQuery));
                                return;
                            }
                            if (_searchContent.Length < 2)
                            {
                                SetRetainerResult((_LastRetainerSearchResult.Retainers, Loc.MnErrorBadSearchQueryAtLeastLetters));
                                return;
                            }

                            var selectedWorlds = WorldSelectorWindow.Instance.SelectedWorlds;
                            var query = new RetainerQueryObject() { Name = _searchContent };

                            if (bFilterMatchAnyPartOfName)
                                query.F_MatchAnyPartOfName = true;

                            if (selectedWorlds.Any())
                            {
                                query.F_WorldIds = selectedWorlds.Select(world => (short)world.WorldId).ToList();
                            }

                            _ = System.Threading.Tasks.Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.GetRetainers<RetainerSearchDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();

                                if (request.Page == null)
                                {
                                    SetRetainerResult((_LastRetainerSearchResult.Retainers, request.Message));
                                    bIsNetworkProcessing = false;
                                    return;
                                }

                                SetRetainerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                bIsNetworkProcessing = false;
                            });
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_LastPlayerSearchResult.Message))
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                Utils.ColoredErrorTextWrapped(_LastPlayerSearchResult.Message);
            }

            if (!string.IsNullOrWhiteSpace(_LastRetainerSearchResult.Message))
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                Utils.ColoredErrorTextWrapped(_LastRetainerSearchResult.Message);
            }

            if (bShowFilters)
            {
                ImGuiHelpers.ScaledDummy(2.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(2.0f);

                ImGui.Checkbox(Loc.MnFilterMatchAnyPartOfName, ref bFilterMatchAnyPartOfName);

                ImGui.SameLine();

                Utils.ColoredTextWrapped(ImGuiColors.HealerGreen, " --- ");

                ImGui.SameLine();

                if (ImGui.Button(Loc.MnSearchByWorld))
                {
                    WorldSelectorWindow.Instance.IsOpen = !WorldSelectorWindow.Instance.IsOpen;
                }

                var selectedWorlds = WorldSelectorWindow.Instance.SelectedWorlds;
                if (selectedWorlds.Any())
                {
                    ImGui.SameLine();

                    ImGui.Text($"{Loc.MnSelectedWorlds} {selectedWorlds.Count}");
                    var worldNames = string.Join(", ", selectedWorlds.Select(w => w.WorldName));
                    Utils.SetHoverTooltip(worldNames);
                    ImGui.SameLine();

                    if (ImGui.Button($"Clear###WorldSelector"))
                    {
                        WorldSelectorWindow.Instance.ResetSelectedWorlds();
                    }
                }
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            if (selectedComboItem_ServerOrLocalDb == 0)
            {
                if (selectedComboItem_PlayerOrRetainer == 0) //Search Player
                {
                    if (_LastPlayerSearchResult.Players == null) return;
                    if (ImGui.BeginTable($"List##{_searchContent}", FromServerTableSearchPlayersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                    {
                        Utils.SetupTableColumns(FromServerTableSearchPlayersColumn);
                       
                        var index = 0;

                        foreach (var (localContentId, player) in _LastPlayerSearchResult.Players)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (player == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            // Show Details column
                            using (ImRaii.Disabled(DetailsWindow.Instance._LastMessage == Loc.DtLoading))
                                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                                {
                                    DetailsWindow.Instance.IsOpen = true;
                                    DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                                }

                            ImGui.TableNextColumn();

                            AvatarViewerWindow.DrawCharacterAvatar($"{player.Name}##{index}", player.AvatarLink);

                            ImGui.TableNextColumn();

                            // PlayerName column
                            if (!string.IsNullOrWhiteSpace(player.Name))
                            {
                                var highlightAsBot = player.Name.Contains("[BOT]");
                                var highlightAsGM = player.Name.Contains("[GM]");
                                using (var textColor = highlightAsBot ? ImRaii.PushColor(ImGuiCol.Text, KnownColor.IndianRed.Vector()) : null)
                                using (var textColor2 = highlightAsGM ? ImRaii.PushColor(ImGuiCol.Text, KnownColor.Gold.Vector()) : null)
                                {
                                    Utils.CopyButton(player.Name, $"##PlayerName_{index}");
                                    ImGui.Text(player.Name);

                                    if (highlightAsBot)
                                        Utils.SetHoverTooltip(Loc.MnPlayerIdentifiedAsBot);
                                    else if (highlightAsGM)
                                        Utils.SetHoverTooltip(Loc.MnPlayerIdentifiedAsGM);
                                }
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();  // World column

                            if (player.WorldId != null)
                            {
                                ImGui.Text(Utils.GetWorldName((uint)player.WorldId));
                            }
                            else
                            {
                                ImGui.Text("");
                            }

                            ImGui.TableNextColumn(); //AccId column

                            if (player.AccountId != null)
                            {
                                Utils.CopyButton(player.AccountId?.ToString(), $"##AccountId_{index}");
                                ImGui.Text(player.AccountId.ToString());
                            }
                            else
                                ImGui.Text("");

                            ImGui.TableNextColumn();  //cId column

                            Utils.CopyButton(localContentId.ToString(), $"##ContentId_{index}");
                            ImGui.Text(localContentId.ToString());

                            if (_clientState.IsLoggedIn)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Panorama, "Adv. Plate" + $"##AdvPlate_{index}"))
                                {
                                    OpenAdventurePlate((ulong)localContentId);
                                }
                                Utils.SetHoverTooltip(Loc.MnOpenAdventurerPlate);
                            }

                            index++;
                        }
                        ImGui.EndTable();
                    }
                }
                else if (selectedComboItem_PlayerOrRetainer == 1) //Search Retainer
                {
                    if (_LastRetainerSearchResult.Retainers == null) return;
                    if (ImGui.BeginTable($"List3", FromServerTableSearchRetainersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                    {
                        Utils.SetupTableColumns(FromServerTableSearchRetainersColumn);
                        
                        var index = 0;

                        foreach (var (localContentId, retainer) in _LastRetainerSearchResult.Retainers)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (retainer == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // RetainerName column

                            using (ImRaii.Disabled(DetailsWindow.Instance._LastMessage == Loc.DtLoading))
                                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                                {
                                    DetailsWindow.Instance.IsOpen = true;
                                    DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                                }

                            ImGui.SameLine();

                            if (!string.IsNullOrWhiteSpace(retainer.Name))
                            {
                                Utils.CopyButton(retainer.Name, $"##RetainerName_{index}");
                                ImGui.Text(retainer.Name);
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn(); //World column

                            Utils.CopyButton(Utils.GetWorldName(retainer.WorldId), $"##RetainerWorldName_{index}");
                            Utils.DisplayWorldInfo(retainer.WorldId);

                            ImGui.TableNextColumn(); //Created At column

                            var _CreatedAt = Tools.ToTimeSinceString(retainer.CreatedAt).ToString();

                            if (Utils.ExternalDbTimestamps.Contains(retainer.CreatedAt))
                                Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);
                            ImGui.Text(_CreatedAt);

                            ImGui.TableNextColumn(); //OwnerContentId column

                            Utils.CopyButton(retainer.OwnerLocalContentId.ToString(), $"##RetainerOwnerContentId_{index}");
                            ImGui.Text(retainer.OwnerLocalContentId.ToString());

                            ImGui.TableNextColumn(); //RetainerContentId column

                            Utils.CopyButton(retainer.LocalContentId.ToString(), $"##RetainerContentId_{index}");
                            ImGui.Text(retainer.LocalContentId.ToString());

                            index++;
                        }
                        ImGui.EndTable();
                    }
                }
            }
            else
            {
                if (_searchContent.Length <= 1)
                    return;

                if (ImGui.BeginTable($"SocialList##{_searchContent}", TableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                {
                    Utils.SetupTableColumns(TableColumn);
                    
                    var index = 0;

                    foreach (var (contentId, player) in SearchPlayer(_searchContent))
                    {
                        if (index > TablePlayerMaxLimit)
                            break;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); // PlayerName column

                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                        {
                            DetailsWindow.Instance.IsOpen = true;
                            DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)contentId, true);
                        }

                        ImGui.SameLine();

                        if (player.Item1.Name != null)
                        {
                            Utils.CopyButton(player.Item1.Name, $"##{index}");
                            ImGui.Text(player.Item1.Name);
                        }
                        else
                            ImGui.Text("-");

                        ImGui.TableNextColumn(); //TotalACCS column

                        if (player.TotalAccCount >= 0)
                            ImGui.Text(player.TotalAccCount.ToString());
                        else
                            ImGui.Text(string.Empty);

                        ImGui.TableNextColumn();

                        if (player.Item2 != null && player.Item2.Count > 0) // TotalRetainerCount column
                        {
                            ImGui.Text(player.Item2.Count.ToString());
                        }
                        else
                        {
                            ImGui.Text(string.Empty);
                        }

                        ImGui.TableNextColumn(); //RetainerNames column

                        if (player.Item2 != null && player.Item2.Count > 0)
                        {
                            List<string> TempRetainerNameList = player.Item2.Select(o => o.Name).ToList();
                            String[] str = TempRetainerNameList.ToArray();
                            ImGui.Text(string.Join(", ", str));
                        }

                        ImGui.TableNextColumn();  //cId column

                        Utils.CopyButton(contentId.ToString(), $"##{index}");
                        ImGui.Text(contentId.ToString());

                        ImGui.TableNextColumn(); //AccId column

                        Utils.CopyButton(player.Item1.AccountId.ToString(), $"##{index}");
                        ImGui.Text(player.Item1.AccountId.ToString());

                        index++;
                    }
                    ImGui.EndTable();
                }
            }
        }

        private void NavigateToSettings()
        {
            if (!Config.LoggedIn || string.IsNullOrWhiteSpace(Config.Key))
            {
                Utils.ShowColoredMessage(Loc.MnErrorYouAreNotConnected);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Lock, Loc.MnOpenSettingsMenutoconnect))
                {
                    SettingsWindow.Instance.IsOpen = true;
                }
            }
            else
            {
                Utils.ShowColoredMessage(Loc.MnYouAreConnected);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LockOpen, Loc.MnOpenSettingsMenu))
                {
                    SettingsWindow.Instance.IsOpen = true;
                }
            }
        }

        bool IsDatabasePendingDeletion;
        private async void DrawLocalStatisticsTab()
        {
            if (ImGui.BeginTabBar("Tabs2"))
            {
                if (ImGui.BeginTabItem(Loc.MnRetainerStatistics))
                {
                    // NavigateToSettings();

                    if (Tools.UnixTime - LastUnix >= 2)
                    {
                        UpdateStatisticsValues();
                        GetServerRetainersCount();
                        LastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    }

                    ImGui.Text($"{Loc.MnTotalCharacters}: {_TotalPlayers_Value}  -  ");
                    ImGui.SameLine();
                    ImGui.Text($"{Loc.MnTotalRetainers}: {_TotalRetainers_Value}");
                    ImGui.SameLine();

                    ImGui.SetNextItemWidth(200);
                    ImGui.Text($"");

                    ImGui.BeginChild("Ch");

                    if (ImGui.BeginTable($"ServerList##{_searchContent}", WorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                    {
                        Utils.SetupTableColumns(WorldsTableColumn);
                        
                        var index = 0;

                        foreach (var server in _TempGetServerRetainersCount.OrderByDescending(a => a.Value.Count))
                        {
                            if (index > TablePlayerMaxLimit)
                                break;

                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();

                            ImGui.Text(Utils.GetWorldName(server.Key)); // Servername column

                            ImGui.TableNextColumn();

                            ImGui.Text(Utils.GetWorld(server.Key).Value.DataCenter.Value.Name.ToString()); //DataCenter column

                            ImGui.TableNextColumn();

                            ImGui.Text(server.Value.Count.ToString()); //Retainer Count column

                            ImGui.TableNextColumn();

                            index++;
                        }
                        ImGui.TableNextRow();

                        ImGui.EndTable();
                    }

                    ImGui.EndChild();

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.MnDatabaseInformation))
                {
                    // NavigateToSettings();
                    
                    ImGuiHelpers.ScaledDummy(10f);

                    ImGui.TextUnformatted(Loc.MnDatabaseDetails);
                    using (ImRaii.PushIndent(ImGui.GetStyle().IndentSpacing, false))
                    {
                        var dbPath = new FileInfo(Path.Combine(Plugin.Instance._pluginInterface.ConfigDirectory.FullName, "PlayerScope.data.sqlite3"));
                        RefreshDatabaseStats(dbPath);

                        if (dbPath.Exists)
                        {
                            DisplayDatabaseInfo(dbPath, DatabaseSize, DatabaseLogSize, IsDatabasePendingDeletion);
                            DisplayDeleteDatabaseButton();
                        }
                    }

                    ImGuiHelpers.ScaledDummy(5f);
                    ImGui.Separator();
                    ImGuiHelpers.ScaledDummy(5f);

                    DisplayLegacyDatabaseInfo("RetainerTrackExpanded", "RetainerTrackExpanded.data.sqlite3", Loc.MnDatabaseLegacyDetails);
                    DisplayLegacyDatabaseInfo("RetainerTrack", "retainertrack.data.sqlite3", Loc.MnDatabaseLegacyDetails);

                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
 
        private long DatabaseLastRefreshTicks;
        private long DatabaseSize;
        private long DatabaseLogSize;
        void RefreshDatabaseStats(FileInfo dbPath)
        {
            if (DatabaseLastRefreshTicks + 5000 < Environment.TickCount64)
            {
                if (dbPath.Exists)
                {
                    UpdateStatisticsValues();
                    GetServerRetainersCount();
                    string logPath = $"{dbPath}-wal";

                    DatabaseSize = dbPath.Length;
                    DatabaseLogSize = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
                    DatabaseLastRefreshTicks = Environment.TickCount64;
                }
            }
        }

        void DisplayDatabaseInfo(FileInfo dbPath, long dbSize, long dbLogSize, bool isPendingDeletion)
        {
            DisplayPathInfo(dbPath.DirectoryName);

            ImGui.SameLine();
            ImGui.PushID($"OpenFolder_{dbPath.Name}");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, Loc.MnDatabaseOpenFolder))
            {
                Utils.OpenFolder(Path.GetDirectoryName(dbPath.FullName));
            }
            ImGui.PopID();

            var sizeText = isPendingDeletion ? $"{Utils.BytesToString(dbSize)} - {Loc.MnDatabasePendingDeletion}" : Utils.BytesToString(dbSize);
            Utils.TextWrapped($"{Loc.MnDatabaseSize} {sizeText}");
            Utils.SetHoverTooltip(Utils.BytesToString(dbSize));

            var logSizeText = isPendingDeletion ? $"{Utils.BytesToString(dbLogSize)} - {Loc.MnDatabasePendingDeletion}" : Utils.BytesToString(dbLogSize);
            Utils.TextWrapped($"{Loc.MnDatabaseLogSize} {logSizeText}");
            Utils.SetHoverTooltip(Utils.BytesToString(dbLogSize));
        }

        void DisplayLegacyDatabaseInfo(string folderName, string dbFileName, string headerText)
        {
            var legacyDbPath = new FileInfo(Path.Combine(Plugin.Instance._pluginInterface.ConfigDirectory.Parent.FullName, folderName, dbFileName));
            if (!legacyDbPath.Exists) return;

            ImGui.TextUnformatted(headerText);
            using (ImRaii.PushIndent(ImGui.GetStyle().IndentSpacing, false))
            {
                DisplayDatabaseInfo(legacyDbPath, legacyDbPath.Length, 0, IsDatabasePendingDeletion);

                ImGui.PushID($"Delete_{folderName}");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, Loc.MnDatabaseLegacyDeleteData))
                {
                    DeleteLegacyDatabase(folderName, dbFileName);
                }
                ImGui.PopID();
            }

            ImGuiHelpers.ScaledDummy(5f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5f);
        }

        void DeleteLegacyDatabase(string folderName, string dbFileName)
        {
            var fullPath = Path.Combine(Plugin.Instance._pluginInterface.ConfigDirectory.Parent.FullName, folderName);

            try
            {
                if (Directory.Exists(fullPath))
                {
                    foreach (var file in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.IsReadOnly)
                        {
                            fileInfo.IsReadOnly = false;
                        }
                    }

                    Directory.Delete(fullPath, true);
                }

                var configPath = Path.ChangeExtension(fullPath, "json");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                Utils.AddNotification(Loc.MnDatabaseSuccessfullyDeletedNotification, NotificationType.Info);
            }
            catch (Exception ex)
            {
                Utils.AddNotification($"Failed to delete database: {ex.Message}", NotificationType.Error);
            }
        }

        void DisplayPathInfo(string path)
        {
            Utils.TextWrapped(path);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText(path);
                Utils.AddNotification(Loc.MnDatabaseFilePathCopied, NotificationType.Info);
            }
            Utils.SetHoverTooltip(Loc.MnDatabaseCopyFilePath);
        }

        void DisplayDeleteDatabaseButton()
        {
            if (Utils.CtrlShiftButton(FontAwesomeIcon.Trash, Loc.MnDatabaseDeleteData, Loc.MnDatabaseDeleteDataTooltip))
            {
                using var scope = _serviceProvider.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();

                dbContext.Players.ExecuteDelete();
                dbContext.Retainers.ExecuteDelete();
                dbContext.Database.ExecuteSqlRaw(@"
                    VACUUM;
                    ANALYZE;
                ");

                ClearCaches();
                dbContext.SaveChanges();
                IsDatabasePendingDeletion = true;
                DatabaseLastRefreshTicks = 0;
                Utils.AddNotification(Loc.MnDatabaseSuccessfullyDeletedNotification, NotificationType.Info);
            }
        }

        void ClearCaches()
        {
            _retainerCache.Clear();
            _playerCache.Clear();
            _playerWithRetainersCache.Clear();
            _worldRetainerCache.Clear();
            _AccountIdCache.Clear();
        }

        private async void DrawMyFavoriesTab()
        {
            var players = Config.FavoritedPlayer;
            if (players == null) return;
            if (ImGui.BeginTable($"FavoritedPlayersTable", FavoritedPlayersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
            {
                Utils.SetupTableColumns(FavoritedPlayersColumn);
               
                var index = 0;

                foreach (var (localContentId, player) in players)
                {
                    if (index > TablePlayerMaxLimit)
                        break;
                    if (player == null)
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                    {
                        DetailsWindow.Instance.IsOpen = true;
                        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                    }

                    ImGui.SameLine();

                    if (!string.IsNullOrWhiteSpace(player.Name)) // PlayerName column
                    {
                        Utils.CopyButton(player.Name, $"FavCharacterName##{index}");
                        ImGui.Text(player.Name);
                    }
                    else
                    {
                        ImGui.Text("---");
                    }

                    ImGui.TableNextColumn();  //cId column

                    Utils.CopyButton(localContentId.ToString(), $"FavCharacterContentId##{index}");
                    ImGui.Text(localContentId.ToString());

                    ImGui.TableNextColumn(); //AccId column

                    Utils.CopyButton(player.AccountId.ToString(), $"FavCharacterAccountId##{index}");
                    ImGui.Text(player.AccountId.ToString());

                    ImGui.TableNextColumn(); // Add Note column

                    bool showNoteInput = favoritedCharacterNoteVisibility.ContainsKey(index) && favoritedCharacterNoteVisibility[index];

                    string note = player.Note ?? string.Empty;

                    if (showNoteInput)
                    {
                        ImGui.SetNextItemWidth(80);
                        if (ImGui.InputText($"##Note{index}", ref note, 100))
                        {
                            player.Note = note;
                            Config.Save();
                        }

                        ImGui.SameLine();
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StickyNote, $"{Loc.MnSaveNote}##{index}"))
                        {
                            favoritedCharacterNoteVisibility[index] = !showNoteInput;
                            showNoteInput = favoritedCharacterNoteVisibility[index];
                        }
                    }
                    else
                    {
                        ImGui.TextWrapped(note);

                        ImGui.SameLine();
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StickyNote, $"{Loc.MnAddNote}##{index}"))
                        {
                            favoritedCharacterNoteVisibility[index] = !showNoteInput;
                            showNoteInput = favoritedCharacterNoteVisibility[index];
                        }
                    }

                    ImGui.TableNextColumn(); // Remove column

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, $"Remove##{index}"))
                    {
                        selectedIndex = index;
                        playerIdToRemove = localContentId;
                        showRemoveConfirmation = true;
                    }

                    if (showRemoveConfirmation && selectedIndex == index)
                    {
                        ImGui.SameLine();
                        ImGui.Text(Loc.MnAreYouSure);
                        ImGui.SameLine();
                        if (ImGui.Button(Loc.MnYes))
                        {
                            if (playerIdToRemove != -1)
                            {
                                Config.FavoritedPlayer.Remove(playerIdToRemove, out _);
                                Config.Save();
                            }
                            showRemoveConfirmation = false;
                            playerIdToRemove = -1;
                        }

                        ImGui.SameLine();
                        if (ImGui.Button(Loc.MnNo))
                        {
                            showRemoveConfirmation = false;
                        }
                    }

                    index++;
                }
                ImGui.EndTable();
            }
        }

        Dictionary<int, bool> favoritedCharacterNoteVisibility = new Dictionary<int, bool>();
        bool showRemoveConfirmation = false;
        long playerIdToRemove = -1;
        int selectedIndex = -1;

        public static ConcurrentDictionary<ushort, List<ulong>> _TempGetServerRetainersCount = new ConcurrentDictionary<ushort, List<ulong>>();
        public static int LastTotalRetainerCount = 0;

        public static ConcurrentDictionary<ushort, List<ulong>> GetServerRetainersCount()
        {
            if (LastTotalRetainerCount != PersistenceContext._retainerCache.Keys.Count)
            {
                foreach (var retainer in PersistenceContext._retainerCache.Values)
                {
                    var GetPlayerRetainers = _TempGetServerRetainersCount.GetOrAdd(retainer.WorldId, _ => new List<ulong>() { retainer.LocalContentId });

                    if (!GetPlayerRetainers.Contains(retainer.LocalContentId))
                    {
                        _TempGetServerRetainersCount[retainer.WorldId].Add(retainer.LocalContentId);
                    }
                }

                LastTotalRetainerCount = PersistenceContext._retainerCache.Count;
            }

            return _TempGetServerRetainersCount;
        }


        ConcurrentDictionary<ulong, (PersistenceContext.CachedPlayer, List<Database.Retainer>, int TotalAccCount)> _TestTempPlayerWithRetainers = new();
        string LastTargetName = "###";

        ConcurrentDictionary<ulong, (PersistenceContext.CachedPlayer, List<Database.Retainer>, int TotalAccCount)> SearchPlayer(string targetName)
        {
            targetName = targetName.ToLower();

            if (LastTargetName == targetName)
                return _TestTempPlayerWithRetainers;
            else
                _TestTempPlayerWithRetainers.Clear();

            bool Compare(string fullname)
            {
                return fullname.ToLower().Contains(targetName);
            }

            if (_CurrentTab == Tabs.SearchCharactersAndRetainers && selectedComboItem_ServerOrLocalDb == 1) //localdb
            {
                if (selectedComboItem_PlayerOrRetainer == 0) //Search Players in localdb
                {
                    foreach (var player in PersistenceContext._playerWithRetainersCache)
                    {
                        var playerName = player.Value.Player.Name.ToLower(); //PlayerName
                        var cId = player.Key; //cId

                        if (Compare(playerName) || Compare(cId.ToString()))
                        {
                            if (player.Value.Player.AccountId != null && !_AccountIdCache.IsEmpty)
                            {
                                _AccountIdCache.TryGetValue((ulong)player.Value.Player.AccountId, out var GetAccountsContentIds);
                                if (GetAccountsContentIds != null)
                                {
                                    _TestTempPlayerWithRetainers.GetOrAdd(cId, _ => (player.Value.Player, player.Value.Retainers, GetAccountsContentIds.Count));
                                }
                            }
                            else
                            {
                                _TestTempPlayerWithRetainers.GetOrAdd(cId, _ => (player.Value.Player, player.Value.Retainers, 0));
                            }
                        }
                    }
                    LastTargetName = targetName;

                    return _TestTempPlayerWithRetainers;
                }
                else //Search Retainers in localdb
                {
                    List<ulong> AddedOwners = new List<ulong>();
                    foreach (var retainer in _retainerCache)
                    {
                        if (!AddedOwners.Contains(retainer.Value.OwnerLocalContentId))
                        {
                            string retainerName = retainer.Value.Name.ToLower();
                            if (retainerName.Contains(targetName))
                            {
                                PersistenceContext._playerWithRetainersCache.TryGetValue(retainer.Value.OwnerLocalContentId, out var _GetPlayerValues);

                                AddedOwners.Add(retainer.Value.OwnerLocalContentId);
                                if (_GetPlayerValues.Player != null && _GetPlayerValues.Player.AccountId != null && !_AccountIdCache.IsEmpty)
                                {
                                    _AccountIdCache.TryGetValue((ulong)_GetPlayerValues.Player.AccountId, out var GetAccountsContentIds);
                                    if (GetAccountsContentIds != null)
                                    {
                                        _TestTempPlayerWithRetainers.GetOrAdd(retainer.Value.OwnerLocalContentId, _ => (_GetPlayerValues.Player, _GetPlayerValues.Retainers, GetAccountsContentIds.Count));
                                    }
                                }
                                else
                                {
                                    _TestTempPlayerWithRetainers.GetOrAdd(retainer.Value.OwnerLocalContentId, _ => (_GetPlayerValues.Player, _GetPlayerValues.Retainers, 0));
                                }
                            }

                        }
                    }

                    LastTargetName = targetName;
                }
            }
            return _TestTempPlayerWithRetainers;
        }
    }
}
