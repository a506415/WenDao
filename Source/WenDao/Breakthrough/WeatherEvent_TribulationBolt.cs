using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WenDao
{
    /// <summary>天劫落雷的視覺與音效。原版 LightningStrike 必帶火焰爆炸，故自行繪製。</summary>
    [StaticConstructorOnStartup]
    public class WeatherEvent_TribulationBolt : WeatherEvent_LightningFlash
    {
        private static readonly Material BoltMat = MatLoader.LoadMat("Weather/LightningBolt");

        private readonly IntVec3 cell;
        private Mesh boltMesh;

        public WeatherEvent_TribulationBolt(Map map, IntVec3 cell) : base(map)
        {
            this.cell = cell;
        }

        public override void FireEvent()
        {
            base.FireEvent();
            boltMesh = LightningBoltMeshPool.RandomBoltMesh;
            SoundDefOf.Thunder_OnMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(cell, map)));
            Vector3 loc = cell.ToVector3Shifted();
            for (int i = 0; i < 4; i++)
            {
                FleckMaker.ThrowSmoke(loc, map, 1.5f);
                FleckMaker.ThrowMicroSparks(loc, map);
                FleckMaker.ThrowLightningGlow(loc, map, 1.5f);
            }
        }

        public override void WeatherEventDraw()
        {
            Graphics.DrawMesh(boltMesh, cell.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather), Quaternion.identity,
                FadedMaterialPool.FadedVersionOf(BoltMat, LightningBrightness), 0);
        }
    }
}
