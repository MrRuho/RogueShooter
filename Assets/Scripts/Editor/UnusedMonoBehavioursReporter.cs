#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueShooter.Tools
{
    /// <summary>
    /// Reports MonoBehaviour scripts that do not appear in any prefab or scene dependencies.
    /// Limitations:
    /// - Does NOT detect scripts that are only added at runtime via AddComponent/Reflection.
    /// - Excludes scripts under any *Editor* folder by default.
    /// - Skips abstract classes and classes that don't inherit MonoBehaviour.
    /// - Prefabs & scenes outside the current project (Packages, external) are not scanned.
    /// </summary>
    public class UnusedMonoBehavioursReporter : EditorWindow
    {

        private const string ScanRoot = "Assets/Scripts";
    
        [MenuItem("Tools/RogueShooter/Report Unused MonoBehaviours")] 
        public static void Open()
        {
            var win = GetWindow<UnusedMonoBehavioursReporter>(true, "Unused MonoBehaviours Report", true);
            win.minSize = new Vector2(820, 480);
            win.RefreshScan();
        }

        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _includeEditorFolderScripts = false; // Usually false; keep Editor scripts out
        private bool _showUsedAlso = false;

        private List<ScriptInfo> _allCandidates = new();   // All MonoBehaviour scripts in project (filtered)
        private HashSet<string> _usedScriptPaths = new(StringComparer.OrdinalIgnoreCase); // .cs paths used by scenes/prefabs
        private List<ScriptInfo> _unused = new();           // Candidates not referenced by any scene/prefab
        private List<ScriptInfo> _used = new();             // Candidates referenced
        private string _status = "";
        private double _elapsedMs;

        private class ScriptInfo
        {
            public MonoScript Mono;
            public Type Type;
            public string Path;
            public string AssemblyName;
            public bool IsAbstract;
            public bool InEditorFolder;
        }

        private void OnGUI()
        {
            GUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                _includeEditorFolderScripts = GUILayout.Toggle(_includeEditorFolderScripts, new GUIContent("Include /Editor scripts"), GUILayout.Width(170));
                _showUsedAlso = GUILayout.Toggle(_showUsedAlso, new GUIContent("Show used scripts too"), GUILayout.Width(170));
                GUILayout.Space(12);

                GUILayout.Label("Search:", GUILayout.Width(48));
                _search = GUILayout.TextField(_search ?? string.Empty);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                    RefreshScan();

                if (GUILayout.Button("Export CSV...", GUILayout.Width(120)))
                    ExportCsv();
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField($"Scan root: {ScanRoot}");
            EditorGUILayout.HelpBox("Lists MonoBehaviour scripts that are NOT referenced by any prefab or scene in this project. " +
                                    "Dynamic AddComponent() usage will not be detected.", MessageType.Info);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status);
            EditorGUILayout.LabelField($"Scanned in {_elapsedMs:F0} ms | Candidates: {_allCandidates.Count} | Used: {_used.Count} | Unused: {_unused.Count}");

            GUILayout.Space(6);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawTableHeader();
                DrawList(_unused, title: "UNUSED SCRIPTS", color: new Color(1f, 0.5f, 0.5f));
                if (_showUsedAlso)
                {
                    GUILayout.Space(6);
                    DrawList(_used, title: "USED SCRIPTS", color: new Color(0.65f, 0.9f, 0.65f));
                }
            }
        }

        private void DrawTableHeader()
        {
            var rect = GUILayoutUtility.GetRect(10, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            var r = rect; r.x += 6; r.width -= 12; r.y += 3;
            GUI.Label(r, "Name | Namespace | Assembly | Path");
        }

        private void DrawList(List<ScriptInfo> list, string title, Color color)
        {
            var shown = ApplySearch(list);

            var head = GUILayoutUtility.GetRect(10, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(head, color);
            var hr = head; hr.x += 6; hr.width -= 12; hr.y += 3;
            GUI.Label(hr, $"{title} ({shown.Count})");

            foreach (var s in shown)
            {
                var row = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    Selection.activeObject = s.Mono; // select asset
                    EditorGUIUtility.PingObject(s.Mono);
                    Repaint();
                }

                EditorGUI.DrawRect(row, new Color(0.12f, 0.12f, 0.12f));
                var rr = row; rr.x += 6; rr.width -= 12; rr.y += 2;
                var name = s.Type != null ? s.Type.Name : Path.GetFileNameWithoutExtension(s.Path);
                var ns = s.Type != null ? (string.IsNullOrEmpty(s.Type.Namespace) ? "-" : s.Type.Namespace) : "-";
                var asm = s.AssemblyName ?? "-";
                GUI.Label(rr, $"{name} | {ns} | {asm} | {s.Path}");
            }
        }

        private List<ScriptInfo> ApplySearch(List<ScriptInfo> src)
        {
            if (string.IsNullOrWhiteSpace(_search)) return src;
            var q = _search.Trim();
            return src.Where(s =>
                    (s.Type != null && (s.Type.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        (!string.IsNullOrEmpty(s.Type.Namespace) && s.Type.Namespace.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)))
                    || (!string.IsNullOrEmpty(s.Path) && s.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
        }

        private static bool IsUnderRoot(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith(ScanRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshScan()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Scanning", "Collecting scripts...", 0f);
                var sw = Stopwatch.StartNew();

                _allCandidates = CollectCandidateScripts(_includeEditorFolderScripts);

                EditorUtility.DisplayProgressBar("Scanning", "Collecting prefab/scene dependencies...", 0.33f);
                _usedScriptPaths = CollectUsedScriptPaths();

                EditorUtility.DisplayProgressBar("Scanning", "Comparing...", 0.66f);
                _unused = new List<ScriptInfo>();
                _used = new List<ScriptInfo>();

                foreach (var s in _allCandidates)
                {
                    if (_usedScriptPaths.Contains(s.Path)) _used.Add(s);
                    else _unused.Add(s);
                }

                sw.Stop();
                _elapsedMs = sw.Elapsed.TotalMilliseconds;
                _status = $"Found {_unused.Count} potentially unused MonoBehaviour scripts.";
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[UnusedMonoBehavioursReporter] Scan failed: {e}\n{e.StackTrace}");
                _status = "Scan failed — see Console.";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private static List<ScriptInfo> CollectCandidateScripts(bool includeEditorFolder)
        {
            var results = new List<ScriptInfo>();
            var guids = AssetDatabase.FindAssets("t:MonoScript");

            for (int i = 0; i < guids.Length; i++)
            {
                if (i % 200 == 0)
                    EditorUtility.DisplayProgressBar("Scanning", $"Inspecting scripts ({i}/{guids.Length})...", Mathf.InverseLerp(0, guids.Length, i));

                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !IsUnderRoot(path)) continue;
                var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (mono == null) continue;

                var type = mono.GetClass();
                if (type == null) continue; // no class in file or compile error

                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                    continue; // not a MonoBehaviour

                if (type.IsAbstract)
                    continue; // we report only concrete components

                var inEditorFolder = path.Split('/')
                                         .Any(seg => string.Equals(seg, "Editor", StringComparison.OrdinalIgnoreCase));
                if (inEditorFolder && !includeEditorFolder)
                    continue; // ignore Editor scripts unless explicitly included

                var asmName = type.Assembly?.GetName().Name;

                results.Add(new ScriptInfo
                {
                    Mono = mono,
                    Type = type,
                    Path = path,
                    AssemblyName = asmName,
                    IsAbstract = false,
                    InEditorFolder = inEditorFolder,
                });
            }

            return results;
        }

        private static HashSet<string> CollectUsedScriptPaths()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Gather all prefab & scene asset paths
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var sceneGuids  = AssetDatabase.FindAssets("t:Scene");

            // Unity's dependency graph: scenes/prefabs depend on MonoScript assets referenced by m_Script
            void Accumulate(string guid, int index, int total, string label)
            {
                if (index % 100 == 0)
                    EditorUtility.DisplayProgressBar("Scanning", $"{label} ({index}/{total})...", Mathf.InverseLerp(0, total, index));

                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) return;

                var deps = AssetDatabase.GetDependencies(path, true);
                foreach (var d in deps)
                {
                    if (d.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        used.Add(d);
                }
            }

            for (int i = 0; i < prefabGuids.Length; i++)
                Accumulate(prefabGuids[i], i, prefabGuids.Length, "Scanning prefabs");

            for (int i = 0; i < sceneGuids.Length; i++)
                Accumulate(sceneGuids[i], i, sceneGuids.Length, "Scanning scenes");

            return used;
        }

        private void ExportCsv()
        {
            try
            {
                var path = EditorUtility.SaveFilePanelInProject("Export CSV", "UnusedMonoBehaviours.csv", "csv", "Select save location");
                if (string.IsNullOrEmpty(path)) return;

                var lines = new List<string> { "Name,Namespace,Assembly,AssetPath" };
                foreach (var s in ApplySearch(_unused))
                {
                    var name = s.Type != null ? s.Type.Name : Path.GetFileNameWithoutExtension(s.Path);
                    var ns = s.Type != null ? (s.Type.Namespace ?? string.Empty) : string.Empty;
                    var asm = s.AssemblyName ?? string.Empty;

                    string Escape(string x) => '"' + (x?.Replace("\"", "\"\"") ?? string.Empty) + '"';
                    lines.Add(string.Join(",", new[] { Escape(name), Escape(ns), Escape(asm), Escape(s.Path) }));
                }

                File.WriteAllLines(path, lines);
                AssetDatabase.ImportAsset(path);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[UnusedMonoBehavioursReporter] Export failed: {e}");
                EditorUtility.DisplayDialog("Export CSV", "Export failed. See Console.", "OK");
            }
        }
    }
}
#endif
