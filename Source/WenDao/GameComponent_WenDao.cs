using RimWorld;
using Verse;

namespace WenDao
{
    /// <summary>
    /// 舊存檔中途加入本 mod 時，替已存在、尚無靈根的人形小人補擲靈根。
    /// </summary>
    public class GameComponent_WenDao : GameComponent
    {
        public GameComponent_WenDao(Game game)
        {
        }

        public override void FinalizeInit()
        {
            int given = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (SpiritRootUtility.CanHaveSpiritRoot(pawn) && pawn.GetSpiritRoot() == null)
                {
                    SpiritRootUtility.TryGiveOnGeneration(pawn);
                    given++;
                }
                // 舊存檔的修士補上已達境界的法術。
                if (!pawn.Dead)
                    SpellUtility.SyncSpells(pawn, false);
            }
            if (given > 0)
                Log.Message("[WenDao] Gave spirit roots to " + given + " existing pawns.");
        }
    }
}
