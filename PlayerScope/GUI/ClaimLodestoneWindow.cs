using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using ImGuiNET;
using PlayerScope.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using System.Text.RegularExpressions;
using Dalamud.Interface.Components;
using PlayerScope.API;
using PlayerScope.API.Models;
using Microsoft.Win32.SafeHandles;
using PlayerScope.Database;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Dalamud.Interface.Style;
using Dalamud.Interface.Colors;
using System.IO;
using System.Drawing;

namespace PlayerScope.GUI
{
    public class ClaimLodestoneWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeClaimLodestone";
        public ClaimLodestoneWindow() : base(WindowId, ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }

            UpdateWindowTitle();

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(620, 400),
                MaximumSize = new Vector2(9999, 9999)
            };
            TitleBarButtons.Add(new()
            {
                Click = (m) => { if (m == ImGuiMouseButton.Left) MainWindow.Instance.IsOpen = true; },
                Icon = FontAwesomeIcon.Database,
                IconOffset = new(2, 2),
                ShowTooltip = () => ImGui.SetTooltip(Loc.MnOpenMainMenu),
            });
            TitleBarButtons.Add(new()
            {
                Click = (m) => { if (m == ImGuiMouseButton.Left) SettingsWindow.Instance.IsOpen = true; },
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 2),
                ShowTooltip = () => ImGui.SetTooltip(Loc.MnOpenSettingsMenu),
            });
        }

        private static ClaimLodestoneWindow _instance = null;
        public static ClaimLodestoneWindow Instance
        {
            get
            {
                return _instance;
            }
        }

        public void LanguageChanged()
        {
            UpdateWindowTitle();

            UserLodestoneCharactersColumn = new string[]
            {
                Loc.LsColumnLodestone,"##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.LsColumnLodestoneId, Loc.LsColumnVerificationStatus
            };
        }

        private string[] UserLodestoneCharactersColumn = new string[]
        {
            Loc.LsColumnLodestone,"##Avatar", Loc.LsColumnNameAndHomeWorld, Loc.LsColumnLodestoneId, Loc.LsColumnVerificationStatus
        };

        private void UpdateWindowTitle()
            => WindowName = $"{Loc.TitleClaimLodestoneWindow}{WindowId}";
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public override void Draw()
        {
            DrawClaimNewProfileTab();
        }

        private ApiClient _client = ApiClient.Instance;
        private bool bIsProcessingRequest = false;
        private string LastNetworkMessage = string.Empty;
        private bool bIsCodeCopied = false;

        private string lodestoneProfileLink = string.Empty;
        private ClaimLodestoneCharacterDto lastFetchedLodestoneProfile = null;
        private int State = 0;

        private void DrawClaimNewProfileTab()
        {
            if (State == 0)
            {
                ImGui.TextWrapped(Loc.LsEnterYourLodestoneLink);
                ImGui.SetNextItemWidth(500);

                var regex = new Regex(@"finalfantasyxiv\.com\/lodestone\/character\/(\d+)(\/|$)", RegexOptions.Compiled);
                var match = regex.Match(lodestoneProfileLink);

                using (ImRaii.Disabled(bIsProcessingRequest))
                {
                    ImGui.InputTextWithHint("##LodestoneLink", Loc.LsLodestoneProfileLink, ref lodestoneProfileLink, 120);
                    using (ImRaii.Disabled(!match.Success))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LongArrowAltRight, Loc.LsNext))
                        {
                            _ = Task.Run(async () =>
                            {
                                bIsProcessingRequest = true;
                                var request = await _client.ClaimLodestoneProfile(lodestoneProfileLink, 0); // Find Profile
                                LastNetworkMessage = request.Message;
                                bIsProcessingRequest = false;
                                if (request.LodestoneProfile != null)
                                {
                                    lastFetchedLodestoneProfile = request.LodestoneProfile;
                                    State = 1;
                                }
                            });
                        }
                    }
                }
            }
            else if (State == 1)
            {
                if (lastFetchedLodestoneProfile != null)
                {
                    DrawLodestoneCharacterTable();

                    ImGui.NewLine();

                    ImGui.TextWrapped($"{Loc.LsPasteTheCodeIntoYourBio}");

                    ImGui.NewLine();

                    ImGui.TextWrapped(lastFetchedLodestoneProfile.VerifyCode);

                    ImGui.SameLine();

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, Loc.LsCopyTheCode))
                    {
                        bIsCodeCopied = true;
                        ImGui.SetClipboardText(lastFetchedLodestoneProfile.VerifyCode);
                        Utils.AddNotification(Loc.LsCodeCopied, Dalamud.Interface.ImGuiNotification.NotificationType.Success);
                    }

                    ImGui.SameLine();

                    ImGui.Spacing();

                    ImGui.SameLine();

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, Loc.LsOpenBiographySettings))
                    {
                        Utils.TryOpenURI(new Uri(Utils.lodestoneCharacterPrivacyUrl));
                    }

                    ImGui.NewLine();

                    using (ImRaii.Disabled(!bIsCodeCopied || bIsProcessingRequest))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, Loc.LsValidate))
                        {
                            _ = Task.Run(async () =>
                            {
                                bIsProcessingRequest = true;
                                var request = await _client.ClaimLodestoneProfile(lodestoneProfileLink, 1); // Check Bio
                                LastNetworkMessage = request.Message;
                                bIsProcessingRequest = false;
                                if (request.LodestoneProfile != null)
                                {
                                    SettingsWindow.Instance.RefreshUserProfileInfo();
                                    lastFetchedLodestoneProfile = request.LodestoneProfile;
                                    State = 2;
                                }
                            });
                        }
                    }

                    ImGui.SameLine();
                    ImGui.Spacing();
                    ImGui.SameLine();

                    using (var textColor = ImRaii.PushColor(ImGuiCol.Button, KnownColor.IndianRed.Vector()))
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, Loc.LsCancel))
                        {
                            Reset();
                        }
                }

            }
            else if (State == 2)
            {
                DrawLodestoneCharacterTable();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Cog, Loc.LsGoToMyCharactersMenu))
                {
                    SettingsWindow.Instance.IsOpen = true;
                    SettingsWindow.Instance.OpenMyCharactersTab = true;
                }

                ImGui.SameLine();
                ImGui.Spacing();
                ImGui.SameLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Redo, Loc.LsVerifyDifferentProfile))
                {
                    Reset();
                }
            }

            ImGui.NewLine();

            if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
            {
                Utils.ColoredErrorTextWrapped(LastNetworkMessage);
            }
        }

        private void Reset()
        {
            bIsCodeCopied = false;
            State = 0;
            lastFetchedLodestoneProfile = null;
            LastNetworkMessage = string.Empty;
            bIsProcessingRequest = false;
            lodestoneProfileLink = string.Empty;
        }

        private void DrawLodestoneCharacterTable()
        {
            ImGui.BeginGroup();

            if (ImGui.BeginTable($"_LodestoneChar", UserLodestoneCharactersColumn.Length, ImGuiTableFlags.BordersInner))
            {
                Utils.SetupTableColumns(UserLodestoneCharactersColumn);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                // Open Lodestone column
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, $"{Loc.StLodestone}"))
                {
                    Utils.TryOpenURI(new Uri($"{Utils.lodestoneCharacterUrl}{lastFetchedLodestoneProfile.LodestoneId}"));
                }

                ImGui.TableNextColumn();

                // Avatar column
                AvatarViewerWindow.DrawCharacterAvatar(lastFetchedLodestoneProfile.NameAndWorld, lastFetchedLodestoneProfile.AvatarLink);
 
                ImGui.TableNextColumn();

                // Character Name and World column
                if (!string.IsNullOrWhiteSpace(lastFetchedLodestoneProfile.NameAndWorld))
                {
                    Utils.CopyButton(lastFetchedLodestoneProfile.NameAndWorld, $"##CharName");
                    ImGui.Text(lastFetchedLodestoneProfile.NameAndWorld);
                }
                else
                {
                    ImGui.Text("---");
                }

                ImGui.TableNextColumn();

                // Lodestone Id column
                if (!string.IsNullOrWhiteSpace(lastFetchedLodestoneProfile.LodestoneId.ToString()))
                {
                    Utils.CopyButton(lastFetchedLodestoneProfile.LodestoneId.ToString(), $"##CharLodestoneId");
                    ImGui.Text(lastFetchedLodestoneProfile.LodestoneId.ToString());
                }
                else
                {
                    ImGui.Text("---");
                }

                ImGui.TableNextColumn();

                // Status column
                var verifyIcon = State == 1 ? FontAwesomeIcon.Times.ToIconString() : FontAwesomeIcon.Check.ToIconString();
                
                using (ImRaii.PushFont(UiBuilder.IconFont))
                using (var textColor = State == 1 ? ImRaii.PushColor(ImGuiCol.Text, KnownColor.IndianRed.Vector()) : ImRaii.PushColor(ImGuiCol.Text, KnownColor.ForestGreen.Vector()))
                {
                    ImGui.TextUnformatted(verifyIcon);
                    using (ImRaii.PushFont(UiBuilder.DefaultFont)) { }
                }
            }

            ImGui.EndTable();
            ImGui.EndGroup();
        }
    }
}