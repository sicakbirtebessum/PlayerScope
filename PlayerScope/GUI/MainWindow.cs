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

namespace PlayerScope.GUI
{
    public class MainWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeMain";
        public MainWindow() : base(WindowId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            LanguageChanged();
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
        public void LanguageChanged()
            => WindowName = $"{Loc.TitleMainMenu}{WindowId}";
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
            GetServerRetainersCount();
            UpdateRetainers();
        }

        public readonly string[] TableColumn = new string[]
        {
            Loc.MnName, Loc.MnAccountCount,Loc.MnRetainerCount, Loc.MnRetainers, Loc.MnContentId,Loc.MnAccountId
        };

        public string[] FromServerTableSearchPlayersColumn = new string[]
        {
            Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAccountId, Loc.MnContentId
        };

        private readonly string[] FromServerTableSearchRetainersColumn = new string[]
        {
            Loc.MnName, Loc.MnHomeWorldColumn, Loc.MnAddedAt, Loc.MnOwnerContentId, Loc.MnContentId
        };

        private readonly string[] WorldsTableColumn = new string[]
        {
            Loc.MnWorld, "DataCenter", Loc.MnTotalRetainers
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

        public int TablePlayerMaxLimit = 50;

        public Tabs _CurrentTab = Tabs.General;

        public static void UpdateStatisticsValues()
        {
            _TotalPlayers_Value = PersistenceContext._playerCache.Count;
            _TotalRetainers_Value = PersistenceContext._retainerCache.Count;
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                if (ImGui.BeginTabItem(Loc.MnTabSearchCharacterAndRetainer))
                {
                    _CurrentTab = Tabs.SearchCharactersAndRetainers;
                    DrawSearchPlayersAndRetainers_FromServerTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.MnTabLocalStats))
                {
                    _CurrentTab = Tabs.General;
                    DrawStatisticsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.MnTabMyFavorites))
                {
                    DrawMyFavoriesTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private int lastSelectedPlayerOrRetainerValue = 0;
        private int selectedComboItem_ServerOrLocalDb = 0;
        private int selectedComboItem_PlayerOrRetainer = 0;
        private int selectedComboItem_NameorId = 0;

        private string[] selectedComboItems0 = [Loc.MnServer, Loc.MnLocalPC];
        private string[] selectedComboItems1 = [Loc.MnCharacter, "Retainer"];
        private string[] selectedComboItems2 = [Loc.MnByName, Loc.MnById];

        public Configuration Config = PlayerScopePlugin.Instance.Configuration;

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

        public async Task DrawSearchPlayersAndRetainers_FromServerTab()
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
            if (ImGui.ArrowButton("filtersArrow", bFiltersGuiArrow))
            {
                bShowFilters = !bShowFilters;
            }

            ImGui.SameLine();

            ImGui.Text(Loc.MnSource);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.Combo("##db1", ref selectedComboItem_ServerOrLocalDb, selectedComboItems0, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.Combo("##serverDB1", ref selectedComboItem_PlayerOrRetainer, selectedComboItems1, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);

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
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##searchC", string.Format(Loc.MnTextInputEnterHere, SearchByPlayerOrRetainer, SearchByNameorId), ref _searchContent, 32);

            ImGui.SetNextItemWidth(140);
            ImGui.SameLine();

            if (selectedComboItem_ServerOrLocalDb == 0)
            {
                string SearchButtonText = (bIsNetworkProcessing ? Loc.MnSearching : Loc.MnSearch);
                using (ImRaii.Disabled(bIsNetworkProcessing))
                {
                    if (ImGui.Button(SearchButtonText) || ImGui.IsKeyPressed(ImGuiKey.Enter))
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

                                var query = new PlayerQueryObject() { Name = _searchContent };
                                if (bFilterMatchAnyPartOfName)
                                    query.F_MatchAnyPartOfName = true;

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

                            var query = new RetainerQueryObject() { Name = _searchContent };
                            if (bFilterMatchAnyPartOfName)
                                query.F_MatchAnyPartOfName = true;

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
                Util.ColoredErrorTextWrapped(_LastPlayerSearchResult.Message);
            }

            if (!string.IsNullOrWhiteSpace(_LastRetainerSearchResult.Message))
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                Util.ColoredErrorTextWrapped(_LastRetainerSearchResult.Message);
            }

            if (bShowFilters)
            {
                ImGui.Checkbox(Loc.MnFilterMatchAnyPartOfName, ref bFilterMatchAnyPartOfName);
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
                        foreach (var t in FromServerTableSearchPlayersColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var (localContentId, player) in _LastPlayerSearchResult.Players)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (player == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // PlayerName column
                            
                            if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                            {
                                DetailsWindow.Instance.IsOpen = true;
                                DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                            }
                            ImGui.SameLine();


                            if (!string.IsNullOrWhiteSpace(player.Name))
                            {
                                var highlightAsBot = player.Name.Contains("[BOT]");
                                using (var textColor = highlightAsBot ? ImRaii.PushColor(ImGuiCol.Text, KnownColor.IndianRed.Vector()) : null)
                                {
                                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                                    {
                                        if (ImGui.Button("c" + $"##PlayerName_{index}"))
                                        {
                                            ImGui.SetClipboardText(player.Name);
                                        }
                                        ImGui.SameLine();
                                    }
                                    ImGui.Text(player.Name);

                                    if (highlightAsBot)
                                    {
                                        Util.SetHoverTooltip(Loc.MnPlayerIdentifiedAsBot);
                                    }
                                }
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();  // World column

                            if (player.WorldId != null)
                            {
                                ImGui.Text(Util.GetWorld((uint)player.WorldId).Name.ToString());
                            }
                            else
                            {
                                ImGui.Text("");
                            }

                            ImGui.TableNextColumn(); //AccId column

                            if (player.AccountId != null)
                            {
                                if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                                {
                                    if (ImGui.Button("c" + $"##AccountId_{index}"))
                                    {
                                        ImGui.SetClipboardText(player.AccountId?.ToString());
                                    }
                                    ImGui.SameLine();
                                }
                                ImGui.Text(player.AccountId.ToString());
                            }
                            else
                                ImGui.Text("");

                            ImGui.TableNextColumn();  //cId column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##ContentId_{index}"))
                                {
                                    ImGui.SetClipboardText(localContentId.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(localContentId.ToString());

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
                        foreach (var t in FromServerTableSearchRetainersColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var (localContentId, retainer) in _LastRetainerSearchResult.Retainers)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (retainer == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // RetainerName column

                            if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                            {
                                DetailsWindow.Instance.IsOpen = true;
                                DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)retainer.OwnerLocalContentId, true);
                            }
                            ImGui.SameLine();

                            if (!string.IsNullOrWhiteSpace(retainer.Name))
                            {
                                if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                                {
                                    if (ImGui.Button("c" + $"##RetainerName_{index}"))
                                    {
                                        ImGui.SetClipboardText(retainer.Name);
                                    }
                                    ImGui.SameLine();
                                }
                                ImGui.Text(retainer.Name);
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();  //World column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##RetainerWorldName_{index}"))
                                {
                                    ImGui.SetClipboardText(Util.GetWorld(retainer.WorldId).Name.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(Util.GetWorld(retainer.WorldId).Name.ToString());

                            ImGui.TableNextColumn(); //Created At column

                            var _CreatedAt = Tools.UnixTimeConverter(retainer.CreatedAt).ToString();
                            ImGui.Text(_CreatedAt);

                            ImGui.TableNextColumn(); //OwnerContentId column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##RetainerOwnerContentId_{index}"))
                                {
                                    ImGui.SetClipboardText(retainer.OwnerLocalContentId.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(retainer.OwnerLocalContentId.ToString());

                            ImGui.TableNextColumn(); //RetainerContentId column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##RetainerContentId_{index}"))
                                {
                                    ImGui.SetClipboardText(retainer.LocalContentId.ToString());
                                }
                                ImGui.SameLine();
                            }
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
                    foreach (var t in TableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var (contentId, player) in SearchPlayer(_searchContent))
                    {
                        if (index > TablePlayerMaxLimit)
                            break;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); // PlayerName column

                        if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                        {
                            DetailsWindow.Instance.IsOpen = true;
                            DetailsWindow.Instance.OpenDetailedPlayerWindow(contentId, false);
                        }
                        ImGui.SameLine();

                        if (player.Item1.Name != null)
                        {
                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##{index}"))
                                {
                                    ImGui.SetClipboardText(player.Item1.ToString());
                                }
                                ImGui.SameLine();
                            }
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

                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"###{index}"))
                            {
                                ImGui.SetClipboardText(contentId.ToString());
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(contentId.ToString());

                        ImGui.TableNextColumn(); //AccId column

                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"###{index}"))
                            {
                                ImGui.SetClipboardText(player.Item1.AccountId.ToString());
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(player.Item1.AccountId.ToString());

                        index++;
                    }
                    ImGui.EndTable();
                }
            }
        }
        private async void DrawStatisticsTab()
        {
            if (Tools.UnixTime - LastUnix >= 2)
            {
                UpdateStatisticsValues();
                GetServerRetainersCount();
                LastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            if (!Config.LoggedIn || string.IsNullOrWhiteSpace(Config.Key))
            {
                Util.ShowColoredMessage(Loc.MnErrorYouAreNotConnected);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Lock, Loc.MnOpenSettingsMenutoconnect))
                {
                    SettingsWindow.Instance.IsOpen = true;
                }
            }
            else
            {
                Util.ShowColoredMessage(Loc.MnYouAreConnected);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LockOpen, Loc.MnOpenSettingsMenu))
                {
                    SettingsWindow.Instance.IsOpen = true;
                }
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
                foreach (var t in WorldsTableColumn)
                {
                    int length = 150;
                    if (t == Loc.MnId)
                    {
                        length = 30;
                    }
                    else if (t == Loc.MnTotalRetainers)
                    {
                        length = 150;
                    }

                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.WidthFixed, length);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var server in _TempGetServerRetainersCount.OrderByDescending(a => a.Value.Count))
                {
                    if (index > TablePlayerMaxLimit)
                        break;

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    ImGui.Text(Util.GetWorld(server.Key).Name); // Servername column

                    ImGui.TableNextColumn();

                    ImGui.Text(Util.GetWorld(server.Key).DataCenter.Value.Name); //DataCenter column

                    ImGui.TableNextColumn();

                    ImGui.Text(server.Value.Count().ToString()); //Retainer Count column

                    ImGui.TableNextColumn();

                    index++;
                }
                ImGui.TableNextRow();

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }

        private readonly string[] FavoritedPlayersColumn = new string[]
        {
            Loc.MnName, Loc.MnContentId, Loc.MnAccountId, Loc.MnRemove
        };

        private async void DrawMyFavoriesTab()
        {
            var players = Config.FavoritedPlayer;
            if (players == null) return;
            if (ImGui.BeginTable($"FavoritedPlayersTable", FavoritedPlayersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
            {
                foreach (var t in FavoritedPlayersColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var (localContentId, player) in players)
                {
                    if (index > TablePlayerMaxLimit)
                        break;
                    if (player == null)
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                    {
                        DetailsWindow.Instance.IsOpen = true;
                        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                    }
                    ImGui.SameLine();

                    if (!string.IsNullOrWhiteSpace(player.Name)) // PlayerName column
                    {
                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"##{index}"))
                            {
                                ImGui.SetClipboardText(player.Name);
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(player.Name);
                    }
                    else
                    {
                        ImGui.Text("---");
                    }

                    ImGui.TableNextColumn();  //cId column

                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        if (ImGui.Button("c" + $"###{index}"))
                        {
                            ImGui.SetClipboardText(localContentId.ToString());
                        }
                        ImGui.SameLine();
                    }
                    ImGui.Text(localContentId.ToString());

                    ImGui.TableNextColumn(); //AccId column

                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        if (ImGui.Button("c" + $"###{index}"))
                        {
                            ImGui.SetClipboardText(player.AccountId.ToString());
                        }
                        ImGui.SameLine();
                    }
                    ImGui.Text(player.AccountId.ToString());

                    ImGui.TableNextColumn(); //Remove column

                    if (ImGui.Button("X" + $"###{index}"))
                    {
                        Config.FavoritedPlayer.Remove(localContentId, out _);
                    }

                    index++;
                }
                ImGui.EndTable();
            }
        }

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
            //var orderedWorlds = _TempGetServerRetainersCount.OrderBy(a => a.Value.Count).ToDictionary();
           // var finalWorlds = _TempGetServerRetainersCount = new ConcurrentDictionary<ushort, List<ulong>>(orderedWorlds);
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
