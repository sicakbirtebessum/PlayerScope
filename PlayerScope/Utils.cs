using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using Lumina.Excel.Sheets;
using PlayerScope.API.Models;
using PlayerScope.Properties;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PlayerScope
{
    public static class Utils
    {
        public static void DrawHelp(bool AtTheEnd, string helpMessage)
        {
            if (AtTheEnd)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

                SetHoverTooltip(helpMessage);
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
                SetHoverTooltip(helpMessage);
                ImGui.SameLine();
            }
        }
        public static string GetWorldName(uint worldId)
        {
            var world = Plugin.DataManager.GetExcelSheet<World>().GetRowOrDefault(worldId);
            if (world != null)
            {
                return world.Value.Name.ToString();
            }
            return "Unknown";
        }

        public static World? GetWorld(uint worldId)
        {
            var worldSheet = Plugin.DataManager.GetExcelSheet<World>();
            if (worldSheet.TryGetRow(worldId, out var world))
            {
                return world;
            }

            return null;
        }

        public static string GetRegionCode(World? world)
        {
            if (world == null)
            {
                return string.Empty;
            }

            return world.Value.DataCenter.ValueNullable?.Region switch
            {
                1 => "JP",
                2 => "NA",
                3 => "EU",
                4 => "OC",
                _ => string.Empty,
            };
        }
        public static string GetRegionLongName(World? world)
        {
            if (world == null)
            {
                return string.Empty;
            }

            return world.Value.DataCenter.ValueNullable?.Region switch
            {
                1 => Loc.UtilsJP,
                2 => Loc.UtilsNA,
                3 => Loc.UtilsEU,
                4 => Loc.UtilsOCE,
                _ => string.Empty,
            };
        }

        public static bool IsWorldValid(uint worldId)
        {
            var world = GetWorld(worldId);
            return IsWorldValid(world);
        }

        public static bool IsWorldValid(World? world)
        {
            if (world == null || world.Value.Name.IsEmpty)
            {
                return false;
            }

            var regionCode = GetRegionCode(world);
            if (string.IsNullOrEmpty(regionCode))
            {
                return false;
            }

            return char.IsUpper(world.Value.Name.ToString()[0]);
        }

        public static void TextCopy(Vector4 col, string text)
        {
            ImGui.TextColored(col, text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
#pragma warning disable
                ImGui.SetClipboardText(text);
#pragma warning restore
            }
        }

        public static bool ButtonCopy(string buttonText, string copyText)
        {
            ImGui.Button(buttonText);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
#pragma warning disable
                ImGui.SetClipboardText(copyText);
                return true;
#pragma warning restore
            }
            return false;
        }

        public static void CompletionProgressBar(int progress, int total, int height = 20, bool parseColors = true)
        {
            ImGui.BeginGroup();

            var cursor = ImGui.GetCursorPos();
            var sizeVec = new Vector2(ImGui.GetContentRegionAvail().X, height);

            //Calculate percentage earlier in code
            decimal percentage2 = (decimal)progress / total;

            var percentage = (float)progress / (float)total;
            var label = string.Format("{0:P} Complete ({1}/{2})", percentage2, progress, total);
            var labelSize = ImGui.CalcTextSize(label);

            if (parseColors) ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetBarseColor(percentage));
            ImGui.ProgressBar(percentage, sizeVec, "");
            if (parseColors) ImGui.PopStyleColor();

            ImGui.SetCursorPos(new Vector2(cursor.X + sizeVec.X - labelSize.X - 4, cursor.Y));
            ImGui.TextUnformatted(label);

            ImGui.EndGroup();
        }
        public static void CenteredWrappedText(string text)
        {
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var textWidth = ImGui.CalcTextSize(text).X;

            // calculate the indentation that centers the text on one line, relative
            // to window left, regardless of the `ImGuiStyleVar_WindowPadding` value
            var textIndentation = (availableWidth - textWidth) * 0.5f;

            // if text is too long to be drawn on one line, `text_indentation` can
            // become too small or even negative, so we check a minimum indentation
            var minIndentation = 20.0f;
            if (textIndentation <= minIndentation)
            {
                textIndentation = minIndentation;
            }

            ImGui.Dummy(new Vector2(0));
            ImGui.SameLine(textIndentation);
            ImGui.PushTextWrapPos(availableWidth - textIndentation);
            ImGui.TextWrapped(text);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(string s)
        {
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(s);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(Vector4? col, string s)
        {
            ImGui.PushTextWrapPos(0);
            Text(col, s);
            ImGui.PopTextWrapPos();
        }

        public static void OpenFolder(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        public static void ColoredErrorTextWrapped(string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;

                Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void ColoredTextWrapped(Vector4? textColor, string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void ColoredTextWrapped(string s, string ping)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                if (!string.IsNullOrWhiteSpace(ping))
                    Text(textColor, $"{s} ({ping})");
                else
                    Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void Text(Vector4? col, string s)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, (System.Numerics.Vector4)col);
            ImGui.TextUnformatted(s);
            ImGui.PopStyleColor();
        }

        public static Vector4 GetBarseColor(double value)
        {
            return value switch
            {
                1 => ImGuiColors.ParsedGold,
                >= 0.95 => ImGuiColors.ParsedOrange,
                >= 0.75 => ImGuiColors.ParsedPurple,
                >= 0.50 => ImGuiColors.ParsedBlue,
                >= 0.25 => ImGuiColors.ParsedGreen,
                _ => ImGuiColors.ParsedGrey * 1.75f
            };
        }
        public static void ShowColoredMessage(string Message)
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (Message.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                ImGui.TextColored(textColor, $"{Message}");
            }
        }
        public static void ShowColoredMessage(string Message, string Ping)
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (Message.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                ImGui.TextColored(textColor, $"{Message} ({Ping})");
            }
        }

        public static void SetHoverTooltip(string tooltip)
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
        }

        public static void IconText(Vector4 textColor, FontAwesomeIcon icon)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted(icon.ToIconString());
            }
        }

        public static void HeaderWarningText(Vector4 textColor, FontAwesomeIcon icon, string text)
        {
            ImGuiHelpers.ScaledDummy(5.0f);

            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted($"{icon.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{text}");
                }
            }
        }

        public static void HeaderProfileVisitInfoText(PlayerDetailed.PlayerProfileVisitInfoDto visitInfo)
        {
            ImGuiHelpers.ScaledDummy(5.0f);
            TextWrapped(string.Format(Loc.DtCharacterVisitInfo, Tools.ToTimeSinceString((int)visitInfo.LastProfileVisitDate), visitInfo.ProfileTotalVisitCount, visitInfo.UniqueVisitorCount));
            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
        }

        public static string GenerateRandomKey(int length = 20)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] array = new byte[length * 4];
            using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(array);
            }

            StringBuilder stringBuilder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                long num = BitConverter.ToUInt32(array, i * 4) % chars.Length;
                stringBuilder.Append(chars[num]);
            }

            return stringBuilder.ToString();
        }

        public static string clientVer => Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        public static string Vector3ToString(Vector3 v)
        {
            return string.Format("{0:0.00}.{1:0.00}.{2:0.00}", v.X, v.Y, v.Z);
        }

        public static Vector3 Vector3FromString(String s)
        {
            string[] parts = s.Split(new string[] { "." }, StringSplitOptions.None);
            return new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                float.Parse(parts[2]));
        }

        public static void AddNotification(string content, NotificationType type, bool minimized = true)
        {
            Plugin.Notification.AddNotification(new Notification { Content = content, Type = type, Minimized = minimized });
        }

        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0 " + suf[0];

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return (Math.Sign(byteCount) * num).ToString("N2") + " " + suf[place];
        }

        public static bool CtrlShiftButton(FontAwesomeIcon icon, string label, string tooltip = "")
        {
            var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

            bool ret;
            using (ImRaii.Disabled(!ctrlShiftHeld))
                ret = ImGuiComponents.IconButtonWithText(icon, label) && ctrlShiftHeld;

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(tooltip);

            return ret;
        }

        public static void TryOpenURI(Uri uri)
        {
            try
            {
                Dalamud.Utility.Util.OpenLink(uri.ToString());
            }
            catch (Exception ex)
            {
                AddNotification("Failed to open the link in the browser, please report this issue", NotificationType.Error);
            }
        }

        public static void SetupTableColumns(string[] columns)
        {
            foreach (var column in columns)
            {
                ImGui.TableSetupColumn(column, ImGuiTableColumnFlags.WidthFixed);
            }
            ImGui.TableHeadersRow();
        }

        /// <summary>
        /// Displays the world information in the ImGui table.
        /// </summary>
        /// <param name="worldId">The ID of the world to resolve.</param>
        /// <param name="helpText">Whether to display the detailed help text.</param>
        public static void DisplayWorldInfo(uint? worldId, bool helpText = true)
        {
            if (!worldId.HasValue)
            {
                ImGui.Text("---");
                return;
            }

            var world = Utils.GetWorld((uint)worldId);
            if (world != null)
            {
                if (helpText)
                {
                    Utils.DrawHelp(false, $"{Loc.UtilRegion}: {Utils.GetRegionCode(world.Value)}\n{Loc.UtilDataCenter}: {world.Value.DataCenter.Value.Name}\n{Loc.MnWorld}: {world.Value.Name}");
                }

                ImGui.Text(world.Value.Name.ExtractText());
            }
            else
            {
                ImGui.Text("---");
            }
        }

        /// <summary>
        /// Displays a text with a copy button when "Ctrl" key is pressed.
        /// </summary>
        /// <param name="clipboardText">The text to copy to the clipboard.</param>
        /// <param name="buttonId">The unique ID for the copy button.</param>
        public static void CopyButton(string clipboardText, string buttonId)
        {
            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"##{buttonId}", new System.Numerics.Vector2(23, 22)))
                {
                    ImGui.SetClipboardText(clipboardText);
                }
                SetHoverTooltip(Loc.UtilsCopyText);
                ImGui.SameLine();
            }
        }

        public static void WarningIconWithTooltip(string tooltipText)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite))
            {
                ImGui.TextUnformatted($"{FontAwesomeIcon.Exclamation.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    SetHoverTooltip(tooltipText);
                }
            }
            ImGui.SameLine();
        }

        public static void IconWithTooltip(FontAwesomeIcon icon, string tooltipText)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite))
            {
                ImGui.TextUnformatted($"{icon.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    SetHoverTooltip(tooltipText);
                }
            }
            ImGui.SameLine();
        }

        public static long[] ExternalDbTimestamps = new long[]
            {
                0,
                1716465600,
                1716465601,
                1716465602,
                1716465603,
                1716465604,
                1716465605,
                1716465606,
                1723032000,
                1723982400,
                1724068800,
                1724068810,
                1724328000,
                1724414400,
                1725451200,
                1725624000,
                1725710400,
                1726228800,
                1726488000,
                1726920000,
                1727006400,
                1727179215,
                1727265600,
                1727265602,
                1727265603,
                1727265604,
                1727265605, //-
                1727265606,
                1727265607,
                1727265608,
                1727265609,
                1727265610,
                1727265611,
                1727265612,
                1727265613,
            };

        /// <summary>
        /// Returns a ISharedImmediateTexture for the appropriate icon.
        /// </summary>
        /// <param name="iconID">ID of the icon.</param>
        public static ISharedImmediateTexture GetIcon(uint iconID)
            => Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconID, false, true));

        /// <summary>
        /// Returns a ISharedImmediateTexture for the appropriate status.
        /// </summary>
        /// <param name="statusID">ID of the status.</param>
        public static ISharedImmediateTexture GetTownIcon(uint townID)
        {
            var townList = Plugin.DataManager.GameData.Excel.GetSheet<Town>();
            var town = townList.GetRow(townID);
            return GetIcon((uint)town.Icon);
        }

        public static string lodestoneCharacterUrl = "https://na.finalfantasyxiv.com/lodestone/character/";
        public static string lodestoneCharacterPrivacyUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";
        private const string AvatarBaseUrl = "https://img2.finalfantasyxiv.com/f/";
        public static string BlankAvatar = "0000_";
        public static string GetAvatarUrl(string avatarLink, bool isLarge)
        {
            if (string.IsNullOrWhiteSpace(avatarLink))
                avatarLink = BlankAvatar; // Blank image

            var sizeSuffix = isLarge ? "fl0.jpg" : "fc0.jpg";
            return $"{AvatarBaseUrl}{avatarLink}{sizeSuffix}";
        }

    }
}
