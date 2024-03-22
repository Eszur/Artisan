using Artisan.RawInformation.Character;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using Artisan.RawInformation;
using Newtonsoft.Json;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.CraftingLogic;
using System.Windows.Forms;

namespace Artisan.UI
{
    internal class MacroEditor : Window
    {
        private MacroSolverSettings.Macro SelectedMacro;
        private bool renameMode = false;
        private string renameMacro = "";
        private int selectedStepIndex = -1;
        private bool Raweditor = false;
        private static string _rawMacro = string.Empty;

        public MacroEditor(MacroSolverSettings.Macro macro) : base($"宏编辑器###{macro.ID}", ImGuiWindowFlags.None)
        {
            SelectedMacro = macro;
            selectedStepIndex = macro.Steps.Count - 1;
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;

            Crafting.CraftStarted += OnCraftStarted;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public override void OnClose()
        {
            Crafting.CraftStarted -= OnCraftStarted;
            base.OnClose();
            P.ws.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (SelectedMacro.ID != 0)
            {
                if (!renameMode)
                {
                    ImGui.TextUnformatted($"选择宏: {SelectedMacro.Name}");
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                    {
                        renameMode = true;
                    }
                }
                else
                {
                    renameMacro = SelectedMacro.Name!;
                    if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        SelectedMacro.Name = renameMacro;
                        P.Config.Save();

                        renameMode = false;
                        renameMacro = String.Empty;
                    }
                }
                if (ImGui.Button("删除宏 (按住 Ctrl)") && ImGui.GetIO().KeyCtrl)
                {
                    P.Config.MacroSolverConfig.Macros.Remove(SelectedMacro);
                    foreach (var e in P.Config.RecipeConfigs)
                        if (e.Value.SolverType == typeof(MacroSolverDefinition).FullName && e.Value.SolverFlavour == SelectedMacro.ID)
                            P.Config.RecipeConfigs.Remove(e.Key); // TODO: do we want to preserve other configs?..
                    P.Config.Save();
                    SelectedMacro = new();
                    selectedStepIndex = -1;

                    this.IsOpen = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("纯文本编辑"))
                {
                    _rawMacro = string.Join("\r\n", SelectedMacro.Steps.Select(x => $"{x.Action.NameOfAction()}"));
                    Raweditor = !Raweditor;
                }

                ImGui.SameLine();
                var exportButton = ImGuiHelpers.GetButtonSize("导出宏");
                ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - exportButton.X);

                if (ImGui.Button("导出宏###ExportButton"))
                {
                    Clipboard.SetText(JsonConvert.SerializeObject(SelectedMacro));
                    Notify.Success("宏已复制到剪贴板。");
                }

                ImGui.Spacing();
                if (ImGui.Checkbox("如果达到100%，则跳过品质操作", ref SelectedMacro.Options.SkipQualityIfMet))
                {
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("一旦你达到100%的品质，宏将跳过所有与品质相关的操作，包括buff。");
                ImGui.SameLine();
                if (ImGui.Checkbox("如果不是低品质，就跳过观察", ref SelectedMacro.Options.SkipObservesIfNotPoor))
                {
                    P.Config.Save();
                }


                if (ImGui.Checkbox("调整品质技能", ref SelectedMacro.Options.UpgradeQualityActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果您获得了“高品质”或 “最高品质”的状态，并且您的宏处于可以提高品质的步骤上（不包括“比尔格的祝福”），那么它就会将动作升级为“集中加工”。");
                ImGui.SameLine();

                if (ImGui.Checkbox("调整进展技能", ref SelectedMacro.Options.UpgradeProgressActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果您获得了“高品质”或 “最高品质”的状态，并且您的宏处于可以提高进展的步骤上，那么它就会将动作升级为“集中制作”。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低作业精度", ref SelectedMacro.Options.MinCraftsmanship))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果选择此宏时不满足其最低作业精度，Artisan将不会开始制作。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低加工精度", ref SelectedMacro.Options.MinControl))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果选择此宏时不满足其最低加工精度，Artisan将不会开始制作。");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("最低CP", ref SelectedMacro.Options.MinCP))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("如果选择此宏时不满足其最低CP，Artisan将不会开始制作。");

                if (!Raweditor)
                {
                    if (ImGui.Button($"插入新技能 ({Skills.BasicSynthesis.NameOfAction()})"))
                    {
                        SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = Skills.BasicSynthesis });
                        ++selectedStepIndex;
                        P.Config.Save();
                    }

                    if (selectedStepIndex >= 0)
                    {
                        if (ImGui.Button($"插入新技能 - 与上一个相同 ({SelectedMacro.Steps[selectedStepIndex].Action.NameOfAction()})"))
                        {
                            SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = SelectedMacro.Steps[selectedStepIndex].Action });
                            ++selectedStepIndex;
                            P.Config.Save();
                        }
                    }
                    

                    ImGui.Columns(2, "actionColumns", true);
                    ImGui.SetColumnWidth(0, 220f.Scale());
                    ImGuiEx.ImGuiLineCentered("###MacroActions", () => ImGuiEx.TextUnderlined("Macro Actions"));
                    ImGui.Indent();
                    for (int i = 0; i < SelectedMacro.Steps.Count; i++)
                    {
                        var step = SelectedMacro.Steps[i];
                        var selectedAction = ImGui.Selectable($"{i + 1}. {(step.Action == Skills.None ? "Artisan 推荐" : step.Action.NameOfAction())}###selectedAction{i}", i == selectedStepIndex);
                        if (selectedAction)
                            selectedStepIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedStepIndex >= 0)
                    {
                        var step = SelectedMacro.Steps[selectedStepIndex];

                        ImGui.NextColumn();
                        ImGuiEx.CenterColumnText($"已选择技能: {(step.Action == Skills.None ? "Artisan 推荐" : step.Action.NameOfAction())}", true);
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                            {
                                selectedStepIndex--;
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                            {
                                selectedStepIndex++;
                            }
                        }

                        ImGui.Dummy(new Vector2(0, 0));
                        ImGui.SameLine();
                        if (ImGui.Checkbox($"阻止该技能的自动调整", ref step.ExcludeFromUpgrade))
                            P.Config.Save();

                        ImGui.Spacing();
                        ImGuiEx.CenterColumnText($"跳过这些条件", true);

                        ImGui.BeginChild("ConditionalExcludes", new Vector2(ImGui.GetContentRegionAvail().X, 100f), false, ImGuiWindowFlags.AlwaysAutoResize);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                        ImGui.Columns(3, null, false);
                        if (ImGui.Checkbox($"普通", ref step.ExcludeNormal))
                            P.Config.Save();
                        if (ImGui.Checkbox($"低品质", ref step.ExcludePoor))
                            P.Config.Save();
                        if (ImGui.Checkbox($"高品质", ref step.ExcludeGood))
                            P.Config.Save();
                        if (ImGui.Checkbox($"最高品质", ref step.ExcludeExcellent))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"安定", ref step.ExcludeCentered))
                            P.Config.Save();
                        if (ImGui.Checkbox($"结实", ref step.ExcludeSturdy))
                            P.Config.Save();
                        if (ImGui.Checkbox($"高效", ref step.ExcludePliant))
                            P.Config.Save();
                        if (ImGui.Checkbox($"大进展", ref step.ExcludeMalleable))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"长持续", ref step.ExcludePrimed))
                            P.Config.Save();
                        if (ImGui.Checkbox($"好兆头", ref step.ExcludeGoodOmen))
                            P.Config.Save();

                        ImGui.Columns(1);
                        ImGui.PopStyleVar();
                        ImGui.EndChild();
                        if (ImGui.Button("删除技能 (按住Ctrl)") && ImGui.GetIO().KeyCtrl)
                        {
                            SelectedMacro.Steps.RemoveAt(selectedStepIndex);
                            P.Config.Save();
                            if (selectedStepIndex == SelectedMacro.Steps.Count)
                                selectedStepIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "替换技能"))
                        {
                            if (ImGui.Selectable($"Artisan 推荐"))
                            {
                                step.Action = Skills.None;
                                P.Config.Save();
                            }

                            foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(SheetExtensions.NameOfAction))
                            {
                                if (ImGui.Selectable(opt.NameOfAction()))
                                {
                                    step.Action = opt;
                                    P.Config.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("技能排序");
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex - 1, 2);
                                selectedStepIndex--;
                                P.Config.Save();
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedStepIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex, 2);
                                selectedStepIndex++;
                                P.Config.Save();
                            }
                        }

                    }
                    ImGui.Columns(1);
                }
                else
                {
                    ImGui.Text($"宏操作（每行一个技能）");
                    ImGuiComponents.HelpMarker("您可以像复制/粘贴普通游戏宏一样直接复制/粘贴宏，也可以按行列出每个动作。\n例如：\n/ac Muscle Memory\n\n与\n\nMuscle Memory\n\n相同。您也可以使用*（星号）或“Artisan推荐”插入Artisan的推荐作为步骤。");
                    ImGui.InputTextMultiline("###MacroEditor", ref _rawMacro, 10000000, new Vector2(ImGui.GetContentRegionAvail().X - 30f, ImGui.GetContentRegionAvail().Y - 30f));
                    if (ImGui.Button("保存"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"宏已更新");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("保存并关闭"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"宏已更新");
                        }

                        Raweditor = !Raweditor;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("关闭"))
                    {
                        Raweditor = !Raweditor;
                    }
                }


                ImGuiEx.ImGuiLineCentered("MTimeHead", delegate
                {
                    ImGuiEx.TextUnderlined($"预计执行时长");
                });
                ImGuiEx.ImGuiLineCentered("MTimeArtisan", delegate
                {
                    ImGuiEx.Text($"Artisan: {MacroUI.GetMacroLength(SelectedMacro)} 秒");
                });
                ImGuiEx.ImGuiLineCentered("MTimeTeamcraft", delegate
                {
                    ImGuiEx.Text($"正常宏: {MacroUI.GetTeamcraftMacroLength(SelectedMacro)} 秒");
                });
            }
            else
            {
                selectedStepIndex = -1;
            }
        }

        private void OnCraftStarted(Lumina.Excel.GeneratedSheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial) => IsOpen = false;
    }
}
