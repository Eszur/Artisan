using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace Artisan.CraftingLists
{
    internal static class Teamcraft
    {
        internal static string importListName = "";
        internal static string importListPreCraft = "";
        internal static string importListItems = "";
        internal static bool openImportWindow = false;
        private static bool precraftQS = false;
        private static bool finalitemQS = false;

        internal static void DrawTeamCraftListButtons()
        {
            string labelText = "Teamcraft清单";
            var labelLength = ImGui.CalcTextSize(labelText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionMax().X - labelLength.X) * 0.5f);
            ImGui.TextColored(ImGuiColors.ParsedGreen, labelText);
            if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Download, "导入", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                openImportWindow = true;
            }
            OpenTeamcraftImportWindow();
            if (CraftingListUI.selectedList.ID != 0)
            {
                if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Upload, "导出", new Vector2(ImGui.GetContentRegionAvail().X, 30), true))
                {
                    ExportSelectedListToTC();
                }
            }
        }

        private static void ExportSelectedListToTC()
        {
            string baseUrl = "https://ffxivteamcraft.com/import/";
            string exportItems = "";

            var sublist = CraftingListUI.selectedList.Items.Distinct().Reverse().ToList();
            for (int i = 0; i < sublist.Count; i++)
            {
                if (i >= sublist.Count) break;

                int number = CraftingListUI.selectedList.Items.Count(x => x == sublist[i]);
                var recipe = LuminaSheets.RecipeSheet[sublist[i]];
                var itemID = recipe.ItemResult.Value.RowId;

                Svc.Log.Debug($"{recipe.ItemResult.Value.Name.RawString} {sublist.Count}");
                ExtractRecipes(sublist, recipe);
            }

            foreach (var item in sublist)
            {
                int number = CraftingListUI.selectedList.Items.Count(x => x == item);
                var recipe = LuminaSheets.RecipeSheet[item];
                var itemID = recipe.ItemResult.Value.RowId;

                exportItems += $"{itemID},null,{number};";
            }

            exportItems = exportItems.TrimEnd(';');

            var plainTextBytes = Encoding.UTF8.GetBytes(exportItems);
            string base64 = Convert.ToBase64String(plainTextBytes);

            Svc.Log.Debug($"{baseUrl}{base64}");
            Clipboard.SetText($"{baseUrl}{base64}");
            Notify.Success("链接已复制到剪贴板");
        }

        private static void ExtractRecipes(List<uint> sublist, Recipe recipe)
        {
            foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                var subRec = CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient);
                if (subRec != null)
                {
                    if (sublist.Contains(subRec.RowId))
                    {
                        foreach (var subIng in subRec.UnkData5.Where(x => x.AmountIngredient > 0))
                        {
                            var subSubRec = CraftingListHelpers.GetIngredientRecipe((uint)subIng.ItemIngredient);
                            if (subSubRec != null)
                            {
                                if (sublist.Contains(subSubRec.RowId))
                                {
                                    for (int y = 1; y <= subIng.AmountIngredient; y++)
                                    {
                                        sublist.Remove(subSubRec.RowId);
                                    }
                                }
                            }
                        }

                        for (int y = 1; y <= ing.AmountIngredient; y++)
                        {
                            sublist.Remove(subRec.RowId);
                        }
                    }
                }
            }
        }

        private static void OpenTeamcraftImportWindow()
        {
            if (!openImportWindow) return;


            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.2f, 0.1f, 0.2f, 1f));
            ImGui.SetNextWindowSize(new Vector2(1, 1), ImGuiCond.Appearing);
            if (ImGui.Begin("Teamcraft导入###TCImport", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("清单名称");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("导入清单指南.\r\n\r\n" +
                    "Step 1. 在Teamcraft上打开一个清单，列出您想要制作的物品t.\r\n\r\n" +
                    "Step 2. 找到半成品部分，然后单击“复制为文本”按钮.\r\n\r\n" +
                    "Step 3. 粘贴到此窗口中的“半成品项目”框中.\r\n\r\n" +
                    "Step 4. 重复步骤2和3，但针对“成品项目”部分.\r\n\r\n" +
                    "Step 5. 为您的列表命名，然后单击导入.");
                ImGui.InputText("###ImportListName", ref importListName, 50);
                ImGui.Text("半成品项目");
                ImGui.InputTextMultiline("###PrecraftItems", ref importListPreCraft, 5000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));

                if (!P.Config.DefaultListQuickSynth)
                    ImGui.Checkbox("作为简易制作导入###ImportQSPre", ref precraftQS);
                else
                    ImGui.TextWrapped($@"由于启用了默认设置，这些项目将尝试作为简易制作添加。");
                ImGui.Text("成品项目");
                ImGui.InputTextMultiline("###FinalItems", ref importListItems, 5000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));
                if (!P.Config.DefaultListQuickSynth)
                    ImGui.Checkbox("作为简易制作导入###ImportQSFinal", ref finalitemQS);
                else
                    ImGui.TextWrapped($@"由于启用了默认设置，这些项目将尝试作为简易制作添加。");

                if (ImGui.Button("导入"))
                {
                    CraftingList? importedList = ParseImport(precraftQS, finalitemQS);
                    if (importedList is not null)
                    {
                        if (importedList.Name.IsNullOrEmpty())
                            importedList.Name = importedList.Items.FirstOrDefault().NameOfRecipe();
                        importedList.SetID();
                        importedList.Save();
                        openImportWindow = false;
                        importListName = "";
                        importListPreCraft = "";
                        importListItems = "";

                    }
                    else
                    {
                        Notify.Error("导入的列表中没有项目。请检查您的导入，然后重试。");
                    }

                }
                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    openImportWindow = false;
                    importListName = "";
                    importListPreCraft = "";
                    importListItems = "";
                }
                ImGui.End();
            }
            ImGui.PopStyleColor();
        }

        private static CraftingList? ParseImport(bool precraftQS, bool finalitemQS)
        {
            if (string.IsNullOrEmpty(importListName) && string.IsNullOrEmpty(importListItems) && string.IsNullOrEmpty(importListPreCraft)) return null;
            CraftingList output = new CraftingList();
            output.Name = importListName;
            using (System.IO.StringReader reader = new System.IO.StringReader(importListPreCraft))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    if (parts[0][^1] == 'x')
                    {
                        int numberOfItem = int.Parse(parts[0].Substring(0, parts[0].Length - 1));
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var item = builder.ToString().Trim();
                        Svc.Log.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Row > 0 && x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            for (int i = 1; i <= Math.Ceiling(numberOfItem / (double)recipe.AmountResult); i++)
                            {
                                output.Items.Add(recipe.RowId);
                            }
                            if (precraftQS && recipe.CanQuickSynth)
                                output.ListItemOptions.TryAdd(recipe.RowId, new ListItemOptions() { NQOnly = true });
                        }
                    }

                }
            }
            using (System.IO.StringReader reader = new System.IO.StringReader(importListItems))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    if (parts[0][^1] == 'x')
                    {
                        int numberOfItem = int.Parse(parts[0].Substring(0, parts[0].Length - 1));
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var item = builder.ToString().Trim();
                        if (DebugTab.Debug) Svc.Log.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Row > 0 && x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            for (int i = 1; i <= Math.Ceiling(numberOfItem / (double)recipe.AmountResult); i++)
                            {
                                output.Items.Add(recipe.RowId);
                            }
                            if (finalitemQS && recipe.CanQuickSynth)
                                output.ListItemOptions.TryAdd(recipe.RowId, new ListItemOptions() { NQOnly = true });
                        }
                    }

                }
            }

            if (output.Items.Count == 0) return null;

            return output;
        }
    }
}
