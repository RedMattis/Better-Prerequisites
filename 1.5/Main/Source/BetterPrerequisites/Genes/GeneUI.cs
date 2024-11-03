﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace BigAndSmall
{
    [HarmonyPatch(typeof(GeneUIUtility), "DrawGeneBasics")]
    public static class DrawGene
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            for (int idx = 0; idx < codes.Count; idx++)
            {
                CodeInstruction code = codes[idx];
                bool runBackgroundPatch = idx > 3 && idx < codes.Count - 2 &&
                    //(codes[idx - 1].opcode == OpCodes.Ldloc_2) &&
                    codes[idx].IsLdloc() && codes[idx].operand is LocalBuilder lb && lb.LocalIndex == 4 &&
                    //(codes[idx].IsLdloc() && codes[idx].LocalIndex() == 4) &&
                    (codes[idx + 1].opcode == OpCodes.Callvirt && codes[idx + 1].OperandIs(typeof(CachedTexture).GetMethod("get_Texture")));

                if (runBackgroundPatch)
                {
                    List<CodeInstruction> newInstructions =
                    [
                        new CodeInstruction(OpCodes.Ldarg_0), // Load Gene.
                        new CodeInstruction(OpCodes.Ldarg_2), // Load GeneType.


                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DrawGene), nameof(GetGeneBackground))),
                    ];
                    codes.InsertRange(idx+1, newInstructions);
                    break;
                }
            }
            return codes;

        }
        public static CachedTexture GetGeneBackground(CachedTexture previous, GeneDef gene, GeneType geneType)
        {
            if (GeneDefPatcher.customGeneBackgrounds.TryGetValue(gene, out GeneUIDrawData gDrawData))
            {
                return gDrawData.GetCachedTexture(geneType, previous, DrawGeneSection.pCache);
            }
            return previous;

        }
    }

    [HarmonyPatch]
    public static class DrawGeneSection
    {
        public static BSCache pCache = null;
        [HarmonyPatch(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGenesInfo))]
        [HarmonyPrefix]
        public static void DrawGenesInfoPrefix(Rect rect, Thing target, float initialHeight, ref Vector2 size, ref Vector2 scrollPosition, GeneSet pregnancyGenes = null)
        {
            if (target is Pawn p && HumanoidPawnScaler.GetCacheUltraSpeed(p) is BSCache cache)
            {
                pCache = cache;
            }
        }

        [HarmonyPatch(typeof(GeneUIUtility), "DrawSection")]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            for (int idx = 0; idx < codes.Count; idx++)
            {
                if (idx > 0 && codes[idx].opcode == OpCodes.Ldstr && codes[idx].operand is string headerStr && (headerStr == "Endogenes" || headerStr == "Xenogenes"))
                {
                    var newLabel = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DrawGeneSection), nameof(GetGeneSectionLabel)));
                    codes.InsertRange(idx+1, [newLabel]);
                }

            }
            return codes;
        }

        public static string GetGeneSectionLabel(string label)
        {
            if (pCache != null)
            {
                bool endo = label == "Endogenes";
                if (pCache.isMechanical)
                {
                    return endo ? "BS_MechEndo" : "BS_MechExo";
                }
            }
            return label;
        }
    }

    [HarmonyPatch]
    public static class CapacityPatches
    {
        [HarmonyPatch(typeof(PawnCapacityDef), nameof(PawnCapacityDef.GetLabelFor), [typeof(Pawn)])]
        [HarmonyPostfix]
        public static void GetLabelForPostfix(ref string __result, PawnCapacityDef __instance, Pawn pawn)
        {
            if (HumanoidPawnScaler.GetCacheUltraSpeed(pawn) is BSCache cache)
            {
                if (cache.isMechanical)
                {
                    __result = !__instance.labelMechanoids.NullOrEmpty() ? __instance.labelMechanoids : __result;
                }
            }
        }
    }
}