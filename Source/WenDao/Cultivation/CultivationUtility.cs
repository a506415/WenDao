using RimWorld;
using Verse;

namespace WenDao
{
    public static class CultivationUtility
    {
        public static Hediff_Cultivation GetCultivation(this Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(WD_DefOf.WD_Cultivation) as Hediff_Cultivation;
        }

        /// <summary>有可修煉的靈根（測過與否皆可）。</summary>
        public static bool HasCultivableRoot(Pawn pawn)
        {
            Hediff_SpiritRoot root = pawn.GetSpiritRoot();
            return root != null && root.CanCultivate;
        }

        /// <summary>此刻能否打坐累積修為（不檢查地點）。</summary>
        public static bool CanCultivateNow(Pawn pawn)
        {
            if (pawn == null || pawn.Downed || pawn.Dead || !HasCultivableRoot(pawn))
                return false;
            Hediff_Cultivation c = pawn.GetCultivation();
            return c == null || !c.AtBottleneck;
        }

        public static Hediff_Cultivation GetOrStartCultivation(Pawn pawn)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c != null || !HasCultivableRoot(pawn))
                return c;
            c = (Hediff_Cultivation)HediffMaker.MakeHediff(WD_DefOf.WD_Cultivation, pawn);
            c.realm = CultivationRealmDef.First;
            pawn.health.AddHediff(c);
            return c;
        }

        /// <summary>修為增量/小時 = 基礎值 × 修煉速度 stat × 該格靈氣 × 打坐處效率。</summary>
        public static float GainPerHour(Pawn pawn, IntVec3 cell, float spotEfficiency)
        {
            float qi = MapComponent_SpiritQi.For(pawn.Map)?.QiAt(cell) ?? 0f;
            return WenDaoTuningDef.Get.baseGainPerHour
                   * pawn.GetStatValue(WD_DefOf.WD_CultivationSpeed)
                   * qi
                   * spotEfficiency;
        }

        public static float SpotEfficiency(Thing spot)
        {
            CompCultivationSpot comp = spot?.TryGetComp<CompCultivationSpot>();
            return comp != null ? comp.Props.efficiency : WenDaoTuningDef.Get.groundEfficiency;
        }
    }

    public class CompProperties_CultivationSpot : CompProperties
    {
        public float efficiency = 1f;

        public CompProperties_CultivationSpot()
        {
            compClass = typeof(CompCultivationSpot);
        }
    }

    /// <summary>打坐處（蒲團等）：提供效率，並在檢視面板顯示此處靈氣。</summary>
    public class CompCultivationSpot : ThingComp
    {
        public CompProperties_CultivationSpot Props => (CompProperties_CultivationSpot)props;

        public override string CompInspectStringExtra()
        {
            MapComponent_SpiritQi grid = MapComponent_SpiritQi.For(parent.Map);
            if (grid == null)
                return null;
            return "WD_SpotInspect".Translate(grid.QiAt(parent.Position).ToString("0.00"), Props.efficiency.ToStringPercent());
        }
    }
}
