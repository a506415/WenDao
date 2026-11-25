using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    public interface ISpellProjectile
    {
        float Power { set; }
    }

    /// <summary>爆炸型法術投射物（火球），傷害隨施法者威力。</summary>
    public class Projectile_SpellExplosive : Projectile_Explosive, ISpellProjectile
    {
        private float power = 1f;

        public float Power { set => power = value; }

        public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * power);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref power, "power", 1f);
        }
    }

    /// <summary>單體法術投射物（飛劍），傷害隨施法者威力。</summary>
    public class Projectile_SpellBullet : Bullet, ISpellProjectile
    {
        private float power = 1f;

        public float Power { set => power = value; }

        public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * power);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref power, "power", 1f);
        }
    }
}
