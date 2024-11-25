using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.GUI.MainWindowTab
{
    public class WorldSelectorWindow : Window
    {
        private const string WindowId = "###PlayerScopeWorldSelector";
        internal WorldSelectorWindow() : base(WindowId, ImGuiWindowFlags.None)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            UpdateWindowTitle();
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 550),
                MaximumSize = new Vector2(400, 800)
            };
            this.SizeCondition = ImGuiCond.FirstUseEver;
            LoadWorldNames();
        }
        public void UpdateWindowTitle()
            => WindowName = $"{Loc.TitleWorldSelectorWindow}{WindowId}";

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        private static WorldSelectorWindow _instance = null;
        public static WorldSelectorWindow Instance
        {
            get
            {
                return _instance;
            }
        }
        private void LoadWorldNames()
        {
            if (worlds.Count > 0)
                return;

            var worldSheet = Plugin.DataManager.GetExcelSheet<World>().ToList();

            worlds = worldSheet
            .Where(world => world.IsPublic)
            .Select(world => (world.RowId, world.Name.ExtractText()))
            .OrderBy(world => world.Item2)
            .ToList();
        }

        private List<(uint WorldId, string WorldName)> worlds = new List<(uint, string)>();
        private string filterText = "";
        private List<(uint WorldId, string WorldName)> selectedWorlds = new List<(uint, string)>();
        public List<(uint WorldId, string WorldName)> SelectedWorlds => selectedWorlds;

        public void ResetSelectedWorlds()
        {
            selectedWorlds.Clear();
        }

        public override void Draw()
        {
            ImGui.Text($"{Loc.MnSearch}:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.InputText("##worldSearch", ref filterText, 30);

            ImGui.SameLine();
            if (ImGui.Button(Loc.WsCloseWindow))
            {
                this.IsOpen = false;
            }

            if (selectedWorlds.Any())
            {
                ImGui.Text(Loc.MnSelectedWorlds);

                foreach (var world in selectedWorlds)
                {
                    ImGui.BulletText($"{world.WorldName} (ID: {world.WorldId})");

                    ImGui.SameLine();
                    if (ImGui.Button($"X###{world.WorldId}"))
                    {
                        selectedWorlds.Remove(world);
                    }
                }
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, Loc.WsClearAll))
                {
                    ResetSelectedWorlds();
                }
            }

            ImGui.Separator();

            ImGuiHelpers.ScaledDummy(10.0f);

            if (ImGui.BeginTable("WorldTable", 3, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn(Loc.WsWorldName, ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn(Loc.WsSelect, ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableHeadersRow();

                foreach (var world in worlds)
                {
                    if (!string.IsNullOrEmpty(filterText) && !world.WorldName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ImGui.TableNextRow();

                    // World name
                    ImGui.TableNextColumn();
                    ImGui.Text($"{world.WorldName}");

                    // World Id
                    ImGui.TableNextColumn();
                    ImGui.Text($"{world.WorldId}");

                    // Select
                    ImGui.TableNextColumn();
                    bool isSelected = selectedWorlds.Contains(world);
                    if (ImGui.Checkbox($"{Loc.WsSelect}##{world.WorldId}", ref isSelected))
                    {
                        if (isSelected)
                        {
                            selectedWorlds.Add(world);
                        }
                        else
                        {
                            selectedWorlds.Remove(world);
                        }
                    }
                }

                ImGui.EndTable();
            }
        }
    }
}