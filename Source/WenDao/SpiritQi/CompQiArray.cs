using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace WenDao
{
    public class CompProperties_QiArray : CompProperties
    {
        public float radius = 5.9f;
        public int setupTicks = 2500;

        public CompProperties_QiArray()
        {
            compClass = typeof(CompQiArray);
        }
    }

    /// <summary>聚靈陣：由修士布陣後，以靈石供能提高範圍內的靈氣基準；斷能、關閉或搬移即失效。</summary>
    public class CompQiArray : ThingComp
    {
        /// <summary>布陣強度（靈氣基準加成）；0 為尚未布陣。</summary>
        private float strength;

        public CompProperties_QiArray Props => (CompProperties_QiArray)props;

        public bool Active => parent.Spawned && strength > 0f
                              && parent.GetComp<CompRefuelable>()?.HasFuel != false
                              && parent.GetComp<CompFlickable>()?.SwitchIsOn != false;

        public float ActiveStrength => Active ? strength : 0f;

        /// <summary>布陣造詣 = (建造 + 智識) / 2 + 境界加成。</summary>
        public static float StrengthFor(Pawn pawn)
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            float skill = pawn.skills == null ? 0f
                : (pawn.skills.GetSkill(SkillDefOf.Construction).Level + pawn.skills.GetSkill(SkillDefOf.Intellectual).Level) / 2f;
            skill += ((pawn.GetCultivation()?.realm?.order ?? 1) - 1) * t.arraySkillPerRealm;
            return t.arrayStrengthBySkill.Evaluate(Mathf.Clamp(skill, 0f, 20f));
        }

        public void Setup(Pawn pawn)
        {
            strength = StrengthFor(pawn);
            Messages.Message("WD_ArraySetUp".Translate(pawn.LabelShort, strength.ToString("0.00")), parent, MessageTypeDefOf.PositiveEvent, false);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            MapComponent_SpiritQi.For(parent.Map)?.RegisterArray(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            MapComponent_SpiritQi.For(map)?.UnregisterArray(this);
            strength = 0f;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref strength, "strength");
        }

        public override string CompInspectStringExtra()
        {
            if (strength <= 0f)
                return "WD_ArrayNotSetUp".Translate();
            string s = "WD_ArrayStrength".Translate(strength.ToString("0.00"));
            return Active ? s : s + " (" + "WD_ArrayInactive".Translate() + ")";
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            string label = strength > 0f ? "WD_ReSetupArray".Translate() : "WD_SetupArray".Translate();
            if (selPawn.GetCultivation() == null)
            {
                yield return new FloatMenuOption(label + " (" + "WD_OnlyCultivatorsArray".Translate() + ")", null);
                yield break;
            }
            label += " (" + "WD_ArrayStrength".Translate(StrengthFor(selPawn).ToString("0.00")) + ")";
            if (!selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
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
                selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(WD_DefOf.WD_SetupArray, parent), JobTag.Misc);
            }), selPawn, parent);
        }
    }

    public class JobDriver_SetupArray : JobDriver
    {
        private CompQiArray Array => job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompQiArray>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil wait = Toils_General.Wait(Array?.Props.setupTicks ?? 2500, TargetIndex.A);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return wait;
            yield return Toils_General.Do(() => Array?.Setup(pawn));
        }
    }
}
