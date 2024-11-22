using Dalamud;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using Lumina;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using RestSharp;
using PlayerScope.API;
using PlayerScope.API.Models;
using PlayerScope.API.Query;
using PlayerScope.Database;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase.Delegates;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonJobHudMNK1.ChakraGauge;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using static PlayerScope.API.Models.User;
using static PlayerScope.Configuration;
using static PlayerScope.GUI.MainWindow;
using static System.Net.Mime.MediaTypeNames;
using Lumina.Excel.Sheets;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PlayerScope.GUI
{
    public class SettingsWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeSettings";
        public SettingsWindow() : base(WindowId, ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            OnLanguageChange();
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 450),
                MaximumSize = new Vector2(9999, 9999)
            };
            TitleBarButtons.Add(new()
            {
                Click = (m) => { if (m == ImGuiMouseButton.Left) MainWindow.Instance.IsOpen = true; },
                Icon = FontAwesomeIcon.Database,
                IconOffset = new(2, 2),
                ShowTooltip = () => ImGui.SetTooltip(Loc.MnOpenMainMenu),
            });
        }
        public void LanguageChanged()
        {
            PlayerAndRetainersWorldStatsColumn = new string[]
            {
            Loc.StHomeWorldName, Loc.StCharacterCountColumn, Loc.StRetainerCountColumn
            };
        }
        private static SettingsWindow _instance = null;
        public static SettingsWindow Instance
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
        public override void OnOpen()
        {
            base.OnOpen();

            if (!Config.AgreementAccepted)
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(700, 650),
                    MaximumSize = new Vector2(900, 900)
                };

            IsRefreshed = false;
            IsSaved = false;
            Option_ObjRefreshInterval = Config.ObjectTableRefreshInterval;
            if (string.IsNullOrWhiteSpace(_client._ServerStatus) || _client._ServerStatus != "ONLINE")
            {
                _client.CheckServerStatus();
            }

            if (Config.LoggedIn)
                RefreshUserProfileInfo();
        }

        public void CheckLocalPlayer()
        {
            if (PersistenceContext._clientState != null && PersistenceContext._clientState is { IsLoggedIn: true, LocalContentId: > 0 })
            {
                IGameObject? localCharacter = PersistenceContext._clientState.LocalPlayer;
                if (localCharacter == null || localCharacter.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    return;

                unsafe
                {
                    var bChar = (BattleChara*)localCharacter.Address;

                    if (_playerName != $"{bChar->NameString}")
                    {
                        if (string.IsNullOrWhiteSpace(_playerName) || string.IsNullOrWhiteSpace(_worldName))
                        {
                            var homeWorld = PersistenceContext._clientState.LocalPlayer?.HomeWorld;
                            _playerName = $"{bChar->NameString}";
                            _worldName = homeWorld.Value.Value.Name.ExtractText();
                            _worldId = homeWorld.Value.RowId;
                            _accountId = (int)bChar->AccountId;
                            _contentId = (long)bChar->ContentId;

                            Config.ContentId = _contentId;
                            Config.AccountId = _accountId;
                            Config.Username = _playerName;
                        }
                    }
                }
            }
        }

        string _playerName = string.Empty;
        string _worldName = string.Empty;
        uint _worldId = 0;
        long _contentId = 0;
        int _accountId = 0;

        public Configuration Config = Plugin.Instance.Configuration;
        ApiClient _client = ApiClient.Instance;

        public void SaveUserResultToConfig(User user)
        {
            if (user != null)
            {
                LastUserInfo = user;

                if (LastUserInfo.BaseUrl != null)
                    Config.BaseUrl = LastUserInfo.BaseUrl;

                Config.Username = LastUserInfo.Name;
                Config.ContentId = LastUserInfo.LocalContentId;
                Config.AccountId = LastUserInfo.GameAccountId;
                Config.AppRoleId = LastUserInfo.AppRoleId;
                
                Config.UploadedPlayersCount = LastUserInfo.NetworkStats?.UploadedPlayersCount;
                Config.UploadedPlayerInfoCount = LastUserInfo.NetworkStats?.UploadedPlayerInfoCount;
                Config.UploadedRetainersCount = LastUserInfo.NetworkStats?.UploadedRetainersCount;
                Config.UploadedRetainerInfoCount = LastUserInfo.NetworkStats?.UploadedRetainerInfoCount;
                Config.FetchedPlayerInfoCount = LastUserInfo.NetworkStats?.FetchedPlayerInfoCount;
                Config.SearchedNamesCount = LastUserInfo.NetworkStats?.SearchedNamesCount;
                Config.LastSyncedTime = LastUserInfo.NetworkStats?.LastSyncedTime;

                if (LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0)
                {
                    LastUserCharactersPrivacySettings = LastUserInfo.Characters
                    .Select(character => character == null ? null : new User.UserCharacterDto
                    {
                        LocalContentId = character.LocalContentId,
                        Privacy = character.Privacy != null ? new CharacterPrivacySettingsDto
                        {
                            HideFullProfile = character.Privacy.HideFullProfile,
                            HideTerritoryInfo = character.Privacy.HideTerritoryInfo,
                            HideCustomizations = character.Privacy.HideCustomizations,
                            HideInSearchResults = character.Privacy.HideInSearchResults,
                            HideRetainersInfo = character.Privacy.HideRetainersInfo,
                            HideAltCharacters = character.Privacy.HideAltCharacters
                        } : null,
                    }).ToList();
                }

                if (LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0)
                {
                    foreach (var character in LastUserInfo.Characters)
                    {
                        if (character.Privacy == null)
                        {
                            character.Privacy = new CharacterPrivacySettingsDto();
                        }
                        _localUserCharacters[(long)character.LocalContentId] = new UserCharacters { Name = character.Name, Privacy = character.Privacy };
                    }
                }
   
                Config.LoggedIn = true;
                Config.FreshInstall = false;

                Config.Save();
            }
        }

        User LastUserInfo { get; set; }
        List<User.UserCharacterDto?> LastUserCharactersPrivacySettings = new();

        bool IsRefreshed;
        bool IsSaved;

        bool bIsNetworkProcessing = false;
        public string LastNetworkMessage = string.Empty;
        public string LastRegistrationWindowMessage = string.Empty;
        public int LastNetworkMessageTime;
        public override void Draw()
        {
            if (!Config.AgreementAccepted)
                DrawAgreementTab();
            else
                ServerStatusGui();
            using (var tabBar = ImRaii.TabBar("Tabs"))
            {
                if (tabBar)
                {
                    using (var tabItem = ImRaii.TabItem(Loc.StTabUserInfo))
                    {
                        if (tabItem)
                        {
                            DrawUserInfoTab();
                        }
                    }
                    using (ImRaii.Disabled(!Config.LoggedIn))
                    {
                        using (var tabItem = ImRaii.TabItem(Loc.StTabServerStatsAndSync))
                        {
                            if (tabItem)
                            {
                                DrawServerStatsTab();
                            }
                        }

                        using (var tabItem = ImRaii.TabItem(Loc.StTabMyCharactersAndPrivacy))
                        {
                            if (tabItem)
                            {
                                DrawMyCharactersTab();
                            }
                        }

                        using (var tabItem = ImRaii.TabItem(Loc.StOptions))
                        {
                            if (tabItem)
                            {
                                DrawOptionsTab();
                            }
                        }
                    }
                }
            }
        }
        public static string GetEnumDisplayName(Roles enumValue)
        {
            //var value = enumValue.GetType().GetMember(enumValue.ToString()).First()?.GetCustomAttribute<DisplayAttribute>()?.Name;
            switch (enumValue)
            {
                case Roles.Banned:
                    return Loc.StRoleBanned;
                case Roles.Guest:
                    return Loc.StRoleGuest;
                case Roles.Member:
                    return Loc.StRoleMember;
                case Roles.Verified_Member:
                    return Loc.StRoleVerifiedMember;
                case Roles.Vip:
                    return Loc.StRoleVip;
                case Roles.Moderator:
                    return Loc.StRoleMod;
                case Roles.Admin:
                    return Loc.StRoleAdmin;
                case Roles.Owner:
                    return Loc.StRoleOwner;
            }
            return "...";
        }
        private bool _agreementAccepted;
        private async void DrawAgreementTab()
        {
            ServerStatusGui();
            using (ImRaii.Disabled(bIsNetworkProcessing || _client._ServerStatus != "ONLINE"))
            {
                Utils.ColoredTextWrapped(ImGuiColors.ParsedPink, Loc.AgCrowsourcedDescription);
                DrawSection(Loc.AgServerBasedFeatures, [ Loc.AgFeature1, Loc.AgFeature2, Loc.AgFeature3, Loc.AgFeature4, Loc.AgFeature5, Loc.AgFeature6 ], true);

                ImGui.Spacing();

                DrawSection(Loc.AgAgreementTitle, [ Loc.AgAgreement1, Loc.AgAgreement2, Loc.AgAgreement3, Loc.AgAgreement4 ], true);

                ImGui.Separator();
                ImGui.Spacing();
                Utils.TextWrapped(Loc.AgDoYouAccept);

                using (var textColor = ImRaii.PushColor(ImGuiCol.Text, KnownColor.IndianRed.Vector()))
                using (var textColor1 = ImRaii.PushColor(ImGuiCol.CheckMark, KnownColor.Green.Vector()))
                    ImGui.Checkbox(Loc.AgIAgreeCheckBox, ref _agreementAccepted);

                using (ImRaii.Disabled(!_agreementAccepted))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, Loc.StSaveConfig))
                    {
                        this.SizeConstraints = new WindowSizeConstraints
                        {
                            MinimumSize = new Vector2(600, 450),
                            MaximumSize = new Vector2(9999, 9999)
                        };
                        Config.AgreementAccepted = _agreementAccepted;
                        Config.Save();
                    }
            }
        }

        private async void DrawUserInfoTab()
        {
            if (!string.IsNullOrWhiteSpace(Config.Key) && Config.LoggedIn)
            {
                ImGui.Text(Loc.StCharacterName);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, Config.Username);

                ImGui.Text(Loc.StRole); 
                ImGui.SameLine();

                if (Config.AppRoleId < (int)User.Roles.Member)
                {
                    ImGui.TextColored(ImGuiColors.DPSRed, $"{GetEnumDisplayName((User.Roles)Config.AppRoleId)}");
                    Utils.DrawHelp(true, Loc.StRoleDescriptionNonVerified);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.HealerGreen, $"{GetEnumDisplayName((User.Roles)Config.AppRoleId)}");
                    ImGui.SameLine();

                    ImGui.Text(Loc.StPermissions); ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.HealerGreen, Loc.StSendAndReceiveData);
                    Utils.DrawHelp(true, Loc.StAuthorizeDescription);
                }

                if (ImGui.CollapsingHeader(Loc.StShowMyIds))
                {
                    ImGui.Text("Account Id:");
                    ImGui.SameLine();
                   
                    Utils.TextCopy(ImGuiColors.TankBlue, Config.AccountId.ToString());
                    Utils.SetHoverTooltip(Loc.StClickToCopyAccountId);

                    ImGui.SameLine();

                    ImGui.Text("LocalContent Id: ");
                    ImGui.SameLine();
                    Utils.TextCopy(ImGuiColors.TankBlue, Config.ContentId.ToString());
                    Utils.SetHoverTooltip(Loc.StClickToCopyContentId);
                }

                if (ImGui.CollapsingHeader(Loc.StMyContributions, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    Utils.DrawHelp(false, Loc.StUploadedCharactersHint);
                    ImGui.Text(Loc.StUploadedCharacters);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayersCount.ToString());

                    Utils.DrawHelp(false, Loc.StUploadedCharacterInfoHint);
                    ImGui.Text(Loc.StUploadedCharacterInfo);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayerInfoCount.ToString());

                    Utils.DrawHelp(false, Loc.StUploadedRetainersHint);
                    ImGui.Text(Loc.StUploadedRetainers);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainersCount.ToString());
                   
                    Utils.DrawHelp(false, Loc.StUploadedRetainerInfoHint);
                    ImGui.Text(Loc.StUploadedRetainerInfo);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainerInfoCount.ToString());

                    ImGui.Separator();
                    //ImGuiHelpers.ScaledDummy(4.0f);

                    Utils.DrawHelp(false, Loc.StFetchedCharactersHint);
                    ImGui.Text(Loc.StFetchedCharacter);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.FetchedPlayerInfoCount.ToString());

                    Utils.DrawHelp(false, Loc.StSearchedNamesHint);
                    ImGui.Text(Loc.StSearchedNames);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.SearchedNamesCount.ToString());
                }

                ImGui.NewLine();

                using (ImRaii.Disabled(bIsNetworkProcessing))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.StRefreshProfileInfo))
                        RefreshUserProfileInfo();

                Utils.SetHoverTooltip(Loc.StRefreshProfileInfoHint);

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    ImGui.SameLine();
                    Utils.ColoredErrorTextWrapped($"{LastNetworkMessage} ({DateTimeOffset.FromUnixTimeSeconds(LastNetworkMessageTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)LastNetworkMessageTime)})");
                }
            }
            else
            {
                if (PersistenceContext._clientState is { IsLoggedIn: true, LocalContentId: > 0 })
                {
                    if (string.IsNullOrWhiteSpace(_playerName) || string.IsNullOrWhiteSpace(_contentId.ToString()) || string.IsNullOrWhiteSpace(_accountId.ToString()))
                    {
                        CheckLocalPlayer();
                    }
                    ImGui.Text(Loc.StCharacterName);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _playerName);

                    ImGui.SameLine();

                    ImGui.Text(Loc.StHomeWorld);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _worldName);

                    ImGui.Text("Account Id:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _accountId.ToString());

                    ImGui.Text("Content Id:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _contentId.ToString());

                    ImGui.Text("\n");

                    //ImGui.SetNextItemWidth(200);
                    using (ImRaii.Disabled(bIsNetworkProcessing || _client._ServerStatus != "ONLINE"))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
                            if (ImGui.Button(Loc.StLoginWithDiscord, new Vector2(200, 40)))
                        {
                            _ = Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.DiscordAuth(new UserRegister
                                {
                                    GameAccountId = _accountId,
                                    UserLocalContentId = _contentId,
                                    Name = _playerName,
                                    ClientId = Config.Key,
                                    Version = Utils.clientVer
                                }).ConfigureAwait(false).GetAwaiter().GetResult();

                                LastRegistrationWindowMessage = request.Message;
                                bIsNetworkProcessing = false;
                            });
                        }
                    }
                    if (_client.IsLoggingIn)
                    {
                        ImGui.SameLine();

                        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed))
                            if (ImGui.Button(Loc.StCancelLogin, new Vector2(200, 40)))
                            {
                                _client.IsLoggingIn = false;
                            }

                        ImGui.NewLine();

                        Utils.TextWrapped(Loc.StDidntTheBrowserOpen);

                        ImGui.SameLine();
                        if (Utils.ButtonCopy(Loc.StCopyTheLink, _client.authUrl))
                        {
                            isAuthLinkCopied = true;
                        }
                        ImGui.SameLine();

                        Utils.TextWrapped(Loc.StPasteItInBrowser);

                        if (isAuthLinkCopied)
                            Utils.ColoredTextWrapped(ImGuiColors.ParsedGold, Loc.StLinkCopied);
                    }

                    Utils.ColoredErrorTextWrapped(LastRegistrationWindowMessage);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, Loc.StErrorLoginWithACharacter);
                }
            }
        }
        bool isAuthLinkCopied;
        bool IsRefreshStatsRequestSent = false;
        bool IsRefreshStatsRequestSentForPlayerAndRetainer;
        public string _LastServerStatsMessage = string.Empty;
        private int _LastServerStatsRefreshTime = 0;
        public string LastPlayerAndRetainerWorldStatsMessage = string.Empty;
        private int _lastPlayerAndRetainerWorldRefreshTime = 0;

        private async void DrawServerStatsTab()
        {
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

            Utils.ShowColoredMessage(_SyncMessage);
        }
        private bool bPlayerAndRetainerWorldsConverted;
        private void DrawCharacterAndRetainerStats()
        {
            if (_client.LastPlayerAndRetainerCountStats.Stats == null) //!IsRefreshStatsRequestSentForPlayerAndRetainer && 
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
                                                foreach (var t in PlayerAndRetainersWorldStatsColumn)
                                                {
                                                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                                                }

                                                ImGui.TableHeadersRow();
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
        int Option_ObjRefreshInterval;
        private void DrawOptionsTab()
        {
            ImGui.SliderInt("Object Refresh Interval (ms)", ref Option_ObjRefreshInterval, 0_100, 10_000);
            if (Config.ObjectTableRefreshInterval != Option_ObjRefreshInterval)
            {
                Config.ObjectTableRefreshInterval = Option_ObjRefreshInterval;
                Config.Save();
            }
        }
        private ConcurrentDictionary<World, (int PlayerCount, int RetainerCount, long LastUpdated)> ConvertedPlayerAndRetainerWorldCounts = new();

        private void ConvertLastPlayerAndRetainerWorlds()
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
        
        private ConcurrentDictionary<long, UserCharacters> _localUserCharacters = new();
        public class UserCharacters
        {
            public string? Name { get; set; }
            public User.CharacterPrivacySettingsDto? Privacy { get; init; }
        }

        private HashSet<long?> editedCharactersPrivacy = new();
        private bool isPrivacySettingsChanged;

        private async void DrawMyCharactersTab()
        {
            ImGui.TextWrapped(Loc.StConfigurePrivacyOfChars);

            ImGui.NewLine();

            ImGui.TextWrapped(string.Format(Loc.StATotalOfCharsFound, _localUserCharacters.Count));

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.BeginGroup();

            if (LastUserInfo != null && LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0 && !_localUserCharacters.IsEmpty)
            {
                var index = 0;
                foreach (var character in LastUserInfo.Characters)
                {
                    using (ImRaii.Disabled(DetailsWindow.Instance._LastMessage == Loc.DtLoading))
                        if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                        {
                            DetailsWindow.Instance.IsOpen = true;
                            DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)character.LocalContentId, true);
                        }

                    ImGui.SameLine();

                    if (!_localUserCharacters.TryGetValue((long)character.LocalContentId, out var _getLocalChara))
                    {
                        continue;
                    }

                    var privacy = _getLocalChara.Privacy;
                    bool _bHideFullProfile = privacy.HideFullProfile;
                    bool _bHideTerritoryInfo = privacy.HideTerritoryInfo;
                    bool _bHideCustomizations = privacy.HideCustomizations;
                    bool _bHideInSearchResults = privacy.HideInSearchResults;
                    bool _bHideRetainersInfo = privacy.HideRetainersInfo;
                    bool _bHideAltCharacters = privacy.HideAltCharacters;

                    if (LastUserCharactersPrivacySettings != null
                         && index < LastUserCharactersPrivacySettings.Count
                         && LastUserCharactersPrivacySettings[index]?.Privacy != null)
                    {
                        if (LastUserCharactersPrivacySettings[index].Privacy.HideFullProfile)
                        {
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                            {
                                ImGui.TextUnformatted($"{FontAwesomeIcon.Lock.ToIconString()}");
                                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                                {
                                    Utils.SetHoverTooltip(Loc.StThisCharacterIsPrivate);
                                    ImGui.SameLine();
                                }
                            }
                        }
                        else if (LastUserCharactersPrivacySettings[index].Privacy.HideTerritoryInfo
                                || LastUserCharactersPrivacySettings[index].Privacy.HideCustomizations
                                || LastUserCharactersPrivacySettings[index].Privacy.HideAltCharacters
                                || LastUserCharactersPrivacySettings[index].Privacy.HideInSearchResults
                                || LastUserCharactersPrivacySettings[index].Privacy.HideRetainersInfo)
                        {
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                            {
                                ImGui.TextUnformatted($"{FontAwesomeIcon.UnlockAlt.ToIconString()}");
                                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                                {
                                    Utils.SetHoverTooltip(Loc.StSomeDetailsThisCharacterAreHidden);
                                    ImGui.SameLine();
                                }
                            }
                        }
                    }


                    var charName = !string.IsNullOrWhiteSpace(character.Name) ? character.Name : Loc.StNameNotFound;
                    var headerText = $"{charName}";

                    int visitCount = character.ProfileVisitInfo != null ? (int)character.ProfileVisitInfo.ProfileTotalVisitCount : 0;
                    if (visitCount > 0)
                    {
                        headerText += $" | {Loc.StTotalVisitCount} {visitCount}";
                    }

                    if (ImGui.CollapsingHeader($"{headerText}"))
                    {
                        if (visitCount > 0)
                        {
                            var lastVisitDateString = $"{Tools.UnixTimeConverter(character.ProfileVisitInfo.LastProfileVisitDate)} ({Tools.ToTimeSinceString((int)character.ProfileVisitInfo.LastProfileVisitDate)})";
                            Utils.ColoredTextWrapped(ImGuiColors.ParsedBlue, $"{Loc.StSomeoneVisitedYourProfile} {lastVisitDateString}");
                        }
                        if (character.Privacy != null)
                        {
                            void PrivacyCheckbox(string label, ref bool value, Action<bool> onChange, string tooltip)
                            {
                                if (ImGui.Checkbox(label + $"##{index}", ref value))
                                {
                                    onChange(value);
                                    editedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Utils.SetHoverTooltip(tooltip);
                            }

                            PrivacyCheckbox(Loc.StPrivacyHideFullProfile, ref _bHideFullProfile,
                                value => _getLocalChara.Privacy.HideFullProfile = value, Loc.StPrivacyHideFullProfileTooltip);

                            ImGui.SameLine();

                            using (ImRaii.Disabled(_bHideFullProfile))
                            {
                                PrivacyCheckbox(Loc.StPrivacyHideLocationHistory, ref _bHideTerritoryInfo,
                                    value => _getLocalChara.Privacy.HideTerritoryInfo = value, Loc.StPrivacyHideLocationHistoryTooltip);

                                ImGui.SameLine();

                                PrivacyCheckbox(Loc.StPrivacyHideCustomizationHistory, ref _bHideCustomizations,
                                    value => _getLocalChara.Privacy.HideCustomizations = value, Loc.StPrivacyHideCustomizationHistoryTooltip);

                                PrivacyCheckbox(Loc.StPrivacyDontAppearInSearchResults, ref _bHideInSearchResults,
                                    value => _getLocalChara.Privacy.HideInSearchResults = value, Loc.StPrivacyDontAppearInSearchResultsTooltip);

                                ImGui.SameLine();

                                PrivacyCheckbox(Loc.StPrivacyHideAltCharacters, ref _bHideAltCharacters,
                                    value => _getLocalChara.Privacy.HideAltCharacters = value, Loc.StPrivacyHideAltCharactersTooltip);
                            }

                            ImGui.SameLine();

                            PrivacyCheckbox(Loc.StPrivacyHideRetainers, ref _bHideRetainersInfo,
                                value => _getLocalChara.Privacy.HideRetainersInfo = value, Loc.StPrivacyHideRetainersTooltip);
                        }

                        ImGuiHelpers.ScaledDummy(5.0f);
                        ImGui.Separator();
                        ImGuiHelpers.ScaledDummy(5.0f);
                    }

                    index++;
                }
            }

            ImGui.EndGroup();

            ImGui.NewLine();

            using (ImRaii.Disabled(bIsNetworkProcessing))
            {
                using (ImRaii.Disabled(editedCharactersPrivacy.Count == 0))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, Loc.StSaveConfig))
                    {
                        var updateCharacters = new List<UserCharacterDto?>();
                        foreach (var chara in _localUserCharacters)
                        {
                            if (editedCharactersPrivacy.Contains(chara.Key))
                            {
                                updateCharacters.Add(new UserCharacterDto { LocalContentId = chara.Key, Name = chara.Value.Name, Privacy = chara.Value.Privacy });
                            }
                        }
                        _ = Task.Run(() =>
                        {
                            bIsNetworkProcessing = true;
                            
                            var response = _client.UserUpdate(new UserUpdateDto { Characters = updateCharacters }).ConfigureAwait(false).GetAwaiter().GetResult();
                            LastNetworkMessage = response;
                            bIsNetworkProcessing = false;
                            RefreshUserProfileInfo();
                        });

                        isPrivacySettingsChanged = false;
                        editedCharactersPrivacy.Clear();
                    }
                Utils.SetHoverTooltip(Loc.StSaveConfigTooltip);

                ImGui.SameLine();

                using (ImRaii.Disabled(bIsNetworkProcessing))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.StRefreshProfileInfo))
                        RefreshUserProfileInfo(); 

                Utils.SetHoverTooltip(Loc.StRefreshProfileInfoHint);

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    Utils.ColoredErrorTextWrapped($"{LastNetworkMessage} ({DateTimeOffset.FromUnixTimeSeconds(LastNetworkMessageTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)LastNetworkMessageTime)})");
                }
            }
        }

        private void DrawSection(string title, string[] bulletPoints, bool wrapped)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
            {
                if (title.StartsWith(Loc.AgAgreementTitle))
                {
                    ImGuiComponents.IconButton(FontAwesomeIcon.ExclamationTriangle);
                    ImGui.SameLine();
                }

                if (wrapped)
                    Utils.TextWrapped(title);
                else
                    ImGui.TextUnformatted(title);

                if (title.StartsWith(Loc.AgAgreementTitle))
                {
                    ImGui.SameLine();
                    ImGuiComponents.IconButton(FontAwesomeIcon.ExclamationTriangle);
                }
            }

            ImGui.Separator();

            foreach (var point in bulletPoints)
            {
                ImGui.Bullet();
                ImGui.SameLine();
                if (wrapped)
                    Utils.TextWrapped(point);
                else
                    ImGui.TextUnformatted(point);
            }
        }

        public void RefreshUserProfileInfo()
        {
            _ = Task.Run(async () =>
            {
                bIsNetworkProcessing = true;
                //var request = _client.UserRefreshMyInfo().ConfigureAwait(false).GetAwaiter().GetResult();
                var request = await _client.UserRefreshMyInfo();
                LastNetworkMessage = request.Message;
                bIsNetworkProcessing = false;
                if (request.User != null)
                {
                    SaveUserResultToConfig(request.User);
                }

                LastNetworkMessageTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            });
        }

        private CancellationTokenSource _cancellationToken;

        private (FontAwesomeIcon Icon, string Text) HourGlass()
        {
            int i = DateTime.Now.Second;
            if (i % 2 == 0)
            {
                return (FontAwesomeIcon.HourglassStart, "..");
            }
            else
            {
                return (FontAwesomeIcon.HourglassHalf, "...");
            }
        }

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
 
                        if (!cts.Token.IsCancellationRequested && request.Page != null && request.Page.Data != null)
                        {

                            foreach (var _data in request.Page.Data)
                            {
                                _playersFetchedFromServer[_data.LocalContentId] = _data;
                            }

                            _LastCursor = request.Page.LastCursor;
                            _SyncMessage = $"{Loc.StFetchingCharacters} ({_playersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount})";

                            if (request.Page.NextCount > 0)
                            {
                                await Task.Delay(1, cts.Token);
                            }
                            else
                            {
                                _LastCursor = 0;
                                IsSyncingPlayers = false;
                                IsSyncingRetainers = true;
                                break;
                            }

                            await Task.Delay(1, cts.Token);
                        }
                        else
                        {
                            _SyncMessage = cts.Token.IsCancellationRequested ? Loc.StErrorStoppedFetching : Loc.StErrorUnableToFetchCharacters;
                            return false;
                        }
                    }
                }
                finally
                {
                    _LastCursor = 0;
                    IsSyncingPlayers = false;
                    _SyncMessage = cts.Token.IsCancellationRequested ? Loc.StErrorStoppedFetching : Loc.StErrorUnableToFetchCharacters;
                }
                return await SyncRetainersWithLocalDb(cts);
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

                        if (request.Page != null && request.Page.Data != null)
                        {
                            foreach (var _data in request.Page.Data)
                            {
                                _retainersFetchedFromServer[_data.LocalContentId] = _data;
                            }

                            _LastCursor = request.Page.LastCursor;
                            _SyncMessage = $"{Loc.StFetchingRetainers} ({_retainersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount})";

                            if (request.Page.NextCount > 0)
                            {
                                await Task.Delay(10, cts.Token);
                            }
                            else
                            {
                                _LastCursor = 0;
                                break;
                            }

                            await Task.Delay(10, cts.Token);
                        }
                        else
                        {
                            _SyncMessage = cts.Token.IsCancellationRequested ? Loc.StErrorStoppedFetching : Loc.StErrorUnableToFetchRetainers;
                            IsSyncingRetainers = false;
                        }
                    }
                }
                finally
                {
                    IsSyncingRetainers = false;
                    IsDbRefreshing = true;

                    SyncWithLocalDB();
                }
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
                RefreshUserProfileInfo();
            }
        }

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
                    _lastPlayerAndRetainerWorldRefreshTime= (int)DateTimeOffset.Now.ToUnixTimeSeconds();

                    bIsNetworkProcessing = false;
                    return request;
                });
            }
            return (null, string.Empty);
        }

        string? _serverIpAdressField = null;
        bool bShowServerIpAdressTextField;
        public void ServerStatusGui()
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Database, "Ip"))
            {
                bShowServerIpAdressTextField = !bShowServerIpAdressTextField;
            }

            if (bShowServerIpAdressTextField)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(300);

                ImGui.InputTextWithHint("", Loc.StServerIp, ref _serverIpAdressField, 50, ImGuiInputTextFlags.CharsNoBlank);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, Loc.StConnect))
                {
                    try
                    {
                        Config.BaseUrl = _serverIpAdressField;
                        Config.Save();
                        ApiClient._restClient = new RestClient(Config.BaseUrl);
                        bShowServerIpAdressTextField = false;

                        _ = Task.Run(() =>
                        {
                            bIsNetworkProcessing = true;
                            var request = _client.CheckServerStatus().ConfigureAwait(false).GetAwaiter().GetResult();
                            bIsNetworkProcessing = false;
                        });
                    }
                    catch (Exception ex) { }
                }
            }
            else
            {
                _serverIpAdressField = Config.BaseUrl.ToString();
                ImGui.SameLine();
            }

            var _checkServerStatusString = Loc.StCheckServerStatus;
            if (_client.IsCheckingServerStatus)
                _checkServerStatusString = Loc.StCheckingStatus;
            using (ImRaii.Disabled(_client.IsCheckingServerStatus || bIsNetworkProcessing))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, _checkServerStatusString))
                {
                    _ = Task.Run(() =>
                    {
                        bIsNetworkProcessing = true;
                        var request = _client.CheckServerStatus().ConfigureAwait(false).GetAwaiter().GetResult();
                        bIsNetworkProcessing = false;
                    });
                }
            }
            ImGui.SameLine();
            if (!string.IsNullOrWhiteSpace(_client._ServerStatus))
            {
                Utils.TextWrapped(ImGuiColors.DalamudWhite, Loc.StServerStatus); ImGui.SameLine();
                if (_client._ServerStatus == "ONLINE")
                {
                    Utils.TextWrapped(ImGuiColors.HealerGreen, Loc.StOnline); ImGui.SameLine();
                    Utils.TextWrapped(ImGuiColors.DalamudWhite, "Ping:"); ImGui.SameLine();
                    Utils.TextWrapped(ImGuiColors.HealerGreen, $"{_client._LastPingValue}");
                }
                else
                {
                    Utils.TextWrapped(ImGuiColors.DalamudRed, _client._ServerStatus.ToString());
                }
            }

            ImGui.SameLine();

            ImGui.Text(Loc.StLanguage);
            ImGui.SetNextItemWidth(50);
            ImGui.SameLine();

            if (Config.Language == LanguageEnum.en)
                selectedLanguageComboItem = 0;
            else if (Config.Language == LanguageEnum.tr)
                selectedLanguageComboItem = 1;

            if (ImGui.Combo("##lang", ref selectedLanguageComboItem, languageComboItems, 2))
            {
                if (selectedLanguageComboItem == 0)
                    SetLanguage(LanguageEnum.en);
                else if (selectedLanguageComboItem == 1)
                    SetLanguage(LanguageEnum.tr);

                OnLanguageChange();
            }

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(4.0f);
        }

        private void OnLanguageChange()
        {
            WindowName = $"{Loc.TitleSettingsMenu}{WindowId}";
            LanguageChanged();
            DetailsWindow.Instance.LanguageChanged();
            MainWindow.Instance.LanguageChanged();
            PlayerDetailed.UpdateFlagMessages();
        }
        
        public void SetLanguage(Configuration.LanguageEnum lang)
        {
            if (lang == LanguageEnum.en)
            {
                Loc.Culture = new CultureInfo("en");
                Config.Language = LanguageEnum.en;
                Config.Save();
            }
            else if (lang == LanguageEnum.tr)
            {
                Loc.Culture = new CultureInfo("tr");
                Config.Language = LanguageEnum.tr;
                Config.Save();
            }
        }
        private int selectedLanguageComboItem = 0;
        private string[] languageComboItems = ["en", "tr"];
    }
}