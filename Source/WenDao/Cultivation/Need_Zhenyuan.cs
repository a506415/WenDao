using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 真元：法術與護體罡氣的能量池。上限隨境界，從環境靈氣自然回復，打坐時更快。
    /// 由 WD_Cultivation hediff 的 causesNeed 帶出，只有修士才有。
    /// </summary>
    public class Need_Zhenyuan : Need
    {
        private const int IntervalTicks = 150;

        public Need_Zhenyuan(Pawn pawn) : base(pawn)
        {
        }

        public override float MaxLevel => pawn.GetCultivation()?.realm?.zhenyuanMax ?? 1f;

        public override int GUIChangeArrow => CurLevel < MaxLevel ? 1 : 0;

        public override void SetInitialLevel()
        {
            CurLevel = 0f;
        }

        public override void NeedInterval()
        {
            if (IsFrozen || pawn.Map == null)
                return;
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            float qi = MapComponent_SpiritQi.For(pawn.Map)?.QiAt(pawn.Position) ?? 0f;
            float factor = pawn.CurJobDef == WD_DefOf.WD_Cultivate ? t.zhenyuanCultivatingFactor : 1f;
            CurLevel += MaxLevel * t.zhenyuanRegenPerDay * IntervalTicks / GenDate.TicksPerDay * qi * factor;
        }

        public override string GetTipString()
        {
            return (LabelCap + ": " + CurLevel.ToString("0.#") + " / " + MaxLevel.ToString("0")).Colorize(ColoredText.TipSectionTitleColor)
                   + "\n" + def.description;
        }
    }
}
