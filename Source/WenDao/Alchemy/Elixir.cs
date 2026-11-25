using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    public class ElixirExtension : DefModExtension
    {
        /// <summary>每顆累積的丹毒（普通品質）。</summary>
        public float toxicity = 0.1f;
        /// <summary>炸爐基礎機率；隨煉丹造詣降低。</summary>
        public float explodeChance;
    }

    /// <summary>
    /// 丹藥。原版品質只看單一技能；這裡改以 (醫療+智識)/2 加境界重擲，炸爐則品質必為極差。
    /// </summary>
    public class Elixir : ThingWithComps
    {
        private Pawn maker;

        public ElixirExtension Ext => def.GetModExtension<ElixirExtension>();

        public override void Notify_RecipeProduced(Pawn pawn)
        {
            base.Notify_RecipeProduced(pawn);
            maker = pawn;
        }

        // 原版 GenRecipe 擲完品質後呼叫；此時以煉丹造詣重擲一次。
        public override void PostQualitySet()
        {
            base.PostQualitySet();
            if (maker == null)
                return;
            Pawn pawn = maker;
            maker = null;
            int level = AlchemyUtility.SkillLevel(pawn);
            bool exploded = Ext != null && Rand.Chance(AlchemyUtility.ExplodeChance(Ext, level));
            GetComp<CompQuality>()?.SetQuality(
                exploded ? QualityCategory.Awful : QualityUtility.GenerateQualityCreatedByPawn(level, false),
                ArtGenerationContext.Colony);
            if (exploded)
                AlchemyUtility.Explode(pawn, def);
        }
    }

    public static class AlchemyUtility
    {
        /// <summary>煉丹造詣 = (醫療 + 智識) / 2 + 境界加成。</summary>
        public static int SkillLevel(Pawn pawn)
        {
            if (pawn.skills == null)
                return 0;
            float skill = (pawn.skills.GetSkill(SkillDefOf.Medicine).Level + pawn.skills.GetSkill(SkillDefOf.Intellectual).Level) / 2f;
            int realmOrder = pawn.GetCultivation()?.realm?.order ?? 1;
            return Mathf.Clamp(Mathf.RoundToInt(skill + (realmOrder - 1) * WenDaoTuningDef.Get.alchemySkillPerRealm), 0, 20);
        }

        public static float ExplodeChance(ElixirExtension ext, int level) => ext.explodeChance * (1f - level / 25f);

        public static void Explode(Pawn pawn, ThingDef elixir)
        {
            if (!pawn.Spawned)
                return;
            Thing furnace = pawn.CurJob?.targetA.Thing;
            IntVec3 cell = furnace != null && furnace.Spawned ? furnace.Position : pawn.Position;
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            GenExplosion.DoExplosion(cell, pawn.Map, t.furnaceExplosionRadius, DamageDefOf.Flame, null, t.furnaceExplosionDamage);
            Messages.Message("WD_FurnaceExploded".Translate(pawn.LabelShort, elixir.label), pawn, MessageTypeDefOf.NegativeEvent);
        }
    }

    /// <summary>煉丹：只有修士能做。</summary>
    public class WorkGiver_Alchemy : WorkGiver_DoBill
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return (!forced && pawn.GetCultivation() == null) || base.ShouldSkip(pawn, forced);
        }

        public override Verse.AI.Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (pawn.GetCultivation() == null)
            {
                Verse.AI.JobFailReason.Is("WD_OnlyCultivators".Translate());
                return null;
            }
            return base.JobOnThing(pawn, thing, forced);
        }
    }
}
