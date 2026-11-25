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
        private List<Pawn> guards = new List<Pawn>();

        private Thing Cushion => job.GetTarget(TargetIndex.A).Thing;

        /// <summary>已守護時間占閉關總時長的比例：過程中只升不降。</summary>
        public float GuardedFraction => UnityEngine.Mathf.Clamp01((float)guardedTicks / (pawn.GetCultivation()?.realm.SeclusionTicks ?? GenDate.TicksPerDay));

        /// <summary>指派護法：離開吃飯睡覺後會自動回崗，直到閉關結束或被玩家改派。</summary>
        public void AssignGuard(Pawn guard)
        {
            if (!guards.Contains(guard))
                guards.Add(guard);
        }

        public void UnassignGuard(Pawn guard) => guards.Remove(guard);

        /// <summary>不觸發反噬地結束閉關（已另行判定時用）。</summary>
        public void EndWithoutBacklash()
        {
            resolved = true;
            EndJobWith(JobCondition.Succeeded);
        }

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
                else
                    RecallGuard();
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
                // 期滿以實際取樣數為分母，避免少一次取樣讓全程守護拿不滿加成。
                BreakthroughUtility.ResolveSeclusion(pawn, elapsedTicks > 0 ? (float)guardedTicks / elapsedTicks : 0f);
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

        private void RecallGuard()
        {
            guards.RemoveAll(g => g == null || g.Dead || g.Destroyed);
            foreach (Pawn g in guards)
            {
                if (!g.Spawned || g.Map != pawn.Map || g.Downed || g.Drafted || g.InMentalState || !g.Awake()
                    || g.CurJobDef == WD_DefOf.WD_GuardSeclusion || g.CurJob?.playerForced == true
                    || !GuardNeeds.CanResume(g) || !g.CanReach(pawn, PathEndMode.Touch, Danger.Deadly))
                    continue;
                if (g.jobs.TryTakeOrderedJob(JobMaker.MakeJob(WD_DefOf.WD_GuardSeclusion, pawn), JobTag.Misc))
                    return;
            }
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
                BreakthroughUtility.SuccessChance(c, GuardedFraction).ToStringPercent(),
                BreakthroughUtility.GuardBonus(c, GuardedFraction).ToStringPercent(),
                GuardedFraction.ToStringPercent());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref guardedTicks, "guardedTicks");
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks");
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref started, "started");
            Scribe_Collections.Look(ref guards, "guards", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && guards == null)
                guards = new List<Pawn>();
        }
    }

    /// <summary>護法的離崗／回崗需求門檻。</summary>
    public static class GuardNeeds
    {
        public static bool MustLeave(Pawn p) =>
            Below(p.needs?.food, 0.2f) || Below(p.needs?.rest, 0.25f) || Below(p.needs?.joy, 0.1f);

        public static bool CanResume(Pawn p) =>
            !Below(p.needs?.food, 0.5f) && !Below(p.needs?.rest, 0.5f) && !Below(p.needs?.joy, 0.25f);

        private static bool Below(Need n, float pct) => n != null && n.CurLevelPercentage < pct;
    }

    /// <summary>護法：守在閉關者身旁，直到閉關結束；餓了累了自行離崗，恢復後由閉關者的 driver 召回。</summary>
    public class JobDriver_GuardSeclusion : JobDriver
    {
        private Pawn Secluded => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Secluded.CurJobDef != WD_DefOf.WD_Seclusion);
            AddFinishAction(condition =>
            {
                // 被玩家改派、徵召等強制打斷：視為解除護法。
                if (condition == JobCondition.InterruptForced)
                    (Secluded?.jobs?.curDriver as JobDriver_Seclusion)?.UnassignGuard(pawn);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil guard = ToilMaker.MakeToil("WD_GuardSeclusion");
            guard.initAction = () => pawn.pather.StopDead();
            guard.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(Secluded);
                if (pawn.IsHashIntervalTick(250) && GuardNeeds.MustLeave(pawn))
                    EndJobWith(JobCondition.Succeeded);
            };
            guard.defaultCompleteMode = ToilCompleteMode.Never;
            guard.handlingFacing = true;
            yield return guard;
        }
    }
}
