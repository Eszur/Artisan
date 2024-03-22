﻿using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static string Search = string.Empty;
        public static unsafe InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static Dictionary<int, int> SelectedRecipeRawIngredients = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = string.Empty;
        internal static CraftingList selectedList = new();
        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static Dictionary<int, int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static Dictionary<int, int> listMaterialsNew = new();
        public static bool Processing;
        public static uint CurrentProcessedItem;
        private static readonly ListFolders ListsUI = new();
        private static bool GatherBuddy => DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);
        private static bool ItemVendor => DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);

        private static bool MonsterLookup => DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var mlh, false, true);

        internal static void Draw()
        {
            ImGui.TextWrapped($"制作清单是一种极好的方式，可以将不同的物品排队，并让它们逐一制作。使用底部的按钮从Teamcraft导入创建列表，或者单击“+”图标并为您的列表命名。" +
                              $"您也可以右键单击游戏配方菜单中的一个项目，如果未选择，则将其添加到新列表中；如果未选择列表，则将该项目作为第一个项目创建新列表。");

            ImGui.Dummy(new Vector2(0, 14f));
            ImGui.TextWrapped("左键单击列表以打开编辑器。在不打开编辑器的情况下，右键单击列表以将其选中。");

            ImGui.Separator();

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            ImGui.BeginChild("ListsSelector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 200f));
            ListsUI.Draw(ImGui.GetContentRegionAvail().X);
            ImGui.EndChild();

            ImGui.BeginChild("ListButtons", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 95f));
            if (selectedList.ID != 0)
            {
                if (Endurance.Enable || Processing)
                    ImGui.BeginDisabled();

                if (ImGui.Button("开始制作清单", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    StartList();
                }

                if (RetainerInfo.ATools)
                {
                    if (RetainerInfo.TM.IsBusy)
                    {
                        if (ImGui.Button("中止从雇员收取素材", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            RetainerInfo.TM.Abort();
                        }
                    }
                    else
                    {
                        if (ImGui.Button("从雇员处收取素材", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            Task.Run(() => RetainerInfo.RestockFromRetainers(selectedList));
                        }
                    }
                }

                if (Endurance.Enable || Processing)
                    ImGui.EndDisabled();
            }

            if (ImGui.Button("从剪贴板导入清单 (Artisan 导出)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                try
                {
                    var clipboard = Clipboard.GetText();
                    if (clipboard != string.Empty)
                    {
                        if (clipboard.TryParseJson<CraftingList>(out var import))
                        {
                            import.SetID();
                            import.Save(true);
                        }
                        else
                        {
                            Notify.Error("导入字符串无效.");
                        }
                    }
                    else
                    {
                        Notify.Error("剪贴板为空.");
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }


            ImGui.EndChild();
            
            ImGui.BeginChild("TeamCraftSection", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 5f), false);
            Teamcraft.DrawTeamCraftListButtons();
            ImGui.EndChild();
        }

        public static void StartList()
        {
            CraftingListFunctions.Materials = null;
            CraftingListFunctions.CurrentIndex = 0;

            if (P.ws.Windows.FindFirst(x => x.WindowName.Contains(selectedList.ID.ToString(), StringComparison.CurrentCultureIgnoreCase), out var window))
                window.IsOpen = false;


            CraftingListFunctions.ListEndTime = GetListTimer(selectedList);
            Crafting.CraftFinished += UpdateListTimer;
            Processing = true;
            Endurance.Enable = false;
        }

        public static void UpdateListTimer(Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
        {
            Task.Run(() =>
            {
                TimeSpan output = new();
                for (int i = CraftingListFunctions.CurrentIndex; i < selectedList.Items.Count; i++)
                {
                    var item = selectedList.Items[i];
                    var options = selectedList.ListItemOptions.GetValueOrDefault(item);
                    output = output.Add(GetCraftDuration(item, (options?.NQOnly ?? false))).Add(TimeSpan.FromSeconds(1));
                }

                CraftingListFunctions.ListEndTime = output;
            });
        }

        public static TimeSpan GetListTimer(CraftingList selectedList)
        {
            TimeSpan output = new();
            try
            {
                if (selectedList is null) return output;
                foreach (var item in selectedList.Items.Distinct())
                {
                    var count = selectedList.Items.Count(x => x == item);
                    var options = selectedList.ListItemOptions.GetValueOrDefault(item);
                    output = output.Add(GetCraftDuration(item, (options?.NQOnly ?? false)) * count).Add(TimeSpan.FromSeconds(1 * count));
                }

                return output;
            }
            catch (Exception ex)
            {
                return output;
            }
        }

        public static TimeSpan GetCraftDuration(uint recipeId, bool qs)
        {
            if (qs)
                return TimeSpan.FromSeconds(3);

            var recipe = LuminaSheets.RecipeSheet[recipeId];
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId) ?? new();
            var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.Row);
            stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ));
            var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.Row, recipe);
            var solver = CraftingProcessor.GetSolverForRecipe(config, craft).CreateSolver(craft);
            var time = SolverUtils.EstimateCraftTime(solver, craft, 0);

            return time;
        }

        private static void DrawNewListPopup()
        {
            if (ImGui.BeginPopup("新的制作清单"))
            {
                if (keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    keyboardFocus = false;
                }

                if (ImGui.InputText("List Name###listName", ref newListName, 100, ImGuiInputTextFlags.EnterReturnsTrue) && newListName.Any())
                {
                    CraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }



        public static void AddAllSubcrafts(Recipe selectedRecipe, CraftingList selectedList, int amounts = 1, int loops = 1)
        {
            foreach (var subItem in selectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                var subRecipe = CraftingListHelpers.GetIngredientRecipe((uint)subItem.ItemIngredient);
                if (subRecipe != null)
                {
                    AddAllSubcrafts(subRecipe, selectedList, subItem.AmountIngredient * amounts, loops);

                    for (int i = 1; i <= Math.Ceiling(subItem.AmountIngredient / (double)subRecipe.AmountResult * loops * amounts); i++)
                    {
                        if (selectedList.Items.IndexOf(subRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(subRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(subRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, subRecipe.RowId);
                        }
                    }
                }
            }
        }


        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true, CraftingList? selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    CraftingListHelpers.SelectedRecipesCraftable[(uint)ing.ItemIngredient] = LuminaSheets.RecipeSheet!.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "ERROR");
            }
        }


        public static unsafe bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true, bool checkRetainer = false)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);

                    if (!checkRetainer)
                    {
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }
                    else
                    {
                        int retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ + retainerCount))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }

                    invNumberHQ = null;
                    invNumberNQ = null;
                }
                catch
                {

                }

            }

            CraftableItems[recipe] = true;
            return true;
        }

        public static unsafe int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient, false, false, false);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true, false, false);

                // Svc.Log.Debug($"{invNumberNQ + invNumberHQ}");
                if (LuminaSheets.ItemSheet[ingredient].AlwaysCollectable)
                {
                    var inventories = new List<InventoryType>
                                          {
                                              InventoryType.Inventory1,
                                              InventoryType.Inventory2,
                                              InventoryType.Inventory3,
                                              InventoryType.Inventory4,
                                          };

                    foreach (var inv in inventories)
                    {
                        var container = invManager->GetInventoryContainer(inv);
                        for (int i = 0; i < container->Size; i++)
                        {
                            var item = container->GetInventorySlot(i);

                            if (item->ItemID == ingredient)
                                invNumberNQ++;
                        }
                    }
                }

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }




        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }
    }
}
