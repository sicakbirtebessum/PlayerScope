using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.VisualBasic;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkFontManager;
using static Lumina.Models.Materials.Texture;

namespace PlayerScope
{
    public class Util
    {
        public static void DrawHelp(bool AtTheEnd,string helpMessage)
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
        public static World GetWorld(uint worldId)
        {
            var worldSheet = PlayerScopePlugin._dataManager.GetExcelSheet<World>()!;
            var world = worldSheet.FirstOrDefault(x => x.RowId == worldId);
            if (world == null)
            {
                return worldSheet.First();
            }

            return world;
        }

        public static string GetRegionCode(World world)
        {
            return world.DataCenter?.Value?.Region switch
            {
                1 => "JP",
                2 => "NA",
                3 => "EU",
                4 => "OC",
                _ => string.Empty,
            };
        }

        public static bool IsWorldValid(uint worldId)
        {
            return IsWorldValid(GetWorld(worldId));
        }

        public static bool IsWorldValid(World world)
        {
            if (world.Name.RawData.IsEmpty || GetRegionCode(world) == string.Empty)
            {
                return false;
            }

            return char.IsUpper((char)world.Name.RawData[0]);
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
            Util.Text(col, s);
            ImGui.PopTextWrapPos();
        }

        public static void ColoredErrorTextWrapped(string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;

                Util.Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }
        public static void ColoredTextWrapped(Vector4? textColor, string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Util.Text(textColor, s);
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
                    Util.Text(textColor, $"{s} ({ping})");
                else
                    Util.Text(textColor, s);
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
        public static void ShowColoredMessage(string Message,string Ping)
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
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
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
    }
}
