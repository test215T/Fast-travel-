using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JsonOverride
{
    // One data patch file: the decompiled form of a Workshop data mod
    // ("type", "dataGroup", "patchData", "newData").
    public sealed class PatchJob
    {
        public string FilePath;
        public JObj Root;
        public string TypeName;
        public bool Done;
    }

    public static class JsonCache
    {
        public static readonly List<PatchJob> Jobs = new List<PatchJob>();

        public static void PreloadAllJsons(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            string[] files = Directory.GetFiles(folderPath, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                string configName = Path.GetFileNameWithoutExtension(file);
                try
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);

                    if (!content.Trim().StartsWith("{") && !content.Trim().StartsWith("["))
                    {
                        JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] Skip " + configName + ".json: Invalid Structure.");
                        continue;
                    }

                    JObj root = MiniJson.Parse(content) as JObj;
                    string typeName = root == null ? null : root.GetString("type");
                    if (root == null || string.IsNullOrEmpty(typeName) ||
                        (root.GetList("patchData") == null && root.GetList("newData") == null))
                    {
                        JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_SKIP_FILE: " + configName +
                            ".json is not a data patch (needs type + patchData/newData).");
                        continue;
                    }

                    PatchJob job = new PatchJob();
                    job.FilePath = file;
                    job.Root = root;
                    job.TypeName = typeName;
                    Jobs.Add(job);

                    JsonOverridePlugin.ModLogger.LogInfo("[JsonOverride] Registered to cache: " + configName +
                        ".json -> " + typeName);
                }
                catch (Exception ex)
                {
                    JsonOverridePlugin.ModLogger.LogError("[JsonOverride] Failed to cache " + configName + ".json: " + ex.Message);
                }
            }

            if (Jobs.Count == 0)
            {
                JsonOverridePlugin.ModLogger.LogWarning("[JsonOverride] DATA_PATCH_NO_FILES: no patch .json found in " + folderPath);
                Patcher.AllDone = true;
            }
        }
    }

    // ---- Minimal JSON reader (objects, arrays, strings, numbers, true/false/null) ----

    public sealed class JObj : List<KeyValuePair<string, object>>
    {
        public object Get(string key)
        {
            for (int i = 0; i < Count; i++)
                if (this[i].Key == key) return this[i].Value;
            return null;
        }

        public string GetString(string key)
        {
            object v = Get(key);
            string s = v as string;
            if (s != null) return s;
            if (v == null) return null;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public List<object> GetList(string key)
        {
            return Get(key) as List<object>;
        }
    }

    public sealed class MiniJson
    {
        private readonly string s;
        private int p;

        private MiniJson(string text) { s = text; }

        public static object Parse(string text)
        {
            MiniJson j = new MiniJson(text);
            return j.Value();
        }

        private void Ws()
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
        }

        private object Value()
        {
            Ws();
            if (p >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[p];
            if (c == '{') return Obj();
            if (c == '[') return Arr();
            if (c == '"') return Str();
            if (c == 't') { Expect("true"); return true; }
            if (c == 'f') { Expect("false"); return false; }
            if (c == 'n') { Expect("null"); return null; }
            return Num();
        }

        private JObj Obj()
        {
            JObj o = new JObj();
            p++; // {
            Ws();
            if (s[p] == '}') { p++; return o; }
            while (true)
            {
                Ws();
                string k = Str();
                Ws();
                if (s[p] != ':') throw new FormatException("Expected ':' at " + p);
                p++;
                object v = Value();
                o.Add(new KeyValuePair<string, object>(k, v));
                Ws();
                if (s[p] == ',') { p++; continue; }
                if (s[p] == '}') { p++; return o; }
                throw new FormatException("Expected ',' or '}' at " + p);
            }
        }

        private List<object> Arr()
        {
            List<object> a = new List<object>();
            p++; // [
            Ws();
            if (s[p] == ']') { p++; return a; }
            while (true)
            {
                a.Add(Value());
                Ws();
                if (s[p] == ',') { p++; continue; }
                if (s[p] == ']') { p++; return a; }
                throw new FormatException("Expected ',' or ']' at " + p);
            }
        }

        private string Str()
        {
            if (s[p] != '"') throw new FormatException("Expected string at " + p);
            p++;
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                char c = s[p++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char e = s[p++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(s.Substring(p, 4), 16));
                            p += 4;
                            break;
                        default: sb.Append(e); break; // \" \\ \/
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private object Num()
        {
            int start = p;
            while (p < s.Length && "+-0123456789.eE".IndexOf(s[p]) >= 0) p++;
            if (p == start) throw new FormatException("Unexpected character '" + s[p] + "' at " + p);
            return double.Parse(s.Substring(start, p - start), CultureInfo.InvariantCulture);
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(s, p, word, 0, word.Length) != 0)
                throw new FormatException("Expected " + word + " at " + p);
            p += word.Length;
        }
    }
}
