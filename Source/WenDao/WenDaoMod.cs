using HarmonyLib;
using Verse;

namespace WenDao
{
    public class WenDaoMod : Mod
    {
        public const string HarmonyId = "a506415.wendao";

        public static WenDaoMod Instance { get; private set; }

        public WenDaoMod(ModContentPack content) : base(content)
        {
            Instance = this;
            new Harmony(HarmonyId).PatchAll(typeof(WenDaoMod).Assembly);
        }
    }

    // Runs after defs and translations are loaded, so Keyed strings resolve here.
    [StaticConstructorOnStartup]
    public static class WenDaoStartup
    {
        static WenDaoStartup()
        {
            string version = typeof(WenDaoMod).Assembly.GetName().Version.ToString(3);
            Log.Message("[WenDao] " + "WD_ModLoaded".Translate(version));
        }
    }
}
