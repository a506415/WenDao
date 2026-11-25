using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>靈根屬性：金木水火土，異屬性為風雷冰。</summary>
    public class SpiritElementDef : Def
    {
        public int order;
        public bool mutant;
    }

    /// <summary>靈根品階。數值全在 XML，方便調整。</summary>
    public class SpiritRootGradeDef : Def
    {
        public int order;
        public float commonality = 1f;
        public int elementCount;
        public bool mutant;
        public bool canCultivate = true;
        /// <summary>大境界突破的基礎成功率倍率；丹藥與護法加成不受影響。</summary>
        public float breakthroughFactor = 1f;
        public FloatRange cultivationSpeed = FloatRange.Zero;

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (canCultivate && elementCount <= 0)
                yield return "canCultivate grade must have elementCount > 0";
            if (canCultivate && cultivationSpeed.max <= 0f)
                yield return "canCultivate grade must have cultivationSpeed > 0";
        }
    }

    [DefOf]
    public static class WD_DefOf
    {
        public static HediffDef WD_SpiritRoot;
        public static JobDef WD_TestSpiritRoot;

        public static WenDaoTuningDef WD_Tuning;
        public static HediffDef WD_Cultivation;
        public static NeedDef WD_Zhenyuan;
        public static StatDef WD_CultivationSpeed;
        public static JobDef WD_Cultivate;
        public static ThingDef WD_MeditationCushion;

        public static HediffDef WD_InSeclusion;
        public static ThoughtDef WD_BreakthroughSucceeded;
        public static ThoughtDef WD_BreakthroughFailed;
        public static JobDef WD_Seclusion;
        public static JobDef WD_GuardSeclusion;
        public static GameConditionDef WD_HeavenlyTribulation;
        public static JobDef WD_EndureTribulation;
        public static HediffDef WD_BloodBurning;
        public static HediffDef WD_PillToxicity;
        public static WorkTypeDef WD_Alchemy;
        public static DamageDef WD_TribulationLightning;

        static WD_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WD_DefOf));
        }
    }
}
