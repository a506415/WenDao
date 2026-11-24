using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    /// <summary>
    /// 打坐：走到蒲團或空地，坐一段時間（WD_Tuning.sessionTicks），
    /// 每 250 tick 結算一次修為（瓶頸時為感悟）並抽取周圍靈氣；無可再得即停。
    /// </summary>
    public class JobDriver_Cultivate : JobDriver
    {
        private const int Interval = 250;

        private LocalTargetInfo Spot => job.GetTarget(TargetIndex.A);

        private IntVec3 SpotCell => Spot.HasThing ? Spot.Thing.Position : Spot.Cell;

        private float Efficiency => CultivationUtility.SpotEfficiency(Spot.Thing);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Spot, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (Spot.HasThing)
                this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !CultivationUtility.HasCultivableRoot(pawn));

            yield return Toils_Goto.GotoCell(SpotCell, PathEndMode.OnCell);

            Toil sit = ToilMaker.MakeToil("WD_Cultivate");
            sit.initAction = () =>
            {
                CultivationUtility.GetOrStartCultivation(pawn);
                pawn.pather.StopDead();
            };
            sit.tickAction = () =>
            {
                if (!pawn.IsHashIntervalTick(Interval))
                    return;
                Hediff_Cultivation c = CultivationUtility.GetOrStartCultivation(pawn);
                if (c == null || !CultivationUtility.CanCultivateNow(pawn))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                float gain = CultivationUtility.GainPerHour(pawn, pawn.Position, Efficiency) * Interval / 2500f;
                c.AddCultivation(gain);
                MapComponent_SpiritQi.For(pawn.Map)?.Drain(pawn.Position, Interval);
                if (!CultivationUtility.CanCultivateNow(pawn))
                    EndJobWith(JobCondition.Succeeded);
            };
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = WenDaoTuningDef.Get.sessionTicks;
            sit.handlingFacing = true;
            sit.socialMode = RandomSocialMode.Off;
            sit.WithProgressBarToilDelay(TargetIndex.A, true);
            yield return sit;
        }

        public override string GetReport()
        {
            string report = base.GetReport();
            if (pawn.Map == null)
                return report;
            float perHour = CultivationUtility.GainPerHour(pawn, SpotCell, Efficiency);
            return report + " " + "WD_GainPerHour".Translate(perHour.ToString("0.00"));
        }
    }
}
