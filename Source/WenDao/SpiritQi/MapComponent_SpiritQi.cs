using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace WenDao
{
    /// <summary>
    /// 每張地圖的天地靈氣格網。baseQi 為地脈基準，arrayBonus 為聚靈陣加成（取最強、不疊加、不存檔），
    /// qi 為目前濃度：打坐者會抽走，之後逐步趨近「基準 + 加成」。
    /// </summary>
    public class MapComponent_SpiritQi : MapComponent, ICellBoolGiver
    {
        private const int UpdateInterval = 250;
        private const float SaveScale = 1000f;

        /// <summary>overlay 開關（全域、不存檔）。</summary>
        public static bool showOverlay;

        private float[] baseQi;
        private float[] qi;
        private bool generated;
        private float[] arrayBonus;
        private readonly Dictionary<CompQiArray, float> arrays = new Dictionary<CompQiArray, float>();
        /// <summary>濃度尚未回到目標值的格子。</summary>
        private readonly HashSet<int> unsettled = new HashSet<int>();
        private CellBoolDrawer drawer;

        public MapComponent_SpiritQi(Map map) : base(map)
        {
        }

        public static MapComponent_SpiritQi For(Map map) => map?.GetComponent<MapComponent_SpiritQi>();

        private int NumCells => map.cellIndices.NumGridCells;

        private void EnsureArrays()
        {
            if (baseQi == null || baseQi.Length != NumCells)
                baseQi = new float[NumCells];
            if (qi == null || qi.Length != NumCells)
                qi = new float[NumCells];
            if (arrayBonus == null || arrayBonus.Length != NumCells)
                arrayBonus = new float[NumCells];
        }

        private float TargetQi(int i) => baseQi[i] + arrayBonus[i];

        public void RegisterArray(CompQiArray array) => arrays[array] = 0f;

        public void UnregisterArray(CompQiArray array)
        {
            if (arrays.Remove(array))
                RecomputeArrays();
        }

        /// <summary>陣法供能狀態或強度變了才重算。</summary>
        private void UpdateArrays()
        {
            bool changed = false;
            foreach (KeyValuePair<CompQiArray, float> kv in arrays)
            {
                if (kv.Key.ActiveStrength != kv.Value)
                {
                    changed = true;
                    break;
                }
            }
            if (changed)
                RecomputeArrays();
        }

        private void RecomputeArrays()
        {
            if (baseQi == null)
                return;
            EnsureArrays();
            var fresh = new float[NumCells];
            foreach (CompQiArray array in new List<CompQiArray>(arrays.Keys))
            {
                float s = array.ActiveStrength;
                arrays[array] = s;
                if (s <= 0f)
                    continue;
                foreach (IntVec3 c in GenRadial.RadialCellsAround(array.parent.Position, array.Props.radius, true))
                {
                    if (!c.InBounds(map))
                        continue;
                    int i = map.cellIndices.CellToIndex(c);
                    fresh[i] = Mathf.Max(fresh[i], s);
                }
            }
            for (int i = 0; i < fresh.Length; i++)
            {
                if (fresh[i] != arrayBonus[i])
                    unsettled.Add(i);
            }
            arrayBonus = fresh;
        }

        public override void FinalizeInit()
        {
            EnsureArrays();
            if (!generated)
                Generate();
            RecomputeArrays();
            unsettled.Clear();
            for (int i = 0; i < NumCells; i++)
            {
                if (Mathf.Abs(qi[i] - TargetQi(i)) > 0.001f)
                    unsettled.Add(i);
            }
        }

        private void Generate()
        {
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            int seed = Gen.HashCombineInt(Find.World?.info?.Seed ?? 0, map.uniqueID);
            Rand.PushState(seed);
            float mapBase = t.mapBaseQi.RandomInRange;
            Rand.PopState();

            var noise = new Perlin(t.qiNoiseFrequency, 2.0, 0.5, 4, seed, QualityMode.Medium);
            foreach (IntVec3 c in map.AllCells)
            {
                int i = map.cellIndices.CellToIndex(c);
                float v = mapBase + (float)noise.GetValue(c.x, 0, c.z) * t.qiNoiseAmplitude;
                baseQi[i] = Mathf.Max(t.minQi, v);
                qi[i] = baseQi[i];
            }
            generated = true;
        }

        public float QiAt(IntVec3 c)
        {
            if (qi == null || !c.InBounds(map))
                return 0f;
            return qi[map.cellIndices.CellToIndex(c)];
        }

        public float BaseQiAt(IntVec3 c)
        {
            if (baseQi == null || !c.InBounds(map))
                return 0f;
            return TargetQi(map.cellIndices.CellToIndex(c));
        }

        /// <summary>打坐者每 <paramref name="ticks"/> tick 呼叫一次，抽取周圍靈氣。</summary>
        public void Drain(IntVec3 center, int ticks)
        {
            if (qi == null)
                return;
            WenDaoTuningDef t = WenDaoTuningDef.Get;
            float amount = t.qiDrainPerHour * ticks / GenDate.TicksPerHour;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, t.qiDrainRadius, true))
            {
                if (!c.InBounds(map))
                    continue;
                int i = map.cellIndices.CellToIndex(c);
                qi[i] = Mathf.Max(t.minQi, qi[i] - amount);
                unsettled.Add(i);
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % UpdateInterval != 0)
                return;
            UpdateArrays();
            if (unsettled.Count == 0)
                return;
            float frac = WenDaoTuningDef.Get.qiRegenFractionPerHour * UpdateInterval / GenDate.TicksPerHour;
            unsettled.RemoveWhere(i =>
            {
                float gap = TargetQi(i) - qi[i];
                if (Mathf.Abs(gap) <= 0.001f)
                {
                    qi[i] = TargetQi(i);
                    return true;
                }
                qi[i] += gap * frac;
                return false;
            });
            if (showOverlay)
                drawer?.SetDirty();
        }

        // ===== overlay =====

        private CellBoolDrawer Drawer => drawer ?? (drawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3600, 0.45f));

        public void DrawOverlayThisFrame()
        {
            Drawer.MarkForDraw();
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || qi == null)
                return;
            if (showOverlay)
                Drawer.MarkForDraw();
            drawer?.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
            if (!showOverlay || Find.CurrentMap != map || qi == null)
                return;
            IntVec3 c = UI.MouseCell();
            if (!c.InBounds(map) || c.Fogged(map))
                return;
            Vector2 pos = Event.current.mousePosition;
            string text = "WD_QiAtCell".Translate(QiAt(c).ToString("0.00"), BaseQiAt(c).ToString("0.00"));
            Text.Font = GameFont.Small;
            Vector2 size = Text.CalcSize(text);
            var rect = new Rect(pos.x + 18f, pos.y + 8f, size.x + 10f, size.y + 4f);
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.6f));
            Widgets.Label(rect.ContractedBy(5f, 2f), text);
        }

        public Color Color => Color.white;

        public bool GetCellBool(int index)
        {
            return qi != null && !map.fogGrid.IsFogged(index);
        }

        public Color GetCellExtraColor(int index)
        {
            return QiColor(qi[index]);
        }

        /// <summary>低→暗紅、1.0→綠、高→青白。</summary>
        public static Color QiColor(float v)
        {
            var low = new Color(0.45f, 0.1f, 0.1f);
            var mid = new Color(0.2f, 0.75f, 0.3f);
            var high = new Color(0.6f, 0.95f, 1f);
            return v < 1f ? Color.Lerp(low, mid, v) : Color.Lerp(mid, high, Mathf.Clamp01(v - 1f));
        }

        // ===== save =====

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generated, "generated");
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.Saving)
            {
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                    EnsureArrays();
                if (generated)
                {
                    MapExposeUtility.ExposeUshort(map,
                        c => ToUshort(baseQi[map.cellIndices.CellToIndex(c)]),
                        (c, v) => baseQi[map.cellIndices.CellToIndex(c)] = v / SaveScale,
                        "baseQi");
                    MapExposeUtility.ExposeUshort(map,
                        c => ToUshort(qi[map.cellIndices.CellToIndex(c)]),
                        (c, v) => qi[map.cellIndices.CellToIndex(c)] = v / SaveScale,
                        "qi");
                }
            }
        }

        private static ushort ToUshort(float v)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(v * SaveScale), 0, ushort.MaxValue);
        }
    }
}
