﻿using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Models.Materials.Texture;

namespace RetainerTrackExpanded
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

        public static void ColoredTextWrapped(string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith("Error:"))
                    textColor = ImGuiColors.DalamudRed;

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
                if (s.StartsWith("Error:"))
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
                if (Message.StartsWith("Error:"))
                    textColor = ImGuiColors.DalamudRed;
                ImGui.TextColored(textColor, $"{Message}");
            }
        }
        public static void ShowColoredMessage(string Message,string Ping)
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (Message.StartsWith("Error:"))
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
    }
}