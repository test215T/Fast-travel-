using HarmonyLib;
using UnityEngine;

namespace JsonOverride
{
    [HarmonyPatch(typeof(TextAsset), nameof(TextAsset.bytes), MethodType.Getter)]
    public class GenericJsonBytesPatch
    {
        static bool Prefix(TextAsset __instance, ref byte[] __result)
        {
            if (__instance == null) return true;

            string configName = __instance.name;
            if (JsonCache.TryGetConfig(configName, out string customJson))
            {
                __result = System.Text.Encoding.UTF8.GetBytes(customJson);
                JsonOverridePlugin.ModLogger.LogInfo($"[JsonOverride] Injected text data for asset: {configName}");
                return false; 
            }
            return true;
        }
    }
}
