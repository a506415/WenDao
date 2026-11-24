using System.Linq;
using System.Text;
using LudeonTK;
using Verse;

namespace WenDao
{
    public static class DebugActions_WenDao
    {
        [DebugAction("WenDao", "Reveal spirit root", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RevealSpiritRoot(Pawn p)
        {
            SpiritRootUtility.Reveal(p, true);
        }

        [DebugAction("WenDao", "Reroll spirit root", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RerollSpiritRoot(Pawn p)
        {
            if (!SpiritRootUtility.CanHaveSpiritRoot(p))
                return;
            Hediff_SpiritRoot root = SpiritRootUtility.Reroll(p);
            Messages.Message(p.LabelShort + ": " + root.LabelCap + " x" + root.cultivationSpeed.ToString("0.00"),
                p, RimWorld.MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("WenDao", "Cultivation +100", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddCultivation(Pawn p)
        {
            CultivationUtility.GetOrStartCultivation(p)?.AddCultivation(100f);
        }

        [DebugAction("WenDao", "Advance realm (skip breakthrough)", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AdvanceRealm(Pawn p)
        {
            Hediff_Cultivation c = CultivationUtility.GetOrStartCultivation(p);
            if (c != null && c.AdvanceRealm())
                p.health.Notify_HediffChanged(c);
        }

        [DebugAction("WenDao", "Fill insight", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillInsight(Pawn p)
        {
            Hediff_Cultivation c = p.GetCultivation();
            if (c != null && c.CanAttemptBreakthrough)
                c.AddInsight(c.realm.insightRequired);
        }

        [DebugAction("WenDao", "Lose 10 years lifespan", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LoseLifespan(Pawn p)
        {
            p.GetCultivation()?.LoseLifespan(10f);
        }

        [DebugAction("WenDao", "Resolve breakthrough now", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResolveBreakthroughNow(Pawn p)
        {
            BreakthroughUtility.ResolveSeclusion(p, 0f);
        }

        [DebugAction("WenDao", "Start tribulation (skip roll)", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartTribulation(Pawn p)
        {
            Hediff_Cultivation c = p.GetCultivation();
            CultivationRealmDef next = c?.realm.Next;
            if (next != null && next.HasTribulation && p.Spawned)
                GameCondition_HeavenlyTribulation.Start(p, next, false);
            else
                Messages.Message("Next realm has no tribulation.", RimWorld.MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("WenDao", "Fill true essence", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillZhenyuan(Pawn p)
        {
            RimWorld.Need n = p.needs?.TryGetNeed(WD_DefOf.WD_Zhenyuan);
            if (n != null)
                n.CurLevel = n.MaxLevel;
        }

        [DebugAction("WenDao", "Log spirit root distribution (10000 rolls)", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogDistribution()
        {
            const int n = 10000;
            var counts = DefDatabase<SpiritRootGradeDef>.AllDefsListForReading.ToDictionary(g => g, g => 0);
            for (int i = 0; i < n; i++)
                counts[SpiritRootUtility.RollGrade()]++;
            var sb = new StringBuilder("[WenDao] Spirit root distribution over " + n + " rolls:");
            foreach (var kv in counts.OrderBy(kv => kv.Key.order))
                sb.AppendLine().Append("  ").Append(kv.Key.defName).Append(": ").Append((kv.Value / (float)n).ToStringPercent());
            Log.Message(sb.ToString());
        }
    }
}
