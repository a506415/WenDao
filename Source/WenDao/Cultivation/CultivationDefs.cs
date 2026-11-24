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

        public static WenDaoTuningDef Get => WD_DefOf.WD_Tuning;
    }

    /// <summary>大境界。小階之間自動推進；大境界之間需要突破。</summary>
    public class CultivationRealmDef : Def
    {
        public int order;
        public float zhenyuanMax = 10f;
        [MustTranslate]
        public List<string> stageLabels = new List<string>();
        public List<float> stageCosts = new List<float>();

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
