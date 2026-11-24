using RimWorld;
using Verse;
using Verse.AI;

namespace WenDao
{
    /// <summary>到蒲團上打坐，偏好靈氣較濃的蒲團。</summary>
    public class WorkGiver_CultivateOnCushion : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(WD_DefOf.WD_MeditationCushion);

        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override bool Prioritized => true;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !CultivationUtility.CanCultivateNow(pawn);
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            return MapComponent_SpiritQi.For(t.Map)?.QiAt(t.Cell) ?? 0f;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.IsForbidden(pawn) || t.IsBurning())
                return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced))
                return false;
            Pawn occupant = t.Position.GetFirstPawn(t.Map);
            return occupant == null || occupant == pawn;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(WD_DefOf.WD_Cultivate, t);
        }
    }

    /// <summary>沒有可用蒲團時，就近找塊空地打坐（效率較低）。</summary>
    public class WorkGiver_CultivateOnGround : WorkGiver
    {
        private const int SearchRadius = 10;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !CultivationUtility.CanCultivateNow(pawn) || pawn.Map == null;
        }

        public override Job NonScanJob(Pawn pawn)
        {
            if (ShouldSkip(pawn))
                return null;
            Map map = pawn.Map;
            bool found = CellFinder.TryFindRandomReachableNearbyCell(pawn.Position, map, SearchRadius,
                TraverseParms.For(pawn), c => GoodGroundCell(pawn, c), null, out IntVec3 cell);
            if (!found)
                return null;
            return JobMaker.MakeJob(WD_DefOf.WD_Cultivate, cell);
        }

        private static bool GoodGroundCell(Pawn pawn, IntVec3 c)
        {
            Map map = pawn.Map;
            return c.Standable(map)
                   && !c.IsForbidden(pawn)
                   && c.GetEdifice(map) == null
                   && c.GetFirstBuilding(map) == null
                   && !c.GetTerrain(map).avoidWander
                   && !c.ContainsStaticFire(map)
                   && c.GetDangerFor(pawn, map) == Danger.None
                   && (c.GetFirstPawn(map) == null || c.GetFirstPawn(map) == pawn)
                   && pawn.CanReserve(c);
        }
    }
}
