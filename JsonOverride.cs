using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace JsonOverride
{
    [BepInPlugin("com.modshare.runtime.jsonloader", "JsonOverride Generic", "2.0.0")]
    public class JsonOverridePlugin : BaseUnityPlugin
    {
        public static string DataFolder { get; private set; }
        public static BepInEx.Logging.ManualLogSource ModLogger;

        private void Awake()
        {
            ModLogger = Logger;

            // ค้นหาตำแหน่งโฟลเดอร์ Data ที่อยู่คู่กับไฟล์ JsonOverride.dll
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string pluginDir = Path.GetDirectoryName(assemblyLocation);
            DataFolder = Path.Combine(pluginDir, "Data");

            // สร้างโฟลเดอร์อัตโนมัติหากยังไม่มีอยู่
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            ModLogger.LogInfo($"[JsonOverride] Generic System Active.");
            ModLogger.LogInfo($"[JsonOverride] Put ANY <FileName>.json into: {DataFolder}");

            var harmony = new Harmony("com.modshare.runtime.jsonloader");
            harmony.PatchAll();
        }
    }

    // ดักจับที่คลาสจัดการตารางข้อมูลหลักของเกม Hero's Adventure
    [HarmonyPatch(typeof(DataTableManager), "GetConfigText")] // ปรับชื่อฟังก์ชันตาม Method หลักที่เกมใช้ดึง Text/Asset
    public class GenericJsonPatch
    {
        static bool Prefix(string configName, ref string __result)
        {
            // ตัวอย่าง: ถ้าเกมเรียกหาคอนฟิก "Cfg_CharacterExp" เราจะหาไฟล์ "Cfg_CharacterExp.json"
            string targetFileName = configName.EndsWith(".json") ? configName : $"{configName}.json";
            string filePath = Path.Combine(JsonOverridePlugin.DataFolder, targetFileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string customJson = File.ReadAllText(filePath);
                    if (!string.IsNullOrEmpty(customJson))
                    {
                        __result = customJson;
                        JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Successfully intercepted & injected: {targetFileName}");
                        return false; // ข้ามการโหลดจาก TextAsset ดั้งเดิมของเกมไปเลย
                    }
                }
                catch (System.Exception ex)
                {
                    JsonOverridePlugin.ModLogger.LogError($"[JsonOverride] Error reading {targetFileName}: {ex.Message}");
                }
            }

            return true; // หากไม่มีไฟล์นอกเกม ให้ทำตามกระบวนการปกติของเกม
        }
    }
}
