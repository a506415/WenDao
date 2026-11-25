using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WenDao
{
    /// <summary>渡劫：原地硬撐，直到天劫結束。</summary>
    public class JobDriver_EndureTribulation : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => BreakthroughUtility.ActiveTribulationFor(pawn) == null);
            Toil endure = ToilMaker.MakeToil("WD_EndureTribulation");
            endure.initAction = () => pawn.pather.StopDead();
            endure.defaultCompleteMode = ToilCompleteMode.Never;
            yield return endure;
        }
    }
}
