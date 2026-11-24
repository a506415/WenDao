using HarmonyLib;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 小人生成時擲靈根。原版沒有可用 XML 掛的「人形小人生成完成」鉤子，
    /// 所以只能 postfix PawnGenerator.GeneratePawn(PawnGenerationRequest)；
    /// 其他 GeneratePawn 多載最後都會走到這一個。
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        public static void Postfix(Pawn __result)
        {
            SpiritRootUtility.TryGiveOnGeneration(__result);
        }
    }
}
