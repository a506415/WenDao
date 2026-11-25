using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    public static class CultivationUtility
    {
        public static Hediff_Cultivation GetCultivation(this Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(WD_DefOf.WD_Cultivation) as Hediff_Cultivation;
        }

        /// <summary>有可修煉的靈根（測過與否皆可）。</summary>
        public static bool HasCultivableRoot(Pawn pawn)
        {
            Hediff_SpiritRoot root = pawn.GetSpiritRoot();
            return root != null && root.CanCultivate;
        }

        /// <summary>此刻能否打坐累積修為（不檢查地點）。</summary>
        public static bool CanCultivateNow(Pawn pawn)
        {
            if (pawn == null || pawn.Downed || pawn.Dead || !HasCultivableRoot(pawn))
                return false;
            if (BreakthroughUtility.IsBusyBreakingThrough(pawn))
                return false;
            Hediff_Cultivation c = pawn.GetCultivation();
            // 未到瓶頸累積修為；瓶頸時累積感悟直到圓滿。
            return c == null || !c.AtBottleneck || (c.CanAttemptBreakthrough && !c.InsightFull);
        }

        public static Hediff_Cultivation GetOrStartCultivation(Pawn pawn)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c != null || !HasCultivableRoot(pawn))
                return c;
            c = (Hediff_Cultivation)HediffMaker.MakeHediff(WD_DefOf.WD_Cultivation, pawn);
            c.realm = CultivationRealmDef.First;
            pawn.health.AddHediff(c);
            return c;
        }

        /// <summary>修為增量/小時 = 基礎值 × 修煉速度 stat × 該格靈氣 × 打坐處效率。</summary>
        public static float GainPerHour(Pawn pawn, IntVec3 cell, float spotEfficiency)
        {
            float qi = MapComponent_SpiritQi.For(pawn.Map)?.QiAt(cell) ?? 0f;
            return WenDaoTuningDef.Get.baseGainPerHour
                   * pawn.GetStatValue(WD_DefOf.WD_CultivationSpeed)
                   * qi
                   * spotEfficiency;
        }

        public static float SpotEfficiency(Thing spot)
        {
            CompCultivationSpot comp = spot?.TryGetComp<CompCultivationSpot>();
            return comp != null ? comp.Props.efficiency : WenDaoTuningDef.Get.groundEfficiency;
        }
    }

    public class CompProperties_CultivationSpot : CompProperties
    {
        public float efficiency = 1f;

        public CompProperties_CultivationSpot()
        {
            compClass = typeof(CompCultivationSpot);
        }
    }

    /// <summary>打坐處（蒲團等）：提供效率，並在檢視面板顯示此處靈氣。</summary>
    public class CompCultivationSpot : ThingComp
    {
        public CompProperties_CultivationSpot Props => (CompProperties_CultivationSpot)props;

        public override string CompInspectStringExtra()
        {
            MapComponent_SpiritQi grid = MapComponent_SpiritQi.For(parent.Map);
            if (grid == null)
                return null;
            return "WD_SpotInspect".Translate(grid.QiAt(parent.Position).ToString("0.00"), Props.efficiency.ToStringPercent());
        }

        /// <summary>右鍵：閉關突破／強行突破；有人在此閉關時可為其護法。</summary>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            Pawn occupant = parent.Position.GetFirstPawn(parent.Map);

            // 護法
            if (occupant != null && occupant != selPawn && occupant.CurJobDef == WD_DefOf.WD_Seclusion)
            {
                string guardLabel = "WD_GuardSeclusion".Translate(occupant.LabelShort, WenDaoTuningDef.Get.guardBonus.ToStringPercent());
                if (!selPawn.CanReach(occupant, PathEndMode.Touch, Danger.Deadly))
                    yield return new FloatMenuOption(guardLabel + " (" + "NoPath".Translate() + ")", null);
                else
                    yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(guardLabel, () =>
                    {
                        if (selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(WD_DefOf.WD_GuardSeclusion, occupant), JobTag.Misc))
                            (occupant.jobs.curDriver as JobDriver_Seclusion)?.AssignGuard(selPawn);
                    }), selPawn, occupant);
                yield break;
            }

            // 閉關突破
            Hediff_Cultivation c = selPawn.GetCultivation();
            if (c == null || !c.CanAttemptBreakthrough || BreakthroughUtility.IsBusyBreakingThrough(selPawn))
                yield break;

            float chance = BreakthroughUtility.SuccessChance(c, 0f);
            string days = c.realm.seclusionDays.ToString("0.#");
            string label = c.InsightFull
                ? "WD_StartSeclusion".Translate(chance.ToStringPercent(), days)
                : "WD_StartForcedSeclusion".Translate(c.InsightPct.ToStringPercent(), chance.ToStringPercent(), days,
                    BreakthroughUtility.ForcedLifespanCost(c).ToString("0.#"));

            if (!selPawn.CanReach(parent, PathEndMode.OnCell, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                yield break;
            }
            if (!selPawn.CanReserve(parent) || (occupant != null && occupant != selPawn))
            {
                yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
                yield break;
            }
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(WD_DefOf.WD_Seclusion, parent), JobTag.Misc);
            }, MenuOptionPriority.High), selPawn, parent);
        }
    }
}
