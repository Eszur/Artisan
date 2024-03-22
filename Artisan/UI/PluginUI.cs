using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.FCWorkshops;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using System;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using ThreadLoadImageHandler = ECommons.ImGuiMethods.ThreadLoadImageHandler;

namespace Artisan.UI
{
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;


        private bool visible = false;
        public OpenWindow OpenWindow { get; set; } = OpenWindow.Overview;

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###Artisan")
        {
            this.RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new(250, 100),
                MaximumSize = new(9999, 9999)
            };
            P.ws.AddWindow(this);
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

        public void Dispose()
        {

        }

        public override void Draw()
        {
            if (DalamudInfo.IsOnStaging())
            {
                ImGui.Text($"Artisan 无法在临时版 Dalamud 上运行。请键入 /xlbranch，然后切换到 release。");
                return;
            }

            var region = ImGui.GetContentRegionAvail();
            var itemSpacing = ImGui.GetStyle().ItemSpacing;

            var topLeftSideHeight = region.Y;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f.Scale(), 0));
            try
            {
                ShowEnduranceMessage();

                using (var table = ImRaii.Table($"ArtisanTableContainer", 2, ImGuiTableFlags.Resizable))
                {
                    if (!table)
                        return;

                    ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                    ImGui.TableNextColumn();

                    var regionSize = ImGui.GetContentRegionAvail();

                    ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                    using (var leftChild = ImRaii.Child($"###ArtisanLeftSide", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                    {
                        var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan-icon.png");

                        if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                        {
                            ImGuiEx.ImGuiLineCentered("###ArtisanLogo", () =>
                            {
                                ImGui.Image(logo.ImGuiHandle, new(125f.Scale(), 125f.Scale()));
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"You are the 69th person to find this secret. Nice!");
                                    ImGui.EndTooltip();
                                }
                            });

                        }
                        ImGui.Spacing();
                        ImGui.Separator();

                        if (ImGui.Selectable("概述", OpenWindow == OpenWindow.Overview))
                        {
                            OpenWindow = OpenWindow.Overview;
                        }
                        if (ImGui.Selectable("设置", OpenWindow == OpenWindow.Main))
                        {
                            OpenWindow = OpenWindow.Main;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("重复制作", OpenWindow == OpenWindow.Endurance))
                        {
                            OpenWindow = OpenWindow.Endurance;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("宏", OpenWindow == OpenWindow.Macro))
                        {
                            OpenWindow = OpenWindow.Macro;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("制作清单", OpenWindow == OpenWindow.Lists))
                        {
                            OpenWindow = OpenWindow.Lists;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("清单生成", OpenWindow == OpenWindow.SpecialList))
                        {
                            OpenWindow = OpenWindow.SpecialList;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("部队工坊", OpenWindow == OpenWindow.FCWorkshop))
                        {
                            OpenWindow = OpenWindow.FCWorkshop;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("模拟器", OpenWindow == OpenWindow.Simulator))
                        {
                            OpenWindow = OpenWindow.Simulator;
                        }
                        ImGui.Spacing();
                        if (ImGui.Selectable("关于", OpenWindow == OpenWindow.About))
                        {
                            OpenWindow = OpenWindow.About;
                        }


#if DEBUG
                        ImGui.Spacing();
                        if (ImGui.Selectable("DEBUG", OpenWindow == OpenWindow.Debug))
                        {
                            OpenWindow = OpenWindow.Debug;
                        }
                        ImGui.Spacing();
#endif

                    }

                    ImGui.PopStyleVar();
                    ImGui.TableNextColumn();
                    using (var rightChild = ImRaii.Child($"###ArtisanRightSide", Vector2.Zero, false))
                    {
                        switch (OpenWindow)
                        {
                            case OpenWindow.Main:
                                DrawMainWindow();
                                break;
                            case OpenWindow.Endurance:
                                Endurance.Draw();
                                break;
                            case OpenWindow.Lists:
                                CraftingListUI.Draw();
                                break;
                            case OpenWindow.About:
                                AboutTab.Draw("Artisan");
                                break;
                            case OpenWindow.Debug:
                                DebugTab.Draw();
                                break;
                            case OpenWindow.Macro:
                                MacroUI.Draw();
                                break;
                            case OpenWindow.FCWorkshop:
                                FCWorkshopUI.Draw();
                                break;
                            case OpenWindow.SpecialList:
                                SpecialLists.Draw();
                                break;
                            case OpenWindow.Overview:
                                DrawOverview();
                                break;
                            case OpenWindow.Simulator:
                                SimulatorUI.Draw();
                                break;
                            case OpenWindow.None:
                                break;
                            default:
                                break;
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
            ImGui.PopStyleVar();
        }

        private void DrawOverview()
        {
            var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
            {
                ImGuiEx.ImGuiLineCentered("###ArtisanTextLogo", () =>
                {
                    ImGui.Image(logo.ImGuiHandle, new Vector2(logo.Width, 100f.Scale()));
                });
            }

            ImGuiEx.ImGuiLineCentered("###ArtisanOverview", () =>
            {
                ImGuiEx.TextUnderlined("Artisan - 概述");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"我首先要感谢你下载我的小制作插件。自2022年6月以来，我一直致力于Artisan，这是我插件的代表作。");
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"在您开始使用Artisan之前，我们应该先了解一下插件的工作原理。一旦你了解了几个关键因素，Artisan就很容易使用。");

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###ArtisanModes", () =>
            {
                ImGuiEx.TextUnderlined("Crafting Modes");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan具有“自动执行操作模式”，它只接受提供给它的建议并代表您执行操作。" +
                                " 默认情况下，它会在游戏允许的范围内以最快的速度执行操作，比普通宏更快" +
                                " 您这样做不会绕过任何游戏限制，但你可以选择设置延迟。" +
                                " 启用此选项与Artisan默认使用的建议生成过程无关。");

            var automode = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/AutoMode.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(automode, out var example))
            {
                ImGuiEx.ImGuiLineCentered("###AutoModeExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"如果您没有启用自动模式，您还可以使用另外两种模式：“半手动模式”和“全手动模式”" +
                                $" 当你开始制作时，“半手动模式”会弹出一个小窗口。");

            var craftWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/ThemeCraftingWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(craftWindowExample, out example))
            {
                ImGuiEx.ImGuiLineCentered("###CraftWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"点击“执行推荐操作”按钮后，您就可以指示插件执行它推荐的建议。" +
                $"这被视为半手动模式，因为您仍需点击每个操作，但不必担心在热键栏上找不到它们。" +
                $"“全手动”模式则是像平常一样按下热键栏上的按钮" +
                $"默认情况下，Artisan 会为你提供帮助，因为如果你的热键栏上有放入该技能，Artisan 就会高亮显示该操作。(这可以在设置中禁用）");

            var outlineExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/OutlineExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(outlineExample, out example))
            {
                ImGuiEx.ImGuiLineCentered("###OutlineExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###ArtisanSuggestions", () =>
            {
                ImGuiEx.TextUnderlined("求解器/宏");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"默认情况下，Artisan会为您提供下一步制作步骤的建议。不过，这个解算器并不完美，它绝对不能取代合适的装备。" +
                $"除了启用 Artisan 外，您不需要做任何事情就可以启用此行为。 " +
                $"\r\n\r\n" +
                $"如果您试图处理默认解算器无法处理的制作，Artisan允许您创建宏，这些宏可以代替默认解算器为您提供建议。" +
                $"Artisan宏的优点是不受长度限制，可以在游戏允许的范围内快速启动，还允许一些额外的选项进行动态调整。");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入宏菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Macro;
            }
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"一旦你创建了一个宏，你就必须把它分配给一个配方。这可以通过使用“配方窗口”下拉菜单轻松完成。默认情况下，它附加在游戏中制作笔记窗口的右上角，但可以在设置中附加。");


            var recipeWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/RecipeWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(recipeWindowExample, out example))
            {
                ImGuiEx.ImGuiLineCentered("###RecipeWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }


            ImGuiEx.TextWrapped($"从下拉框中选择已创建的宏。 " +
                $"当您制作此物品时，建议将被宏的内容所取代。");


            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###Endurance", () =>
            {
                ImGuiEx.TextUnderlined("Endurance");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan有一个名为“重复模式”的模式，基本上就是“自动重复模式”的一种更高级的说法，它会不断尝试为你制作相同的物品。" +
                $"重复模式通过从游戏内制作笔记中选择配方并启用该功能来工作 " +
                $"然后，您的角色会尝试重复制作该物品，次数不限，只要您有制作该物品的材料。 " +
                $"\r\n\r\n" +
                $"重复模式还可以管理食物、药水、手册、修理和精制魔晶石，其他功能希望不言自明。 " +
                $"修理功能只支持使用暗物质进行修理，不支持修理NPC");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入重复模式菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Endurance;
            }

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###Lists", () =>
            {
                ImGuiEx.TextUnderlined("制作清单");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan还可以创建一个物品清单，并让它自动开始制作一个又一个物品。 " +
                $"制作清单有很多强大的工具，可以简化从材料到最终产品的过程。 " +
                $"它还支持导入和导出到 Teamcraft。");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"单击此处进入制作清单菜单。");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Lists;
            }

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###Questions", () =>
            {
                ImGuiEx.TextUnderlined("有问题吗？");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"如果您对此处未概述的内容有疑问，可以在我们的");
            ImGui.SameLine(ImGui.GetCursorPosX(), 1.5f);
            ImGuiEx.TextUnderlined($"Discord 频道.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://discord.gg/Zzrcc8kmvy");
                }
            }

            ImGuiEx.TextWrapped($"您还可以在我们的");
            ImGui.SameLine(ImGui.GetCursorPosX(), 2f);
            ImGuiEx.TextUnderlined($"Github 页面.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://github.com/PunishXIV/Artisan");
                }
            }

        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"在这里您可以更改 Artisan 将使用的一些设置。其中一些设置还可以在制作过程中进行调整。");
            ImGui.TextWrapped($"要使用Artisan的手动模式高亮推荐技能功能，请将您已解锁的所有制作技能放到可见的热键栏中。");
            bool autoEnabled = P.Config.AutoMode;
            bool delayRec = P.Config.DelayRecommendation;
            bool failureCheck = P.Config.DisableFailurePrediction;
            int maxQuality = P.Config.MaxPercentage;
            bool useTricksGood = P.Config.UseTricksGood;
            bool useTricksExcellent = P.Config.UseTricksExcellent;
            bool useSpecialist = P.Config.UseSpecialist;
            //bool showEHQ = P.Config.ShowEHQ;
            //bool useSimulated = P.Config.UseSimulatedStartingQuality;
            bool disableGlow = P.Config.DisableHighlightedAction;
            bool disableToasts = P.Config.DisableToasts;
            bool disableMini = P.Config.DisableMiniMenu;

            ImGui.Separator();

            if (ImGui.CollapsingHeader("常规设置"))
            {
                if (ImGui.Checkbox("自动执行操作模式", ref autoEnabled))
                {
                    P.Config.AutoMode = autoEnabled;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"自动使用每个推荐的操作。");
                if (autoEnabled)
                {
                    var delay = P.Config.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("执行延迟 (ms)###ActionDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        P.Config.AutoDelay = delay;
                        P.Config.Save();
                    }
                }

                if (ImGui.Checkbox("延迟获得建议", ref delayRec))
                {
                    P.Config.DelayRecommendation = delayRec;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("如果您遇到“最终确认”没有在应该触发时触发的问题，请使用此功能。");

                if (delayRec)
                {
                    var delay = P.Config.RecommendationDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("设置延迟 (ms)###RecommendationDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        P.Config.RecommendationDelay = delay;
                        P.Config.Save();
                    }
                }

                bool requireFoodPot = P.Config.AbortIfNoFoodPot;
                if (ImGui.Checkbox("使用消耗品", ref requireFoodPot))
                {
                    P.Config.AbortIfNoFoodPot = requireFoodPot;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("Artisan将需要配置的食物、工程学指南或药品，如果找不到将拒绝制作。");

                if (ImGui.Checkbox("在制作练习中使用消耗品", ref P.Config.UseConsumablesTrial))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("在简易制作中使用消耗品", ref P.Config.UseConsumablesQuickSynth))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox($"NPC修理优先于自己修理", ref P.Config.PrioritizeRepairNPC))
                {
                    P.Config.Save();
                }

                ImGuiComponents.HelpMarker("修理时，如果附近有修理 NPC，将尝试使用他们进行修理，而不是自己修理。如果没有找到修理NPC，并且您达到修理所需等级，则仍会尝试使用自己修理。");

                if (ImGui.Checkbox($"如果无法修理，则禁用重复制作", ref P.Config.DisableEnduranceNoRepair))
                    P.Config.Save();

                ImGuiComponents.HelpMarker($"一旦达到修复临界值，如果自己或通过 NPC 无法修理，则禁用重复模式。");

                if (ImGui.Checkbox($"如果无法修理，则暂停清单", ref P.Config.DisableListsNoRepair))
                    P.Config.Save();

                ImGuiComponents.HelpMarker($"一旦达到修复阈值，如果自己或通过 NPC 无法修理，则暂停当前清单。");

                bool requestStop = P.Config.RequestToStopDuty;
                bool requestResume = P.Config.RequestToResumeDuty;
                int resumeDelay = P.Config.RequestToResumeDelay;

                if (ImGui.Checkbox("当任务搜索器准备就绪时，让Artisan关闭重复制作/暂停清单", ref requestStop))
                {
                    P.Config.RequestToStopDuty = requestStop;
                    P.Config.Save();
                }

                if (requestStop)
                {
                    if (ImGui.Checkbox("让Artisan在离开副本后恢复重复制作/取消暂停清单", ref requestResume))
                    {
                        P.Config.RequestToResumeDuty = requestResume;
                        P.Config.Save();
                    }

                    if (requestResume)
                    {
                        if (ImGui.SliderInt("恢复延迟（秒）", ref resumeDelay, 5, 60))
                        {
                            P.Config.RequestToResumeDelay = resumeDelay;
                        }
                    }
                }

                if (ImGui.Checkbox("禁止自动使用制作时所需的消耗品", ref P.Config.DontEquipItems))
                    P.Config.Save();

                if (ImGui.Checkbox("重复制作完成后播放声音", ref P.Config.PlaySoundFinishEndurance))
                    P.Config.Save();

                if (ImGui.Checkbox($"清单完成后播放声音", ref P.Config.PlaySoundFinishList))
                    P.Config.Save();

                if (P.Config.PlaySoundFinishEndurance || P.Config.PlaySoundFinishList)
                {
                    if (ImGui.SliderFloat("音量", ref P.Config.SoundVolume, 0f, 1f, "%.2f"))
                        P.Config.Save();
                }
            }
            if (ImGui.CollapsingHeader("宏设置"))
            {
                if (ImGui.Checkbox("无法使用技能时跳过宏步骤", ref P.Config.SkipMacroStepIfUnable))
                    P.Config.Save();

                if (ImGui.Checkbox($"Prevent Artisan from Continuing After Macro Finishes", ref P.Config.DisableMacroArtisanRecommendation))
                    P.Config.Save();
            }
            if (ImGui.CollapsingHeader("标准配方求解器设置"))
            {
                if (ImGui.Checkbox($"使用 {Skills.TricksOfTrade.NameOfAction()} - {LuminaSheets.AddonSheet[227].Text.RawString}", ref useTricksGood))
                {
                    P.Config.UseTricksGood = useTricksGood;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"使用 {Skills.TricksOfTrade.NameOfAction()} - {LuminaSheets.AddonSheet[228].Text.RawString}", ref useTricksExcellent))
                {
                    P.Config.UseTricksExcellent = useTricksExcellent;
                    P.Config.Save();
                }
                //ImGuiComponents.HelpMarker($"These 2 options allow you to make {Skills.TricksOfTrade.NameOfAction()} a priority when condition is {LuminaSheets.AddonSheet[227].Text.RawString} or {LuminaSheets.AddonSheet[228].Text.RawString}.\n\nThis will replace {Skills.PreciseTouch.NameOfAction()} & {Skills.IntensiveSynthesis.NameOfAction()} usage.\n\n{Skills.TricksOfTrade.NameOfAction()} will still be used before learning these or under certain circumstances regardless of settings.");
                ImGuiComponents.HelpMarker($"这两个选项允许您在条件为 {LuminaSheets.AddonSheet[227].Text.RawString} 或 {LuminaSheets.AddonSheet[228].Text.RawString} 时将 {Skills.TricksOfTrade.NameOfAction()} 设置为优先级。\n\n这将取代 {Skills.PreciseTouch.NameOfAction()} 和 {Skills.IntensiveSynthesis.NameOfAction()}的使用.\n\n{Skills.TricksOfTrade.NameOfAction()} 在学习这些技能之前或在某些情况下仍会使用，无论设置如何。");
                if (ImGui.Checkbox("使用专家技能", ref useSpecialist))
                {
                    P.Config.UseSpecialist = useSpecialist;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("如果当前职业有专家认证, 则消耗你可能拥有的任何“能工巧匠图纸”。\n“设计变动”将替代“观察”.\n“专心致志”将被用于一次初期的“集中加工”");
                ImGui.TextWrapped("最高品质%%");
                ImGuiComponents.HelpMarker($"一旦品质达到以下百分比，Artisan将只关注于推进作业进度。");
                if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"%d%%"))
                {
                    P.Config.MaxPercentage = maxQuality;
                    P.Config.Save();
                }

                ImGui.Text($"收藏品阈值断点");
                ImGuiComponents.HelpMarker("一旦集合达到某个阈值，解算器将停止追求品质。");

                if (ImGui.RadioButton($"最低档", P.Config.SolverCollectibleMode == 1))
                {
                    P.Config.SolverCollectibleMode = 1;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"中间档", P.Config.SolverCollectibleMode == 2))
                {
                    P.Config.SolverCollectibleMode = 2;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"最高档", P.Config.SolverCollectibleMode == 3))
                {
                    P.Config.SolverCollectibleMode = 3;
                    P.Config.Save();
                }

                if (ImGui.Checkbox($"使用品质起手 ({Skills.Reflect.NameOfAction()})", ref P.Config.UseQualityStarter))
                    P.Config.Save();
                ImGuiComponents.HelpMarker($"这往往对耐久性较低的工艺品更为有利。");

                ImGui.TextWrapped($"{Skills.PreparatoryTouch.NameOfAction()} -  {Buffs.InnerQuiet.NameOfBuff()} 最高层数");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker($"只使用 {Skills.PreparatoryTouch.NameOfAction()} 最多不超过 {Buffs.InnerQuiet.NameOfBuff()} 的堆叠数。这对于节省CP非常有用。");
                if (ImGui.SliderInt($"###MaxIQStacksPrepTouch", ref P.Config.MaxIQPrepTouch, 0, 10))
                    P.Config.Save();


            }
            if (ImGui.CollapsingHeader("专家配方求解器设置"))
            {
                if (P.Config.ExpertSolverConfig.Draw())
                    P.Config.Save();
            }
            if (ImGui.CollapsingHeader("脚本求解器设置"))
            {
                if (P.Config.ScriptSolverConfig.Draw())
                    P.Config.Save();
            }
            if (ImGui.CollapsingHeader("UI设置"))
            {
                if (ImGui.Checkbox("禁用高亮框", ref disableGlow))
                {
                    P.Config.DisableHighlightedAction = disableGlow;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("这是一个框，用于突出显示热栏上的操作，以便手动操作。");

                if (ImGui.Checkbox($"禁用推荐toasts", ref disableToasts))
                {
                    P.Config.DisableToasts = disableToasts;
                    P.Config.Save();
                }

                ImGuiComponents.HelpMarker("每当建议执行新技能时，这些弹出窗口就会出现。");

                if (ImGui.Checkbox("禁用配方列表迷你菜单", ref disableMini))
                {
                    P.Config.DisableMiniMenu = disableMini;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("隐藏配方列表中的配置设置迷你菜单。仍显示单个宏菜单。");

                bool lockMini = P.Config.LockMiniMenu;
                if (ImGui.Checkbox("保持配方列表迷你菜单位置与配方列表相连。", ref lockMini))
                {
                    P.Config.LockMiniMenu = lockMini;
                    P.Config.Save();
                }

                if (!P.Config.LockMiniMenu)
                {
                    if (ImGui.Checkbox($"固定迷你菜单位置", ref P.Config.PinMiniMenu))
                    {
                        P.Config.Save();
                    }
                }

                if (ImGui.Button("重置配方列表迷你菜单位置"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }

                if (ImGui.Checkbox($"扩展搜索栏功能", ref P.Config.ReplaceSearch))
                {
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"扩展配方菜单中的搜索栏，提供即时结果和点击打开配方的功能。");

                bool hideQuestHelper = P.Config.HideQuestHelper;
                if (ImGui.Checkbox($"隐藏任务助手", ref hideQuestHelper))
                {
                    P.Config.HideQuestHelper = hideQuestHelper;
                    P.Config.Save();
                }

                bool hideTheme = P.Config.DisableTheme;
                if (ImGui.Checkbox("禁用自定义主题", ref hideTheme))
                {
                    P.Config.DisableTheme = hideTheme;
                    P.Config.Save();
                }
                ImGui.SameLine();

                if (IconButtons.IconTextButton(FontAwesomeIcon.Clipboard, "复制主题"))
                {
                    Clipboard.SetText("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA");
                    Notify.Success("主题已复制到剪贴板");
                }

                if (ImGui.Checkbox("Disable Allagan Tools Integration With Lists", ref P.Config.DisableAllaganTools))
                    P.Config.Save();

                if (ImGui.Checkbox("禁用 Artisan 上下文菜单选项", ref P.Config.HideContextMenus))
                    P.Config.Save();

                ImGuiComponents.HelpMarker("当您在配方列表中的配方上单击右键或按□键时，就会出现这些新选项。");

                ImGui.Indent();
                if (ImGui.CollapsingHeader("模拟器设置"))
                {
                    if (ImGui.Checkbox("隐藏配方窗口模拟器结果", ref P.Config.HideRecipeWindowSimulator))
                        P.Config.Save();

                    if (ImGui.SliderFloat("模拟器技能图标大小", ref P.Config.SimulatorActionSize, 5f, 70f))
                    {
                        P.Config.Save();
                    }
                    ImGuiComponents.HelpMarker("设置模拟器选项卡中显示的技能图标的比例。");

                    if (ImGui.Checkbox("启用手动模式悬停预览", ref P.Config.SimulatorHoverMode))
                        P.Config.Save();

                    if (ImGui.Checkbox($"隐藏技能提示信息", ref P.Config.DisableSimulatorActionTooltips))
                        P.Config.Save();

                    ImGuiComponents.HelpMarker("在手动模式下将鼠标悬停在技能上时，将不显示说明提示信息。");
                }
                ImGui.Unindent();
            }
            if (ImGui.CollapsingHeader("清单设置"))
            {
                ImGui.TextWrapped($"这些设置将在创建制作清单时自动应用。");

                if (ImGui.Checkbox("跳过已足够的项目", ref P.Config.DefaultListSkip))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("自动精致魔晶石", ref P.Config.DefaultListMateria))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox("自动修理", ref P.Config.DefaultListRepair))
                {
                    P.Config.Save();
                }

                if (P.Config.DefaultListRepair)
                {
                    ImGui.TextWrapped($"修理地点");
                    ImGui.SameLine();
                    if (ImGui.SliderInt("###SliderRepairDefault", ref P.Config.DefaultListRepairPercent, 0, 100, $"%d%%"))
                    {
                        P.Config.Save();
                    }
                }

                if (ImGui.Checkbox("将添加到清单中的新项目设置为简易制作", ref P.Config.DefaultListQuickSynth))
                {
                    P.Config.Save();
                }

                if (ImGui.Checkbox($@"添加到清单后，重置“要添加的次数”。", ref P.Config.ResetTimesToAdd))
                    P.Config.Save();

                ImGui.PushItemWidth(100);
                if (ImGui.InputInt("使用上下文菜单添加的时间", ref P.Config.ContextMenuLoops))
                {
                    if (P.Config.ContextMenuLoops <= 0)
                        P.Config.ContextMenuLoops = 1;

                    P.Config.Save();
                }

                ImGui.PushItemWidth(400);
                if (ImGui.SliderFloat("制作之间的延迟", ref P.Config.ListCraftThrottle, 0.2f, 2f, "%.1f"))
                {
                    if (P.Config.ListCraftThrottle < 0.2f)
                        P.Config.ListCraftThrottle = 0.2f;

                    if (P.Config.ListCraftThrottle > 2f)
                        P.Config.ListCraftThrottle = 2f;

                    P.Config.Save();
                }

                ImGui.Indent();
                if (ImGui.CollapsingHeader("素材表设置"))
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"如果您已经查看了清单的素材表，则“所有列设置”不会产生效果。");

                    if (ImGui.Checkbox($@"默认隐藏“背包”列", ref P.Config.DefaultHideInventoryColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“雇员”列", ref P.Config.DefaultHideRetainerColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“所需剩余时间”列", ref P.Config.DefaultHideRemainingColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“来源”列", ref P.Config.DefaultHideCraftableColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“可加工数量”列", ref P.Config.DefaultHideCraftableCountColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“用于制作”列", ref P.Config.DefaultHideCraftItemsColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“类别”列", ref P.Config.DefaultHideCategoryColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“采集区域”列", ref P.Config.DefaultHideGatherLocationColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认隐藏“ID”列", ref P.Config.DefaultHideIdColumn))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认“仅显示HQ物品”已启用", ref P.Config.DefaultHQCrafts))
                        P.Config.Save();

                    if (ImGui.Checkbox($"默认“Colour Validation”已启用", ref P.Config.DefaultColourValidation))
                        P.Config.Save();

                    if (ImGui.Checkbox($"从Universalis获取价格", ref P.Config.UseUniversalis))
                        P.Config.Save();

                    if (P.Config.UseUniversalis)
                    {
                        if (ImGui.Checkbox($"将Universalis限制为当前DC", ref P.Config.LimitUnversalisToDC))
                            P.Config.Save();

                        if (ImGui.Checkbox($"仅按需获取价格", ref P.Config.UniversalisOnDemand))
                            P.Config.Save();

                        ImGuiComponents.HelpMarker("你必须点击按钮才能获取每件商品的价格。");
                    }
                }

                ImGui.Unindent();
            }
        }

        private void ShowEnduranceMessage()
        {
            if (!P.Config.ViewedEnduranceMessage)
            {
                P.Config.ViewedEnduranceMessage = true;
                P.Config.Save();

                ImGui.OpenPopup("EndurancePopup");

                var windowSize = new Vector2(512 * ImGuiHelpers.GlobalScale,
                    ImGui.GetTextLineHeightWithSpacing() * 13 + 2 * ImGui.GetFrameHeightWithSpacing() * 2f);
                ImGui.SetNextWindowSize(windowSize);
                ImGui.SetNextWindowPos((ImGui.GetIO().DisplaySize - windowSize) / 2);

                using var popup = ImRaii.Popup("EndurancePopup",
                    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.Modal);
                if (!popup)
                    return;

                ImGui.TextWrapped($@"我收到了很多关于重复模式不再设置素材的消息。截至上次更新，重复模式的旧功能已移至新设置。");
                ImGui.Dummy(new Vector2(0));

                var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/EnduranceNewSetting.png");

                if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var img))
                {
                    ImGuiEx.ImGuiLineCentered("###EnduranceNewSetting", () =>
                    {
                        ImGui.Image(img.ImGuiHandle, new Vector2(img.Width,img.Height));
                    });
                }

                ImGui.Spacing();

                ImGui.TextWrapped($"这一改动是为了恢复重复制作模式最原始的行为。如果您不关心您的原料比例，请确保启用最大数量模式。");

                ImGui.SetCursorPosY(windowSize.Y - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
                if (ImGui.Button("关闭", -Vector2.UnitX))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    public enum OpenWindow
    {
        None = 0,
        Main = 1,
        Endurance = 2,
        Macro = 3,
        Lists = 4,
        About = 5,
        Debug = 6,
        FCWorkshop = 7,
        SpecialList = 8,
        Overview = 9,
        Simulator = 10,
    }
}
