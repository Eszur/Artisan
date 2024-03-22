using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.Sounds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    public class EnduranceIngredients
    {
        public int HQSet { get; set; }
        public int IngredientSlot { get; set; }
        public int NQSet { get; set; }
    }

    internal static unsafe class Endurance
    {
        internal static bool SkipBuffs = false;
        internal static List<Task> Tasks = new();
        internal static CircularBuffer<long> Errors = new(5);

        internal static List<int>? HQData = null;

        internal static ushort RecipeID = 0;

        internal static EnduranceIngredients[] SetIngredients = new EnduranceIngredients[6];

        internal static readonly List<uint> UnableToCraftErrors = new List<uint>()
        {
            1134,1135,1136,1137,1138,1139,1140,1141,1142,1143,1144,1145,1146,1148,1149,1198,1199,1222,1223,1224,
        };

        internal static bool Enable
        {
            get => enable;
            set
            {
                Tasks.Clear();
                enable = value;
            }
        }

        internal static string RecipeName
        {
            get => RecipeID == 0 ? "未选择配方" : LuminaSheets.RecipeSheet[RecipeID].ItemResult.Value.Name.RawString.Trim();
        }

        internal static void ToggleEndurance(bool enable)
        {
            if (RecipeID > 0)
            {
                Enable = enable;
            }
        }

        internal static void Dispose()
        {
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
            Svc.Toasts.ErrorToast -= CheckNonMaxQuantityModeFinished;
        }

        internal static void Draw()
        {
            if (CraftingListUI.Processing)
            {
                ImGui.TextWrapped("正在处理列表...");
                return;
            }

            ImGui.TextWrapped("重复模式是Artisan反复重复相同物品的方式，可以重复多次，也可以直到材料用完。一旦一件装备的耐久比例低于一定值时，它就可以自动修复你的装备，使用食物/药品/经验手册，和精制魔晶石。请注意，这些设置独立于制作清单设置，仅用于重复制作一个项目。");
            ImGui.Separator();
            ImGui.Spacing();

            if (RecipeID == 0)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudRed, "未选择配方");
            }
            else
            {
                if (!CraftingListFunctions.HasItemsForRecipe(RecipeID))
                    ImGui.BeginDisabled();

                if (ImGui.Checkbox("启用重复模式", ref enable))
                {
                    ToggleEndurance(enable);
                }

                if (!CraftingListFunctions.HasItemsForRecipe(RecipeID))
                {
                    ImGui.EndDisabled();

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"配方材料不足，无法开始重复制作。");
                        ImGui.EndTooltip();
                    }
                }

                ImGuiComponents.HelpMarker("为了开始重复制作模式，你应该首先在制作菜单中选择配方。\n重复模式将自动重复选择的配方，类似于自动制作，但在重复之前会考虑到食物/药物的buff。");

                ImGuiEx.Text($"配方: {RecipeName} {(RecipeID != 0 ? $"({LuminaSheets.ClassJobSheet[LuminaSheets.RecipeSheet[RecipeID].CraftType.Row + 8].Abbreviation})" : "")}");
            }

            bool repairs = P.Config.Repair;
            if (ImGui.Checkbox("自动修理", ref repairs))
            {
                P.Config.Repair = repairs;
                P.Config.Save();
            }
            ImGuiComponents.HelpMarker($"如果启用，当任何装备达到配置的修复阈值时，Artisan将自动修理您的装备。\n\n当前的条件为：装备最低耐久{RepairManager.GetMinEquippedPercent()}%，修理工的修理价格低于{RepairManager.GetNPCRepairPrice()}gil。\n\n如果无法用暗物质修复，将尝试在附近修理NPC。");
            if (P.Config.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                int percent = P.Config.RepairPercent;
                if (ImGui.SliderInt("##repairp", ref percent, 10, 100, $"%d%%"))
                {
                    P.Config.RepairPercent = percent;
                    P.Config.Save();
                }
            }

            if (!CharacterInfo.MateriaExtractionUnlocked())
                ImGui.BeginDisabled();

            bool materia = P.Config.Materia;
            if (ImGui.Checkbox("自动精制魔晶石", ref materia))
            {
                P.Config.Materia = materia;
                P.Config.Save();
            }

            if (!CharacterInfo.MateriaExtractionUnlocked())
            {
                ImGui.EndDisabled();

                ImGuiComponents.HelpMarker("此角色尚未解锁精制魔晶石，该设置将被忽略。");
            }
            else
                ImGuiComponents.HelpMarker("一旦装备的精炼度达到100%，就会自动从装备的装备中提取魔晶石");

            ImGui.Checkbox("只制作 X 次", ref P.Config.CraftingX);
            if (P.Config.CraftingX)
            {
                ImGui.Text("次数：");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref P.Config.CraftX))
                {
                    if (P.Config.CraftX < 0)
                        P.Config.CraftX = 0;
                }
            }

            if (ImGui.Checkbox("尽可能使用简易制作", ref P.Config.QuickSynthMode))
            {
                P.Config.Save();
            }

            bool stopIfFail = P.Config.EnduranceStopFail;
            if (ImGui.Checkbox("在制作失败后禁用重复模式", ref stopIfFail))
            {
                P.Config.EnduranceStopFail = stopIfFail;
                P.Config.Save();
            }

            bool stopIfNQ = P.Config.EnduranceStopNQ;
            if (ImGui.Checkbox("在制作出NQ后禁用重复模式", ref stopIfNQ))
            {
                P.Config.EnduranceStopNQ = stopIfNQ;
                P.Config.Save();
            }

            if (ImGui.Checkbox("最大数量模式", ref P.Config.MaxQuantityMode))
            {
                P.Config.Save();
            }

            ImGuiComponents.HelpMarker("将为您设置素材，以最大限度地增加制作的数量。");
        }

        internal static void DrawRecipeData()
        {
            var addonPtr = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon))
            {
                if (addonPtr == IntPtr.Zero)
                {
                    return;
                }

                if (addon->AtkUnitBase.IsVisible && addon->AtkUnitBase.UldManager.NodeListCount >= 49)
                {
                    try
                    {
                        if (addon->AtkUnitBase.UldManager.NodeList[88]->IsVisible)
                        {
                            RecipeID = 0;
                            return;
                        }

                        if (addon->SelectedRecipeName is null)
                            return;

                        var selectedRecipe = Operations.GetSelectedRecipeEntry();
                        if (selectedRecipe == null)
                        {
                            RecipeID = 0;
                            return;
                        }

                        if (addon->AtkUnitBase.UldManager.NodeList[49]->IsVisible)
                        {
                            RecipeID = selectedRecipe->RecipeId;
                        }
                        Array.Clear(SetIngredients);

                        for (int i = 0; i <= 5; i++)
                        {
                            try
                            {
                                var node = addon->AtkUnitBase.UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                                if (node->Component->UldManager.NodeListCount < 16)
                                    return;

                                if (node is null || !node->AtkResNode.IsVisible)
                                {
                                    break;
                                }

                                var hqSetButton = node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode();
                                var nqSetButton = node->Component->UldManager.NodeList[9]->GetAsAtkComponentNode();

                                var hqSetText = hqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;
                                var nqSetText = nqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;

                                int hqSet = Convert.ToInt32(hqSetText.ToString().GetNumbers());
                                int nqSet = Convert.ToInt32(nqSetText.ToString().GetNumbers());

                                EnduranceIngredients ingredients = new EnduranceIngredients()
                                {
                                    IngredientSlot = i,
                                    HQSet = hqSet,
                                    NQSet = nqSet,
                                };

                                SetIngredients[i] = ingredients;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex, "Setting Recipe ID");
                        RecipeID = 0;
                    }
                }
            }
        }

        internal static void Init()
        {
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
            Svc.Toasts.ErrorToast += CheckNonMaxQuantityModeFinished;
        }

        private static bool enable = false;
        private static void CheckNonMaxQuantityModeFinished(ref SeString message, ref bool isHandled)
        {
            if (!P.Config.MaxQuantityMode && Enable &&
                (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1147).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1146).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1145).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1144).Text.ExtractText()))
            {
                if (P.Config.PlaySoundFinishEndurance)
                    SoundPlayer.PlaySound();

                ToggleEndurance(false);
            }
        }

        public static void Update()
        {
            if (!Enable) return;
            var needToRepair = P.Config.Repair && RepairManager.GetMinEquippedPercent() < P.Config.RepairPercent && (RepairManager.CanRepairAny() || RepairManager.RepairNPCNearby(out _));
            if ((Crafting.CurState == Crafting.State.QuickCraft && Crafting.QuickSynthCompleted) || needToRepair || IPC.IPC.StopCraftingRequest ||
                (P.Config.Materia && Spiritbond.IsSpiritbondReadyAny() && CharacterInfo.MateriaExtractionUnlocked()))
            {
                Operations.CloseQuickSynthWindow();
            }

            if (!P.TM.IsBusy && Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
            {
                var isCrafting = Svc.Condition[ConditionFlag.Crafting];
                var preparing = Svc.Condition[ConditionFlag.PreparingToCraft];
                var recipe = LuminaSheets.RecipeSheet[RecipeID];
                if (PreCrafting.Tasks.Count > 0)
                {
                    return;
                }

                if (P.Config.CraftingX && P.Config.CraftX == 0 || PreCrafting.GetNumberCraftable(recipe) == 0)
                {
                    Enable = false;
                    P.Config.CraftingX = false;
                    DuoLog.Information("Craft X has completed.");
                    if (P.Config.PlaySoundFinishEndurance)
                        SoundPlayer.PlaySound();

                    return;
                }

                if (RecipeID == 0)
                {
                    Svc.Toasts.ShowError("没有设置重复模式的配方。停止重复模式。");
                    DuoLog.Error("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    Enable = false;
                    return;
                }

                if ((Job)LuminaSheets.RecipeSheet[RecipeID].CraftType.Row + 8 != CharacterInfo.JobID)
                {
                    PreCrafting.equipGearsetLoops = 0;
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskClassChange((Job)LuminaSheets.RecipeSheet[RecipeID].CraftType.Row + 8), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                bool needEquipItem = recipe.ItemRequired.Row > 0 && !PreCrafting.IsItemEquipped(recipe.ItemRequired.Row);
                if (needEquipItem)
                {
                    PreCrafting.equipAttemptLoops = 0;
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskEquipItem(recipe.ItemRequired.Row), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                if (!Spiritbond.ExtractMateriaTask(P.Config.Materia))
                {
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                if (P.Config.Repair && !RepairManager.ProcessRepair())
                {
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                var config = P.Config.RecipeConfigs.GetValueOrDefault(RecipeID) ?? new();
                PreCrafting.CraftType type = P.Config.QuickSynthMode && recipe.CanQuickSynth && P.ri.HasRecipeCrafted(recipe.RowId) ? PreCrafting.CraftType.Quick : PreCrafting.CraftType.Normal;
                bool needConsumables = (type == PreCrafting.CraftType.Normal || (type == PreCrafting.CraftType.Quick && P.Config.UseConsumablesQuickSynth)) && (!ConsumableChecker.IsFooded(config) || !ConsumableChecker.IsPotted(config) || !ConsumableChecker.IsManualled(config) || !ConsumableChecker.IsSquadronManualled(config));
                bool hasConsumables = config != default ? ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && ConsumableChecker.HasItem(config.RequiredManual, false) && ConsumableChecker.HasItem(config.RequiredSquadronManual, false) : true;

                if (P.Config.AbortIfNoFoodPot && needConsumables && !hasConsumables)
                {
                    DuoLog.Error($"无法制作 {recipe.ItemResult.Value?.Name}: 所需素材未满足");
                    Enable = false;
                    return;
                }

                if (needConsumables)
                {
                    if (!P.TM.IsBusy && !PreCrafting.Occupied())
                    {
                        P.TM.Enqueue(() => PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200))));
                        P.TM.Enqueue(() => PreCrafting.Tasks.Add((() => PreCrafting.TaskUseConsumables(config, type), TimeSpan.FromMilliseconds(200))));
                        P.TM.DelayNext(100);
                    }
                    return;
                }

                if (Crafting.CurState is Crafting.State.IdleBetween or Crafting.State.IdleNormal && !PreCrafting.Occupied())
                {
                    if (!P.TM.IsBusy)
                    {
                        PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe), TimeSpan.FromMilliseconds(200)));

                        if (!CraftingListFunctions.RecipeWindowOpen()) return;

                        if (type == PreCrafting.CraftType.Quick)
                        {
                            P.TM.Enqueue(() => Operations.QuickSynthItem(P.Config.CraftingX ? P.Config.CraftX : 99), "EnduranceQSStart");
                            P.TM.Enqueue(() => Crafting.CurState is Crafting.State.InProgress or Crafting.State.QuickCraft, 2500, "EnduranceQSWaitStart");
                        }
                        else if (type == PreCrafting.CraftType.Normal)
                        {
                            P.TM.DelayNext(200);
                            if (P.Config.MaxQuantityMode)
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(), "EnduranceSetIngredientsNonLayout");
                            else
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(SetIngredients), "EnduranceSetIngredientsLayout");

                            P.TM.Enqueue(() => Operations.RepeatActualCraft(), "EnduranceNormalStart");
                            P.TM.Enqueue(() => Crafting.CurState is Crafting.State.InProgress or Crafting.State.QuickCraft, 2500, "EnduranceNormalWaitStart");
                        }
                    }

                }
            }
        }

        private static void Toasts_ErrorToast(ref SeString message, ref bool isHandled)
        {
            if (Enable || CraftingListUI.Processing)
            {
                foreach (uint errorId in UnableToCraftErrors)
                {
                    if (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == errorId).Text.ExtractText())
                    {
                        Svc.Toasts.ShowError($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to unable to craft error.");
                        DuoLog.Error($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to unable to craft error.");
                        if (enable)
                            Enable = false;
                        if (CraftingListUI.Processing)
                            CraftingListFunctions.Paused = true;
                        PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));

                        P.TM.Abort();
                        CraftingListFunctions.CLTM.Abort();
                    }
                }

                Errors.PushBack(Environment.TickCount64);
                Svc.Log.Warning($"Error Warnings [{Errors.Count(x => x > Environment.TickCount64 - 10 * 1000)}]: {message}");
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 10 * 1000))
                {
                    Svc.Toasts.ShowError($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to too many errors in succession.");
                    DuoLog.Error($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to too many errors in succession.");
                    if (enable)
                        Enable = false;
                    if (CraftingListUI.Processing)
                        CraftingListFunctions.Paused = true;
                    Errors.Clear();
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));

                    P.TM.Abort();
                    CraftingListFunctions.CLTM.Abort();
                }
            }
        }
    }
}
