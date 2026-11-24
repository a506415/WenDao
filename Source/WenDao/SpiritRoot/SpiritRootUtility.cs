using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WenDao
{
    public static class SpiritRootUtility
    {
        public static bool CanHaveSpiritRoot(Pawn pawn)
        {
            return pawn?.RaceProps != null && pawn.RaceProps.Humanlike && pawn.health?.hediffSet != null;
        }

        public static Hediff_SpiritRoot GetSpiritRoot(this Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(WD_DefOf.WD_SpiritRoot) as Hediff_SpiritRoot;
        }

        public static SpiritRootGradeDef RollGrade()
        {
            return DefDatabase<SpiritRootGradeDef>.AllDefsListForReading.RandomElementByWeight(g => g.commonality);
        }

        /// <summary>替尚無靈根的人形小人擲靈根。已有則不動。</summary>
        public static Hediff_SpiritRoot EnsureSpiritRoot(Pawn pawn)
        {
            if (!CanHaveSpiritRoot(pawn))
                return null;
            return pawn.GetSpiritRoot() ?? GiveSpiritRoot(pawn, RollGrade());
        }

        public static Hediff_SpiritRoot GiveSpiritRoot(Pawn pawn, SpiritRootGradeDef grade)
        {
            var hediff = (Hediff_SpiritRoot)HediffMaker.MakeHediff(WD_DefOf.WD_SpiritRoot, pawn);
            hediff.grade = grade;
            hediff.elements = DefDatabase<SpiritElementDef>.AllDefsListForReading
                .Where(e => e.mutant == grade.mutant)
                .InRandomOrder()
                .Take(grade.elementCount)
                .ToList();
            hediff.cultivationSpeed = grade.canCultivate ? grade.cultivationSpeed.RandomInRange : 0f;
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        /// <summary>重新擲靈根（除錯用），保留是否已測出。</summary>
        public static Hediff_SpiritRoot Reroll(Pawn pawn)
        {
            Hediff_SpiritRoot old = pawn.GetSpiritRoot();
            bool wasRevealed = old != null && old.revealed;
            if (old != null)
                pawn.health.RemoveHediff(old);
            Hediff_SpiritRoot fresh = GiveSpiritRoot(pawn, RollGrade());
            fresh.revealed = wasRevealed;
            return fresh;
        }

        public static void Reveal(Pawn pawn, bool sendMessage)
        {
            Hediff_SpiritRoot root = EnsureSpiritRoot(pawn);
            if (root == null)
                return;
            root.revealed = true;
            if (!sendMessage)
                return;

            if (root.CanCultivate)
            {
                MessageTypeDef type = root.grade.order >= 5 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent;
                Messages.Message("WD_SpiritRootRevealed".Translate(pawn.LabelShort, root.grade.label, root.ElementsLabel),
                    pawn, type);
            }
            else
            {
                Messages.Message("WD_SpiritRootRevealedMortal".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>生成時呼叫；任何例外都吞掉並記錄，絕不打斷原版小人生成。</summary>
        public static void TryGiveOnGeneration(Pawn pawn)
        {
            try
            {
                EnsureSpiritRoot(pawn);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[WenDao] Failed to give spirit root to " + pawn.ToStringSafe() + ": " + ex,
                    "WenDao.SpiritRootGen".GetHashCode());
            }
        }
    }
}
