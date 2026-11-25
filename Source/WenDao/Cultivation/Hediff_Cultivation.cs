using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 修為：大境界、小階、本階修為、瓶頸時的感悟，以及壽元。
    /// 小階自動推進；最後一階修滿進入瓶頸，此後打坐改累積感悟，感悟圓滿即可閉關突破。
    /// 剩餘壽元 = 境界壽元上限 − 生理年齡 − 累計折壽；歸零即坐化。
    /// </summary>
    public class Hediff_Cultivation : Hediff
    {
        private const int LifespanCheckInterval = 2500;

        public CultivationRealmDef realm;
        public int stage;
        public float progress;
        public float insight;
        /// <summary>累計折壽（年）。</summary>
        public float lifespanLost;
        private bool lifespanWarned;
        /// <summary>已服延壽丹數，用於效果遞減。</summary>
        public int longevityTaken;

        public float StageCost => realm.stageCosts[stage];

        public bool IsLastStage => stage >= realm.StageCount - 1;

        public bool AtBottleneck => IsLastStage && progress >= StageCost;

        /// <summary>瓶頸且有下一境界可突破。</summary>
        public bool CanAttemptBreakthrough => AtBottleneck && realm.Next != null;

        public bool InsightFull => insight >= realm.insightRequired;

        public float InsightPct => realm.insightRequired > 0f ? Mathf.Clamp01(insight / realm.insightRequired) : 1f;

        public float StageProgressPct => StageCost > 0f ? progress / StageCost : 0f;

        public string StageLabel => realm.StageLabel(stage);

        public float LifespanRemainingYears =>
            realm.lifespanYears - pawn.ageTracker.AgeBiologicalYearsFloat - lifespanLost;

        public override bool Visible => true;

        public override string LabelBase => realm != null ? realm.label : base.LabelBase;

        public override string LabelInBrackets
        {
            get
            {
                if (realm == null)
                    return null;
                if (!AtBottleneck)
                    return StageLabel + " " + StageProgressPct.ToStringPercent();
                if (CanAttemptBreakthrough)
                    return StageLabel + ", " + "WD_Bottleneck".Translate() + " " + "WD_Insight".Translate() + " " + InsightPct.ToStringPercent();
                return StageLabel + ", " + "WD_Bottleneck".Translate();
            }
        }

        public override string TipStringExtra
        {
            get
            {
                if (realm == null)
                    return base.TipStringExtra;
                var sb = new StringBuilder(base.TipStringExtra);
                sb.AppendLine("WD_CultivationProgress".Translate(progress.ToString("0.#"), StageCost.ToString("0.#")));
                if (CanAttemptBreakthrough)
                    sb.AppendLine("WD_InsightProgress".Translate(insight.ToString("0.#"), realm.insightRequired.ToString("0.#")));
                sb.AppendLine("WD_ZhenyuanCapacity".Translate(realm.zhenyuanMax.ToString("0")));
                sb.AppendLine("WD_LifespanRemaining".Translate(LifespanRemainingYears.ToString("0.#"), realm.lifespanYears.ToString("0"), lifespanLost.ToString("0.#")));
                if (AtBottleneck)
                    sb.AppendLine((CanAttemptBreakthrough ? "WD_BottleneckDesc" : "WD_BottleneckNoNextRealm").Translate());
                return sb.ToString().TrimEndNewlines();
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (realm == null)
                realm = CultivationRealmDef.First;
            SpellUtility.SyncSpells(pawn, false);
            pawn.Notify_DisabledWorkTypesChanged(); // 成為修士後可煉丹
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            pawn.Notify_DisabledWorkTypesChanged();
        }

        private const int AutoDefendInterval = 30;
        private int ticksSinceLifespanCheck;

        // 1.6 的 hediff 以 TickInterval(delta) 批次推進。
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(AutoDefendInterval, delta))
                SpellUtility.TryAutoDefend(pawn);
            ticksSinceLifespanCheck += delta;
            if (ticksSinceLifespanCheck >= LifespanCheckInterval)
            {
                ticksSinceLifespanCheck = 0;
                CheckLifespan();
            }
        }

        private void CheckLifespan()
        {
            if (realm == null || pawn.Dead)
                return;
            float remaining = LifespanRemainingYears;
            if (remaining <= 0f)
            {
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Find.LetterStack.ReceiveLetter("WD_LetterLifespanEndedLabel".Translate(pawn.LabelShort),
                        "WD_LetterLifespanEnded".Translate(pawn.LabelShort, realm.label),
                        LetterDefOf.Death, pawn);
                }
                pawn.Kill(null, this);
                return;
            }
            if (!lifespanWarned && remaining < WenDaoTuningDef.Get.lifespanWarningYears)
            {
                lifespanWarned = true;
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Find.LetterStack.ReceiveLetter("WD_LetterLifespanWarningLabel".Translate(pawn.LabelShort),
                        "WD_LetterLifespanWarning".Translate(pawn.LabelShort, remaining.ToString("0.#")),
                        LetterDefOf.NegativeEvent, pawn);
                }
            }
        }

        /// <summary>折壽（年）。可在突破失敗、強行突破、燃血秘術等處呼叫。</summary>
        public void LoseLifespan(float years)
        {
            if (years <= 0f)
                return;
            lifespanLost += years;
            CheckLifespan();
        }

        /// <summary>增壽（年）；累計折壽可為負。</summary>
        public void GainLifespan(float years)
        {
            lifespanLost -= years;
            if (LifespanRemainingYears >= WenDaoTuningDef.Get.lifespanWarningYears)
                lifespanWarned = false;
        }

        /// <summary>打坐所得：未到瓶頸加修為；瓶頸時加感悟。</summary>
        public void AddCultivation(float amount)
        {
            if (realm == null || amount <= 0f)
                return;
            if (AtBottleneck)
                AddInsight(amount);
            else
                AddProgress(amount);
        }

        /// <summary>加修為；跨過小階門檻就升階，最後一階封頂在瓶頸。</summary>
        public void AddProgress(float amount)
        {
            if (realm == null || amount <= 0f || AtBottleneck)
                return;
            progress += amount;
            int oldStage = stage;
            while (progress >= StageCost && !IsLastStage)
            {
                progress -= StageCost;
                stage++;
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("WD_StageAdvanced".Translate(pawn.LabelShort, realm.label, StageLabel),
                        pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            if (stage != oldStage)
                SpellUtility.SyncSpells(pawn, true);
            if (IsLastStage && progress >= StageCost)
            {
                progress = StageCost;
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("WD_ReachedBottleneck".Translate(pawn.LabelShort, realm.label),
                        pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
        }

        public void AddInsight(float amount)
        {
            if (!CanAttemptBreakthrough || InsightFull || amount <= 0f)
                return;
            insight = Mathf.Min(realm.insightRequired, insight + amount);
            if (InsightFull && PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message("WD_InsightFull".Translate(pawn.LabelShort, realm.Next.label),
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>進入下一個大境界的第一階（突破成功時呼叫）。</summary>
        public bool AdvanceRealm()
        {
            CultivationRealmDef next = realm?.Next;
            if (next == null)
                return false;
            realm = next;
            stage = 0;
            progress = 0f;
            insight = 0f;
            lifespanWarned = false;
            pawn.health.Notify_HediffChanged(this);
            SpellUtility.SyncSpells(pawn, true);
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref realm, "realm");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref insight, "insight");
            Scribe_Values.Look(ref lifespanLost, "lifespanLost");
            Scribe_Values.Look(ref lifespanWarned, "lifespanWarned");
            Scribe_Values.Look(ref longevityTaken, "longevityTaken");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (realm == null)
                    realm = CultivationRealmDef.First;
                stage = Mathf.Clamp(stage, 0, realm.StageCount - 1);
            }
        }
    }
}
