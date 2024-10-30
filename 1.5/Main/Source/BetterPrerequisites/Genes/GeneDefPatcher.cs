﻿using BetterPrerequisites;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BigAndSmall
{
    /// <summary>
    /// Patch defs in other mods. And things related to defs.
    /// </summary>
    public static class ModDefPatcher
    {
        public static void PatchDefs()
        {
            var allGeneDefs = DefDatabase<GeneDef>.AllDefsListForReading;

            var allGenesWithGenExt = allGeneDefs.Where(x => x.modExtensions != null && x.modExtensions.Any(y => y is PawnExtension));

            // If vanilla expanded insectoids 2 is loaded
            if (ModsConfig.IsActive("OskarPotocki.VFE.Insectoid2"))
            {
                try
                {
                    // Find the "static HashSet<string> allowedGenes" in VFEInsectoids.PathFinder_FindPath_Patch.
                    var allowedGenesFInfo = AccessTools.Field(AccessTools.TypeByName("VFEInsectoids.PathFinder_FindPath_Patch"), "allowedGenes");
                    if (allowedGenesFInfo != null)
                    {
                        var allowedGenes = allowedGenesFInfo.GetValue(null) as HashSet<string>;
                        var genesToAdd = allGenesWithGenExt.Where(x => x.modExtensions.Any(y => y is PawnExtension geneExt && geneExt.canWalkOnCreep));
                        allowedGenes.AddRange(genesToAdd.Select(x => x.defName));

                        // Print the hashset as a comma seperated string.
                        Log.Message($"[Big and Small]: Patched VFEInsectoids 2 with {genesToAdd.Count()} genes.\nIt now contains the following genes: {string.Join(", ", allowedGenes)}");
                    }
                    else
                    {
                        Log.Message("Failed to patch VFEInsectoids 2's Creep with additional genes.");
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to patch VFEInsectoids 2's Creep with additional genes. {e.Message}");
                }
            }
        }
    }

    public static class XenotypeDefPatcher
    {
        public static void PatchDefs()
        {
            foreach(var xeno in DefDatabase<XenotypeDef>.AllDefs
                .Where(x=>x.modExtensions != null && x.modExtensions
                .FirstOrDefault(y=>y is XenotypeExtension) != null))
            {
                var modExt = xeno.modExtensions.First(x=>x is XenotypeExtension) as XenotypeExtension;
                if (modExt.genePickPriority != null)
                {
                    foreach (var genePickerList in modExt.genePickPriority)
                    {
                        foreach (var geneName in genePickerList)
                        {
                            GeneDef geneDef = DefDatabase<GeneDef>.GetNamed(geneName, errorOnFail:false);
                            if (geneDef != null)
                            {
                                xeno.genes.Add(geneDef);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    public static class GeneDefPatcher
    {
        private static List<GeneAutoPatcherSettings> patchSettings;
        public static void PatchDefs()
        {
            patchSettings = DefDatabase<GeneAutoPatcherSettings>.AllDefs.ToList();
            patchSettings.SortBy(x => x.priority);
            var activeMods = LoadedModManager.RunningMods.ToList();
            patchSettings.ForEach(x => x.Setup(activeMods));

            var geneDefs = DefDatabase<GeneDef>.AllDefsListForReading;
            foreach (var geneDef in geneDefs)
            {
                foreach (var patchSetting in patchSettings)
                {
                    if (ShouldPatchWithData(geneDef, patchSetting))
                    {
                        geneDef.modExtensions ??= new List<DefModExtension>();
                        AddGeneBackgrounds(geneDef, patchSetting);
                    }
                }

                if (geneDef.modExtensions != null)
                {
                    foreach (var modExt in geneDef.modExtensions)
                    {
                        if (modExt is PawnExtension geneExt)
                        {
                            if (geneExt.hideInGenePicker)
                            {
                                Dialog_CreateXenotypePatches.hiddenGenes.Add(geneDef);
                            }
                        }
                    }
                }
                // Replace this with a "hide categories" function later.
                if (geneDef?.displayCategory?.defName == "BS_Metamorph")
                {
                    Dialog_CreateXenotypePatches.hiddenGenes.Add(geneDef);
                }
            }
        }

        private static bool ShouldPatchWithData(GeneDef def, GeneAutoPatcherSettings patchData)
        {
            if (patchData.targetMods != null && !patchData.targetMods.Contains(def.modContentPack))
            {
                return false;
            }
            if (patchData.targetGeneType != null && !def.GetType().ToString().EndsWith(patchData.targetGeneType))
            {
                return false;
            }
            if (patchData.geneWildcard != null && !def.defName.Contains(patchData.geneWildcard))
            {
                return false;
            }
            if (patchData.targetModExtension != null && (def.modExtensions?.Any(x => x.GetType().ToString().EndsWith(patchData.targetModExtension)) != true))
            {
                return false;
            }
            return true;
        }

        private static void AddGeneBackgrounds(GeneDef geneDef, GeneAutoPatcherSettings patchData)
        {
            if (VFEGeneExtensionWrapper.IsVFEActive == false)
            {
                return;
            }
            Type vfegType = VFEGeneExtensionWrapper.GetExtensionType();

            // Check if the gene has the VFEGeneExtension already.
            DefModExtension existingInstace = geneDef.modExtensions.FirstOrDefault(x => x.GetType() == vfegType);

            var geneExt = new VFEGeneExtensionWrapper(existingInstace);  // If the extension is null it will create a new one.
            if (geneExt != null)
            {
                if (patchData.backgroundPathEndogenes != null && geneExt.BackgroundPathEndogenes.NullOrEmpty())
                {
                    geneExt.BackgroundPathEndogenes = patchData.backgroundPathEndogenes;
                }
                if (patchData.backgroundPathXenogenes != null && geneExt.BackgroundPathXenogenes.NullOrEmpty())
                {
                    geneExt.BackgroundPathXenogenes = patchData.backgroundPathXenogenes;
                }
                if (patchData.backgroundPathArchite != null && geneExt.BackgroundPathArchite.NullOrEmpty())
                {
                    geneExt.BackgroundPathArchite = patchData.backgroundPathArchite;
                }
                if (existingInstace == null)
                    geneDef.modExtensions.Add(geneExt.ext);
            }
        }
    }

    public class GeneAutoPatcherSettings : Def
    {
        public int priority = 0; // Lower means it will be patched first.
        public string targetModID = null; // Uses .StartsWith for comparisons
        public string targetGeneType = null; // Uses .EndsWith for comparisons to deal with namespaces.
        public string geneWildcard = null;
        public string targetModExtension = null;
        public string backgroundPathEndogenes = null;
        public string backgroundPathXenogenes = null;
        public string backgroundPathArchite = null;

        [Unsaved(true)]
        public List<ModContentPack> targetMods = null;

        public void Setup(List<ModContentPack> activeMods)
        {
            if (targetModID != null)
            {
                targetModID = targetModID.ToLower();
                targetMods = activeMods.Where(x => x.PackageId.StartsWith(targetModID)).ToList();
            }
        }
    }
}
