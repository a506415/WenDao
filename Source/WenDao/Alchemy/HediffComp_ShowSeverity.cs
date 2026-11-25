using Verse;

namespace WenDao
{
    /// <summary>在健康頁標籤的括號內顯示嚴重度百分比。</summary>
    public class HediffComp_ShowSeverity : HediffComp
    {
        public override string CompLabelInBracketsExtra => parent.Severity.ToStringPercent();
    }
}
