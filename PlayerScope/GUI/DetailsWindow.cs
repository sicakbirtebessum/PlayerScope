using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Collections.Concurrent;
using PlayerScope.Handlers;
using static PlayerScope.Handlers.PersistenceContext;
using System.Xml.Linq;
using PlayerScope.API.Models;
using PlayerScope.API;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Drawing;
using PlayerScope.Models;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using PlayerScope.Properties;
using static PlayerScope.API.Models.PlayerDetailed;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
namespace PlayerScope.GUI
{
    public class DetailsWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeDetails";
        public DetailsWindow() : base(WindowId, ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            UpdateWindowTitle();
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(670, 550),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
        public void UpdateWindowTitle()
            => WindowName = $"{Loc.TitleDetailsMenu}{WindowId}";

        public void LanguageChanged()
        {
            UpdateWindowTitle();

            PlayerTableColumn = new string[]
            {
            Loc.MnCharacter, Loc.MnAccountId, Loc.MnContentId,
            };

            RetainerTableColumn = new string[]
            {
            Loc.DtRetainerName, Loc.MnContentId, Loc.MnWorld, Loc.DtOwnerName
            };

            DetailedPlayerCId_AccIdTableColumn = new string[]
            {
            "##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.MnContentId, Loc.MnAccountId, Loc.DtLodestoneId, Loc.DtCharacterCreationDate
            };

            DetailedPlayerLastSeenZoneTableColumn = new string[]
            {
            Loc.DtLastSeenZoneName, Loc.MnWorld, Loc.MnAddedAt
            };

            DetailedPlayerNamesTableColumn = new string[]
            {
            Loc.DtCharacterName, Loc.MnAddedAt
            };

            DetailedRetainerNamesTableColumn = new string[]
            {
            Loc.DtRetainerName, Loc.MnAddedAt
            };

            DetailedPlayerWorldsTableColumn = new string[]
            {
            Loc.MnHomeWorldColumn, Loc.MnAddedAt
            };

            DetailedPlayerRetainerTableColumn = new string[]
            {
            Loc.DtRetainerName, Loc.MnHomeWorldColumn, Loc.DtLastSeen, Loc.MnAddedAt, Loc.DtOwnerName, Loc.MnContentId
            };

            AltCharPlayerTableColumn = new string[]
            {
             Loc.DtOpenRightArrow, "##Avatar", Loc.DtCharacterName, Loc.MnHomeWorldColumn, Loc.MnContentId
            };

            DetailedPlayerTerritoriesTableColumn = new string[]
            {
            Loc.DtLastSeenZoneName, Loc.MnWorld, Loc.DtFirstSeen, Loc.DtLastSeen, Loc.DtDuration
            };

            DetailedPlayerCustomizationTableColumn = new string[]
            {
            Loc.DtRaceInfo, Loc.DtHeight, Loc.DtMuscleMass, Loc.DtBustSize, Loc.MnAddedAt
            };
        }

        public Configuration Config = Plugin.Instance.Configuration;
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

        private string[] PlayerTableColumn = new string[]
        {
            Loc.MnCharacter, Loc.MnAccountId, Loc.MnContentId,
        };

        private string[] RetainerTableColumn = new string[]
        {
            Loc.DtRetainerName, Loc.MnContentId, Loc.MnWorld, Loc.DtOwnerName
        };

        private string[] DetailedPlayerCId_AccIdTableColumn = new string[]
        {
            "##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.MnContentId, Loc.MnAccountId, Loc.DtLodestoneId, Loc.DtCharacterCreationDate
        };

        private string[] DetailedPlayerLastSeenZoneTableColumn = new string[]
        {
            Loc.DtLastSeenZoneName, Loc.MnWorld, Loc.MnAddedAt
        };

        private string[] DetailedPlayerNamesTableColumn = new string[]
        {
            Loc.DtCharacterName, Loc.MnAddedAt
        };

        private string[] DetailedRetainerNamesTableColumn = new string[]
        {
            Loc.DtRetainerName, Loc.MnAddedAt
        };

        private string[] DetailedPlayerWorldsTableColumn = new string[]
        {
            Loc.MnHomeWorldColumn, Loc.MnAddedAt
        };

        private string[] DetailedPlayerRetainerTableColumn = new string[]
        {
            Loc.DtRetainerName, Loc.MnHomeWorldColumn, Loc.DtLastSeen, Loc.MnAddedAt, Loc.DtOwnerName, Loc.MnContentId
        };

        private string[] AltCharPlayerTableColumn = new string[]
        {
            Loc.DtOpenRightArrow, "##Avatar", Loc.DtCharacterName, Loc.MnHomeWorldColumn, Loc.MnContentId
        };

        private string[] DetailedPlayerTerritoriesTableColumn = new string[]
        {
            Loc.DtLastSeenZoneName, Loc.MnWorld, Loc.DtFirstSeen, Loc.DtLastSeen, Loc.DtDuration
        };
        private string[] DetailedPlayerCustomizationTableColumn = new string[]
        {
            Loc.DtRaceInfo, Loc.DtHeight, Loc.DtMuscleMass, Loc.DtBustSize, Loc.MnAddedAt
        };

        ApiClient _client = ApiClient.Instance;
        public void OpenDetailedPlayerWindow(ulong PlayerContentId, bool GetInfoFromServer)
        {
            _TestTempPlayerWithRetainers.Clear();

            _LastMessage = Loc.DtLoading;
            _seeExtraDetailsOfRetainer = null;

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

                SelectedPlayerContentId = PlayerContentId;
                _LastMessage = string.Empty;
            }
        }

