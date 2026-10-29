using System;
using System.Collections.Generic;
using System.IO;

namespace JsonOverride
{
    public static class JsonCache
    {
        private static readonly Dictionary<string, string> LoadedConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // โหลดและตรวจสอบไฟล์ JSON ทั้งหมดในโฟลเดอร์ Data มารอไว้ล่วงหน้า
        public static void PreloadAllJsons(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            string[] files = Directory.GetFiles(folderPath, "*.json");
            foreach (string file in files)
            {
                string configName = Path.GetFileNameWithoutExtension(file);
                try
                {
                    string content = File.ReadAllText(file);
                    
                    // ระบบตรวจจับความผิดพลาดขั้นพื้นฐาน (ป้องกัน JSON เสียหายจนเกมพัง)
                    if (!content.Trim().StartsWith("{") && !content.Trim().StartsWith("["))
                    {
                        JsonOverridePlugin.ModLogger.LogWarning($"[JsonOverride] Skip {configName}.json: Invalid JSON structure.");
                        continue;
                    }

                    LoadedConfigs[configName] = content;
                    JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Registered to cache: {configName}.json");
                }
                catch (Exception ex)
                {
                    JsonOverridePlugin.ModLogger.LogError($"[JsonOverride] Failed to cache {configName}.json: {ex.Message}");
                }
            }
        }

        // ดึงข้อความ JSON ออกมาใช้งานแบบด่วน
        public static bool TryGetConfig(string configName, out string jsonContent)
        {
            string cleanName = configName.Replace(".json", "");
            return LoadedConfigs.TryGetValue(cleanName, out jsonContent);
        }
    }
}
