using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WenDao
{
    public class JobDriver_TestSpiritRoot : JobDriver
    {
        private Thing Plate => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            int duration = Plate.TryGetComp<CompSpiritTester>()?.Props.testDurationTicks ?? 300;
            Toil wait = Toils_General.Wait(duration, TargetIndex.A);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return wait;

            yield return Toils_General.Do(() => SpiritRootUtility.Reveal(pawn, true));
        }
    }
}
