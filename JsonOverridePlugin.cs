using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace JsonOverride
{
    // v3: same plugin id, name and Data folder as before. The old TextAsset hooks never fired
    // (the game's data tables are not TextAssets), so JSON files are now applied to the game's
    // own tables through TableType.PathchData(BaseDataClassPatch), the same call the Workshop
    // mod loader uses.
    [BepInPlugin("com.modshare.runtime.jsonloader", "JsonOverride Pro", "3.0.0")]
    public class JsonOverridePlugin : BepInEx.Unity.IL2CPP.BasePlugin
    {
        public static BepInEx.Logging.ManualLogSource ModLogger;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ExpMultiplier;
        internal static ConfigEntry<float> SpeedMultiplier;
        internal static ConfigEntry<bool> FillThaiFromEnglish;

        public override void Load()
        {
            ModLogger = Log;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the plugin on or off.");
            ExpMultiplier = Config.Bind("General", "ExpMultiplier", 1.0f,
                "Multiplies every expToNextLevel value read from the JSON files (values above 0 only). " +
                "1 = use the JSON as is, 0.1 = one tenth. Minimum result is 1.");
            SpeedMultiplier = Config.Bind("General", "SpeedMultiplier", 1.0f,
                "Multiplies every Speed value read from the JSON files (values above 0 only). " +
                "1 = use the JSON as is, 0.5 = half of the JSON value.");

            FillThaiFromEnglish = Config.Bind("General", "FillThaiFromEnglish", true,
                "New LocData rows that have no Thai (TH) text get their English text as TH, " +
                "so names and descriptions are not blank when the game runs in Thai.");

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string pluginDir = Path.GetDirectoryName(assemblyLocation);
            string dataFolder = Path.Combine(pluginDir, "Data");

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            ModLogger.LogInfo("[JsonOverride] Initializing Pro System v3 (IL2CPP data table patcher)...");

            JsonCache.PreloadAllJsons(dataFolder);

            Harmony harmony = new Harmony("com.modshare.runtime.jsonloader");
            GenericPatches.InstallHooks(harmony);

            ModLogger.LogInfo("[JsonOverride] DATA_PATCH_READY: enabled=" + Enabled.Value +
                " jobs=" + JsonCache.Jobs.Count +
                " expMult=" + ExpMultiplier.Value + " speedMult=" + SpeedMultiplier.Value);
        }
    }
}
