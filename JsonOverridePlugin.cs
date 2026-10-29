using BepInEx;
using BepInEx.IL2CPP; // เปลี่ยนมาใช้คลาสสำหรับ IL2CPP บนมือถือโดยเฉพาะ
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace JsonOverride
{
    [BepInPlugin("com.modshare.runtime.jsonloader", "JsonOverride Pro", "2.5.0")]
    public class JsonOverridePlugin : BasePlugin // เปลี่ยนจาก BaseUnityPlugin เป็น BasePlugin ของ IL2CPP
    {
        public static BepInEx.Logging.ManualLogSource ModLogger;

        public override void Load() // สำหรับ IL2CPP จะใช้คำสั่ง Load() แทน Awake()
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
