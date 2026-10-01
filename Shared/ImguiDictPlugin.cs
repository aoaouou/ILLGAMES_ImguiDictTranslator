using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace IL2CPPImguiDict
{
    // Translates IMGUI texts (ConfigurationManager etc.) on IL2CPP games via a
    // static dictionary. Hooks the interop proxy methods of GUI/GUILayout that
    // take GUIContent (universal __args prefix) and string "text" params
    // (ref-string prefix). Dictionary: Translation/zh-CN/Text/*.txt (key=value,
    // {{A}}/{{B}} templates allowed on both sides, occurrence-order mapping).
    // v1.2: diagnostic logging of first 40 unique texts seen by the hooks;
    // handles CM collapsed-title suffix "\n..."; whitespace-tolerant fallback.
    [BepInPlugin("workbuddy.imguidict", "IL2CPP IMGUI Dictionary Translator", "1.3.0")]
    public class ImguiDictPlugin : BasePlugin
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<KeyValuePair<Regex, string>> Tmpl = new List<KeyValuePair<Regex, string>>();
        private static bool _enabled = true;
        private static long _hits;
        private static long _calls;
        private static readonly HashSet<string> _seen = new HashSet<string>();
        internal static ManualLogSource DiagLog;

        public override void Load()
        {
            DiagLog = Log;
            try
            {
                _enabled = Config.Bind("General", "Enabled", true, "Enable IMGUI dictionary translation").Value;

                var textDir = Path.Combine(Paths.BepInExRootPath, "Translation", "zh-CN", "Text");
                if (!Directory.Exists(textDir))
                {
                    Log.LogWarning("Translation dir not found: " + textDir);
                    return;
                }

                var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
                int files = 0;
                foreach (var f in Directory.EnumerateFiles(textDir, "*.txt", opts))
                {
                    var name = Path.GetFileName(f);
                    if (name.StartsWith("_")) continue;
                    try { LoadDictFile(f); files++; }
                    catch (Exception e) { Log.LogWarning("Dict parse failed [" + name + "]: " + e.Message); }
                }

                var harmony = new Harmony("workbuddy.imguidict");
                int patched = 0, failed = 0;
                var prefixContent = typeof(ImguiDictPlugin).GetMethod(nameof(ContentPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                var prefixString = typeof(ImguiDictPlugin).GetMethod(nameof(StringPrefix), BindingFlags.NonPublic | BindingFlags.Static);

                foreach (var t in new[] { typeof(global::UnityEngine.GUI), typeof(global::UnityEngine.GUILayout) })
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        var ps = m.GetParameters();
                        if (!ps.Any(p => p.ParameterType == typeof(global::UnityEngine.GUIContent))
                            && !ps.Any(p => p.ParameterType == typeof(string) && (p.Name == "text" || p.Name == "label")))
                            continue;
                        if (m.ContainsGenericParameters) { failed++; continue; }
                        var hasContent = ps.Any(p => p.ParameterType == typeof(global::UnityEngine.GUIContent));
                        try
                        {
                            harmony.Patch(m, prefix: new HarmonyMethod(hasContent ? prefixContent : prefixString));
                            patched++;
                        }
                        catch (Exception e) { failed++; Log.LogDebug("Patch failed " + t.Name + "." + m.Name + ": " + e.Message); }
                    }
                }

                Log.LogInfo($"IMGUI dict translator v1.3: files={files}, exact={Exact.Count}, tmpl={Tmpl.Count}, patched={patched}, failed={failed}. Diagnostic: first 40 unique texts will be logged as [DIAG].");
            }
            catch (Exception e)
            {
                Log.LogError("Load failed: " + e);
            }
        }

        private static void Diag(string t)
        {
            if (t != null && (t.Contains("FPS,") || (t.Length > 0 && t[0] >= '0' && t[0] <= '9'))) return;
            _calls++;
            if (_seen.Count < 40 && _seen.Add(t))
                DiagLog.LogWarning("[DIAG " + _calls + "] text: " + t.Replace("\n", "\\n").Replace("\r", ""));
        }

        private static void LoadDictFile(string path)
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith(";") || line.StartsWith("r:")) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;
                if (key.Contains("{{"))
                {
                    Tmpl.Add(new KeyValuePair<Regex, string>(BuildTemplateRegex(key), val));
                }
                else
                {
                    Exact[key] = val;
                }
            }
        }

        private static Regex BuildTemplateRegex(string key)
        {
            // 先把占位符换成哨兵字符再转义（Regex.Escape 转义 { 但不转义 }，直接在转义串上替换会失配）
            var sentinel = Regex.Replace(key, @"\{\{[A-Za-z]\}\}", "\uE000");
            var escaped = Regex.Escape(sentinel).Replace("\uE000", "([0-9]+(?:\\.[0-9]+)*)");
            return new Regex("^" + escaped + "$", RegexOptions.CultureInvariant);
        }

        private static string ApplyTemplate(string tmplVal, Match m)
        {
            int n = 0;
            return Regex.Replace(tmplVal, @"\{\{[A-Za-z]\}\}", _ => m.Groups[++n].Value);
        }

        private static string Lookup(string t)
        {
            if (Exact.TryGetValue(t, out var cn)) return cn;
            var trimmed = t.Trim();
            if (trimmed.Length != t.Length && Exact.TryGetValue(trimmed, out var cn2))
            {
                var head = t.Substring(0, t.Length - t.TrimStart().Length);
                var tail = t.Substring(t.TrimEnd().Length);
                return head + cn2 + tail;
            }
            foreach (var kv in Tmpl)
            {
                var m = kv.Key.Match(t);
                if (!m.Success) continue;
                return ApplyTemplate(kv.Value, m);
            }
            return null;
        }

        private static void TranslateContent(global::UnityEngine.GUIContent c)
        {
            var t = c.text;
            if (string.IsNullOrEmpty(t)) return;
            string tail = null;
            if (t.EndsWith("\n...")) { tail = "\n..."; t = t.Substring(0, t.Length - 4); }
            var cn = Lookup(t);
            if (cn == null) return;
            c.text = cn + (tail ?? "");
            _hits++;
        }

        private static void ContentPrefix(object[] __args)
        {
            if (!_enabled || (Exact.Count == 0 && Tmpl.Count == 0)) return;
            try
            {
                foreach (var a in __args)
                {
                    if (a is global::UnityEngine.GUIContent c)
                    {
                        Diag(c.text);
                        TranslateContent(c);
                    }
                }
            }
            catch { }
        }

        private static void StringPrefix(ref string text)
        {
            if (!_enabled || (Exact.Count == 0 && Tmpl.Count == 0)) return;
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Diag(text);
                string t = text;
                string tail = null;
                if (t.EndsWith("\n...")) { tail = "\n..."; t = t.Substring(0, t.Length - 4); }
                var cn = Lookup(t);
                if (cn == null) return;
                text = cn + (tail ?? "");
                _hits++;
            }
            catch { }
        }
    }
}
