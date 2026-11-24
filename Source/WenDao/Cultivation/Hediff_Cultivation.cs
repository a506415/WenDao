using System.Text;
using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 修為：目前大境界、小階與該小階的修為進度。小階自動推進；
    /// 大境界最後一階修滿即進入瓶頸，需突破（突破流程另做）。
    /// </summary>
    public class Hediff_Cultivation : Hediff
    {
        public CultivationRealmDef realm;
        public int stage;
        public float progress;

        public float StageCost => realm.stageCosts[stage];

        public bool IsLastStage => stage >= realm.StageCount - 1;

        public bool AtBottleneck => IsLastStage && progress >= StageCost;

        public float StageProgressPct => StageCost > 0f ? progress / StageCost : 0f;

        public string StageLabel => realm.StageLabel(stage);

        public override bool Visible => true;

        public override string LabelBase => realm != null ? realm.label : base.LabelBase;

        public override string LabelInBrackets
        {
            get
            {
                if (realm == null)
                    return null;
                return AtBottleneck
                    ? StageLabel + ", " + "WD_Bottleneck".Translate()
                    : StageLabel + " " + StageProgressPct.ToStringPercent();
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
                sb.AppendLine("WD_ZhenyuanCapacity".Translate(realm.zhenyuanMax.ToString("0")));
                if (AtBottleneck)
                    sb.AppendLine("WD_BottleneckDesc".Translate());
                return sb.ToString().TrimEndNewlines();
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (realm == null)
                realm = CultivationRealmDef.First;
        }

        /// <summary>加修為；跨過小階門檻就升階，最後一階封頂在瓶頸。</summary>
        public void AddProgress(float amount)
        {
            if (realm == null || amount <= 0f || AtBottleneck)
                return;
            progress += amount;
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

        /// <summary>進入下一個大境界的第一階（突破成功時呼叫）。</summary>
        public bool AdvanceRealm()
        {
            CultivationRealmDef next = realm?.Next;
            if (next == null)
                return false;
            realm = next;
            stage = 0;
            progress = 0f;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref realm, "realm");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref progress, "progress");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (realm == null)
                    realm = CultivationRealmDef.First;
                stage = UnityEngine.Mathf.Clamp(stage, 0, realm.StageCount - 1);
            }
        }
    }
}
