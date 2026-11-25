using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 非修士在工作分頁不能指派煉丹。原版只支援以 WorkTags 停用工作，
    /// 無法針對單一自訂 WorkType，故 postfix GetDisabledWorkTypes 與其原因。
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Patch_Pawn_GetDisabledWorkTypes
    {
        public static bool Forbidden(Pawn pawn) => pawn.RaceProps.Humanlike && pawn.GetCultivation() == null;

        // __result 是原版的快取清單；修為增減時由 Hediff_Cultivation 通知清快取。
        public static void Postfix(Pawn __instance, bool permanentOnly, List<WorkTypeDef> __result)
        {
            if (permanentOnly || Scribe.mode != LoadSaveMode.Inactive || !Forbidden(__instance))
                return;
            if (!__result.Contains(WD_DefOf.WD_Alchemy))
                __result.Add(WD_DefOf.WD_Alchemy);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetReasonsForDisabledWorkType))]
    public static class Patch_Pawn_GetReasonsForDisabledWorkType
    {
        public static void Postfix(Pawn __instance, WorkTypeDef workType, List<string> __result)
        {
            if (workType != WD_DefOf.WD_Alchemy || !Patch_Pawn_GetDisabledWorkTypes.Forbidden(__instance))
                return;
            string reason = "WD_OnlyCultivators".Translate();
            if (!__result.Contains(reason))
                __result.Add(reason);
        }
    }
}
