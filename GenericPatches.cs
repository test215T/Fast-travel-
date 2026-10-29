using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays; // ดึงตัวแปรโครงสร้าง IL2CPP เข้ามาใช้งานตรงๆ

namespace JsonOverride
{
    // จุดดักจับที่ 1: ดักจับเวลาเกมเรียกอ่านข้อมูลแบบข้อความสากล (TextAsset.text)
    [HarmonyPatch(typeof(TextAsset), nameof(TextAsset.text), MethodType.Getter)]
    public class GenericJsonTextPatch
    {
        static bool Prefix(TextAsset __instance, ref string __result)
        {
            if (__instance == null) return true;

            string configName = __instance.name;
            if (JsonCache.TryGetConfig(configName, out string customJson))
            {
                __result = customJson;
                JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Injected text data for asset: {configName}");
                return false; 
            }
            return true;
        }
    }

    // จุดดักจับที่ 2: ดักจับเวลาเกมเรียกอ่านข้อมูลแบบดิบไบต์ (แก้ทางตาม Error ใน Log)
    [HarmonyPatch(typeof(TextAsset), nameof(TextAsset.bytes), MethodType.Getter)]
    public class GenericJsonBytesPatch
    {
        static bool Prefix(TextAsset __instance, ref Il2CppStructArray<byte> __result)
        {
            if (__instance == null) return true;

            string configName = __instance.name;
            if (JsonCache.TryGetConfig(configName, out string customJson))
            {
                // แปลงข้อความสากลเป็น Byte Array ของระบบ IL2CPP โดยเฉพาะ
                byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(customJson);
                __result = new Il2CppStructArray<byte>(rawBytes);
                
                JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Injected byte data for asset: {configName}");
                return false; 
            }
            return true;
        }
    }
}
