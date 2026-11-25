using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    /// <summary>丹藥效果共用：品質倍率、丹毒、是否限修士。</summary>
    public abstract class IngestionOutcomeDoer_Elixir : IngestionOutcomeDoer
    {
        public bool cultivatorsOnly = true;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            int quality = ingested.TryGetQuality(out QualityCategory qc) ? (int)qc : (int)QualityCategory.Normal;
            Hediff_Cultivation c = pawn.GetCultivation();
            if (cultivatorsOnly && c == null)
                Notify(pawn, "WD_ElixirWastedMortal".Translate(pawn.LabelShort, ingested.def.label));
            else
                Apply(pawn, c, ingested, t.elixirEffectByQuality.Evaluate(quality) * ingestedCount);

            float tox = (ingested.def.GetModExtension<ElixirExtension>()?.toxicity ?? 0f) * t.elixirToxicityByQuality.Evaluate(quality) * ingestedCount;
            if (tox > 0f && !pawn.Dead)
                HealthUtility.AdjustSeverity(pawn, WD_DefOf.WD_PillToxicity, tox);
        }

        protected abstract void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor);

        protected static void Notify(Pawn pawn, string text)
        {
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
                Messages.Message(text, pawn, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    /// <summary>聚氣丹：限時 hediff，時長固定，嚴重度 = 品質倍率（由 multiplyStatChangesBySeverity 放大加成）。</summary>
    public class IngestionOutcomeDoer_ElixirHediff : IngestionOutcomeDoer_Elixir
    {
        public HediffDef hediff;
        public int durationTicks = 60000;

        protected override void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
            // 藥力較弱的一顆不會蓋過、也不會延長較強的藥力。
            if (h != null && h.Severity > factor + 0.001f)
            {
                Notify(pawn, "WD_ElixirWeaker".Translate(pawn.LabelShort, elixir.def.label));
                return;
            }
            if (h == null)
                h = pawn.health.AddHediff(hediff);
            h.Severity = factor;
            HediffComp_Disappears d = h.TryGetComp<HediffComp_Disappears>();
            if (d != null)
                d.ticksToDisappear = durationTicks;
        }
    }

    /// <summary>回元丹：回復真元上限的比例。</summary>
    public class IngestionOutcomeDoer_ElixirZhenyuan : IngestionOutcomeDoer_Elixir
    {
        public float fraction = 0.4f;

        protected override void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor)
        {
            Need n = pawn.needs?.TryGetNeed(WD_DefOf.WD_Zhenyuan);
            if (n != null)
                n.CurLevel += n.MaxLevel * fraction * factor;
        }
    }

    /// <summary>療傷丹：癒合新傷，凡人也有效。</summary>
    public class IngestionOutcomeDoer_ElixirHeal : IngestionOutcomeDoer_Elixir
    {
        public float healAmount = 20f;

        protected override void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor)
        {
            SpellUtility.HealInjuries(pawn, healAmount * factor);
        }
    }

    /// <summary>築基丹：下一次突破的成功率加成（限定被突破的境界）。</summary>
    public class IngestionOutcomeDoer_ElixirBreakthrough : IngestionOutcomeDoer_Elixir
    {
        public HediffDef hediff;
        public float bonus = 0.2f;

        protected override void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor)
        {
            Hediff old = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
            float value = bonus * factor;
            // 不疊加，取較強的一顆。
            if (old != null)
            {
                value = Mathf.Max(value, old.Severity);
                pawn.health.RemoveHediff(old);
            }
            Hediff h = HediffMaker.MakeHediff(hediff, pawn);
            h.Severity = value;
            pawn.health.AddHediff(h);
        }
    }

    /// <summary>延壽丹：增加剩餘壽元，同種丹藥每服一顆效果遞減。</summary>
    public class IngestionOutcomeDoer_ElixirLongevity : IngestionOutcomeDoer_Elixir
    {
        public float years = 15f;

        protected override void Apply(Pawn pawn, Hediff_Cultivation c, Thing elixir, float factor)
        {
            float gain = years * factor * Mathf.Pow(WenDaoTuningDef.Get.longevityRepeatFactor, c.longevityTaken);
            c.longevityTaken++;
            c.GainLifespan(gain);
            Notify(pawn, "WD_LifespanGained".Translate(pawn.LabelShort, gain.ToString("0.#")));
        }
    }

    public class BreakthroughAidExtension : DefModExtension
    {
        public CultivationRealmDef realm;
    }

    /// <summary>突破輔助：嚴重度即成功率加成，突破判定後消耗。</summary>
    public class Hediff_BreakthroughAid : HediffWithComps
    {
        public override string LabelInBrackets
        {
            get
            {
                string s = "+" + Severity.ToStringPercent();
                string b = base.LabelInBrackets;
                return b.NullOrEmpty() ? s : s + ", " + b;
            }
        }

        public bool AppliesTo(CultivationRealmDef realm) => def.GetModExtension<BreakthroughAidExtension>()?.realm == realm;
    }
}
