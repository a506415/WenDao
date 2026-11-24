using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>靈根屬性：金木水火土，異屬性為風雷冰。</summary>
    public class SpiritElementDef : Def
    {
        public int order;
        public bool mutant;
    }

    /// <summary>靈根品階。數值全在 XML，方便調整。</summary>
    public class SpiritRootGradeDef : Def
    {
        public int order;
        public float commonality = 1f;
        public int elementCount;
        public bool mutant;
        public bool canCultivate = true;
        public FloatRange cultivationSpeed = FloatRange.Zero;

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (canCultivate && elementCount <= 0)
                yield return "canCultivate grade must have elementCount > 0";
            if (canCultivate && cultivationSpeed.max <= 0f)
                yield return "canCultivate grade must have cultivationSpeed > 0";
        }
    }

    [DefOf]
    public static class WD_DefOf
    {
        public static HediffDef WD_SpiritRoot;
        public static JobDef WD_TestSpiritRoot;

        static WD_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WD_DefOf));
        }
    }
}