        bool IsDataFromServer = false;

        PlayerDetailed.RetainerDto _seeExtraDetailsOfRetainer = null;
        public string _LastMessage = string.Empty;
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
                        var player = _LastPlayerDetailedInfo.Player;

                        var playerName = player.PlayerNameHistories != null && player.PlayerNameHistories.Count > 0
                            ? player.PlayerNameHistories.LastOrDefault()?.Name ?? "(NO-NAME)"
                            : "(NO-NAME)";

                        ulong? accountId = player.AccountId != null ? (ulong)player.AccountId : null;
                        if (bFavoritesContainPlayer)
                        {
                            Config.FavoritedPlayer.Remove(player.LocalContentId, out _);
                        }
                        else
                        {
                            Config.FavoritedPlayer.GetOrAdd(player.LocalContentId, new Configuration.CachedFavoritedPlayer
                            {
                                AccountId = accountId,
                                Name = playerName
                            });
                        }

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

                    using (ImRaii.Disabled(_LastMessage == Loc.DtLoading))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.DtRefreshProfile))
                        {
                            OpenDetailedPlayerWindow((ulong)_LastPlayerDetailedInfo.Player.LocalContentId, true);
                        }
                    }

                    ImGui.SameLine();

                    using (ImRaii.Disabled(!_clientState.IsLoggedIn))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Panorama, "Adv. Plate" + $"##AdvPlate"))
                        {
                            MainWindow.Instance.OpenAdventurePlate((ulong)_LastPlayerDetailedInfo.Player.LocalContentId);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(_LastMessage))
                {
                    if (_LastMessage == Loc.DtLoading)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(ImGuiColors.ParsedGreen, _LastMessage); // Loading... message
                    }
                    else
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
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Database, Loc.DtFetchDetailedCharacterInfo))
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
                Utils.SetupTableColumns(PlayerTableColumn);

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

                    index++;
                }

                ImGui.EndTable();
            }
            
            ImGui.EndGroup();

            ImGui.BeginGroup();
            ImGui.Text("\n");
            ImGui.Text(Loc.DtRetainersOfThePlayer);

            if (_TestTempPlayerWithRetainers != null && !_TestTempPlayerWithRetainers.IsEmpty)
            {
                if (ImGui.BeginTable($"SocialList##{_searchContent}", RetainerTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    Utils.SetupTableColumns(RetainerTableColumn);
                    
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

                            ImGui.Text($"{Utils.GetWorldName(retainer.WorldId)}"); // Servername column

                            ImGui.TableNextColumn();

                            ImGui.Text(Item.Value.Item1.Name); //OwnerName column
                        }

                        index++;
                    }
                    ImGui.EndTable();
                }
                
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

                if (player.TerritoryHistory.Count > 0)
                {
                    if (ImGui.BeginTabItem(Loc.DtLocationHistory))
                    {
                        DrawPlayerTerritoryInfoTab();
                        ImGui.EndTabItem();
                    }
                }

                if (player.PlayerCustomizationHistories?.Count > 0)
                {
                    string customizationTabHeader = player.PlayerCustomizationHistories.Count > 1
                        ? $"{Loc.DtCustomizationHistory} ({player.PlayerCustomizationHistories.Count})"
                    : Loc.DtCustomizationHistory;

                    if (ImGui.BeginTabItem(customizationTabHeader))
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

            if (player.Flags.Length > 0)
            {
                foreach (var flagValue in player.Flags)
                {
                    if (Enum.IsDefined(typeof(PlayerFlagKey), flagValue))
                    {
                        var flagKey = (PlayerFlagKey)flagValue;

                        if (PlayerFlagsDict.TryGetValue(flagKey, out var flagInfo))
                        {
                            Utils.HeaderWarningText(flagInfo.Color, flagInfo.Icon, flagInfo.Message);
                        }
                    }
                }

                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
            }

            if (player.ProfileVisitInfo != null && player.ProfileVisitInfo.ProfileTotalVisitCount != 0)
            {
                Utils.HeaderProfileVisitInfoText(player.ProfileVisitInfo);
            }

            ImGui.BeginGroup();

            ImGui.Text(Loc.DtShowingResultsFor); 

            if (ImGui.BeginTable($"_Char", DetailedPlayerCId_AccIdTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                Utils.SetupTableColumns(DetailedPlayerCId_AccIdTableColumn);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                //Avatar column
                AvatarViewerWindow.DrawCharacterAvatar(player.PlayerNameHistories.Last().Name, player.PlayerLodestone?.AvatarLink);

                ImGui.TableNextColumn();

                // Name and World column
                string name = player.PlayerNameHistories.LastOrDefault()?.Name ?? string.Empty;
                var world = player.PlayerWorldHistories.Count > 0
                 ? Utils.GetWorld((uint)player.PlayerWorldHistories.Last().WorldId)
                 : null;

                string worldName = world != null ? world.Value.Name.ExtractText() : null;
                if (worldName != null)
                {
                    string nameAndWorld = $"{name}@{worldName}";
                    Utils.CopyButton(nameAndWorld, "##CharacterNameAndWorld");
                    ImGui.Text(nameAndWorld);
                }
                else
                {
                    Utils.CopyButton(name, "##CharacterNameAndWorld");
                    ImGui.Text(name);
                }

                ImGui.TableNextColumn();

                // CId column
                Utils.CopyButton(player.LocalContentId.ToString(), $"##CharacterContentId");
                ImGui.Text(player.LocalContentId.ToString()); 

                ImGui.TableNextColumn();

                // AccId column
                Utils.CopyButton(player.AccountId.ToString(), $"##CharacterAccountId");
                ImGui.Text(player.AccountId.ToString());

                ImGui.TableNextColumn();

                // LodestoneId column
                if (player.PlayerLodestone != null && player.PlayerLodestone.LodestoneId != null)
                {
                    Utils.CopyButton(player.PlayerLodestone.LodestoneId.ToString(), $"##CharacterLodestoneId");

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, player.PlayerLodestone.LodestoneId?.ToString()))
                    {
                        Utils.TryOpenURI(new Uri($"{Utils.lodestoneCharacterUrl}{player.PlayerLodestone.LodestoneId}"));
                    }
                }
                else
                {
                    ImGui.Text(string.Empty);
                }

                ImGui.TableNextColumn();

                // Char CreatedAt column
                if (player.PlayerLodestone != null && player.PlayerLodestone.CharacterCreationDate != null)
                {
                    var addedAtDate = Config.bShowDetailedDate
                               ? $"{Tools.UnixTimeConverter(player.PlayerLodestone.CharacterCreationDate)} ({Tools.ToTimeSinceString((int)player.PlayerLodestone.CharacterCreationDate)})"
                               : Tools.ToTimeSinceString((int)player.PlayerLodestone.CharacterCreationDate);
                    ImGui.Text(addedAtDate);
                }
                else
                {
                    ImGui.Text(string.Empty);
                }

                ImGui.EndTable();
            }

            ImGuiHelpers.ScaledDummy(5.0f);

            if (player.TerritoryHistory.Count > 0)
            {
                var latestCharTerritory = player.TerritoryHistory.First();
                var getTerritory = Tools.GetTerritory((ushort)latestCharTerritory.TerritoryId);

                if (getTerritory != null)
                {
                    var territory = getTerritory.Value;

                    if (ImGui.BeginTable($"_Lastseen", DetailedPlayerLastSeenZoneTableColumn.Length, ImGuiTableFlags.BordersInner))
                    {
                        Utils.SetupTableColumns(DetailedPlayerLastSeenZoneTableColumn);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        // Zone Name column
                        string dutyName = territory.ContentFinderCondition.ValueNullable?.Name.ExtractText();
                        string placeName = territory.PlaceName.ValueNullable?.Name.ExtractText();
                        string regionName = territory.PlaceNameRegion.ValueNullable?.Name.ExtractText();

                        string IntentedUseDescription = $"{((TerritoryIntendedUseEnum)territory.TerritoryIntendedUse.RowId).GetDescription()}";

                        if (!string.IsNullOrEmpty(dutyName))
                        {
                            Utils.IconWithTooltip(FontAwesomeIcon.Dungeon, $"[{IntentedUseDescription}] {placeName} | {regionName}");
                            ImGui.Text(dutyName);
                        }
                        else if (!string.IsNullOrEmpty(placeName) && !string.IsNullOrEmpty(regionName))
                        {
                            Utils.DrawHelp(false, $"[{IntentedUseDescription}] {placeName} | {regionName}");
                            ImGui.Text(placeName);
                        }
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        // World Column
                        Utils.DisplayWorldInfo((uint?)latestCharTerritory?.WorldId);

                        ImGui.TableNextColumn();

                        // LastSeenAt Column
                        if (latestCharTerritory.LastSeenAt > 0)
                        {
                            var addedAtDate = Config.bShowDetailedDate
                                ? $"{Tools.UnixTimeConverter(latestCharTerritory.LastSeenAt)} ({Tools.ToTimeSinceString((int)latestCharTerritory.LastSeenAt)})"
                                : Tools.ToTimeSinceString((int)latestCharTerritory.LastSeenAt);
                            ImGui.Text(addedAtDate); 
                        }
                        else
                            ImGui.Text("---");

                        ImGui.EndTable();
                    }
                }
            }

            ImGui.EndGroup();

            if (_LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0 || _LastPlayerDetailedInfo.Player.PlayerWorldHistories.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                if (_LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0)
                {
                    ImGui.Text(Loc.DtNameHistory);

                    var tableHeight = _LastPlayerDetailedInfo.Player.PlayerNameHistories.Count * ImGui.GetTextLineHeightWithSpacing();

                    if (ImGui.BeginTable("_CharacterNames", DetailedPlayerNamesTableColumn.Length, ImGuiTableFlags.BordersInner, new Vector2(ImGui.GetContentRegionAvail().X / 2, tableHeight)))
                    {
                        Utils.SetupTableColumns(DetailedPlayerNamesTableColumn);

                        foreach (var name in _LastPlayerDetailedInfo.Player.PlayerNameHistories)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            Utils.CopyButton(name.Name, $"##NameHistory{name.Name}");
                            ImGui.Text(name.Name);

                            ImGui.TableNextColumn();
                            var AddedAtDate = Config.bShowDetailedDate
                                ? $"{Tools.UnixTimeConverter(name.CreatedAt)} ({Tools.ToTimeSinceString((int)name.CreatedAt)})"
                                : Tools.ToTimeSinceString((int)name.CreatedAt);

                            if (Utils.ExternalDbTimestamps.Contains(name.CreatedAt))
                                Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);

                            ImGui.Text(AddedAtDate);
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.EndGroup();

                ImGui.SameLine();

                ImGui.BeginGroup();

                if (_LastPlayerDetailedInfo.Player.PlayerWorldHistories.Count > 0)
                {
                    ImGui.Text(Loc.DtHomeworldHistory);

                    var tableHeight = _LastPlayerDetailedInfo.Player.PlayerWorldHistories.Count * ImGui.GetTextLineHeightWithSpacing();

                    if (ImGui.BeginTable("_CharacterWorlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner, new Vector2(ImGui.GetContentRegionAvail().X, tableHeight)))
                    {
                        Utils.SetupTableColumns(DetailedPlayerWorldsTableColumn);

                        foreach (var world in _LastPlayerDetailedInfo.Player.PlayerWorldHistories)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            Utils.DisplayWorldInfo((uint?)world.WorldId);

                            ImGui.TableNextColumn();
                            var AddedAtDate = Config.bShowDetailedDate
                                ? $"{Tools.UnixTimeConverter(world.CreatedAt)} ({Tools.ToTimeSinceString((int)world.CreatedAt)})"
                                : Tools.ToTimeSinceString((int)world.CreatedAt);

                            if (Utils.ExternalDbTimestamps.Contains(world.CreatedAt))
                                Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);

                            ImGui.Text(AddedAtDate);
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.EndGroup();
            }

            ImGuiHelpers.ScaledDummy(2.0f);
            ImGui.Separator();

            if (player.PlayerAltCharacters.Count > 0)
            {
                if (player.PlayerAltCharacters.Count >= 10)
                {
                    if (ImGui.CollapsingHeader($"{Loc.DtShowAltCharacters} ({player.PlayerAltCharacters.Count})"))
                    {
                        DisplayAltCharacters(player.PlayerAltCharacters);
                    }
                }
                else
                {
                    DisplayAltCharacters(player.PlayerAltCharacters);
                }
            }

            var AllRetainers = new List<PlayerDetailed.RetainerDto>(player.Retainers);
            player.PlayerAltCharacters.ToList().ForEach(r =>
            {
                AllRetainers.AddRange(r.Retainers);
            });

            if (AllRetainers.Count > 0)
            {
                if (AllRetainers.Count >= 10)
                {
                    if (ImGui.CollapsingHeader($"{Loc.DtShowRetainers} ({AllRetainers.Count})"))
                    {
                        DisplayRetainers(AllRetainers);
                    }
                }
                else
                {
                    DisplayRetainers(AllRetainers);
                }
            }
        }

        void DisplayAltCharacters(List<PlayerDetailedInfoAltCharDto> altCharacters)
        {
            ImGui.Spacing();

            ImGui.BeginGroup();

            if (altCharacters.Count < 10)
            {
                ImGui.Text($"{Loc.DtAltCharacters} ({altCharacters.Count}):");
            }

            if (ImGui.BeginTable($"AltCharacters", AltCharPlayerTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                Utils.SetupTableColumns(AltCharPlayerTableColumn);

                var index = 0;

                foreach (var altChar in altCharacters)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    using (ImRaii.Disabled(_LastMessage == Loc.DtLoading))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                        {
                            OpenDetailedPlayerWindow((ulong)altChar.LocalContentId, true);
                        }
                    }

                    ImGui.TableNextColumn();

                    AvatarViewerWindow.DrawCharacterAvatar(altChar.Name, altChar.AvatarLink);

                    ImGui.TableNextColumn();

                    if (!string.IsNullOrWhiteSpace(altChar.Name))
                    {
                        Utils.CopyButton(altChar.Name, $"##AltCharNameHistory{index}");
                        ImGui.Text(altChar.Name); //PlayerName
                    }
                    else
                    {
                        ImGui.Text("---");
                    }

                    ImGui.TableNextColumn();

                    Utils.DisplayWorldInfo((uint?)altChar.WorldId);

                    ImGui.TableNextColumn();

                    Utils.CopyButton(altChar.LocalContentId.ToString(), $"##AltCharContentId{index}");
                    ImGui.Text(altChar.LocalContentId.ToString()); //CId

                    index++;
                }

                ImGui.EndTable();
            }

            ImGui.EndGroup();
        }

        void DisplayRetainers(List<PlayerDetailed.RetainerDto> AllRetainers)
        {
            if (AllRetainers.Count < 10)
            {
                ImGui.Text($"{Loc.MnRetainers} ({AllRetainers.Count}):");
            }

            if (_seeExtraDetailsOfRetainer != null)
            {
                if (_seeExtraDetailsOfRetainer.Names != null && _seeExtraDetailsOfRetainer.Names.Count > 0)
                {
                    ImGuiHelpers.ScaledDummy(5.0f);
                    ImGui.Separator();
                    ImGuiHelpers.ScaledDummy(5.0f);

                    ImGui.BeginGroup();

                    ImGui.Text(Loc.DtNameHistory);
                    ImGui.SameLine();

                    if (ImGui.BeginTable($"_RetainerNames", DetailedRetainerNamesTableColumn.Length, ImGuiTableFlags.BordersInner))
                    {
                        Utils.SetupTableColumns(DetailedRetainerNamesTableColumn);

                        var index = 0;
                        foreach (var name in _seeExtraDetailsOfRetainer.Names)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            // Name column
                            if (!string.IsNullOrEmpty(name.Name))
                            {
                                Utils.CopyButton(name.Name, $"##RetainerDetailName{index}");
                                ImGui.Text(name.Name);
                            }
                            else
                                ImGui.Text("---");

                            ImGui.TableNextColumn();

                            // Added At column
                            var AddedAtDate = Config.bShowDetailedDate
                                ? $"{Tools.UnixTimeConverter(name?.CreatedAt ?? 0)} ({Tools.ToTimeSinceString((int)(name?.CreatedAt ?? 0))})"
                                : Tools.ToTimeSinceString((int)(name?.CreatedAt ?? 0));

                            if (Utils.ExternalDbTimestamps.Contains(name.CreatedAt))
                                Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);

                            ImGui.Text(AddedAtDate);

                            index++;
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndGroup();
                }

                ImGui.SameLine();

                if (_seeExtraDetailsOfRetainer.Worlds != null && _seeExtraDetailsOfRetainer.Worlds.Count > 0)
                {
                    ImGui.BeginGroup();

                    ImGui.Text(Loc.DtHomeworldHistory);
                    ImGui.SameLine();

                    if (ImGui.BeginTable($"_RetainerWorlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                    {
                        Utils.SetupTableColumns(DetailedPlayerWorldsTableColumn);

                        foreach (var world in _seeExtraDetailsOfRetainer.Worlds)
                        {
                            if (world == null)
                                continue;

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            Utils.DisplayWorldInfo((uint?)world.WorldId);

                            ImGui.TableNextColumn();

                            var AddedAtDate = Config.bShowDetailedDate
                                ? $"{Tools.UnixTimeConverter(world?.CreatedAt ?? 0)} ({Tools.ToTimeSinceString((int)(world?.CreatedAt ?? 0))})"
                                : Tools.ToTimeSinceString((int)(world?.CreatedAt ?? 0));

                            if (Utils.ExternalDbTimestamps.Contains(world!.CreatedAt))
                                Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);

                            ImGui.Text(AddedAtDate); // Added At column
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndGroup();
                }
            }

            ImGui.BeginGroup();

            if (ImGui.BeginTable($"_Retainers", DetailedPlayerRetainerTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                Utils.SetupTableColumns(DetailedPlayerRetainerTableColumn);

                var index = 0;

                foreach (var retainer in AllRetainers)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    var retainerLastName = retainer?.Names?.LastOrDefault()?.Name ?? "---";
                    var retainerLastWorld = retainer?.Worlds?.LastOrDefault();

                    if ((retainer?.Names?.Count ?? 0) > 1 || (retainer?.Worlds?.Count ?? 0) > 1)
                    {
                        if (ImGui.Button(Loc.DtRetainerInfo + $"##{index}"))
                        {
                            _seeExtraDetailsOfRetainer = retainer;
                        }
                        ImGui.SameLine();
                    }

                    // Name Column
                    Utils.CopyButton(retainerLastName, $"##RetainerName{index}");
                    ImGui.Text(retainerLastName);

                    ImGui.TableNextColumn();

                    // World Column
                    Utils.DisplayWorldInfo((uint?)retainerLastWorld?.WorldId);

                    ImGui.TableNextColumn();

                    // Last Seen column
                    string lastSeenDate;
                    if (retainer?.LastSeen != 0)
                    {
                        lastSeenDate = Config.bShowDetailedDate
                            ? $"{Tools.UnixTimeConverter(retainer.LastSeen)} ({Tools.ToTimeSinceString((int)retainer.LastSeen)})"
                            : Tools.ToTimeSinceString((int)retainer.LastSeen);
                    }
                    else
                    {
                        var firstCreatedAt = retainer?.Names?.FirstOrDefault()?.CreatedAt ?? 0;
                        lastSeenDate = Config.bShowDetailedDate
                            ? $"{Tools.UnixTimeConverter(firstCreatedAt)} ({Tools.ToTimeSinceString((int)firstCreatedAt)})"
                            : Tools.ToTimeSinceString((int)firstCreatedAt);
                    }
                    ImGui.Text(lastSeenDate);

                    ImGui.TableNextColumn();

                    // Added At column
                    var addedAtDate = retainer?.Names?.FirstOrDefault()?.CreatedAt ?? 0;
                    var addedAtText = Config.bShowDetailedDate
                        ? $"{Tools.UnixTimeConverter(addedAtDate)} ({Tools.ToTimeSinceString((int)addedAtDate)})"
                        : Tools.ToTimeSinceString((int)addedAtDate);

                    if (Utils.ExternalDbTimestamps.Contains(addedAtDate))
                        Utils.WarningIconWithTooltip(Loc.DtDatabaseRecordAddedTimeUnavailable);

                    ImGui.Text(addedAtText);

                    ImGui.TableNextColumn();

                    // OwnerName
                    var retainerOwner = _LastPlayerDetailedInfo.Player?.PlayerAltCharacters
                                        ?.FirstOrDefault(a => a.LocalContentId == retainer?.OwnerLocalContentId);
                    string retainerOwnerName = retainerOwner?.Name
                        ?? _LastPlayerDetailedInfo.Player?.PlayerNameHistories?.LastOrDefault()?.Name
                        ?? "---";
                    Utils.CopyButton(retainerOwnerName, $"##RetainerOwnerName{index}");
                    ImGui.Text(retainerOwnerName);

                    ImGui.TableNextColumn();

                    // LocalContentId Column
                    Utils.CopyButton(retainer?.LocalContentId.ToString(), $"##RetainerContentId{index}");
                    ImGui.Text(retainer?.LocalContentId.ToString() ?? "---");

                    index++;
                }
            }
            ImGui.EndTable();
            ImGui.EndGroup();
        }
        public void DrawPlayerTerritoryInfoTab()
        {
            var player = _LastPlayerDetailedInfo.Player;

            if (player.TerritoryHistory.Count > 0)
            {
                ImGui.Text(string.Format(Loc.DtLocationHistoryResultsAreDisplayed, player.TerritoryHistory.Count));
                ImGui.SameLine();

                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();
 
                if (ImGui.BeginTable($"_Territories", DetailedPlayerTerritoriesTableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                {
                    Utils.SetupTableColumns(DetailedPlayerTerritoriesTableColumn);

                    var index = 0;

                    foreach (var territory in player.TerritoryHistory)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        var getTerritory = Tools.GetTerritory((ushort)territory.TerritoryId);
                        if (!getTerritory.HasValue)
                        {
                            continue;
                        }

                        using (ImRaii.Disabled(territory.PlayerPos == null))
                        {
                            if (ImGui.Button(Loc.DtMapButton + $"##MapButton{index}"))
                            {
                                var playerPos = Utils.Vector3FromString(territory.PlayerPos);
                                var mapLink = new MapLinkPayload(
                                    getTerritory.Value.RowId,
                                    getTerritory.Value.Map.Value.RowId,
                                    playerPos.X,
                                    playerPos.Y
                                );
                                Plugin._gameGui.OpenMapWithMapLink(mapLink);
                            }
                        }

                        ImGui.SameLine();

                        // Zone Name column
                        string dutyName = getTerritory.Value.ContentFinderCondition.ValueNullable?.Name.ExtractText();
                        string placeName = getTerritory.Value.PlaceName.ValueNullable?.Name.ExtractText();
                        string regionName = getTerritory.Value.PlaceNameRegion.ValueNullable?.Name.ExtractText();

                        string IntentedUseDescription = $"{((TerritoryIntendedUseEnum)getTerritory.Value.TerritoryIntendedUse.RowId).GetDescription()}";

                        if (!string.IsNullOrEmpty(dutyName))
                        {
                            Utils.IconWithTooltip(FontAwesomeIcon.Dungeon, $"[{IntentedUseDescription}] {placeName} | {regionName}");
                            ImGui.Text(dutyName);
                        }
                        else if (!string.IsNullOrEmpty(placeName) && !string.IsNullOrEmpty(regionName))
                        {
                            Utils.DrawHelp(false, $"[{IntentedUseDescription}] {placeName} | {regionName}");
                            ImGui.Text(placeName);
                        }
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        //WorldName Column
                        var worldName = territory.WorldId != null ? Utils.GetWorldName((uint)territory.WorldId) : "---";
                        ImGui.Text(worldName); 

                        ImGui.TableNextColumn();

                        //Added At column
                        var FirstSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(territory.FirstSeenAt)} ({Tools.ToTimeSinceString((int)territory.FirstSeenAt)})" : Tools.ToTimeSinceString((int)territory.FirstSeenAt);
                        ImGui.Text(FirstSeenDate); 

                        ImGui.TableNextColumn();

                        var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(territory.LastSeenAt)} ({Tools.ToTimeSinceString((int)territory.LastSeenAt)})" : Tools.ToTimeSinceString((int)territory.LastSeenAt);
                        ImGui.Text(LastSeenDate); //Added At column

                        ImGui.TableNextColumn();

                        //Duration column
                        var durationSeconds = (int)(territory.LastSeenAt - territory.FirstSeenAt);
                        if (durationSeconds > 0)
                        {
                            var hours = durationSeconds / 3600;
                            var minutes = (durationSeconds % 3600) / 60;
                            var seconds = durationSeconds % 60;

                            var duration = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                            ImGui.Text(duration);
                        }

                        index++;
                    }
                    ImGui.EndTable();
                }
                
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
                Utils.SetupTableColumns(DetailedPlayerCustomizationTableColumn);
                
                var index = 0;

                foreach (var customization in player.PlayerCustomizationHistories)
                {
                    if (!IsCustomizationValid(customization))
                        continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    var race = (GenderSubRace)(customization.GenderRace ?? (int)GenderSubRace.Unknown);
                    var gender = race.SplitRace().Gender;
                    var genderIcon = gender == Gender.Male ? FontAwesomeIcon.Mars : FontAwesomeIcon.Venus;
                    var genderColor = gender == Gender.Male ? ImGuiColors.ParsedBlue : ImGuiColors.ParsedPink;

                    Utils.IconText(genderColor, genderIcon);
                    ImGui.SameLine();
                    ImGui.Text($"{gender.ToName()} {race.ToRaceName()} | {race.ToSubRaceName()}");

                    ImGui.TableNextColumn();

                    ImGui.Text($"{customization.Height.ToString()}%"); //Height

                    ImGui.TableNextColumn();

                    //RoeFem has Muscle Tone option too
                    if (race.SplitRace().Gender == Gender.Male || race == GenderSubRace.SeawolfFemale || race == GenderSubRace.HellsguardFemale)
                        ImGui.Text($"{customization.MuscleMass.ToString()}%");//MuscleMass
                    else
                        ImGui.Text("---");

                    ImGui.TableNextColumn();

                    if (race.SplitRace().Gender == Gender.Male)
                        ImGui.Text("---"); //BustSize
                    else
                        ImGui.Text($"{customization.BustSize.ToString()}%");

                    ImGui.TableNextColumn();

                    var LastSeenDate = Config.bShowDetailedDate ? $"{Tools.UnixTimeConverter(customization.CreatedAt)} ({Tools.ToTimeSinceString((int)customization.CreatedAt)})" : Tools.ToTimeSinceString((int)customization.CreatedAt);
                    ImGui.Text(LastSeenDate); //Added At column

                    index++;
                }
                ImGui.EndTable();
            }
            
            ImGui.EndGroup();
        }

        private static bool IsCustomizationValid(PlayerCustomizationHistoryDto customization)
        {
            return customization.GenderRace.HasValue &&
                   customization.TailShape.HasValue &&
                   customization.SmallIris.HasValue &&
                   customization.Nose.HasValue &&
                   customization.MuscleMass.HasValue &&
                   customization.Mouth.HasValue &&
                   customization.Jaw.HasValue &&
                   customization.Height.HasValue &&
                   customization.Face.HasValue &&
                   customization.EyeShape.HasValue &&
                   customization.BustSize.HasValue &&
                   customization.BodyType.HasValue;
        }

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
