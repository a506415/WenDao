using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    /// <summary>
    /// 閉關：在蒲團上坐滿 seclusionDays，期滿擲成功率。
    /// 期間任何打斷（受傷、精神崩潰、倒地、玩家徵召、蒲團被毀）都算反噬。
    /// 護法在旁的時間比例會加成功率。
    /// </summary>
    public class JobDriver_Seclusion : JobDriver
    {
        private const int Interval = 250;

        private int guardedTicks;
        private int elapsedTicks;
        private bool resolved;
        private bool started;

        private Thing Cushion => job.GetTarget(TargetIndex.A).Thing;

        public float GuardedFraction => elapsedTicks > 0 ? (float)guardedTicks / elapsedTicks : 0f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            AddFinishAction(condition =>
            {
                BreakthroughUtility.RemoveSeclusionHediff(pawn);
                // 已進入閉關卻未走到判定就結束：反噬。
                if (!resolved && started && !pawn.Dead)
                {
                    resolved = true;
                    BreakthroughUtility.Fail(pawn, "WD_FailReasonInterrupted".Translate());
                }
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);

            int duration = pawn.GetCultivation()?.realm.SeclusionTicks ?? GenDate.TicksPerDay;
            Toil seclude = ToilMaker.MakeToil("WD_Seclusion");
            seclude.initAction = () =>
            {
                pawn.pather.StopDead();
                started = true;
                if (!BreakthroughUtility.IsInSeclusion(pawn))
                    pawn.health.AddHediff(WD_DefOf.WD_InSeclusion);
            };
            seclude.tickAction = () =>
            {
                if (!pawn.IsHashIntervalTick(Interval))
                    return;
                elapsedTicks += Interval;
                if (GuardPresent())
                    guardedTicks += Interval;
            };
            seclude.defaultCompleteMode = ToilCompleteMode.Delay;
            seclude.defaultDuration = duration;
            seclude.handlingFacing = true;
            seclude.WithProgressBarToilDelay(TargetIndex.A, true);
            yield return seclude;

            yield return Toils_General.Do(() =>
            {
                resolved = true;
                BreakthroughUtility.RemoveSeclusionHediff(pawn);
                BreakthroughUtility.ResolveSeclusion(pawn, GuardedFraction);
            });
        }

        private bool GuardPresent()
        {
            float radius = WenDaoTuningDef.Get.guardRadius;
            foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (other != pawn
                    && other.CurJobDef == WD_DefOf.WD_GuardSeclusion
                    && other.CurJob.targetA.Thing == pawn
                    && other.Position.InHorDistOf(pawn.Position, radius))
                    return true;
            }
            return false;
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            if (dinfo.Def != null && dinfo.Def.harmsHealth && !resolved && started)
                EndJobWith(JobCondition.InterruptForced);
        }

        public override string GetReport()
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c == null || !c.CanAttemptBreakthrough)
                return base.GetReport();
            return base.GetReport() + " " + "WD_SeclusionReport".Translate(
                BreakthroughUtility.SuccessChance(c, GuardedFraction).ToStringPercent());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref guardedTicks, "guardedTicks");
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks");
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref started, "started");
        }
    }

    /// <summary>護法：守在閉關者身旁，直到閉關結束。</summary>
    public class JobDriver_GuardSeclusion : JobDriver
    {
        private Pawn Secluded => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Secluded.CurJobDef != WD_DefOf.WD_Seclusion);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil guard = ToilMaker.MakeToil("WD_GuardSeclusion");
            guard.initAction = () => pawn.pather.StopDead();
            guard.tickAction = () => pawn.rotationTracker.FaceTarget(Secluded);
            guard.defaultCompleteMode = ToilCompleteMode.Never;
            guard.handlingFacing = true;
            yield return guard;
        }
    }
}
