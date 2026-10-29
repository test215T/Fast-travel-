using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace JsonOverride
{
    [BepInPlugin("com.modshare.runtime.jsonloader", "JsonOverride Pro", "2.5.0")]
    // เปลี่ยนตำแหน่งปลายทางมาเกาะที่โครงสร้างหลักสากลตามที่คุณมีในรูปภาพแน่นอน
    public class JsonOverridePlugin : BepInEx.Unity.IL2CPP.BasePlugin
    {
        public static BepInEx.Logging.ManualLogSource ModLogger;

        public override void Load()
        {
            ModLogger = Log;

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string pluginDir = Path.GetDirectoryName(assemblyLocation);
            string dataFolder = Path.Combine(pluginDir, "Data");

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            ModLogger.LogInfo("[JsonOverride] Initializing Pro System (IL2CPP Variant)...");
            
            JsonCache.PreloadAllJsons(dataFolder);

            var harmony = new Harmony("com.modshare.runtime.jsonloader");
            harmony.PatchAll();
            
            ModLogger.LogInfo("[JsonOverride] Injection Complete.");
        }
    }
}
