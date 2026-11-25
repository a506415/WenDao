using System.Text;
using RimWorld;
using Verse;

namespace WenDao
{
    public class SpiritPlantExtension : DefModExtension
    {
        /// <summary>低於此靈氣濃度便不生長。</summary>
        public float minQi;
    }

    /// <summary>靈植：生長速度乘以該格靈氣濃度。</summary>
    public class Plant_Spirit : Plant
    {
        private float QiFactor
        {
            get
            {
                if (!Spawned)
                    return 1f;
                float qi = MapComponent_SpiritQi.For(Map)?.QiAt(Position) ?? 0f;
                float min = def.GetModExtension<SpiritPlantExtension>()?.minQi ?? 0f;
                return qi < min ? 0f : qi;
            }
        }

        public override float GrowthRate => base.GrowthRate * QiFactor;

        public override string GrowthRateCalcDesc
        {
            get
            {
                var sb = new StringBuilder(base.GrowthRateCalcDesc);
                sb.AppendInNewLine("WD_PlantQi".Translate(QiFactor.ToStringPercent()));
                return sb.ToString();
            }
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            if (!Spawned)
                return s;
            float qi = MapComponent_SpiritQi.For(Map)?.QiAt(Position) ?? 0f;
            string line = QiFactor > 0f
                ? "WD_PlantQiInspect".Translate(qi.ToString("0.00"), QiFactor.ToStringPercent())
                : "WD_PlantNeedsQi".Translate(qi.ToString("0.00"), (def.GetModExtension<SpiritPlantExtension>()?.minQi ?? 0f).ToString("0.00"));
            return s.NullOrEmpty() ? line : s + "\n" + line;
        }
    }
}
