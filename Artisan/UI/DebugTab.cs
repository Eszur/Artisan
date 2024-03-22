using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.GameInterop.CSExt;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using OtterGui;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static Artisan.GameInterop.CSExt.CraftingEventHandler;
using static ECommons.GenericHelpers;

namespace Artisan.UI
{
    internal unsafe class DebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;
        public static int DebugValue = 1;

        internal static void Draw()
        {
            try
            {
                ImGui.Checkbox("调试日志", ref Debug);
                if (ImGui.CollapsingHeader("能工巧匠食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包内工匠食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("背包内HQ工匠食物"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("所有能工巧匠药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包内工匠药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("背包内HQ工匠药水"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("工程学指南"))
                {
                    foreach (var x in ConsumableChecker.GetManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包内工程学指南"))
                {
                    foreach (var x in ConsumableChecker.GetManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("军用指南"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("背包内的军用指南"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }

                if (ImGui.CollapsingHeader("制作状态") && Crafting.CurCraft != null && Crafting.CurStep != null)
                {
                    ImGui.Text($"加工精度: {Crafting.CurCraft.StatControl}");
                    ImGui.Text($"作业精度: {Crafting.CurCraft.StatCraftsmanship}");
                    ImGui.Text($"当前耐久: {Crafting.CurStep.Durability}");
                    ImGui.Text($"最大耐久: {Crafting.CurCraft.CraftDurability}");
                    ImGui.Text($"当前进展: {Crafting.CurStep.Progress}");
                    ImGui.Text($"最大进展: {Crafting.CurCraft.CraftProgress}");
                    ImGui.Text($"当前品质: {Crafting.CurStep.Quality}");
                    ImGui.Text($"最大品质: {Crafting.CurCraft.CraftQualityMax}");
                    ImGui.Text($"优质率: {Calculations.GetHQChance(Crafting.CurStep.Quality * 100.0 / Crafting.CurCraft.CraftQualityMax)}");
                    ImGui.Text($"物品名称: {Crafting.CurRecipe?.ItemResult.Value?.Name}");
                    ImGui.Text($"当前状态: {Crafting.CurStep.Condition}");
                    ImGui.Text($"当前步骤: {Crafting.CurStep.Index}");
                    ImGui.Text($"简易制作: {Crafting.QuickSynthState.Cur} / {Crafting.QuickSynthState.Max}");
                    ImGui.Text($"阔步+比尔格Combo: {StandardSolver.GreatStridesByregotCombo(Crafting.CurCraft, Crafting.CurStep)}");
                    ImGui.Text($"初期品质: {Simulator.BaseQuality(Crafting.CurCraft)}");
                    ImGui.Text($"初期进展: {Simulator.BaseProgress(Crafting.CurCraft)}");
                    ImGui.Text($"预期品质: {StandardSolver.CalculateNewQuality(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"预期进展: {StandardSolver.CalculateNewProgress(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"低收藏价值: {Crafting.CurCraft.CraftQualityMin1}");
                    ImGui.Text($"中收藏价值: {Crafting.CurCraft.CraftQualityMin2}");
                    ImGui.Text($"高收藏价值: {Crafting.CurCraft.CraftQualityMin3}");
                    ImGui.Text($"制作状态: {Crafting.CurState}");
                    ImGui.Text($"能否完成: {StandardSolver.CanFinishCraft(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action)}");
                    ImGui.Text($"当前记录: {CraftingProcessor.NextRec.Action.NameOfAction()}");
                    ImGui.Text($"上一步技能: {Crafting.CurStep.PrevComboAction.NameOfAction()}");
                    ImGui.Text($"Can insta 精密制作: {Crafting.CurStep.Index == 1 && StandardSolver.CanFinishCraft(Crafting.CurCraft, Crafting.CurStep, Skills.DelicateSynthesis) && StandardSolver.CalculateNewQuality(Crafting.CurCraft, Crafting.CurStep, Skills.DelicateSynthesis) >= Crafting.CurCraft.CraftQualityMin3}");
                }

                if (ImGui.CollapsingHeader("魔晶石精制"))
                {
                    ImGui.Text($"主手 精炼度: {Spiritbond.Weapon}");
                    ImGui.Text($"副手 精炼度: {Spiritbond.Offhand}");
                    ImGui.Text($"头部 精炼度: {Spiritbond.Helm}");
                    ImGui.Text($"身体 精炼度: {Spiritbond.Body}");
                    ImGui.Text($"手臂 精炼度: {Spiritbond.Hands}");
                    ImGui.Text($"腿部 精炼度: {Spiritbond.Legs}");
                    ImGui.Text($"脚部 精炼度: {Spiritbond.Feet}");
                    ImGui.Text($"耳部 精炼度: {Spiritbond.Earring}");
                    ImGui.Text($"颈部 精炼度: {Spiritbond.Neck}");
                    ImGui.Text($"腕部 精炼度: {Spiritbond.Wrist}");
                    ImGui.Text($"右指 精炼度: {Spiritbond.Ring1}");
                    ImGui.Text($"左指 精炼度: {Spiritbond.Ring2}");

                    ImGui.Text($"已经满精炼的装备: {Spiritbond.IsSpiritbondReadyAny()}");

                }

                if (ImGui.CollapsingHeader("任务"))
                {
                    QuestManager* qm = QuestManager.Instance();
                    foreach (var quest in qm->DailyQuestsSpan)
                    {
                        ImGui.TextWrapped($"任务 ID: {quest.QuestId}, 序列: {QuestManager.GetQuestSequence(quest.QuestId)}, 名称: {quest.QuestId.NameOfQuest()}, Flags: {quest.Flags}");
                    }

                }

                if (ImGui.CollapsingHeader("IPC"))
                {
                    ImGui.Text($"AutoRetainer: {IPC.AutoRetainer.IsEnabled()}");
                    if (ImGui.Button("Suppress"))
                    {
                        IPC.AutoRetainer.Suppress();
                    }
                    if (ImGui.Button("Unsuppress"))
                    {
                        IPC.AutoRetainer.Unsuppress();
                    }

                    ImGui.Text($"Endurance IPC: {Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.GetEnduranceStatus").InvokeFunc()}");
                    ImGui.Text($"List IPC: {Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.IsListRunning").InvokeFunc()}");
                    if (ImGui.Button("Enable"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(true);
                    }
                    if (ImGui.Button("Disable"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(false);
                    }

                    if (ImGui.Button("Send Stop Request (true)"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(true);
                    }

                    if (ImGui.Button("Send Stop Request (false)"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(false);
                    }

                    if (ImGui.Button($"Stop Navmesh"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Stop").InvokeAction();
                    }

                    ImGui.Text($"Navmesh Ready: {Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.IsNavmeshReady").InvokeFunc()}");
                    ImGui.Text($"Navmesh Running: {Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.IsPathRunning").InvokeFunc()}");
                }

                if (ImGui.CollapsingHeader("收藏品"))
                {
                    foreach (var item in LuminaSheets.ItemSheet.Values.Where(x => x.IsCollectable).OrderBy(x => x.LevelItem.Row))
                    {
                        if (Svc.Data.GetExcelSheet<CollectablesShopItem>().TryGetFirst(x => x.Item.Row == item.RowId, out var collectibleSheetItem))
                        {
                            if (collectibleSheetItem != null)
                            {
                                ImGui.Text($"{item.Name} - {collectibleSheetItem.CollectablesShopRewardScrip.Value.LowReward}");
                            }
                        }
                    }
                }

                if (ImGui.CollapsingHeader("RecipeNote"))
                {
                    var recipes = RecipeNoteRecipeData.Ptr();
                    if (recipes != null && recipes->Recipes != null)
                    {
                        if (recipes->SelectedIndex < recipes->RecipesCount)
                            DrawRecipeEntry($"已选", recipes->Recipes + recipes->SelectedIndex);
                        for (int i = 0; i < recipes->RecipesCount; ++i)
                            DrawRecipeEntry(i.ToString(), recipes->Recipes + i);
                    }
                    else
                    {
                        ImGui.TextUnformatted($"Null: {(nint)recipes:X}");
                    }
                }

                if (ImGui.CollapsingHeader("装备"))
                {
                    ImGui.TextUnformatted($"游戏内统计: {CharacterInfo.Craftsmanship}/{CharacterInfo.Control}/{CharacterInfo.MaxCP}");
                    DrawEquippedGear();
                    foreach (ref var gs in RaptureGearsetModule.Instance()->EntriesSpan)
                        DrawGearset(ref gs);
                }

                if (ImGui.CollapsingHeader("修理"))
                {
                    if (ImGui.Button("修复所有装备"))
                    {
                        RepairManager.ProcessRepair();
                    }
                    ImGuiEx.Text($"装备耐久: {RepairManager.GetMinEquippedPercent()}");

                    ImGui.Text($"Can Repair: {(LuminaSheets.ItemSheet.ContainsKey((uint)DebugValue) ? LuminaSheets.ItemSheet[(uint)DebugValue].Name : "")} {RepairManager.CanRepairItem((uint)DebugValue)}");
                    ImGui.Text($"Can Repair Any: {RepairManager.CanRepairAny()}");
                    ImGui.Text($"附近的修理NPC: {RepairManager.RepairNPCNearby(out _)}");

                    if (ImGui.Button("与修理NPC交互"))
                    {
                        P.TM.Enqueue(() => RepairManager.InteractWithRepairNPC(), "RepairManagerDebug");
                    }

                    ImGui.Text($"修理费用: {RepairManager.GetNPCRepairPrice()}");

                }

                ImGui.Separator();

                ImGui.Text($"Endurance Item: {Endurance.RecipeID} {Endurance.RecipeName}");
                if (ImGui.Button($"Open Endurance Item"))
                {
                    CraftingListFunctions.OpenRecipeByID(Endurance.RecipeID);
                }

                ImGui.InputInt("Debug Value", ref DebugValue);
                if (ImGui.Button($"Open Recipe"))
                {
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)DebugValue);
                }

                ImGui.Text($"Item Count? {CraftingListUI.NumberOfIngredient((uint)DebugValue)}");

                ImGui.Text($"Completed Recipe? {((uint)DebugValue).NameOfRecipe()} {P.ri.HasRecipeCrafted((uint)DebugValue)}");

                if (ImGui.Button($"打开并简易制作"))
                {
                    Operations.QuickSynthItem(DebugValue);
                }
                if (ImGui.Button($"关闭简易制作窗口"))
                {
                    Operations.CloseQuickSynthWindow();
                }
                if (ImGui.Button($"打开精制魔晶石窗口"))
                {
                    Spiritbond.OpenMateriaMenu();
                }
                if (ImGui.Button($"精制第一个魔晶石"))
                {
                    Spiritbond.ExtractFirstMateria();
                }

                if (ImGui.Button($"Pandora IPC"))
                {
                    var state = Svc.PluginInterface.GetIpcSubscriber<string, bool?>($"PandorasBox.GetFeatureEnabled").InvokeFunc("Auto-Fill Numeric Dialogs");
                    Svc.Log.Debug($"State of Auto-Fill Numeric Dialogs: {state}");
                    Svc.PluginInterface.GetIpcSubscriber<string, bool, object>($"PandorasBox.SetFeatureEnabled").InvokeAction("Auto-Fill Numeric Dialogs", !(state ?? false));
                    state = Svc.PluginInterface.GetIpcSubscriber<string, bool?>($"PandorasBox.GetFeatureEnabled").InvokeFunc("Auto-Fill Numeric Dialogs");
                    Svc.Log.Debug($"State of Auto-Fill Numeric Dialogs after setting: {state}");
                }

                if (ImGui.Button("Set Ingredients"))
                {
                    CraftingListFunctions.SetIngredients();
                }

                if (TryGetAddonByName<AtkUnitBase>("RetainerHistory", out var addon))
                {
                    var list = addon->UldManager.SearchNodeById(10)->GetAsAtkComponentList();
                    ImGui.Text($"{list->ListLength}");
                }
            }
            catch (Exception e)
            {
                e.Log();
            }

            ImGui.Text($"{Crafting.CurState}");
            ImGui.Text($"{PreCrafting.Tasks.Count()}");
            ImGui.Text($"{P.TM.IsBusy}");
            ImGui.Text($"{CraftingListFunctions.CLTM.IsBusy}");
        }

        private static void DrawRecipeEntry(string tag, RecipeNoteRecipeEntry* e)
        {
            var recipe = Svc.Data.GetExcelSheet<Recipe>()?.GetRow(e->RecipeId);
            using var n = ImRaii.TreeNode($"{tag}: {e->RecipeId} '{recipe?.ItemResult.Value?.Name}'###{tag}");
            if (!n)
                return;

            int i = 0;
            foreach (ref var ing in e->IngredientsSpan)
            {
                if (ing.NumTotal != 0)
                {
                    var item = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Item>()?.GetRow(ing.ItemId);
                    using var n1 = ImRaii.TreeNode($"Ingredient {i}: {ing.ItemId} '{item?.Name}' (ilvl={item?.LevelItem.Row}, hq={item?.CanBeHq}), max={ing.NumTotal}, nq={ing.NumAssignedNQ}/{ing.NumAvailableNQ}, hq={ing.NumAssignedHQ}/{ing.NumAvailableHQ}", ImGuiTreeNodeFlags.Leaf);
                }
                i++;
            }

            if (recipe != null)
            {
                var startingQuality = Calculations.GetStartingQuality(recipe, e->GetAssignedHQIngredients());
                using var n2 = ImRaii.TreeNode($"Starting quality: {startingQuality}/{Calculations.RecipeMaxQuality(recipe)}", ImGuiTreeNodeFlags.Leaf);
            }
        }

        private static void DrawEquippedGear()
        {
            using var nodeEquipped = ImRaii.TreeNode("Equipped gear");
            if (!nodeEquipped)
                return;

            var stats = CharacterStats.GetBaseStatsEquipped();
            ImGui.TextUnformatted($"Total stats: {stats.Craftsmanship}/{stats.Control}/{stats.CP}/{stats.Splendorous}/{stats.Specialist}");

            var inventory = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (inventory == null)
                return;

            for (int i = 0; i < inventory->Size; ++i)
            {
                var item = inventory->Items + i;
                var details = new ItemStats(item);
                if (details.Data == null)
                    continue;

                using var n = ImRaii.TreeNode($"{i}: {item->ItemID} '{details.Data.Name}' ({item->Flags}): crs={details.Stats[0].Base}+{details.Stats[0].Melded}/{details.Stats[0].Max}, ctrl={details.Stats[1].Base}+{details.Stats[1].Melded}/{details.Stats[1].Max}, cp={details.Stats[2].Base}+{details.Stats[2].Melded}/{details.Stats[2].Max}");
                if (n)
                {
                    for (int j = 0; j < 5; ++j)
                    {
                        using var m = ImRaii.TreeNode($"Materia {j}: {item->Materia[j]} {item->MateriaGrade[j]}", ImGuiTreeNodeFlags.Leaf);
                    }
                }
            }
        }

        private static void DrawGearset(ref RaptureGearsetModule.GearsetEntry gs)
        {
            if (!gs.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                return;

            fixed (byte* name = gs.Name)
            {
                using var nodeGearset = ImRaii.TreeNode($"Gearset {gs.ID} '{Dalamud.Memory.MemoryHelper.ReadString((nint)name, 48)}' {(Job)gs.ClassJob} ({gs.Flags})");
                if (!nodeGearset)
                    return;

                var stats = CharacterStats.GetBaseStatsGearset(ref gs);
                ImGui.TextUnformatted($"Total stats: {stats.Craftsmanship}/{stats.Control}/{stats.CP}/{stats.Splendorous}/{stats.Specialist}");

                for (int i = 0; i < gs.ItemsSpan.Length; ++i)
                {
                    ref var item = ref gs.ItemsSpan[i];
                    var details = new ItemStats((RaptureGearsetModule.GearsetItem*)Unsafe.AsPointer(ref item));
                    if (details.Data == null)
                        continue;

                    using var n = ImRaii.TreeNode($"{i}: {item.ItemID} '{details.Data.Name}' ({item.Flags}): crs={details.Stats[0].Base}+{details.Stats[0].Melded}/{details.Stats[0].Max}, ctrl={details.Stats[1].Base}+{details.Stats[1].Melded}/{details.Stats[1].Max}, cp={details.Stats[2].Base}+{details.Stats[2].Melded}/{details.Stats[2].Max}");
                    if (n)
                    {
                        for (int j = 0; j < 5; ++j)
                        {
                            using var m = ImRaii.TreeNode($"Materia {j}: {item.Materia[j]} {item.MateriaGrade[j]}", ImGuiTreeNodeFlags.Leaf);
                        }
                    }
                }
            }
        }

        public class Item
        {
            public uint Key { get; set; }
            public string Name { get; set; } = "";
            public ushort CraftingTime { get; set; }
            public uint UIIndex { get; set; }
        }
    }
}
