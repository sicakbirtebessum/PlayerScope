using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using System.Text.RegularExpressions;
using Dalamud.Interface.Utility;

namespace PlayerScope.GUI
{
    public class AvatarViewerWindow : Window, IDisposable
    {
        private const string WindowId = "###PlayerScopeAvatarViewer";

        private static string? currentCharacterName;
        private static string? currentAvatarLink;
        private float zoomFactor = 1.0f;
        private Vector2 lastWindowPosition = Vector2.Zero;
        private Vector2 lastWindowSize = new Vector2(680, 1000);
        private Vector2 minimumWindowSize = new Vector2(440, 600);

        private static AvatarViewerWindow _instance = null;
        public static AvatarViewerWindow Instance
        {
            get
            {
                return _instance;
            }
        }

        public AvatarViewerWindow() : base("###PlayerScopeAvatarViewer", ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = minimumWindowSize,
                MaximumSize = new Vector2(2000, 2000)
            };
        }

        public void Open(string characterName, string avatarLink)
        {
            currentCharacterName = characterName;
            currentAvatarLink = avatarLink;
            UpdateWindowTitle();

            IsOpen = true;
        }

        public static Configuration Config = Plugin.Instance.Configuration;

        private void UpdateWindowTitle()
            => WindowName = $"PlayerScope - Avatar - {currentCharacterName}{WindowId}";

        public static bool IsValidAvatarFormat(string input)
        {
            if (input == null || input.Length != 65)
            {
                return false;
            }

            int underscorePosition = input.IndexOf('_');

            return underscorePosition == 32;
        }

        public static void DrawCharacterAvatar(string characterName, string avatarLink)
        {
            if (Config.bHideCharacterAvatars)
            {
                ImGui.Text(string.Empty);
                return;
            }
                
            if (!IsValidAvatarFormat(avatarLink))
                avatarLink = Utils.BlankAvatar;

            var avatarHandle = Plugin.AvatarCacheManager.GetAvatarHandle(Utils.GetAvatarUrl(avatarLink, false));
            if (avatarHandle != 0)
            {
                ImGui.Image(avatarHandle, new Vector2(22 * ImGuiHelpers.GlobalScale, 22 * ImGuiHelpers.GlobalScale));

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();

                    string currentAvatarKey = $"{currentCharacterName}_{currentAvatarLink}";
                    string incomingAvatarKey = $"{characterName}_{avatarLink}";

                    string fullSizeDescription = (Instance.IsOpen && currentAvatarKey == incomingAvatarKey)
                        ? Loc.UtilClickToCloseTheImage
                        : Loc.UtilClickToViewFullImage;

                    ImGui.Text(fullSizeDescription);
                    ImGui.Image(avatarHandle, new Vector2(256 * ImGuiHelpers.GlobalScale, 256 * ImGuiHelpers.GlobalScale));

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if ((Instance.IsOpen && currentAvatarKey != incomingAvatarKey) || (!Instance.IsOpen))
                        {
                            Instance.Open(characterName, avatarLink);
                        }
                        else
                        {
                            Instance.IsOpen = false;
                        }
                    }

                    ImGui.EndTooltip();
                }
            }
            else
            {
                ImGui.Text(string.Empty);
            }
        }

        public override void Draw()
        {
            if (string.IsNullOrEmpty(currentAvatarLink))
            {
                ImGui.Text("No avatar to display.");
                return;
            }

            var scaledMinimumWindowSize = minimumWindowSize * ImGuiHelpers.GlobalScale;
            var currentWindowSize = ImGui.GetWindowSize();

            using (ImRaii.Disabled(currentWindowSize.X <= scaledMinimumWindowSize.X && currentWindowSize.Y <= scaledMinimumWindowSize.Y))
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.VectorSquare, Loc.AvatarResetSize))
                {
                    zoomFactor = 1.0f;
                    ImGui.SetWindowSize(minimumWindowSize);
                }

            ImGui.SameLine();

            using (ImRaii.Disabled(zoomFactor == 1.0f))
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.SearchMinus, Loc.AvatarResetZoom))
                {
                    zoomFactor = 1.0f;
                }

            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, Loc.AvatarOpenOnBrowser))
            {
                Utils.TryOpenURI(new Uri(Utils.GetAvatarUrl(currentAvatarLink, true)));
            }

            ImGui.Separator();

            ImGui.BeginChild("Avatar", new Vector2(0, 0), true, ImGuiWindowFlags.NoScrollWithMouse);

            var bigAvatarHandle = Plugin.AvatarCacheManager.GetAvatarHandle(Utils.GetAvatarUrl(currentAvatarLink, true));

            if (bigAvatarHandle != 0)
            {
                var availableSize = ImGui.GetContentRegionAvail();
                var aspectRatio = availableSize.X / availableSize.Y;
                var displaySize = availableSize * zoomFactor;

                if (ImGui.IsWindowHovered())
                {
                    float scrollDelta = ImGui.GetIO().MouseWheel;
                    if (scrollDelta != 0)
                    {
                        float previousZoomFactor = zoomFactor;
                        zoomFactor += scrollDelta * 0.25f;
                        zoomFactor = Math.Clamp(zoomFactor, 0.5f, 3.0f);

                        var mousePos = ImGui.GetMousePos();
                        var cursorPos = ImGui.GetCursorScreenPos();
                        var mouseOffset = mousePos - cursorPos;

                        if (zoomFactor != previousZoomFactor)
                        {
                            var zoomRatio = zoomFactor / previousZoomFactor;
                            var newOffsetX = mouseOffset.X * zoomRatio;
                            var newOffsetY = mouseOffset.Y * zoomRatio;

                            ImGui.SetScrollX(ImGui.GetScrollX() + (newOffsetX - mouseOffset.X));
                            ImGui.SetScrollY(ImGui.GetScrollY() + (newOffsetY - mouseOffset.Y));
                        }
                    }
                }

                ImGui.Image(bigAvatarHandle, displaySize);
            }
            else if (bigAvatarHandle == 0)
            {
                ImGui.Text(Loc.DtLoading);
            }
            ImGui.EndChild();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}