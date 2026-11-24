using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 右下角遊玩設定列加一個靈氣 overlay 開關。原版沒有讓 mod 加按鈕的 XML 介面，
    /// 所以只能 postfix PlaySettings.DoPlaySettingsGlobalControls（其他 mod 也是這樣做）。
    /// </summary>
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings_QiOverlay
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null)
                return;
            Texture2D icon = WD_DefOf.WD_MeditationCushion.uiIcon ?? BaseContent.BadTex;
            row.ToggleableIcon(ref MapComponent_SpiritQi.showOverlay, icon,
                "WD_ToggleQiOverlay".Translate(), SoundDefOf.Mouseover_ButtonToggle);
        }
    }

    /// <summary>擺放蒲團時自動顯示靈氣 overlay，並在游標格標出濃度。</summary>
    public class PlaceWorker_ShowSpiritQi : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            MapComponent_SpiritQi grid = MapComponent_SpiritQi.For(map);
            if (grid == null)
                return;
            grid.DrawOverlayThisFrame();
        }
    }
}
