using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace JsonOverride
{
    // Hooks that tell the patcher "the game is running, try to apply now".
    // The hooked methods are only used as timing triggers; their results are not changed.
    public static class GenericPatches
    {
        public static void InstallHooks(Harmony harmony)
        {
            HashSet<string> hooked = new HashSet<string>();

            // 1) The GameConfig getter of every table that has a patch file.
            for (int i = 0; i < JsonCache.Jobs.Count; i++)
            {
                string typeName = JsonCache.Jobs[i].TypeName;
                string shortName = typeName.Substring(typeName.LastIndexOf('.') + 1);
                if (hooked.Add(shortName))
                    Hook(harmony, "WuLin.GameConfig", "get_" + shortName, true);
            }

            // 2) Every other table getter on GameConfig. Some tables (for example CabNpcConfig)
            //    have no getter of their own there, and these are called often once a save is loaded.
            try
            {
                Type gc = Reflect.FindType("WuLin.GameConfig");
                if (gc != null)
                {
                    MethodInfo[] all = gc.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < all.Length; i++)
                    {
                        string n = all[i].Name;
                        if (n.StartsWith("get_") && n.EndsWith("ScriptObject") && hooked.Add(n.Substring(4)))
                            Hook(harmony, "WuLin.GameConfig", n, true);
                    }
                }
            }
            catch (Exception ex)
            {
                JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_EXTRA_HOOKS_FAILED: " + ex.Message);
            }

            // 3) Runs before the game works out how much EXP a character needs (level cap mod).
            Hook(harmony, "WuLin.GameCharacterInstance", "get_TotalExpToLevelUp", false);
        }

        private static void Hook(Harmony harmony, string typeName, string methodName, bool asPostfix)
        {
            try
            {
                Type t = Reflect.FindType(typeName);
                if (t == null)
                {
                    JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_HOOK_FAILED: type not found " + typeName);
                    return;
                }

                MethodInfo original = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (original == null)
                {
                    JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_HOOK_SKIPPED: no " + typeName + "." + methodName);
                    return;
                }

                MethodInfo trigger = typeof(Triggers).GetMethod("Trigger",
                    BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod hm = new HarmonyMethod(trigger);
                if (asPostfix) harmony.Patch(original, null, hm);
                else harmony.Patch(original, hm, null);
                JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_HOOK_OK: " + typeName + "." + methodName);
            }
            catch (Exception ex)
            {
                JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_HOOK_FAILED: " + typeName + "." + methodName + " : " + ex.Message);
            }
        }
    }

    public static class Triggers
    {
        private static int calls;

        // Harmony patch with no parameters: runs when a hooked game method is called.
        public static void Trigger()
        {
            try
            {
                if (Patcher.AllDone) return;
                calls++;
                // Try on each of the early calls, then only now and then.
                if (calls > 400 && (calls % 300) != 0) return;
                Patcher.TryApplyAll();
            }
            catch (Exception)
            {
                // never let an exception travel into the game
            }
        }
    }

    public static class Reflect
    {
        private static readonly Dictionary<string, Type> cache = new Dictionary<string, Type>();

        public static Type FindType(string name)
        {
            Type t;
            if (cache.TryGetValue(name, out t)) return t;

            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    t = asms[i].GetType(name, false);
                }
                catch (Exception)
                {
                    t = null;
                }
                if (t != null)
                {
                    cache[name] = t;
                    return t;
                }
            }
            return null;
        }

        public static object CreateScriptable(Type t)
        {
            Type so = FindType("UnityEngine.ScriptableObject");
            if (so == null) throw new InvalidOperationException("UnityEngine.ScriptableObject not found");
            MethodInfo[] ms = so.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].Name == "CreateInstance" && ms[i].IsGenericMethodDefinition &&
                    ms[i].GetParameters().Length == 0)
                {
                    return ms[i].MakeGenericMethod(t).Invoke(null, null);
                }
            }
            throw new InvalidOperationException("ScriptableObject.CreateInstance<T>() not found");
        }

        // Returns the first loaded instance of objType that already holds data.
        public static object FindLoadedObject(Type objType)
        {
            Type res = FindType("UnityEngine.Resources");
            if (res == null) return null;
            MethodInfo[] ms = res.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i].Name == "FindObjectsOfTypeAll" && ms[i].IsGenericMethodDefinition &&
                    ms[i].GetParameters().Length == 0)
                {
                    object arr = ms[i].MakeGenericMethod(objType).Invoke(null, null);
                    IEnumerable en = arr as IEnumerable;
                    if (en == null) return null;
                    foreach (object o in en)
                    {
                        if (o != null && Patcher.HasData(objType, o)) return o;
                    }
                    return null;
                }
            }
            return null;
        }

        public static void Set(Type t, object o, string prop, object value)
        {
            PropertyInfo p = t.GetProperty(prop);
            if (p == null) throw new MissingMemberException(t.Name, prop);
            p.SetValue(o, value, null);
        }
    }

    public static class Patcher
    {
        public static bool AllDone;
        private static bool busy;

        public static void TryApplyAll()
        {
            if (AllDone || busy) return;
            if (!JsonOverridePlugin.Enabled.Value) { AllDone = true; return; }

            busy = true;
            try
            {
                Type patchType = Reflect.FindType("BaseDataClassPatch");
                if (patchType == null) return;

                int pending = 0;
                for (int i = 0; i < JsonCache.Jobs.Count; i++)
                {
                    PatchJob job = JsonCache.Jobs[i];
                    if (job.Done) continue;

                    try
                    {
                        Type tableType = Reflect.FindType(job.TypeName);
                        if (tableType == null)
                        {
                            JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_NO_TYPE: " + job.TypeName +
                                " (" + Path.GetFileName(job.FilePath) + ")");
                            job.Done = true;
                            continue;
                        }

                        object table = Reflect.FindLoadedObject(tableType);
                        if (table == null) { pending++; continue; } // not loaded yet, retry later

                        ApplyJob(tableType, patchType, table, job);
                        job.Done = true;
                    }
                    catch (Exception ex)
                    {
                        job.Done = true; // do not retry forever
                        Exception inner = ex;
                        if (ex is TargetInvocationException && ex.InnerException != null) inner = ex.InnerException;
                        JsonOverridePlugin.ModLogger.LogError("[JsonOverride] DATA_PATCH_ERROR: " +
                            Path.GetFileName(job.FilePath) + " : " + inner);
                    }
                }

                if (pending == 0) AllDone = true;
            }
            finally
            {
                busy = false;
            }
        }

        private static void ApplyJob(Type tableType, Type patchType, object table, PatchJob job)
        {
            JObj root = job.Root;
            string name = Path.GetFileName(job.FilePath);
            int before = RowCount(tableType, table);
            JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_FOUND: " + job.TypeName +
                " rows=" + before + " file=" + name);

            float expMult = JsonOverridePlugin.ExpMultiplier.Value;
            float speedMult = JsonOverridePlugin.SpeedMultiplier.Value;

            List<object> newItems = root.GetList("newData");

            object patch = Reflect.CreateScriptable(patchType);
            Reflect.Set(patchType, patch, "type", job.TypeName);
            Reflect.Set(patchType, patch, "dataGroup", root.GetString("dataGroup") ?? "");

            // patchData: List<SingleDataPatcher>
            Type pdType = patchType.GetProperty("patchData").PropertyType;
            Type spType = pdType.GetGenericArguments()[0];
            object pdList = Activator.CreateInstance(pdType);
            MethodInfo pdAdd = pdType.GetMethod("Add");
            int patchCount = 0;
            string firstPatchKey = null;
            string firstPatchJson = null;
            List<object> patchItems = root.GetList("patchData");
            if (patchItems != null)
            {
                for (int i = 0; i < patchItems.Count; i++)
                {
                    JObj item = patchItems[i] as JObj;
                    if (item == null) continue;
                    string key = item.GetString("key");
                    string json = Scale(item.GetString("json"), expMult, speedMult);
                    object sp = Activator.CreateInstance(spType);
                    Reflect.Set(spType, sp, "key", key);
                    Reflect.Set(spType, sp, "json", json);
                    pdAdd.Invoke(pdList, new object[] { sp });
                    if (patchCount == 0) { firstPatchKey = key; firstPatchJson = json; }
                    patchCount++;
                }
            }

            // newData: List<string>. A row whose uid is not in the table yet is added.
            // A row whose uid already exists is applied as an edit instead (patchData),
            // so tables that already hold that row (for example LocData) still get the new values.
            Type ndType = patchType.GetProperty("newData").PropertyType;
            object ndList = Activator.CreateInstance(ndType);
            MethodInfo ndAdd = ndType.GetMethod("Add");
            int newCount = 0;
            int upsertCount = 0;
            if (newItems != null)
            {
                for (int i = 0; i < newItems.Count; i++)
                {
                    string s = newItems[i] as string;
                    if (s == null) continue;
                    string scaled = Scale(s, expMult, speedMult);

                    string uid = null;
                    try
                    {
                        JObj ro = MiniJson.Parse(s) as JObj;
                        uid = ro == null ? null : ro.GetString("uid");
                    }
                    catch (Exception)
                    {
                        uid = null;
                    }

                    if (uid != null && GetRow(tableType, table, uid) != null)
                    {
                        object sp = Activator.CreateInstance(spType);
                        Reflect.Set(spType, sp, "key", uid);
                        Reflect.Set(spType, sp, "json", scaled);
                        pdAdd.Invoke(pdList, new object[] { sp });
                        upsertCount++;
                    }
                    else
                    {
                        ndAdd.Invoke(ndList, new object[] { scaled });
                        newCount++;
                    }
                }
            }

            Reflect.Set(patchType, patch, "patchData", pdList);
            Reflect.Set(patchType, patch, "newData", ndList);

            JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_PATCH: " + name +
                " patchData=" + patchCount + " upserts=" + upsertCount + " newData=" + newCount);

            MethodInfo patchMethod = tableType.GetMethod("PathchData");
            if (patchMethod == null) throw new MissingMethodException(tableType.Name, "PathchData");
            patchMethod.Invoke(table, new object[] { patch });

            JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_APPLIED: " + job.TypeName +
                " rows " + before + " -> " + RowCount(tableType, table));

            // Verification lines for LogOutput.log
            if (firstPatchKey != null) LogRowFields(tableType, table, firstPatchKey, firstPatchJson);
            if (newItems != null && newItems.Count > 0)
            {
                LogNewRow(tableType, table, newItems[0] as string);
                LogNewRow(tableType, table, newItems[newItems.Count - 1] as string);
            }
        }

        public static bool HasData(Type tableType, object table)
        {
            PropertyInfo dp = tableType.GetProperty("data");
            if (dp == null) return true; // table without a "data" property: nothing to wait for
            object dict = dp.GetValue(table, null);
            if (dict == null) return false;
            PropertyInfo cp = dict.GetType().GetProperty("Count");
            if (cp == null) return true;
            return (int)cp.GetValue(dict, null) > 0;
        }

        private static int RowCount(Type tableType, object table)
        {
            PropertyInfo dp = tableType.GetProperty("data");
            if (dp == null) return 0;
            object dict = dp.GetValue(table, null);
            if (dict == null) return 0;
            PropertyInfo cp = dict.GetType().GetProperty("Count");
            if (cp == null) return 0;
            return (int)cp.GetValue(dict, null);
        }

        private static object GetRow(Type tableType, object table, string key)
        {
            MethodInfo mi = tableType.GetMethod("TryGetData", new Type[] { typeof(string) });
            if (mi == null) return null;
            return mi.Invoke(table, new object[] { key });
        }

        // Logs the current value of every field named in "json" for the row "key".
        private static void LogRowFields(Type tableType, object table, string key, string json)
        {
            try
            {
                object row = GetRow(tableType, table, key);
                if (row == null)
                {
                    JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_ROW key " + key + ": missing");
                    return;
                }
                JObj fields = MiniJson.Parse(json) as JObj;
                if (fields == null) return;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < fields.Count; i++)
                {
                    PropertyInfo p = row.GetType().GetProperty(fields[i].Key,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    object v = p == null ? null : p.GetValue(row, null);
                    sb.Append(fields[i].Key).Append('=').Append(v).Append(' ');
                }
                JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] DATA_PATCH_ROW key " + key + ": " + sb.ToString().Trim());
            }
            catch (Exception ex)
            {
                JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_ROW_ERROR key " + key + ": " + ex.Message);
            }
        }

        // Logs a row created from a "newData" entry, identified by its "uid".
        private static void LogNewRow(Type tableType, object table, string rowJson)
        {
            try
            {
                if (rowJson == null) return;
                JObj o = MiniJson.Parse(rowJson) as JObj;
                if (o == null) return;
                string uid = o.GetString("uid");
                if (uid == null) return;
                LogRowFields(tableType, table, uid, rowJson);
            }
            catch (Exception ex)
            {
                JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_ROW_ERROR: " + ex.Message);
            }
        }

        // Scales "expToNextLevel" and "Speed" inside one record's JSON text. Untouched when both multipliers are 1.
        private static string Scale(string json, float expMult, float speedMult)
        {
            if (json == null) return json;
            bool scaleExp = Math.Abs(expMult - 1f) > 0.0001f;
            bool scaleSpeed = Math.Abs(speedMult - 1f) > 0.0001f;
            if (!scaleExp && !scaleSpeed) return json;

            JObj o = MiniJson.Parse(json) as JObj;
            if (o == null) return json;

            bool relevant = false;
            for (int i = 0; i < o.Count; i++)
            {
                if ((scaleExp && o[i].Key == "expToNextLevel") || (scaleSpeed && o[i].Key == "Speed"))
                    relevant = true;
            }
            if (!relevant) return json;

            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            for (int i = 0; i < o.Count; i++)
            {
                string key = o[i].Key;
                string val = o[i].Value as string;
                if (val == null && o[i].Value != null)
                    val = Convert.ToString(o[i].Value, CultureInfo.InvariantCulture);
                if (val == null) val = "";

                if (scaleExp && key == "expToNextLevel")
                {
                    long v;
                    if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v > 0)
                    {
                        v = (long)(v * (double)expMult);
                        if (v < 1) v = 1;
                        if (v > int.MaxValue) v = int.MaxValue;
                        val = v.ToString(CultureInfo.InvariantCulture);
                    }
                }
                else if (scaleSpeed && key == "Speed")
                {
                    double d;
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0)
                    {
                        d = d * (double)speedMult;
                        if (d < 0.01) d = 0.01;
                        val = d.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                }

                sb.Append("  \"").Append(Esc(key)).Append("\": \"").Append(Esc(val)).Append("\"");
                if (i < o.Count - 1) sb.Append(",");
                sb.Append("\r\n");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
