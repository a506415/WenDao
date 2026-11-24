using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 天劫：預警一段時間後，每隔 tribulationIntervalTicks 一道雷。
    /// 落雷在突破者旁 2~3 格（原版雷擊爆炸，會波及旁人、引火），並直接灼傷突破者。
    /// 劈完仍活著即進境；離開地圖算失敗。
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

        private void Strike()
        {
            Map map = SingleMap;
            IntVec3 center = target.Position;
            if (!CellFinder.TryFindRandomCellNear(center, map, 3,
                    c => c.InBounds(map) && !c.InHorDistOf(center, 1.9f), out IntVec3 boltCell))
                boltCell = center;
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, boltCell));

            float dmg = targetRealm.tribulationDamage.RandomInRange;
            target.TakeDamage(new DamageInfo(DamageDefOf.Burn, dmg));
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
