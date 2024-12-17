using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;
using RestSharp;
using PlayerScope.API;
using PlayerScope.API.Models;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static PlayerScope.API.Models.User;
using static PlayerScope.Configuration;
using PlayerScope.GUI.MainWindowTab;
using PlayerScope.Database;
using Microsoft.Extensions.Logging;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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

        private static SettingsWindow _instance = null;
        public static SettingsWindow Instance
        {
            get
            {
                return _instance;
            }
        }
        public void LanguageChanged()
        {
            WindowName = $"{Loc.TitleSettingsMenu}{WindowId}";

            UserLodestoneCharactersColumn = new string[]
            {
                Loc.LsColumnLodestone,"##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.LsColumnLodestoneId, Loc.StLodestoneVerifiedAt
            };
        }

        private string[] UserLodestoneCharactersColumn = new string[]
        {
             Loc.LsColumnLodestone,"##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.LsColumnLodestoneId, Loc.StLodestoneVerifiedAt
        };

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

                    if (_contentId != (long)bChar->ContentId)
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

                if (LastUserInfo.LodestoneCharacters != null && LastUserInfo.LodestoneCharacters.Count > 0)
                {
                    foreach (var character in LastUserInfo.LodestoneCharacters)
                    {
                        _localUserLodestoneCharacters[character.LodestoneId] =
                            new UserLodestoneCharacterDto
                            {
                                LodestoneId = character.LodestoneId,
                                NameAndWorld = character.NameAndWorld,
                                AvatarLink = character.AvatarLink,
                                VerifiedAt = character.VerifiedAt,
                            };
                    }
                }

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

        public bool OpenMyCharactersTab = false;

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
                    if (OpenMyCharactersTab)
                    {
                        using (ImRaii.Disabled(!Config.LoggedIn))
                        {
                            string tabTitle = _localUserCharacters.Count > 0
                            ? $"{Loc.StTabMyCharactersAndPrivacy} ({_localUserCharacters.Count})"
                            : Loc.StTabMyCharactersAndPrivacy;
                            using (var tabItem = ImRaii.TabItem(tabTitle))
                            {
                                if (tabItem)
                                {
                                    DrawMyCharactersAndPrivacyTab();
                                }
                            }
                        }
                        OpenMyCharactersTab = false;
                    }
                    else
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
                            string tabTitle = _localUserCharacters.Count > 0
                            ? $"{Loc.StTabMyCharactersAndPrivacy} ({_localUserCharacters.Count})"
                            : Loc.StTabMyCharactersAndPrivacy;
                            using (var tabItem = ImRaii.TabItem(tabTitle))
                            {
                                if (tabItem)
                                {
                                    DrawMyCharactersAndPrivacyTab();
                                }
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

        private void DrawMyCharactersAndPrivacyTab()
        {
            using (var tabBar = ImRaii.TabBar("TabsCharactersAndPrivacy"))
            {
                if (tabBar)
                {
                    string tabTitle = _localUserCharacters.Count > 0
                      ? $"{Loc.StTabMyCharacters} ({_localUserCharacters.Count})"
                      : Loc.StTabMyCharacters;
                    using (var tabItem = ImRaii.TabItem(tabTitle))
                    {
                        if (tabItem)
                        {
                            DrawMyCharactersTab();
                        }
                    }
                
                    using (var tabItem = ImRaii.TabItem(ClaimedLodestoneProfilesTabTitle))
                    {
                        if (tabItem)
                        {
                            DrawClaimedLodestoneProfilesTab();
                        }
                        else
                        {
                            ClaimedLodestoneProfilesTabTitle = _localUserLodestoneCharacters.Count > 0
                               ? $"{Loc.StTabClaimedLodestoneProfiles} ({_localUserLodestoneCharacters.Count})"
                               : Loc.StTabClaimedLodestoneProfiles;
                        }
                    }
                }
            }
        }
        string ClaimedLodestoneProfilesTabTitle = string.Empty;

        private void DrawClaimedLodestoneProfilesTab()
        {
            ImGui.TextWrapped(Loc.StThisTabDisplaysLodestoneProfiles);

            ImGui.NewLine();

            ImGui.TextWrapped(string.Format(Loc.StATotalOfLodestoneProfiles, _localUserLodestoneCharacters.Count));
            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Link, $"{Loc.StClaimNewProfile}"))
            {
                ClaimLodestoneWindow.Instance.IsOpen = true;
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.BeginGroup();

            if (LastUserInfo != null && LastUserInfo.LodestoneCharacters != null && LastUserInfo.LodestoneCharacters.Count > 0 && !_localUserLodestoneCharacters.IsEmpty)
            {
                if (ImGui.BeginTable($"List##LodestoneChars", UserLodestoneCharactersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                {
                    Utils.SetupTableColumns(UserLodestoneCharactersColumn);

                    var index = 0;

                    foreach (var character in LastUserInfo.LodestoneCharacters)
                    {
                        if (!_localUserLodestoneCharacters.TryGetValue(character.LodestoneId, out var _getLocalChara))
                        {
                            continue;
                        }

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        // Open Lodestone column
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, $"{Loc.StLodestone}##_{index}"))
                        {
                            Utils.TryOpenURI(new Uri($"{Utils.lodestoneCharacterUrl}{character.LodestoneId}"));
                        }

                        ImGui.TableNextColumn();

                        // Avatar column
                        AvatarViewerWindow.DrawCharacterAvatar(character.NameAndWorld, character.AvatarLink);

                        ImGui.TableNextColumn();

                        // Character Name and World column
                        if (!string.IsNullOrWhiteSpace(character.NameAndWorld))
                        {
                            Utils.CopyButton(character.NameAndWorld, $"##CharName_{index}");
                            ImGui.Text(character.NameAndWorld);
                        }
                        else
                        {
                            ImGui.Text("---");
                        }

                        ImGui.TableNextColumn();

                        // Lodestone Id column
                        if (!string.IsNullOrWhiteSpace(character.LodestoneId.ToString()))
                        {
                            Utils.CopyButton(character.LodestoneId.ToString(), $"##CharLodestoneId_{index}");
                            ImGui.Text(character.LodestoneId.ToString());
                        }
                        else
                        {
                            ImGui.Text("---");
                        }

                        ImGui.TableNextColumn();

                        // Verified At column
                        if (!string.IsNullOrWhiteSpace(character.VerifiedAt.ToString()))
                        {
                            ImGui.Text(Tools.UnixTimeConverter(character.VerifiedAt));
                        }
                        else
                        {
                            ImGui.Text("---");
                        }

                        index++;
                    }
                }
                ImGui.EndTable();
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
                    if (_contentId == 0 || _accountId == 0 || string.IsNullOrWhiteSpace(_playerName) ||
                        _contentId != (long)PersistenceContext._clientState.LocalContentId)
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

        bool _hideCharacterAvatars;
        int Option_ObjRefreshInterval;
        bool _enableDebuging;
        
        private void DrawOptionsTab()
        {
            ImGui.Checkbox(Loc.StSettingsHideCharacterAvatars, ref _hideCharacterAvatars);
            if (Config.bHideCharacterAvatars != _hideCharacterAvatars)
            {
                Config.bHideCharacterAvatars = _hideCharacterAvatars;
                Config.Save();
            }

            ImGui.Spacing();

            ImGui.Separator();

            ImGui.Spacing();

            ImGui.SliderInt("Object Refresh Interval (ms)", ref Option_ObjRefreshInterval, 0_100, 10_000);
            if (Config.ObjectTableRefreshInterval != Option_ObjRefreshInterval)
            {
                Config.ObjectTableRefreshInterval = Option_ObjRefreshInterval;
                Config.Save();
            }

            ImGui.Spacing();

            ImGui.Separator();

            ImGui.Spacing();

            ImGui.Checkbox("Enable debugging", ref _enableDebuging);
            if (_enableDebuging)
                DrawDebugging();
        }

        void DrawDebugging()
        {
            using (var tabBar = ImRaii.TabBar("Tabs"))
            {
                if (tabBar)
                {
                    using (var tabItem = ImRaii.TabItem("Upload Players"))
                    {
                        if (tabItem)
                        {
                            ImGui.Text($"UploadPlayers Count: {PersistenceContext._UploadPlayers.Count}");

                            ImGuiHelpers.ScaledDummy(10.0f);

                            if (ImGui.BeginTable("PTable", 9, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("AccountId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("HomeWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CurrentWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("TerritoryId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("PlayerPos", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("Customization", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CreatedAt", ImGuiTableColumnFlags.WidthFixed);

                                ImGui.TableHeadersRow();

                                foreach (var player in PersistenceContext._UploadPlayers)
                                {
                                    ImGui.TableNextRow();

                                    // Name
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.Name}");

                                    // ContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.LocalContentId}");

                                    // AccountId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.AccountId}");

                                    // HomeWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.HomeWorldId}");

                                    // CurrentWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.CurrentWorldId}");

                                    // TerritoryId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.TerritoryId}");

                                    // PlayerPos
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.PlayerPos}");

                                    // Customization
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.Customization?.GenderRace}");

                                    // CreatedAt
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{Tools.ToTimeSinceString(player.Value.CreatedAt)}");
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                    using (var tabItem = ImRaii.TabItem("Sent Players"))
                    {
                        if (tabItem)
                        {
                            ImGui.Text($"UploadedPlayers Count: {PersistenceContext._UploadedPlayersCache.Count}");

                            ImGuiHelpers.ScaledDummy(10.0f);

                            if (ImGui.BeginTable("PTable1", 9, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("AccountId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("HomeWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CurrentWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("TerritoryId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("PlayerPos", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("Customization", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CreatedAt", ImGuiTableColumnFlags.WidthFixed);

                                ImGui.TableHeadersRow();

                                foreach (var player in PersistenceContext._UploadedPlayersCache)
                                {
                                    ImGui.TableNextRow();

                                    // Name
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.Name}");

                                    // ContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.LocalContentId}");

                                    // AccountId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.AccountId}");

                                    // HomeWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.HomeWorldId}");

                                    // CurrentWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.CurrentWorldId}");

                                    // TerritoryId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.TerritoryId}");

                                    // PlayerPos
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.PlayerPos}");

                                    // Customization
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{player.Value.Customization?.GenderRace}");

                                    // CreatedAt
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{Tools.ToTimeSinceString(player.Value.CreatedAt)}");
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                    using (var tabItem = ImRaii.TabItem("Upload Retainers"))
                    {
                        if (tabItem)
                        {
                            ImGui.Text($"UploadRetainers Count: {PersistenceContext._UploadRetainers.Count}");

                            ImGuiHelpers.ScaledDummy(10.0f);

                            if (ImGui.BeginTable("PTable1", 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("HomeWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("OwnerLocalContentId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CreatedAt", ImGuiTableColumnFlags.WidthFixed);

                                ImGui.TableHeadersRow();

                                foreach (var retainer in PersistenceContext._UploadRetainers)
                                {
                                    ImGui.TableNextRow();

                                    // Name
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.Name}");

                                    // ContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.LocalContentId}");

                                    // HomeWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.WorldId}");

                                    // OwnerLocalContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.OwnerLocalContentId}");

                                    // CreatedAt
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{Tools.ToTimeSinceString(retainer.Value.CreatedAt)}");
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                    using (var tabItem = ImRaii.TabItem("Sent Retainers"))
                    {
                        if (tabItem)
                        {
                            ImGui.Text($"UploadedRetainers Count: {PersistenceContext._UploadedRetainersCache.Count}");

                            ImGuiHelpers.ScaledDummy(10.0f);

                            if (ImGui.BeginTable("PTable1", 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("HomeWorldId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("OwnerLocalContentId", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("CreatedAt", ImGuiTableColumnFlags.WidthFixed);

                                ImGui.TableHeadersRow();

                                foreach (var retainer in PersistenceContext._UploadedRetainersCache)
                                {
                                    ImGui.TableNextRow();

                                    // Name
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.Name}");

                                    // ContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.LocalContentId}");

                                    // HomeWorldId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.WorldId}");

                                    // OwnerLocalContentId
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{retainer.Value.OwnerLocalContentId}");

                                    // CreatedAt
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{Tools.ToTimeSinceString(retainer.Value.CreatedAt)}");
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                    using (var tabItem = ImRaii.TabItem("Avatar Cache"))
                    {
                        if (tabItem)
                        {
                            ImGui.Text($"Image Cache Count: {Plugin.AvatarCacheManager._avatarCache.Count}");

                            if (ImGui.Button("Clear Cache"))
                            {
                                AvatarViewerWindow.Instance.IsOpen = false;
                                Plugin.AvatarCacheManager.ClearAvatarCache();
                            }

                            ImGuiHelpers.ScaledDummy(10.0f);

                            if (ImGui.BeginTable("ATable1", 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("TextureHandle", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn("Expiration", ImGuiTableColumnFlags.WidthFixed);

                                ImGui.TableHeadersRow();

                                foreach (var cache in Plugin.AvatarCacheManager._avatarCache)
                                {
                                    ImGui.TableNextRow();

                                    // Key
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{cache.Key}");

                                    // Size
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{cache.Value.Texture.Size.ToString()}");

                                    // TextureHandle
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{cache.Value.TextureHandle}");

                                    // Expiration
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{cache.Value.Expiration.ToString()}");
                                }
                                ImGui.EndTable();
                            }
                        }
                    }
                }
            }
        }
        
        private ConcurrentDictionary<long, UserCharacters> _localUserCharacters = new();
        private ConcurrentDictionary<int, UserLodestoneCharacterDto> _localUserLodestoneCharacters = new();
        
        public class UserCharacters
        {
            public string? Name { get; set; }
            public User.CharacterPrivacySettingsDto? Privacy { get; init; }
        }

        private HashSet<long?> editedCharactersPrivacy = new();

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
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Lock, Loc.StPrivacySetAllPrivate))
                {
                    foreach (var character in LastUserInfo.Characters)
                    {
                        if (_localUserCharacters.TryGetValue((long)character.LocalContentId, out var localChara))
                        {
                            localChara.Privacy.HideFullProfile = true;
                            localChara.Privacy.HideTerritoryInfo = true;
                            localChara.Privacy.HideCustomizations = true;
                            localChara.Privacy.HideInSearchResults = true;
                            localChara.Privacy.HideRetainersInfo = true;
                            localChara.Privacy.HideAltCharacters = true;

                            editedCharactersPrivacy.Add(character.LocalContentId);
                        }
                    }
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Globe, Loc.StPrivacySetAllPublic))
                {
                    foreach (var character in LastUserInfo.Characters)
                    {
                        if (_localUserCharacters.TryGetValue((long)character.LocalContentId, out var localChara))
                        {
                            localChara.Privacy.HideFullProfile = false;
                            localChara.Privacy.HideTerritoryInfo = false;
                            localChara.Privacy.HideCustomizations = false;
                            localChara.Privacy.HideInSearchResults = false;
                            localChara.Privacy.HideRetainersInfo = false;
                            localChara.Privacy.HideAltCharacters = false;

                            editedCharactersPrivacy.Add(character.LocalContentId);
                        }
                    }
                }

                ImGui.Spacing();

                var index = 0;
                foreach (var character in LastUserInfo.Characters)
                {
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

                    using (ImRaii.Disabled(DetailsWindow.Instance._LastMessage == Loc.DtLoading))
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, $"{Loc.DtOpenRightArrow}##{index}"))
                        {
                            DetailsWindow.Instance.IsOpen = true;
                            DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)character.LocalContentId, true);
                        }

                    ImGui.SameLine();

                    AvatarViewerWindow.DrawCharacterAvatar(character.Name, character.AvatarLink);

                    ImGui.SameLine();

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

                    if (ImGui.CollapsingHeader($"{headerText}##Character{index}"))
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
                            LastNetworkMessage = response.Message;
                            bIsNetworkProcessing = false;

                            if (response.User != null)
                            {
                                SaveUserResultToConfig(response.User);
                            }

                            LastNetworkMessageTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                        });

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

            this.LanguageChanged();
            DetailsWindow.Instance.LanguageChanged();
            MainWindow.Instance.LanguageChanged();

            PlayerDetailed.UpdateFlagMessages();
            Models.EnumExtensions.RefreshResources();
            if (WorldSelectorWindow.Instance != null)
            {
                WorldSelectorWindow.Instance.UpdateWindowTitle();
            }
            if (ClaimLodestoneWindow.Instance != null)
            {
                ClaimLodestoneWindow.Instance.LanguageChanged();
            }
        }
        
        public void SetLanguage(Configuration.LanguageEnum lang)
        {
            if (lang == LanguageEnum.en)
            {
                Loc.Culture = new CultureInfo("en");
                CultureInfo.CurrentCulture = new CultureInfo("en");
                CultureInfo.CurrentUICulture = new CultureInfo("en");
                Config.Language = LanguageEnum.en;
                Config.Save();
            }
            else if (lang == LanguageEnum.tr)
            {
                Loc.Culture = new CultureInfo("tr");
                CultureInfo.CurrentCulture = new CultureInfo("tr");
                CultureInfo.CurrentUICulture = new CultureInfo("tr");
                Config.Language = LanguageEnum.tr;
                Config.Save();
            }
        }
        private int selectedLanguageComboItem = 0;
        private string[] languageComboItems = ["en", "tr"];
    }
}