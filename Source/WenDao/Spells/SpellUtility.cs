using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WenDao
{
    public static class SpellUtility
    {
        private static List<AbilityDef> allSpells;

        public static List<AbilityDef> AllSpells =>
            allSpells ?? (allSpells = DefDatabase<AbilityDef>.AllDefs.Where(d => d.SpellProps() != null).ToList());

        public static CompProperties_Spell SpellProps(this AbilityDef def)
        {
            return def.comps.OfType<CompProperties_Spell>().FirstOrDefault();
        }

        public static bool IsUnlocked(CompProperties_Spell props, Hediff_Cultivation c)
        {
            if (c?.realm == null || props.unlockRealm == null)
                return false;
            return c.realm.order > props.unlockRealm.order
                   || (c.realm == props.unlockRealm && c.stage + 1 >= props.unlockStage);
        }

        /// <summary>依境界補上該會的法術（MVP 按境界領悟；Phase 2 改由功法決定）。</summary>
        public static void SyncSpells(Pawn pawn, bool sendMessage)
        {
            Hediff_Cultivation c = pawn.GetCultivation();
            if (c == null || pawn.abilities == null)
                return;
            foreach (AbilityDef def in AllSpells)
            {
                if (pawn.abilities.GetAbility(def) != null || !IsUnlocked(def.SpellProps(), c))
                    continue;
                pawn.abilities.GainAbility(def);
                if (sendMessage && PawnUtility.ShouldSendNotificationAbout(pawn))
                    Messages.Message("WD_SpellLearned".Translate(pawn.LabelShort, def.label), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>法術威力 = 境界係數 × 屬性相合 × 燃血。屬性越雜，相合加成越小。</summary>
        public static float Power(Pawn pawn, CompProperties_Spell props)
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            float power = pawn.GetCultivation()?.realm?.spellPower ?? 1f;
            Hediff_SpiritRoot root = pawn.GetSpiritRoot();
            if (root != null && root.elements.Any(e => props.elements.Contains(e)))
                power *= 1f + t.spellElementBonus / root.elements.Count;
            if (pawn.health.hediffSet.HasHediff(WD_DefOf.WD_BloodBurning))
                power *= t.bloodBurningSpellPower;
            return power;
        }

        public static IEnumerable<Hediff_Injury> Healable(Pawn p)
        {
            return p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i => !i.IsPermanent() && i.Severity > 0f);
        }

        /// <summary>癒合新傷，先治流血最兇的傷口。</summary>
        public static void HealInjuries(Pawn p, float total)
        {
            foreach (Hediff_Injury injury in Healable(p).OrderByDescending(i => i.BleedRate).ThenByDescending(i => i.Severity).ToList())
            {
                float amount = UnityEngine.Mathf.Min(total, injury.Severity);
                injury.Heal(amount);
                total -= amount;
                if (total <= 0f)
                    break;
            }
            if (p.Spawned)
                FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.HealingCross);
        }

        /// <summary>非徵召時只自動施放防禦法術：剛受到攻擊、真元足夠才用。</summary>
        public static void TryAutoDefend(Pawn pawn)
        {
            if (!pawn.Spawned || !pawn.IsColonistPlayerControlled || pawn.Drafted || pawn.Downed || !pawn.Awake()
                || pawn.abilities == null
                || Find.TickManager.TicksGame - pawn.mindState.lastHarmTick > WenDaoTuningDef.Get.autoDefendWindowTicks
                || BreakthroughUtility.IsBusyBreakingThrough(pawn))
                return;
            foreach (AbilityDef def in AllSpells)
            {
                CompProperties_Spell props = def.SpellProps();
                if (!props.autoCastDefensive)
                    continue;
                Ability ability = pawn.abilities.GetAbility(def);
                if (ability == null || !ability.CanCast || ability.GizmoDisabled(out _))
                    continue;
                ability.Activate(pawn, LocalTargetInfo.Invalid);
            }
        }
    }
}
