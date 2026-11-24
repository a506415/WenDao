using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 靈根。生成時決定、終生不變；測靈前不顯示在健康頁。
    /// </summary>
    public class Hediff_SpiritRoot : Hediff
    {
        public SpiritRootGradeDef grade;
        public List<SpiritElementDef> elements = new List<SpiritElementDef>();
        /// <summary>修煉速度倍率（品階範圍內擲出，終生不變）。</summary>
        public float cultivationSpeed;
        public bool revealed;

        public bool CanCultivate => grade != null && grade.canCultivate;

        public override bool Visible => revealed;

        public override string LabelBase => grade != null ? grade.label : base.LabelBase;

        public override string LabelInBrackets => ElementsLabel;

        public string ElementsLabel
        {
            get
            {
                if (elements.NullOrEmpty())
                    return null;
                return string.Join("WD_ElementSeparator".Translate().Resolve(),
                    elements.OrderBy(e => e.order).Select(e => e.label));
            }
        }

        public override string TipStringExtra
        {
            get
            {
                var sb = new StringBuilder(base.TipStringExtra);
                if (grade != null)
                {
                    if (!grade.description.NullOrEmpty())
                        sb.AppendLine(grade.description);
                    if (grade.canCultivate)
                        sb.AppendLine("WD_CultivationSpeed".Translate(cultivationSpeed.ToStringPercent()));
                    else
                        sb.AppendLine("WD_CannotCultivate".Translate());
                }
                return sb.ToString().TrimEndNewlines();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref grade, "grade");
            Scribe_Collections.Look(ref elements, "elements", LookMode.Def);
            Scribe_Values.Look(ref cultivationSpeed, "cultivationSpeed");
            Scribe_Values.Look(ref revealed, "revealed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (elements == null)
                    elements = new List<SpiritElementDef>();
                elements.RemoveAll(e => e == null);
            }
        }
    }
}
