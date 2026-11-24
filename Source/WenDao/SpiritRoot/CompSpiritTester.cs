using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    public class CompProperties_SpiritTester : CompProperties
    {
        public int testDurationTicks = 300;

        public CompProperties_SpiritTester()
        {
            compClass = typeof(CompSpiritTester);
        }
    }

    /// <summary>測靈盤：右鍵選單讓選中的小人去測自己的靈根。</summary>
    public class CompSpiritTester : ThingComp
    {
        public CompProperties_SpiritTester Props => (CompProperties_SpiritTester)props;

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!SpiritRootUtility.CanHaveSpiritRoot(selPawn))
                yield break;

            string label = "WD_TestSpiritRoot".Translate();
            Hediff_SpiritRoot root = selPawn.GetSpiritRoot();

            if (root != null && root.revealed)
            {
                yield return new FloatMenuOption(label + " (" + "WD_AlreadyTested".Translate() + ")", null);
                yield break;
            }
            if (!selPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                yield break;
            }
            if (!selPawn.CanReserve(parent))
            {
                yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                Job job = JobMaker.MakeJob(WD_DefOf.WD_TestSpiritRoot, parent);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }), selPawn, parent);
        }
    }
}
