using HarmonyLib;

namespace JsonOverride
{
    [HarmonyPatch(typeof(DataTableManager), "GetConfigText")] 
    public class GenericJsonPatch
    {
        static bool Prefix(string configName, ref string __result)
        {
            // ตรวจสอบใน Cache ด่วนบน RAM ถ้าเจอไฟล์เขียนทับ ให้ทำงานทันที
            if (JsonCache.TryGetConfig(configName, out string customJson))
            {
                __result = customJson;
                JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Injected data from memory for: {configName}");
                return false; // สั่งข้ามคำสั่งเดิมของเกม
            }

            return true; // ปล่อยให้เกมทำตามกระบวนการปกติถ้าไม่มีมอดไฟล์นั้น
        }
    }
}
