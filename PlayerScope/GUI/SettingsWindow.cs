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

namespace PlayerScope.GUI
{
    public class SettingsWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeSettings";
        public SettingsWindow() : base(WindowId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            LanguageChanged();
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
            => WindowName = $"{Loc.TitleSettingsMenu}{WindowId}";

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
            if (string.IsNullOrWhiteSpace(_client._ServerStatus))
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
                            _worldName = homeWorld.GameData.Name;
                            _worldId = homeWorld.Id;
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
        string _key = string.Empty;

        public Configuration Config = PlayerScopePlugin.Instance.Configuration;
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
  
                LastUserCharacters = LastUserInfo.Characters;

                if (LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0)
                {
                    foreach (var character in LastUserInfo.Characters)
                    {
                        if (character.Privacy == null)
                        {
                            character.Privacy = new CharacterPrivacySettingsDto();
                        }
                        _LocalUserCharacters[(long)character.LocalContentId] = new UserCharacters { Name = character.Name, Privacy = character.Privacy };
                    }
                }
   
                Config.LoggedIn = true;
                Config.FreshInstall = false;

                Config.Save();
            }
        }

        User LastUserInfo { get; set; }
        List<User.UserCharacterDto?> LastUserCharacters { get; set; }

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
            if (ImGui.BeginTabBar("Tabs"))
            {
                if (Config.AgreementAccepted)
                {
                    if (ImGui.BeginTabItem(Loc.StTabUserInfo))
                    {
                        DrawUserInfoTab();
                        ImGui.EndTabItem();
                    }

                    using (ImRaii.Disabled(!Config.LoggedIn))
                    {
                        if (ImGui.BeginTabItem(Loc.StTabServerStatsAndSync))
                        {
                            DrawServerStatsTab();
                            ImGui.EndTabItem();
                        }
                    }
                    using (ImRaii.Disabled(!Config.LoggedIn))
                    {
                        if (ImGui.BeginTabItem(Loc.StTabMyCharactersAndPrivacy))
                        {
                            DrawMyCharactersTab();
                            ImGui.EndTabItem();
                        }
                    }
                }
                ImGui.EndTabBar();
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
                Util.ColoredTextWrapped(ImGuiColors.ParsedPink, Loc.AgCrowsourcedDescription);
                DrawSection(Loc.AgServerBasedFeatures, [ Loc.AgFeature1, Loc.AgFeature2, Loc.AgFeature3, Loc.AgFeature4, Loc.AgFeature5, Loc.AgFeature6 ], true);

                ImGui.Spacing();

                DrawSection(Loc.AgAgreementTitle, [ Loc.AgAgreement1, Loc.AgAgreement2, Loc.AgAgreement3, Loc.AgAgreement4 ], true);

                ImGui.Separator();
                ImGui.Spacing();
                Util.TextWrapped(Loc.AgDoYouAccept);

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
            ServerStatusGui();

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
                    Util.DrawHelp(true, Loc.StRoleDescriptionNonVerified);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.HealerGreen, $"{GetEnumDisplayName((User.Roles)Config.AppRoleId)}");
                    ImGui.SameLine();

                    ImGui.Text(Loc.StPermissions); ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.HealerGreen, Loc.StSendAndReceiveData);
                    Util.DrawHelp(true, Loc.StAuthorizeDescription);
                }

                if (ImGui.CollapsingHeader(Loc.StShowMyIds))
                {
                    ImGui.Text("Account Id:");
                    ImGui.SameLine();
                   
                    Util.TextCopy(ImGuiColors.TankBlue, Config.AccountId.ToString());
                    Util.SetHoverTooltip(Loc.StClickToCopyAccountId);

                    ImGui.SameLine();

                    ImGui.Text("LocalContent Id: ");
                    ImGui.SameLine();
                    Util.TextCopy(ImGuiColors.TankBlue, Config.ContentId.ToString());
                    Util.SetHoverTooltip(Loc.StClickToCopyContentId);
                }

                if (ImGui.CollapsingHeader(Loc.StMyContributions, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    Util.DrawHelp(false, Loc.StUploadedCharactersHint);
                    ImGui.Text(Loc.StUploadedCharacters);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayersCount.ToString());

                    Util.DrawHelp(false, Loc.StUploadedCharacterInfoHint);
                    ImGui.Text(Loc.StUploadedCharacterInfo);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayerInfoCount.ToString());

                    Util.DrawHelp(false, Loc.StUploadedRetainersHint);
                    ImGui.Text(Loc.StUploadedRetainers);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainersCount.ToString());
                   
                    Util.DrawHelp(false, Loc.StUploadedRetainerInfoHint);
                    ImGui.Text(Loc.StUploadedRetainerInfo);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainerInfoCount.ToString());

                    ImGui.Separator();
                    //ImGuiHelpers.ScaledDummy(4.0f);

                    Util.DrawHelp(false, Loc.StFetchedCharactersHint);
                    ImGui.Text(Loc.StFetchedCharacter);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.FetchedPlayerInfoCount.ToString());

                    Util.DrawHelp(false, Loc.StSearchedNamesHint);
                    ImGui.Text(Loc.StSearchedNames);
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.SearchedNamesCount.ToString());
                }

                ImGui.NewLine();

                using (ImRaii.Disabled(bIsNetworkProcessing))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.StRefreshProfileInfo))
                        RefreshUserProfileInfo();

                Util.SetHoverTooltip(Loc.StRefreshProfileInfoHint);

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    ImGui.SameLine();
                    Util.ColoredErrorTextWrapped($"{LastNetworkMessage} ({DateTimeOffset.FromUnixTimeSeconds(LastNetworkMessageTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)LastNetworkMessageTime)})");
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
                                    Version = Util.clientVer
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

                        Util.TextWrapped(Loc.StDidntTheBrowserOpen);

                        ImGui.SameLine();
                        if (Util.ButtonCopy(Loc.StCopyTheLink, _client.authUrl))
                        {
                            isAuthLinkCopied = true;
                        }
                        ImGui.SameLine();

                        Util.TextWrapped(Loc.StPasteItInBrowser);

                        if (isAuthLinkCopied)
                            Util.ColoredTextWrapped(ImGuiColors.ParsedGold, Loc.StLinkCopied);
                    }

                    Util.ColoredErrorTextWrapped(LastRegistrationWindowMessage);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, Loc.StErrorLoginWithACharacter);
                }
            }
        }
        bool isAuthLinkCopied;
        bool IsRefreshStatsRequestSent = false;
        public string _LastServerStatsMessage = string.Empty;
        private int _LastServerStatsRefreshTime = 0;
        private async void DrawServerStatsTab()
        {
            ServerStatusGui();

            if (!IsRefreshStatsRequestSent && _client._LastServerStats.ServerStats == null)
            {
                IsRefreshStatsRequestSent = true;
                CheckServerStats();
            }

            ImGui.TextColored(ImGuiColors.ParsedGold, Loc.StServerDatabaseStats);

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
                Util.ColoredErrorTextWrapped($"{_LastServerStatsMessage} ({DateTimeOffset.FromUnixTimeSeconds(_LastServerStatsRefreshTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)_LastServerStatsRefreshTime)})");
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

            Util.DrawHelp(false, Loc.StSyncCharacterAndRetainerFromServerTooltip);
            bool _syncDatabaseButtonCondition = Config.LastSyncedTime != null ? Tools.UnixTime - Config.LastSyncedTime < 300 : true;
            using (ImRaii.Disabled(bIsNetworkProcessing || IsSyncingPlayers || IsSyncingRetainers || IsDbRefreshing || _client._LastServerStats.ServerStats == null|| _syncDatabaseButtonCondition)) // 5 minutes
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.UserFriends, Loc.StSyncCharacterAndRetainerFromServer))
                {
                    IsSyncingPlayers = true;
                    _cancellationToken = new CancellationTokenSource();
                    var syncPlayers = SyncPlayersWithLocalDb(_cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                if (_syncDatabaseButtonCondition)
                {
                    var syncAgainTime = Config.LastSyncedTime + 300;
                    using (ImRaii.Disabled()) { Util.TextWrapped($"{Loc.StCanSyncAgainTime} {Tools.TimeFromNow((int)syncAgainTime)}"); }
                }
            }
            Util.SetHoverTooltip(Loc.StSyncCharacterAndRetainerFromServerTooltip);

            if (IsSyncingPlayers || IsSyncingRetainers)
            {
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, Loc.StStopFetching))
                {
                    _cancellationToken.Cancel();
                    IsSyncingPlayers = false; IsSyncingRetainers = false;
                    _playersFetchedFromServer.Clear(); 
                    _retainersFetchedFromServer.Clear();
                    _LastCursor = 0;
                }

                Util.CompletionProgressBar(_playersFetchedFromServer.Count + _retainersFetchedFromServer.Count,
                    (_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount)
                    + (_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount));
            }
                
            Util.ShowColoredMessage(_SyncMessage);
        }

        private ConcurrentDictionary<long, UserCharacters> _LocalUserCharacters = new();
        public class UserCharacters
        {
            public string? Name { get; set; }
            public User.CharacterPrivacySettingsDto? Privacy { get; init; }
        }

        private List<long?> EditedCharactersPrivacy = new List<long?>();
        private async void DrawMyCharactersTab()
        {
            ServerStatusGui();

            ImGui.TextWrapped(Loc.StConfigurePrivacyOfChars);

            ImGui.NewLine();

            ImGui.TextWrapped(string.Format(Loc.StATotalOfCharsFound, _LocalUserCharacters.Count));

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.BeginGroup();

            if (LastUserInfo != null && LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0 && _LocalUserCharacters.Count > 0)
            {

                var index = 0;
                foreach (var character in LastUserInfo.Characters)
                {
                    if (ImGui.Button(Loc.StLoadDetails + $"##{index}"))
                    {
                        DetailsWindow.Instance.IsOpen = true;
                        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)character.LocalContentId, true);
                    }

                    ImGui.SameLine();
                    var charName = character.Name != null || !string.IsNullOrWhiteSpace(character.Name) ? character.Name : Loc.StNameNotFound;
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
                            Util.ColoredTextWrapped(ImGuiColors.ParsedBlue, $"{Loc.StSomeoneVisitedYourProfile} {lastVisitDateString}");
                        }
                        if (character.Privacy != null)
                        {
                            _LocalUserCharacters.TryGetValue((long)character.LocalContentId, out var _getLocalChara);

                            bool _bHideFullProfile = _getLocalChara.Privacy.HideFullProfile;
                            bool _bHideTerritoryInfo = _getLocalChara.Privacy.HideTerritoryInfo;
                            bool _bHideCustomizations = _getLocalChara.Privacy.HideCustomizations;
                            bool _bHideInSearchResults = _getLocalChara.Privacy.HideInSearchResults;
                            bool _bHideRetainersInfo = _getLocalChara.Privacy.HideRetainersInfo;
                            bool _bHideAltCharacters = _getLocalChara.Privacy.HideAltCharacters;

                            if (ImGui.Checkbox(Loc.StPrivacyHideFullProfile + $"##{index}", ref _bHideFullProfile))
                            {
                                _getLocalChara.Privacy.HideFullProfile = _bHideFullProfile;
                                if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                            }
                            Util.SetHoverTooltip(Loc.StPrivacyHideFullProfileTooltip);

                            ImGui.SameLine();

                            using (ImRaii.Disabled(_bHideFullProfile))
                            {
                                if (ImGui.Checkbox(Loc.StPrivacyHideLocationHistory + $"##{index}", ref _bHideTerritoryInfo))
                                {
                                    _getLocalChara.Privacy.HideTerritoryInfo = _bHideTerritoryInfo;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip(Loc.StPrivacyHideLocationHistoryTooltip);

                                ImGui.SameLine();

                                if (ImGui.Checkbox(Loc.StPrivacyHideCustomizationHistory + $"##{index}", ref _bHideCustomizations))
                                {
                                    _getLocalChara.Privacy.HideCustomizations = _bHideCustomizations;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip(Loc.StPrivacyHideCustomizationHistoryTooltip);

                                if (ImGui.Checkbox(Loc.StPrivacyDontAppearInSearchResults + $"##{index}", ref _bHideInSearchResults))
                                {
                                    _getLocalChara.Privacy.HideInSearchResults = _bHideInSearchResults;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip(Loc.StPrivacyDontAppearInSearchResultsTooltip);

                                ImGui.SameLine();

                                if (ImGui.Checkbox(Loc.StPrivacyHideAltCharacters + $"##{index}", ref _bHideAltCharacters))
                                {
                                    _getLocalChara.Privacy.HideAltCharacters = _bHideAltCharacters;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip(Loc.StPrivacyHideAltCharactersTooltip);
                            }
                           
                            ImGui.SameLine();

                            if (ImGui.Checkbox(Loc.StPrivacyHideRetainers + $"##{index}", ref _bHideRetainersInfo))
                            {
                                _getLocalChara.Privacy.HideRetainersInfo = _bHideRetainersInfo;
                                if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                            }
                            Util.SetHoverTooltip(Loc.StPrivacyHideRetainersTooltip);
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
                using (ImRaii.Disabled(EditedCharactersPrivacy.Count == 0))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, Loc.StSaveConfig))
                    {
                        var updateCharacters = new List<UserCharacterDto?>();
                        foreach (var chara in _LocalUserCharacters)
                        {
                            if (EditedCharactersPrivacy.Contains(chara.Key))
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

                        EditedCharactersPrivacy.Clear();
                    }
                Util.SetHoverTooltip(Loc.StSaveConfigTooltip);

                ImGui.SameLine();

                using (ImRaii.Disabled(bIsNetworkProcessing))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, Loc.StRefreshProfileInfo))
                        RefreshUserProfileInfo();

                Util.SetHoverTooltip(Loc.StRefreshProfileInfoHint);

                //ImGui.SameLine();

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    Util.ColoredErrorTextWrapped($"{LastNetworkMessage} ({DateTimeOffset.FromUnixTimeSeconds(LastNetworkMessageTime).LocalDateTime.ToLocalTime().ToLongTimeString()} - {Tools.ToTimeSinceString((int)LastNetworkMessageTime)})");
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
                    Util.TextWrapped(title);
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
                    Util.TextWrapped(point);
                else
                    ImGui.TextUnformatted(point);
            }
        }

        public void RefreshUserProfileInfo()
        {
            _ = Task.Run(() =>
            {
                bIsNetworkProcessing = true;
                var request = _client.UserRefreshMyInfo().ConfigureAwait(false).GetAwaiter().GetResult();
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

                while (!cts.Token.IsCancellationRequested)
                {
                    var query = new PlayerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                    var request = ApiClient.Instance.GetPlayers<PlayerDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();

                    if (request.Page.Data != null)
                    {
                        foreach (var _data in request.Page.Data)
                        {
                            _playersFetchedFromServer[_data.LocalContentId] = _data;
                        }

                        _LastCursor = request.Page.LastCursor;
                        _SyncMessage = $"{Loc.StFetchingCharacters} ({_playersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount})";

                        if (request.Page.NextCount > 0)
                        {
                            _SyncMessage = $"{Loc.StFetchingCharacters} ({_playersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount})";
                            await Task.Delay(300);
                        }
                        else
                        {
                            _LastCursor = 0;
                            IsSyncingPlayers = false;
                            IsSyncingRetainers = true;
                            break;
                        }

                        await Task.Delay(300, cts.Token);
                    }
                    else
                    {
                        _SyncMessage = cts.Token.IsCancellationRequested ? Loc.StErrorStoppedFetching : Loc.StErrorUnableToFetchCharacters;
                        IsSyncingPlayers = false;
                        return false;
                    }
                }

                IsSyncingPlayers = false;
                return await SyncRetainersWithLocalDb(cts).ConfigureAwait(false);
            });

            return true;
        }

        public async Task<bool> SyncRetainersWithLocalDb(CancellationTokenSource cts)
        {
            _ = Task.Run(async () =>
            {
                IsSyncingRetainers = true;
                _LastCursor = 0;

                while (!cts.Token.IsCancellationRequested)
                {
                    var query = new RetainerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                    var request = ApiClient.Instance.GetRetainers<RetainerDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();

                    if (request.Page.Data != null)
                    {
                        foreach (var _data in request.Page.Data)
                        {
                            _retainersFetchedFromServer[_data.LocalContentId] = _data;
                        }

                        _LastCursor = request.Page.LastCursor;
                        _SyncMessage = $"{Loc.StFetchingRetainers} ({_retainersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount})";

                        if (request.Page.NextCount > 0)
                        {
                            _SyncMessage = $"{Loc.StFetchingRetainers} ({_retainersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount})";
                            await Task.Delay(300);
                        }
                        else
                        {
                            _LastCursor = 0;
                            IsSyncingRetainers = false;
                            break;
                        }

                        await Task.Delay(300, cts.Token);
                    }
                    else
                    {
                        _SyncMessage = cts.Token.IsCancellationRequested ? Loc.StErrorStoppedFetching : Loc.StErrorUnableToFetchRetainers;
                        IsSyncingRetainers = false;
                    }
                }

                IsSyncingRetainers = false;
                IsDbRefreshing = true;

                SyncWithLocalDB();
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
                   // _client._LastServerStats.ServerStats = null;

                    var request = _client.CheckServerStats().ConfigureAwait(false).GetAwaiter().GetResult();
                    _LastServerStatsMessage = request.Message;
                    _LastServerStatsRefreshTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

                    bIsNetworkProcessing = false;
                    return request;
                });
            }
            return (null, string.Empty);
        }

        int TablePlayerMaxLimit = 50;
        
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
                //ImGui.TextWrapped(_serverIpAdressField.ToString());
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
                Util.TextWrapped(ImGuiColors.DalamudWhite, Loc.StServerStatus); ImGui.SameLine();
                if (_client._ServerStatus == "ONLINE")
                {
                    Util.TextWrapped(ImGuiColors.HealerGreen, Loc.StOnline); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.DalamudWhite, "Ping:"); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.HealerGreen, $"{_client._LastPingValue}");
                }
                else
                {
                    Util.TextWrapped(ImGuiColors.DalamudRed, _client._ServerStatus.ToString());
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

                LanguageChanged();
                DetailsWindow.Instance.LanguageChanged();
                MainWindow.Instance.LanguageChanged();
            }

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(4.0f);
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
