using System.Collections.Generic;
using System.Linq;
using Verse;

namespace WenDao
{
    /// <summary>全 mod 數值總表，唯一實例 WD_Tuning。</summary>
    public class WenDaoTuningDef : Def
    {
        public float baseGainPerHour = 1f;
        public float groundEfficiency = 0.6f;
        public int sessionTicks = 5000;

        public FloatRange mapBaseQi = new FloatRange(0.7f, 1.3f);
        public float qiNoiseAmplitude = 0.35f;
        public float qiNoiseFrequency = 0.04f;
        public float minQi = 0.05f;
        public float qiDrainPerHour = 0.01f;
        public float qiDrainRadius = 2.9f;
        public float qiRegenFractionPerHour = 0.1f;

        public float zhenyuanRegenPerDay = 1f;
        public float zhenyuanCultivatingFactor = 3f;

        public float forcedChanceMin = 0.3f;
        public float forcedLifespanCapFraction = 0.1f;
        public float guardBonus = 0.15f;
        public float guardRadius = 3.9f;
        public FloatRange breakthroughChanceRange = new FloatRange(0.02f, 0.95f);

        public int tribulationWarningTicks = 2500;
        public int tribulationIntervalTicks = 600;
        public float tribulationSplashRadius = 2.9f;

        public float lifespanWarningYears = 15f;

        public float spellElementBonus = 0.5f;
        public float bloodBurningSpellPower = 1.5f;
        public int autoDefendWindowTicks = 120;

        public float alchemySkillPerRealm = 3f;
        public float furnaceExplosionRadius = 1.9f;
        public int furnaceExplosionDamage = 8;
        public float longevityRepeatFactor = 0.5f;
        public SimpleCurve elixirEffectByQuality;
        public SimpleCurve elixirToxicityByQuality;

        public static WenDaoTuningDef Get => WD_DefOf.WD_Tuning;
    }

    public class HediffSeverity
    {
        public HediffDef hediff;
        public float severity = 1f;
    }

    /// <summary>大境界。小階之間自動推進；大境界之間需要突破。</summary>
    public class CultivationRealmDef : Def
    {
        public int order;
        public float zhenyuanMax = 10f;
        // 整份清單翻譯（<li> 形式）需要 TranslationCanChangeCount 才會被套用。
        [MustTranslate, TranslationCanChangeCount]
        public List<string> stageLabels = new List<string>();
        public List<float> stageCosts = new List<float>();

        /// <summary>此境界的壽元上限（年）。</summary>
        public float lifespanYears = 100f;

        /// <summary>法術威力的境界係數。</summary>
        public float spellPower = 1f;

        // ---- 突破出此境界 ----
        public float insightRequired = 50f;
        public float seclusionDays = 1f;
        public float breakthroughChance = 0.5f;
        public FloatRange failureLifespanLoss = FloatRange.Zero;
        public List<HediffSeverity> failureHediffs = new List<HediffSeverity>();

        // ---- 突破進此境界 ----
        public int tribulationStrikes;
        public FloatRange tribulationDamage = FloatRange.Zero;

        public bool HasTribulation => tribulationStrikes > 0;

        public int SeclusionTicks => UnityEngine.Mathf.RoundToInt(seclusionDays * RimWorld.GenDate.TicksPerDay);

        public int StageCount => stageCosts.Count;

        public string StageLabel(int stage)
        {
            return stage >= 0 && stage < stageLabels.Count ? stageLabels[stage] : (stage + 1).ToString();
        }

        public static IEnumerable<CultivationRealmDef> AllInOrder =>
            DefDatabase<CultivationRealmDef>.AllDefs.OrderBy(r => r.order);

        public static CultivationRealmDef First => AllInOrder.First();

        public CultivationRealmDef Next => AllInOrder.FirstOrDefault(r => r.order > order);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (stageCosts.NullOrEmpty())
                yield return "no stageCosts";
            if (stageLabels.Count != stageCosts.Count)
                yield return "stageLabels count must match stageCosts count";
            if (stageCosts.Any(c => c <= 0f))
                yield return "stageCosts must be positive";
        }
    }
}
