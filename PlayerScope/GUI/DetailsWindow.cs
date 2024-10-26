using Dalamud.Interface.Utility.Table;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Linq;
using System.Collections.Concurrent;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Lumina;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;
using PlayerScope.Handlers;
using static PlayerScope.Handlers.PersistenceContext;
using static PlayerScope.GUI.MainWindow;
using PlayerScope.Database;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using System.Security.Principal;
using static FFXIVClientStructs.ThisAssembly.Git;
using PlayerScope.API.Models;
using PlayerScope.API;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using static System.Net.Mime.MediaTypeNames;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Utility;
using static FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Delegates;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Dalamud.Interface.Utility.Raii;
using System.Drawing;
using PlayerScope.Models;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using PlayerScope.Properties;
namespace PlayerScope.GUI
{
    public class DetailsWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeDetails";
        public DetailsWindow() : base(WindowId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            LanguageChanged();
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 550),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
        public void LanguageChanged()
            => WindowName = $"{Loc.TitleDetailsMenu}{WindowId}";

        public Configuration Config = PlayerScopePlugin.Instance.Configuration;
        private static DetailsWindow _instance = null;
        public static DetailsWindow Instance
        {
            get
            {
                return _instance;
            }
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public bool AccountsFetched = false;
        public ulong SelectedPlayerContentId = 0;
        private string _searchContent = "";

        private readonly string[] PlayerTableColumn = new string[]
        {
            Loc.MnCharacter, Loc.MnAccountId, Loc.MnContentId,
        };

        private readonly string[] RetainerTableColumn = new string[]
        {
            Loc.DtRetainerName, Loc.MnContentId, Loc.MnWorld, Loc.DtOwnerName
        };

        private readonly string[] DetailedPlayerCId_AccIdTableColumn = new string[]
        {
            Loc.MnContentId,Loc.MnAccountId
        };

        private readonly string[] DetailedPlayerLastSeenZoneTableColumn = new string[]
        {
            Loc.DtZoneName, Loc.MnWorld, Loc.MnAddedAt
        };

        private readonly string[] DetailedPlayerNamesTableColumn = new string[]
        {
            Loc.DtCharacterName, Loc.MnAddedAt
        };

        private readonly string[] DetailedPlayerWorldsTableColumn = new string[]
        {
            Loc.StHomeWorld, Loc.MnAddedAt
        };

        private readonly string[] DetailedPlayerRetainerTableColumn = new string[]
        {
            Loc.DtRetainerName, Loc.StHomeWorld, Loc.DtLastSeen, Loc.MnAddedAt, Loc.DtOwnerName, Loc.MnContentId
        };

        private readonly string[] AltCharPlayerTableColumn = new string[]
        {
            Loc.DtCharacterName, Loc.StHomeWorld, Loc.MnContentId
        };

        private readonly string[] DetailedPlayerTerritoriesTableColumn = new string[]
        {
            Loc.DtZoneName, Loc.MnWorld, Loc.DtFirstSeen, Loc.DtLastSeen, Loc.DtDuration
        };
        private readonly string[] DetailedPlayerCustomizationTableColumn = new string[]
        {
            Loc.DtRaceInfo, Loc.DtHeight, Loc.DtMuscleMass, Loc.DtBustSize, Loc.MnAddedAt
        };

        ApiClient _client = ApiClient.Instance;
        public void OpenDetailedPlayerWindow(ulong PlayerContentId, bool GetInfoFromServer)
        {
            _TestTempPlayerWithRetainers.Clear();

            _LastMessage = string.Empty;
            _seeExtraDetailsOfRetainer = null;
            //_LastPlayerDetailedInfo = new();

            if (GetInfoFromServer)
            {
                IsDataFromServer = true;

                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    var request = _client.GetPlayerById((long)PlayerContentId).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (request.Player == null)
                        _LastMessage = request.Message;
                    else
                    {
                        _LastPlayerDetailedInfo = (request.Player, request.Message);
                        _LastMessage = string.Empty;
                    }
                });
            }
            else
            {
                IsDataFromServer = false;
                AccountsFetched = false;
                //_TestTempPlayerWithRetainers.Clear();

                SelectedPlayerContentId = PlayerContentId;
            }
        }

        bool IsDataFromServer = false;

        PlayerDetailed.RetainerDto _seeExtraDetailsOfRetainer = null;
        string _LastMessage = string.Empty;
        static ConcurrentDictionary<ulong, (CachedPlayer, List<Database.Retainer>)> _TestTempPlayerWithRetainers = new();
        static (PlayerDetailed Player, string Message) _LastPlayerDetailedInfo = new();

        bool bShowDetailedDate;
        public override void Draw()
        {
            if (IsDataFromServer)
            {
                if (_LastPlayerDetailedInfo.Player != null)
                {
                    var bFavoritesContainPlayer = Config.FavoritedPlayer.ContainsKey(_LastPlayerDetailedInfo.Player.LocalContentId);
                    var AddToFavoriteText = bFavoritesContainPlayer ? Loc.DtRemoveFromFavorites : Loc.DtAddToFavorites;
                    FontAwesomeIcon _ButtonIcon = bFavoritesContainPlayer ? FontAwesomeIcon.UserMinus : FontAwesomeIcon.UserPlus;

                    if (ImGuiComponents.IconButtonWithText(_ButtonIcon, AddToFavoriteText))
                    {
                        var player = _LastPlayerDetailedInfo.Player; var playerName = _LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0 ? _LastPlayerDetailedInfo.Player.PlayerNameHistories.LastOrDefault().Name : "(NO-NAME)";
                        ulong? accountId = player.AccountId != null ? (ulong)player.AccountId : null;
                        if (bFavoritesContainPlayer)
                            Config.FavoritedPlayer.Remove(player.LocalContentId, out _);
                        else
                            Config.FavoritedPlayer.GetOrAdd(player.LocalContentId, new Configuration.CachedFavoritedPlayer { AccountId = accountId, Name = playerName });
                        Config.Save();
                    }

                    ImGui.SameLine();

                    using (var textColor = ImRaii.PushColor(ImGuiCol.CheckMark, KnownColor.Yellow.Vector()))
                    {
                        if (ImGui.Checkbox(Loc.DtShowTheDateDetail, ref bShowDetailedDate))
                        {
                            Config.bShowDetailedDate = bShowDetailedDate;
                            Config.Save();
                        }
                    }
                   
                    ImGui.SameLine();

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.DtRefreshProfile))
                    {
                        OpenDetailedPlayerWindow((ulong)_LastPlayerDetailedInfo.Player.LocalContentId, true);
                    }
                }

                if (!string.IsNullOrWhiteSpace(_LastMessage))
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, _LastMessage); //Error message
                }
                else if (_LastPlayerDetailedInfo.Player == null)
                {
                    if (!string.IsNullOrWhiteSpace(_LastPlayerDetailedInfo.Message))
                        ImGui.TextColored(ImGuiColors.DalamudRed, _LastPlayerDetailedInfo.Message);
                    else
                        ImGui.TextColored(ImGuiColors.DalamudWhite, Loc.DtLoading);
                }
                if (_LastPlayerDetailedInfo.Player != null)
                    DrawPlayerDetailsFromServer();
            }
            else
            {
                DrawPlayerDetailsFromLocal();
            }
        }

        private void DrawPlayerDetailsFromLocal()
        {
            if (Config.LoggedIn && !string.IsNullOrWhiteSpace(Config.Key))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Server, Loc.DtFetchDetailedCharacterInfo))
                {
                    OpenDetailedPlayerWindow(SelectedPlayerContentId, true);
                }
            }

            ImGui.BeginGroup();

            ImGui.Text(Loc.DtCharactersOfThePlayer);
            if (!AccountsFetched)
            {
                GetAllRetainersofAllAccounts(SelectedPlayerContentId);
                AccountsFetched = true;
            }

            if (ImGui.BeginTable($"SocialList2##a{_searchContent}", PlayerTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in PlayerTableColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var account in _TestTempPlayerWithRetainers)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.Text(account.Value.Item1.Name); //PlayerName

                    ImGui.TableNextColumn();

                    ImGui.Text(account.Value.Item1.AccountId.ToString()); //AccId column

                    ImGui.TableNextColumn();

                    ImGui.Text(account.Key.ToString()); //cId column
                }

                index++;
            }
            ImGui.EndTable();
            ImGui.EndGroup();

            ImGui.BeginGroup();
            ImGui.Text("\n");
            ImGui.Text(Loc.DtRetainersOfThePlayer);

            if (_TestTempPlayerWithRetainers != null && !_TestTempPlayerWithRetainers.IsEmpty)
            {
                if (ImGui.BeginTable($"SocialList##{_searchContent}", RetainerTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in RetainerTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var Item in _TestTempPlayerWithRetainers)
                    {
                        foreach (var retainer in Item.Value.Item2)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.Name); // RetainerName column

                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.LocalContentId.ToString()); //cId column

                            ImGui.TableNextColumn();

                            ImGui.Text($"{Util.GetWorld(retainer.WorldId).Name}"); // Servername column

                            ImGui.TableNextColumn();

                            ImGui.Text(Item.Value.Item1.Name); //OwnerName column
                        }
                    }

                    index++;
                }
                ImGui.EndTable();
            }
            ImGui.EndGroup();
        }
        private void DrawPlayerDetailsFromServer()
        {
            var player = _LastPlayerDetailedInfo.Player;

            if (ImGui.BeginTabBar("Tabs"))
            {
                if (ImGui.BeginTabItem(Loc.DtPlayerInfo))
                {
                    DrawPlayerInfoTab();
                    ImGui.EndTabItem();
                }

                if (player.PlayerLastSeenInfo != null && player.PlayerLastSeenInfo.TerritoryHistory != null && player.PlayerLastSeenInfo.TerritoryHistory.Count > 0)
                {
                    if (ImGui.BeginTabItem(Loc.DtLocationHistory))
                    {
                        DrawPlayerTerritoryInfoTab();
                        ImGui.EndTabItem();
                    }
                }
                if (player.PlayerCustomizationHistories != null && player.PlayerCustomizationHistories.Count > 0)
                {
                    if (ImGui.BeginTabItem(Loc.DtCustomizationHistory))
                    {
                        DrawPlayerCustomizationInfoTab();
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        public void DrawPlayerInfoTab()
        {
            var player = _LastPlayerDetailedInfo.Player;

            ImGui.BeginGroup();
            ImGui.Text(Loc.DtShowingResultsFor); ImGui.SameLine();

            if (ImGui.BeginTable($"_Char", DetailedPlayerCId_AccIdTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in DetailedPlayerCId_AccIdTableColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.Text(player.LocalContentId.ToString()); //CId column

                ImGui.TableNextColumn();

                ImGui.Text(player.AccountId.ToString()); //AccId column
            }
            ImGui.EndTable();

            if (player.PlayerLastSeenInfo != null)
            {
                TerritoryType getLastTerritory = new TerritoryType();
                if (player.PlayerLastSeenInfo.TerritoryHistory.Count > 0)
                {
                    getLastTerritory = PersistenceContext.Instance.Territories.Where(a => a.RowId == player.PlayerLastSeenInfo.TerritoryHistory.FirstOrDefault()?.TerritoryId).FirstOrDefault();
                }

                ImGui.Text(Loc.DtCharacterLastSeenIn); ImGui.SameLine();
                if (ImGui.BeginTable($"_Lastseen", DetailedPlayerLastSeenZoneTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerLastSeenZoneTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (player.PlayerLastSeenInfo.TerritoryHistory.Count > 0)
                    {
                        if (getLastTerritory != null && player.PlayerLastSeenInfo.CreatedAt == player.PlayerLastSeenInfo.TerritoryHistory.FirstOrDefault().LastSeenAt)
                        {
                            Util.DrawHelp(false, $"{getLastTerritory.PlaceName.Value.Name.ToString()} | {getLastTerritory.PlaceNameRegion.Value.Name.ToString()}");
                            ImGui.Text(getLastTerritory.PlaceName.Value.Name.ToString()); //Zone Name column
                        }
                    }
                    else
                    {
                        ImGui.Text("---");
                    }

                    ImGui.TableNextColumn();

                    if (player.PlayerLastSeenInfo.WorldId != null)
                    {
                        ImGui.Text(Util.GetWorld((uint)player.PlayerLastSeenInfo.WorldId).Name.ToString()); //World column
                    }
                    else
                        ImGui.Text("---");

                    ImGui.TableNextColumn();

                    if (player.PlayerLastSeenInfo.CreatedAt != null)
                    {
                        var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(player.PlayerLastSeenInfo.CreatedAt)} ({Tools.ToTimeSinceString((int)player.PlayerLastSeenInfo.CreatedAt)})" : Tools.ToTimeSinceString((int)player.PlayerLastSeenInfo.CreatedAt);
                        ImGui.Text(AddedAtDate); //Added At column
                    }
                    else
                        ImGui.Text("---");
                }
                ImGui.EndTable();
            }

            ImGui.EndGroup();

            if (_LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text(Loc.DtNameHistory); ImGui.SameLine();

                if (ImGui.BeginTable($"_Names", DetailedPlayerNamesTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerNamesTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var name in _LastPlayerDetailedInfo.Player.PlayerNameHistories)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.Text(name.Name); //PlayerName

                        ImGui.TableNextColumn();

                        var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(name.CreatedAt)} ({Tools.ToTimeSinceString((int)name.CreatedAt)})" : Tools.ToTimeSinceString((int)name.CreatedAt);
                        ImGui.Text(AddedAtDate); //Added At column
                    }

                    index++;
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }

            if (_LastPlayerDetailedInfo.Player.PlayerWorldHistories.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text(Loc.DtHomeworldHistory); ImGui.SameLine();

                if (ImGui.BeginTable($"_Worlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerWorldsTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var world in _LastPlayerDetailedInfo.Player.PlayerWorldHistories)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.Text(Util.GetWorld((uint)world.WorldId).Name.ToString()); //World Name

                        ImGui.TableNextColumn();

                        var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(world.CreatedAt)} ({Tools.ToTimeSinceString((int)world.CreatedAt)})" : Tools.ToTimeSinceString((int)world.CreatedAt);
                        ImGui.Text(AddedAtDate); //Added At column
                    }

                    index++;
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }

            if (player.PlayerAltCharacters.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text(Loc.DtAltCharacters); ImGui.SameLine();

                if (ImGui.BeginTable($"AltCharacters", AltCharPlayerTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in AltCharPlayerTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var altChar in _LastPlayerDetailedInfo.Player.PlayerAltCharacters)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        if (ImGui.Button($"{Loc.StLoadDetails} ->" + $"##{index}"))
                        {
                            IsOpen = true;
                            OpenDetailedPlayerWindow((ulong)altChar.LocalContentId, true);
                        }
                        ImGui.SameLine();

                        if (!string.IsNullOrWhiteSpace(altChar.Name))
                            ImGui.Text(altChar.Name); //PlayerName
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        if (!string.IsNullOrWhiteSpace(altChar.WorldId.ToString()))
                            ImGui.Text(Util.GetWorld(((uint)altChar.WorldId)).Name.ToString()); //World
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        ImGui.Text(altChar.LocalContentId.ToString()); //CId

                        index++;
                    }
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }


            var AllRetainers = new List<PlayerDetailed.RetainerDto>(player.Retainers);
            player.PlayerAltCharacters.ToList().ForEach(r =>
            {
                AllRetainers.AddRange(r.Retainers);
            });

            if (AllRetainers.Count > 0)
            {
                if (ImGui.CollapsingHeader($"{Loc.DtShowRetainers} ({AllRetainers.Count})"))
                {
                    if (_seeExtraDetailsOfRetainer != null)
                    {
                        if (_seeExtraDetailsOfRetainer.Names.Count > 0)
                        {
                            ImGuiHelpers.ScaledDummy(5.0f);
                            ImGui.Separator();
                            ImGuiHelpers.ScaledDummy(5.0f);

                            ImGui.BeginGroup();

                            ImGui.Text(Loc.DtNameHistory); ImGui.SameLine();

                            if (ImGui.BeginTable($"_RetainerNames", DetailedPlayerNamesTableColumn.Length, ImGuiTableFlags.BordersInner))
                            {
                                foreach (var t in DetailedPlayerNamesTableColumn)
                                {
                                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                                }
                                ImGui.TableHeadersRow();

                                foreach (var name in _seeExtraDetailsOfRetainer.Names)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text(name.Name); // Name

                                    ImGui.TableNextColumn();

                                    var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(name.CreatedAt)} ({Tools.ToTimeSinceString((int)name.CreatedAt)})" : Tools.ToTimeSinceString((int)name.CreatedAt);
                                    ImGui.Text(AddedAtDate); //Added At column
                                }
                            }
                            ImGui.EndTable();
                            ImGui.EndGroup();
                        }

                        ImGui.SameLine();

                        ImGui.Text("\n");
                        ImGui.SameLine();
                        if (_seeExtraDetailsOfRetainer.Worlds.Count > 0)
                        {
                            ImGui.BeginGroup();

                            ImGui.Text(Loc.DtHomeworldHistory); ImGui.SameLine();

                            if (ImGui.BeginTable($"_RetainerWorlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                            {
                                foreach (var t in DetailedPlayerWorldsTableColumn)
                                {
                                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                                }
                                ImGui.TableHeadersRow();

                                foreach (var world in _seeExtraDetailsOfRetainer.Worlds)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text(Util.GetWorld((uint)world.WorldId).Name.ToString()); //World Name

                                    ImGui.TableNextColumn();

                                    var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(world.CreatedAt)} ({Tools.ToTimeSinceString((int)world.CreatedAt)})" : Tools.ToTimeSinceString((int)world.CreatedAt);
                                    ImGui.Text(AddedAtDate); //Added At column
                                }
                            }
                            ImGui.EndTable();
                            ImGui.EndGroup();
                        }
                    }

                    ImGui.BeginGroup();

                    if (ImGui.BeginTable($"_Retainers", DetailedPlayerRetainerTableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                    {
                        foreach (var t in DetailedPlayerRetainerTableColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var retainer in AllRetainers)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            if (retainer.Names.Count > 1 || retainer.Worlds.Count > 1)
                            {
                                if (ImGui.Button(Loc.DtRetainerInfo + $"##{index}"))
                                {
                                    _seeExtraDetailsOfRetainer = retainer;
                                }
                                ImGui.SameLine();
                            }

                            ImGui.Text(retainer.Names.LastOrDefault().Name); // Name

                            ImGui.TableNextColumn();

                            ImGui.Text(Util.GetWorld((uint)retainer.Worlds.LastOrDefault().WorldId).Name.ToString()); // World

                            ImGui.TableNextColumn();

                            if (retainer.LastSeen != 0)
                            {
                                var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(retainer.LastSeen)} ({Tools.ToTimeSinceString((int)retainer.LastSeen)})" : Tools.ToTimeSinceString((int)retainer.LastSeen);
                                ImGui.Text(LastSeenDate); //Last Seen column
                            }
                            else
                            {
                                var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(retainer.Names.FirstOrDefault().CreatedAt)} ({Tools.ToTimeSinceString((int)retainer.Names.FirstOrDefault().CreatedAt)})" : Tools.ToTimeSinceString((int)retainer.Names.FirstOrDefault().CreatedAt);
                                ImGui.Text(LastSeenDate); //Last Seen column
                            }

                            ImGui.TableNextColumn();

                            var AddedAtDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(retainer.Names.First().CreatedAt)} ({Tools.ToTimeSinceString((int)retainer.Names.First().CreatedAt)})" : Tools.ToTimeSinceString((int)retainer.Names.First().CreatedAt);
                            ImGui.Text(AddedAtDate); //Added At column

                            ImGui.TableNextColumn();

                            var _GetAltCharAsOwner = _LastPlayerDetailedInfo.Player.PlayerAltCharacters.FirstOrDefault(a => a.LocalContentId == retainer.OwnerLocalContentId);
                            string? RetainerOwnerName = _GetAltCharAsOwner != null ? _GetAltCharAsOwner.Name : _LastPlayerDetailedInfo.Player.PlayerNameHistories.LastOrDefault()?.Name;

                            if (!string.IsNullOrWhiteSpace(RetainerOwnerName))
                            {
                                ImGui.Text(RetainerOwnerName); // OwnerName
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.LocalContentId.ToString()); // CId

                            index++;
                        }
                    }
                    ImGui.EndTable();
                    ImGui.EndGroup();
                }

            }
        }

        public void DrawPlayerTerritoryInfoTab()
        {
            var player = _LastPlayerDetailedInfo.Player;

            if (_LastPlayerDetailedInfo.Player.PlayerLastSeenInfo?.TerritoryHistory.Count > 0)
            {
                ImGui.Text(string.Format(Loc.DtLocationHistoryResultsAreDisplayed, _LastPlayerDetailedInfo.Player.PlayerLastSeenInfo?.TerritoryHistory.Count));
                
                ImGui.SameLine();

                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();
 
                if (ImGui.BeginTable($"_Territories", DetailedPlayerTerritoriesTableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                {
                    foreach (var t in DetailedPlayerTerritoriesTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var territory in _LastPlayerDetailedInfo.Player.PlayerLastSeenInfo.TerritoryHistory)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        var getTerritory = PersistenceContext.Instance.Territories.Where(a => a.RowId == territory.TerritoryId).FirstOrDefault();
                        if (getTerritory != null)
                        {
                            if (ImGui.Button(Loc.DtMapButton + $"##{index}"))
                            {
                                var playerPos = Util.Vector3FromString(territory.PlayerPos);
                                var _territory = PersistenceContext.Instance.Territories.First(row => row.RowId == _LastPlayerDetailedInfo.Player.PlayerLastSeenInfo.TerritoryHistory[index].TerritoryId);
                                var mapLink = new MapLinkPayload(
                                    _territory.RowId,
                                    _territory.Map.Value.RowId,
                                    playerPos.X,
                                    playerPos.Y
                                );
                                _logger.LogCritical(playerPos.ToString());
                                _logger.LogCritical($"{playerPos.X} Y: {playerPos.Y}");
                                PlayerScopePlugin._gameGui.OpenMapWithMapLink(mapLink);
                            }
                            ImGui.SameLine();

                            Util.DrawHelp(false, $"{getTerritory.PlaceName.Value.Name.ToString()} | {getTerritory.PlaceNameRegion.Value.Name.ToString()}");
                            ImGui.Text(getTerritory.PlaceName.Value.Name.ToString());
                        }
                        else
                        {
                            Util.DrawHelp(false, $"Player's map could not be detected. Most likely, they were added from lists like player search or friend list.");
                            ImGui.Text(" - ");
                        }
                            

                        ImGui.TableNextColumn();

                        ImGui.Text(Util.GetWorld((uint)territory.WorldId).Name); //World Name

                        ImGui.TableNextColumn();

                        var FirstSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(territory.FirstSeenAt)} ({Tools.ToTimeSinceString((int)territory.FirstSeenAt)})" : Tools.ToTimeSinceString((int)territory.FirstSeenAt);
                        ImGui.Text(FirstSeenDate); //Added At column

                        ImGui.TableNextColumn();

                        var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(territory.LastSeenAt)} ({Tools.ToTimeSinceString((int)territory.LastSeenAt)})" : Tools.ToTimeSinceString((int)territory.LastSeenAt);
                        ImGui.Text(LastSeenDate); //Added At column

                        ImGui.TableNextColumn();

                        int hours;
                        int minutes;
                        int seconds;

                        seconds = (int)territory.LastSeenAt - (int)territory.FirstSeenAt;
                        hours = (int)(Math.Floor((double)(seconds / 3600)));
                        seconds = seconds % 3600;
                        minutes = (int)(Math.Floor((double)(seconds / 60)));
                        seconds = seconds % 60;
                        string time = hours + ":" + minutes + ":" + seconds;

                        ImGui.Text(time); //Duration column

                        index++;
                    }
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }
        }

        public void DrawPlayerCustomizationInfoTab()
        {
            var player = _LastPlayerDetailedInfo.Player;

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.BeginGroup();

            if (ImGui.BeginTable($"_Customization", DetailedPlayerCustomizationTableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
            {
                foreach (var t in DetailedPlayerCustomizationTableColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var customization in _LastPlayerDetailedInfo.Player.PlayerCustomizationHistories)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    var Race = (GenderSubRace)customization.GenderRace;

                    if (Race.SplitRace().Gender == Gender.Male)
                    {
                        ImGuiComponents.IconButton(FontAwesomeIcon.Mars);
                        ImGui.SameLine();
                        ImGui.Text($"{Race.SplitRace().Gender.ToName()} {Race.ToRaceName()} | {Race.ToSubRaceName()}"); //SubRace Name
                    }
                    else
                    {
                        ImGuiComponents.IconButton(FontAwesomeIcon.Venus);
                        ImGui.SameLine();
                        ImGui.Text($"{Race.SplitRace().Gender.ToName()} {Race.ToRaceName()} | {Race.ToSubRaceName()}"); //SubRace Name
                    }

                    ImGui.TableNextColumn();

                    ImGui.Text($"{customization.Height.ToString()}%"); //Height

                    ImGui.TableNextColumn();

                    //RoeFem has Muscle Tone option too
                    if (Race.SplitRace().Gender == Gender.Male || Race == GenderSubRace.SeawolfFemale || Race == GenderSubRace.HellsguardFemale)
                        ImGui.Text($"{customization.MuscleMass.ToString()}%");//MuscleMass
                    else
                        ImGui.Text("---");

                    ImGui.TableNextColumn();

                    if (Race.SplitRace().Gender == Gender.Male)
                        ImGui.Text("---"); //BustSize
                    else
                        ImGui.Text($"{customization.BustSize.ToString()}%");

                    ImGui.TableNextColumn();

                    var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(customization.CreatedAt)} ({Tools.ToTimeSinceString((int)customization.CreatedAt)})" : Tools.ToTimeSinceString((int)customization.CreatedAt);
                    ImGui.Text(LastSeenDate); //Added At column
                }

                index++;
            }
            ImGui.EndTable();
            ImGui.EndGroup();

        }

        List<KeyValuePair<ulong, (CachedPlayer, List<Database.Retainer>)>> FetchedAccounts = new();

        public static void GetAllRetainersofAllAccounts(ulong PlayerContentId)
        {
            var GetRetainers = PersistenceContext._playerWithRetainersCache.Where(p => PlayerContentId == p.Key)
                .SelectMany(player => PersistenceContext._playerWithRetainersCache.Where(x => (x.Value.Player.AccountId == player.Value.Player.AccountId && x.Value.Player.AccountId != null) || 
                                                                                               x.Key == PlayerContentId))
           .ToList();


            foreach(var player in GetRetainers)
            {
                var _GetRetainers = PersistenceContext._playerWithRetainersCache.TryGetValue(player.Key, out var GetPlayer);
                if (_GetRetainers)
                {
                    _TestTempPlayerWithRetainers.GetOrAdd(player.Key, _ => (GetPlayer.Player, GetPlayer.Retainers));
                }
                else
                {
                    _TestTempPlayerWithRetainers.GetOrAdd(player.Key, _ => (GetPlayer.Player, new List<Database.Retainer>()));
                }
            }
        }

    }
}
