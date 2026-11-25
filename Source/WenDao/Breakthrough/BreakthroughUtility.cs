using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 突破：瓶頸 → 閉關 → 成功率判定 →（目標境界有天劫則）渡天劫 → 進境；
    /// 失敗或閉關被打斷即反噬，代價依「被突破的境界」的 Def 設定。
    /// </summary>
    public static class BreakthroughUtility
    {
        public static bool IsInSeclusion(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(WD_DefOf.WD_InSeclusion) == true;
        }

        public static GameCondition_HeavenlyTribulation ActiveTribulationFor(Pawn pawn)
        {
            Map map = pawn?.MapHeld;
            if (map == null)
                return null;
            return map.gameConditionManager.ActiveConditions
                .OfType<GameCondition_HeavenlyTribulation>()
                .FirstOrDefault(t => t.target == pawn);
        }

        public static bool IsBusyBreakingThrough(Pawn pawn)
        {
            return IsInSeclusion(pawn) || ActiveTribulationFor(pawn) != null;
        }

        /// <summary>
        /// 成功率 = 基礎 × 強行係數 + 護法加成，夾在範圍內。
        /// 感悟圓滿時強行係數為 1；否則 Lerp(forcedChanceMin, 1, 感悟比例)。
        /// </summary>
        public static float SuccessChance(Hediff_Cultivation c, float guardedFraction)
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            float forcedFactor = c.InsightFull ? 1f : Mathf.Lerp(t.forcedChanceMin, 1f, c.InsightPct);
            float chance = c.realm.breakthroughChance * forcedFactor + t.guardBonus * Mathf.Clamp01(guardedFraction) + AidBonus(c);
            return Mathf.Clamp(chance, t.breakthroughChanceRange.min, t.breakthroughChanceRange.max);
        }

        /// <summary>築基丹等突破輔助的加成。</summary>
        public static float AidBonus(Hediff_Cultivation c)
        {
            float sum = 0f;
            foreach (Hediff h in c.pawn.health.hediffSet.hediffs)
            {
                if (h is Hediff_BreakthroughAid aid && aid.AppliesTo(c.realm))
                    sum += aid.Severity;
            }
            return sum;
        }

        private static void ConsumeAids(Hediff_Cultivation c)
        {
            foreach (Hediff_BreakthroughAid aid in c.pawn.health.hediffSet.hediffs.OfType<Hediff_BreakthroughAid>()
                         .Where(a => a.AppliesTo(c.realm)).ToList())
                c.pawn.health.RemoveHediff(aid);
        }

        /// <summary>護法實際帶來的成功率加成（已計入上下限）。</summary>
        public static float GuardBonus(Hediff_Cultivation c, float guardedFraction)
        {
            return SuccessChance(c, guardedFraction) - SuccessChance(c, 0f);
        }

        /// <summary>若正在閉關，不反噬地結束它並回傳護法在場比例。</summary>
        public static float EndSeclusionQuietly(Pawn pawn)
        {
            if (!(pawn.jobs?.curDriver is JobDriver_Seclusion driver))
                return 0f;
            float guarded = driver.GuardedFraction;
            driver.EndWithoutBacklash();
            return guarded;
        }

        /// <summary>強行突破成功時要扣的壽元（年）。</summary>
        public static float ForcedLifespanCost(Hediff_Cultivation c)
        {
            CultivationRealmDef next = c.realm.Next;
            return next == null ? 0f : next.lifespanYears * WenDaoTuningDef.Get.forcedLifespanCapFraction;
        }

        /// <summary>閉關期滿，擲成功率。</summary>
        public static void ResolveSeclusion(Pawn pawn, float guardedFraction)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c == null || !c.CanAttemptBreakthrough)
                return;
            bool forced = !c.InsightFull;
            float chance = SuccessChance(c, guardedFraction);
            float guardBonus = GuardBonus(c, guardedFraction);
            ConsumeAids(c);
            if (!Rand.Chance(chance))
            {
                Fail(pawn, "WD_FailReasonRoll".Translate(chance.ToStringPercent(), guardBonus.ToStringPercent()));
                return;
            }
            CultivationRealmDef next = c.realm.Next;
            if (next.HasTribulation && pawn.Spawned)
                GameCondition_HeavenlyTribulation.Start(pawn, next, forced);
            else
                Succeed(pawn, forced);
        }

        public static void Succeed(Pawn pawn, bool forced)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c == null || !c.AdvanceRealm())
                return;
            var text = new StringBuilder("WD_LetterBreakthroughSuccess".Translate(pawn.LabelShort, c.realm.label));
            if (forced)
            {
                float cost = c.realm.lifespanYears * WenDaoTuningDef.Get.forcedLifespanCapFraction;
                text.Append("\n\n").Append("WD_LetterForcedCost".Translate(cost.ToString("0.#")));
                c.LoseLifespan(cost);
            }
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(WD_DefOf.WD_BreakthroughSucceeded);
            if (!pawn.Dead && PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Find.LetterStack.ReceiveLetter("WD_LetterBreakthroughSuccessLabel".Translate(pawn.LabelShort),
                    text.ToString(), LetterDefOf.PositiveEvent, pawn);
            }
        }

        /// <summary>突破失敗或閉關被打斷：反噬傷、永久傷、折壽、清空感悟。</summary>
        public static void Fail(Pawn pawn, string reason)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c == null || pawn.Dead)
                return;
            CultivationRealmDef realm = c.realm;
            float years = realm.failureLifespanLoss.RandomInRange;
            c.insight = 0f;
            ConsumeAids(c);

            var text = new StringBuilder("WD_LetterBreakthroughFailed".Translate(pawn.LabelShort, realm.label, reason));
            foreach (HediffSeverity hs in realm.failureHediffs)
            {
                if (hs.hediff == null)
                    continue;
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hs.hediff);
                if (existing != null)
                    existing.Severity += hs.severity;
                else
                {
                    Hediff h = HediffMaker.MakeHediff(hs.hediff, pawn);
                    h.Severity = hs.severity;
                    pawn.health.AddHediff(h);
                }
                text.Append("\n- ").Append(hs.hediff.LabelCap);
            }
            if (years > 0f)
                text.Append("\n- ").Append("WD_LetterLifespanLost".Translate(years.ToString("0.#")));

            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(WD_DefOf.WD_BreakthroughFailed);
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Find.LetterStack.ReceiveLetter("WD_LetterBreakthroughFailedLabel".Translate(pawn.LabelShort),
                    text.ToString(), LetterDefOf.NegativeEvent, pawn);
            }
            c.LoseLifespan(years);
        }

        public static void RemoveSeclusionHediff(Pawn pawn)
        {
            Hediff h = pawn?.health?.hediffSet?.GetFirstHediffOfDef(WD_DefOf.WD_InSeclusion);
            if (h != null)
                pawn.health.RemoveHediff(h);
        }
    }
}
