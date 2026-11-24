using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>修煉速度 stat：只對已測出、可修煉的靈根顯示，避免洩漏未測靈根。</summary>
    public class StatWorker_CultivationSpeed : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req))
                return false;
            Hediff_SpiritRoot root = (req.Thing as Pawn)?.GetSpiritRoot();
            return root != null && root.revealed && root.CanCultivate;
        }
    }

    /// <summary>R：靈根倍率。無靈根為 0。</summary>
    public class StatPart_SpiritRoot : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                Hediff_SpiritRoot root = pawn.GetSpiritRoot();
                val *= root != null && root.CanCultivate ? root.cultivationSpeed : 0f;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Hediff_SpiritRoot root = (req.Thing as Pawn)?.GetSpiritRoot();
            if (root == null)
                return null;
            return "WD_StatPartSpiritRoot".Translate(root.LabelCap,
                (root.CanCultivate ? root.cultivationSpeed : 0f).ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
        }
    }

    /// <summary>M：心境係數，由心情換算。</summary>
    public class StatPart_MindState : StatPart
    {
        public SimpleCurve curve;

        private float Factor(Pawn pawn)
        {
            if (curve == null || pawn?.needs?.mood == null)
                return 1f;
            return curve.Evaluate(pawn.needs.mood.CurLevel);
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
                val *= Factor(pawn);
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || pawn.needs?.mood == null)
                return null;
            return "WD_StatPartMindState".Translate(Factor(pawn).ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
        }
    }
}
