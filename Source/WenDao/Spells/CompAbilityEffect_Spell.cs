using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>每個法術必備：真元消耗、折壽、領悟條件、屬性。</summary>
    public class CompProperties_Spell : CompProperties_AbilityEffect
    {
        public float zhenyuanCost;
        public float lifespanCost;
        public CultivationRealmDef unlockRealm;
        /// <summary>從 1 起算的小階。</summary>
        public int unlockStage = 1;
        public List<SpiritElementDef> elements = new List<SpiritElementDef>();
        public bool autoCastDefensive;

        public CompProperties_Spell()
        {
            compClass = typeof(CompAbilityEffect_Spell);
        }

        public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
                yield return e;
            if (unlockRealm == null)
                yield return "spell has no unlockRealm";
        }
    }

    public class CompAbilityEffect_Spell : CompAbilityEffect
    {
        public new CompProperties_Spell Props => (CompProperties_Spell)props;

        private Need Zhenyuan => parent.pawn.needs?.TryGetNeed(WD_DefOf.WD_Zhenyuan);

        private bool EnoughZhenyuan => Props.zhenyuanCost <= 0f || (Zhenyuan?.CurLevel ?? 0f) >= Props.zhenyuanCost;

        private float LifespanLeft => parent.pawn.GetCultivation()?.LifespanRemainingYears ?? 0f;

        private bool EnoughLifespan => Props.lifespanCost <= 0f || LifespanLeft > Props.lifespanCost;

        public override bool CanCast => EnoughZhenyuan && EnoughLifespan;

        public override bool GizmoDisabled(out string reason)
        {
            if (!EnoughZhenyuan)
            {
                reason = "WD_NotEnoughZhenyuan".Translate(Props.zhenyuanCost.ToString("0.#"));
                return true;
            }
            if (!EnoughLifespan)
            {
                reason = "WD_NotEnoughLifespan".Translate(Props.lifespanCost.ToString("0.#"));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Need n = Zhenyuan;
            if (n != null)
                n.CurLevel -= Props.zhenyuanCost;
            if (Props.lifespanCost > 0f)
                parent.pawn.GetCultivation()?.LoseLifespan(Props.lifespanCost);
        }

        public override Window ConfirmationDialog(LocalTargetInfo target, Action confirmAction)
        {
            if (Props.lifespanCost <= 0f)
                return null;
            return Dialog_MessageBox.CreateConfirmation("WD_ConfirmLifespanSpell".Translate(parent.pawn.LabelShort, parent.def.label,
                Props.lifespanCost.ToString("0.#"), LifespanLeft.ToString("0.#")), confirmAction, destructive: true);
        }

        public override string ExtraTooltipPart()
        {
            var lines = new List<string>();
            if (Props.zhenyuanCost > 0f)
                lines.Add("WD_SpellCost".Translate(Props.zhenyuanCost.ToString("0.#")));
            if (Props.lifespanCost > 0f)
                lines.Add("WD_SpellLifespanCost".Translate(Props.lifespanCost.ToString("0.#")));
            if (Props.elements.Count > 0)
                lines.Add("WD_SpellElements".Translate(string.Join("WD_ElementSeparator".Translate().Resolve(),
                    Props.elements.Select(e => e.label))));
            lines.Add("WD_SpellPower".Translate(SpellUtility.Power(parent.pawn, Props).ToStringPercent()));
            if (Props.autoCastDefensive)
                lines.Add("WD_SpellAutoCast".Translate());
            return string.Join("\n", lines);
        }
    }

    /// <summary>法術效果共用：取得威力。</summary>
    public abstract class CompAbilityEffect_SpellEffect : CompAbilityEffect
    {
        protected float Power => SpellUtility.Power(parent.pawn, parent.def.SpellProps());
    }

    // ---- 投射物 ----

    public class CompProperties_SpellProjectile : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;

        public CompProperties_SpellProjectile()
        {
            compClass = typeof(CompAbilityEffect_SpellProjectile);
        }
    }

    public class CompAbilityEffect_SpellProjectile : CompAbilityEffect_SpellEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            var projectile = (Projectile)GenSpawn.Spawn(((CompProperties_SpellProjectile)props).projectileDef, pawn.Position, pawn.Map);
            if (projectile is ISpellProjectile spell)
                spell.Power = Power;
            projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
        }
    }

    // ---- 給自己上 hediff ----

    public class CompProperties_SpellSelfHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediff;
        public int durationTicks = 1200;
        /// <summary>持續時間是否隨威力延長。</summary>
        public bool durationScales = true;

        public CompProperties_SpellSelfHediff()
        {
            compClass = typeof(CompAbilityEffect_SpellSelfHediff);
        }
    }

    public class CompAbilityEffect_SpellSelfHediff : CompAbilityEffect_SpellEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var p = (CompProperties_SpellSelfHediff)props;
            Pawn pawn = parent.pawn;
            float power = p.durationScales ? Power : 1f;
            Hediff old = pawn.health.hediffSet.GetFirstHediffOfDef(p.hediff);
            if (old != null)
                pawn.health.RemoveHediff(old);
            Hediff h = HediffMaker.MakeHediff(p.hediff, pawn);
            HediffComp_Disappears d = h.TryGetComp<HediffComp_Disappears>();
            if (d != null)
                d.ticksToDisappear = UnityEngine.Mathf.RoundToInt(p.durationTicks * power);
            pawn.health.AddHediff(h);
            if (pawn.Spawned)
                FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.Map, 1.5f);
        }
    }

    // ---- 治癒 ----

    public class CompProperties_SpellHeal : CompProperties_AbilityEffect
    {
        public float healAmount = 12f;

        public CompProperties_SpellHeal()
        {
            compClass = typeof(CompAbilityEffect_SpellHeal);
        }
    }

    public class CompAbilityEffect_SpellHeal : CompAbilityEffect_SpellEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn p = target.Pawn;
            if (p == null || !p.RaceProps.IsFlesh || !SpellUtility.Healable(p).Any())
            {
                if (throwMessages && p != null)
                    Messages.Message("WD_NothingToHeal".Translate(p.LabelShort), p, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn p = target.Pawn;
            if (p == null)
                return;
            SpellUtility.HealInjuries(p, ((CompProperties_SpellHeal)props).healAmount * Power);
        }
    }

    // ---- 回滿真元 ----

    public class CompProperties_SpellRefillZhenyuan : CompProperties_AbilityEffect
    {
        public CompProperties_SpellRefillZhenyuan()
        {
            compClass = typeof(CompAbilityEffect_SpellRefillZhenyuan);
        }
    }

    public class CompAbilityEffect_SpellRefillZhenyuan : CompAbilityEffect_SpellEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Need n = parent.pawn.needs?.TryGetNeed(WD_DefOf.WD_Zhenyuan);
            if (n != null)
                n.CurLevel = n.MaxLevel;
        }
    }
}
