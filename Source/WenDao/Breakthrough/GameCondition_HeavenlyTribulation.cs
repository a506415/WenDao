using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    /// <summary>
    /// 天劫：預警一段時間後，每隔 tribulationIntervalTicks 一道雷。
    /// 雷直接劈在突破者身上（無視護甲、不引火），並以同一種雷傷波及周圍的人。
    /// 突破者會原地打坐硬撐；劈完仍活著即進境，離開地圖算失敗。
    /// </summary>
    public class GameCondition_HeavenlyTribulation : GameCondition
    {
        public Pawn target;
        public CultivationRealmDef targetRealm;
        public bool forced;
        private int strikesDone;
        private int nextStrikeTick;
        private bool finished;

        public static void Start(Pawn pawn, CultivationRealmDef realm, bool forced)
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            int duration = t.tribulationWarningTicks + realm.tribulationStrikes * t.tribulationIntervalTicks + 2500;
            var cond = (GameCondition_HeavenlyTribulation)GameConditionMaker.MakeCondition(WD_DefOf.WD_HeavenlyTribulation, duration);
            cond.target = pawn;
            cond.targetRealm = realm;
            cond.forced = forced;
            cond.nextStrikeTick = Find.TickManager.TicksGame + t.tribulationWarningTicks;
            pawn.Map.gameConditionManager.RegisterCondition(cond);

            Find.LetterStack.ReceiveLetter("WD_LetterTribulationLabel".Translate(pawn.LabelShort),
                "WD_LetterTribulation".Translate(pawn.LabelShort, realm.label, realm.tribulationStrikes,
                    (t.tribulationWarningTicks / (float)GenDate.TicksPerHour).ToString("0.#")),
                LetterDefOf.ThreatBig, pawn);
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (finished)
                return;
            if (target == null || target.Dead)
            {
                Finish();
                return;
            }
            if (!target.Spawned || target.Map != SingleMap)
            {
                BreakthroughUtility.Fail(target, "WD_FailReasonFledTribulation".Translate());
                Finish();
                return;
            }
            if (target.IsHashIntervalTick(30))
                KeepEnduring();
            if (Find.TickManager.TicksGame < nextStrikeTick)
                return;

            Strike();
            strikesDone++;
            nextStrikeTick += WenDaoTuningDef.Get.tribulationIntervalTicks;
            if (target.Dead)
            {
                Finish();
                return;
            }
            if (strikesDone >= targetRealm.tribulationStrikes)
            {
                BreakthroughUtility.Succeed(target, forced);
                Finish();
            }
        }

        /// <summary>讓突破者留在原地硬撐；徵召、倒地、著火、精神崩潰時不干預。</summary>
        private void KeepEnduring()
        {
            if (target.Downed || target.Drafted || target.InMentalState || target.IsBurning()
                || target.jobs == null || target.CurJobDef == WD_DefOf.WD_EndureTribulation)
                return;
            Job job = JobMaker.MakeJob(WD_DefOf.WD_EndureTribulation);
            job.playerForced = true; // 不因敵人靠近等反應自行逃離
            target.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        private void Strike()
        {
            Map map = SingleMap;
            IntVec3 center = target.Position;
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_TribulationBolt(map, center));
            GenExplosion.DoExplosion(center, map, WenDaoTuningDef.Get.tribulationSplashRadius, WD_DefOf.WD_TribulationLightning, null,
                damAmount: GenMath.RoundRandom(targetRealm.tribulationDamage.RandomInRange),
                ignoredThings: new List<Thing> { target }, doSoundEffects: false);
            target.TakeDamage(new DamageInfo(WD_DefOf.WD_TribulationLightning, targetRealm.tribulationDamage.RandomInRange));
        }

        private void Finish()
        {
            finished = true;
            End();
        }

        public override string Label => base.Label + (target != null ? " (" + target.LabelShort + ")" : "");

        public override string Description
        {
            get
            {
                string s = base.Description;
                if (targetRealm != null)
                    s += "\n\n" + "WD_TribulationProgress".Translate(strikesDone, targetRealm.tribulationStrikes);
                return s;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Defs.Look(ref targetRealm, "targetRealm");
            Scribe_Values.Look(ref forced, "forced");
            Scribe_Values.Look(ref strikesDone, "strikesDone");
            Scribe_Values.Look(ref nextStrikeTick, "nextStrikeTick");
            Scribe_Values.Look(ref finished, "finished");
        }
    }
}
