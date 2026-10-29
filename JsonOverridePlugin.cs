using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace JsonOverride
{
    [BepInPlugin("com.modshare.runtime.jsonloader", "JsonOverride Pro", "2.5.0")]
    public class JsonOverridePlugin : BaseUnityPlugin
    {
        public static BepInEx.Logging.ManualLogSource ModLogger;

        private void Awake()
        {
            ModLogger = Logger;

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string pluginDir = Path.GetDirectoryName(assemblyLocation);
            string dataFolder = Path.Combine(pluginDir, "Data");

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            ModLogger.LogInfo($"[JsonOverride] Initializing Pro System...");
            
            // เรียกฟังก์ชันโหลดไฟล์ JSON มารอบน RAM ทันที
            JsonCache.PreloadAllJsons(dataFolder);

            var harmony = new Harmony("com.modshare.runtime.jsonloader");
            harmony.PatchAll();
            
            ModLogger.LogInfo($"[JsonOverride] Injection Complete.");
        }
    }
}
