using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text;
using System.Reflection;

namespace AM.Editor
{
    [CustomEditor(typeof(AudioManager))]
    public class AudioManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty audioMixerProp;
        private SerializedProperty initialBGMPoolSizeProp;
        private SerializedProperty initialSFXPoolSizeProp;
        private SerializedProperty bgmFadeDurationProp;
        private SerializedProperty audioClipsProp;

        private List<(string name, string path)> _projectClips = new List<(string, string)>();
        private string[] _clipDisplayNames = Array.Empty<string>();

        private const string k_OutputPathKey = "AM.AudioConstants.OutputPath";
        private string OutputPath
        {
            get => EditorPrefs.GetString(k_OutputPathKey, "Assets/audio-manager/Runtime/AudioConstants.cs");
            set => EditorPrefs.SetString(k_OutputPathKey, value);
        }

        private void OnEnable()
        {
            audioMixerProp = serializedObject.FindProperty("audioMixer");
            initialBGMPoolSizeProp = serializedObject.FindProperty("initialBGMPoolSize");
            initialSFXPoolSizeProp = serializedObject.FindProperty("initialSFXPoolSize");
            bgmFadeDurationProp = serializedObject.FindProperty("bgmFadeDuration");
            audioClipsProp = serializedObject.FindProperty("audioClips");
            RefreshProjectClips();
        }

        private void RefreshProjectClips()
        {
            _projectClips.Clear();
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                _projectClips.Add((Path.GetFileNameWithoutExtension(path), path));
            }
            _projectClips = _projectClips.OrderBy(c => c.name).ToList();
            _clipDisplayNames = _projectClips.Select(c => c.name).ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(audioMixerProp);

            if (audioMixerProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "AudioMixer is not assigned!\n" +
                    "SetMasterVolume / SetSFXVolume will not work.\n" +
                    "Required exposed parameters: \"MasterVolume\", \"SFXVolume\"",
                    MessageType.Error);
            
                EditorGUILayout.Space(4);
            }
            else
            {
                DrawMixerStructureCheck();
            }

            EditorGUILayout.PropertyField(initialBGMPoolSizeProp);
            EditorGUILayout.PropertyField(initialSFXPoolSizeProp);
            EditorGUILayout.PropertyField(bgmFadeDurationProp);

            EditorGUILayout.Space(8);

            // ── Header row ──────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);
            if (GUILayout.Button("↻ Scan Project", GUILayout.Width(110)))
                RefreshProjectClips();
            EditorGUILayout.EndHorizontal();

            // ── AudioConstants output path row ───────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Path", GUILayout.Width(76));
            string editedPath = EditorGUILayout.TextField(OutputPath);
            if (editedPath != OutputPath) OutputPath = editedPath;
            if (GUILayout.Button("Browse…", GUILayout.Width(64)))
            {
                string folder = EditorUtility.OpenFolderPanel("Select output folder", Path.GetDirectoryName(OutputPath), "");
                if (!string.IsNullOrEmpty(folder))
                {
                    // Convert absolute path to project-relative
                    string dataPath = Application.dataPath;
                    if (folder.StartsWith(dataPath))
                        folder = "Assets" + folder.Substring(dataPath.Length);
                    OutputPath = folder.TrimEnd('/') + "/AudioConstants.cs";
                }
            }
            if (GUILayout.Button("Generate", GUILayout.Width(68)))
                GenerateAudioConstants();
            bool fileExists = File.Exists(Path.GetFullPath(OutputPath));
            using (new EditorGUI.DisabledScope(!fileExists))
            {
                if (GUILayout.Button("Delete", GUILayout.Width(54)))
                    DeleteAudioConstants();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Clip list ────────────────────────────────────────────────────
            int removeIndex = -1;
            for (int i = 0; i < audioClipsProp.arraySize; i++)
            {
                SerializedProperty element  = audioClipsProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = element.FindPropertyRelative("name");
                SerializedProperty clipProp = element.FindPropertyRelative("clip");

                EditorGUILayout.BeginHorizontal();

                // Dropdown — project audio clip names
                int currentIdx = Array.IndexOf(_clipDisplayNames, nameProp.stringValue);
                bool missing   = currentIdx < 0 && !string.IsNullOrEmpty(nameProp.stringValue);

                if (missing)
                {
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 0.55f, 0.55f);
                    EditorGUILayout.LabelField($"⚠ {nameProp.stringValue}", GUILayout.Width(184));
                    GUI.color = prev;
                }
                else if (_clipDisplayNames.Length > 0)
                {
                    string btnLabel = currentIdx >= 0 ? _clipDisplayNames[currentIdx] : "\u2014 select clip \u2014";
                    var btnRect = GUILayoutUtility.GetRect(new GUIContent(btnLabel), EditorStyles.popup, GUILayout.Width(184));
                    if (EditorGUI.DropdownButton(btnRect, new GUIContent(btnLabel), FocusType.Keyboard))
                    {
                        int capturedI = i;
                        SearchablePopup.Show(btnRect, _clipDisplayNames, Mathf.Max(currentIdx, 0), newIdx =>
                        {
                            var elem = audioClipsProp.GetArrayElementAtIndex(capturedI);
                            elem.FindPropertyRelative("name").stringValue          = _projectClips[newIdx].name;
                            elem.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(_projectClips[newIdx].path);
                            serializedObject.ApplyModifiedProperties();
                        });
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No clips found in project", GUILayout.Width(184));
                }

                // Clip reference (read-only — driven by dropdown)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(clipProp, GUIContent.none);

                // Preview controls
                AudioClip clip = clipProp.objectReferenceValue as AudioClip;
                if (clip != null)
                {
                    if (GUILayout.Button("▶", GUILayout.Width(26))) PlayClip(clip);
                    if (GUILayout.Button("■", GUILayout.Width(26))) StopAllClips();
                }

                // Remove
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
                audioClipsProp.DeleteArrayElementAtIndex(removeIndex);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("+ Add Entry"))
            {
                audioClipsProp.InsertArrayElementAtIndex(audioClipsProp.arraySize);
                var el = audioClipsProp.GetArrayElementAtIndex(audioClipsProp.arraySize - 1);
                el.FindPropertyRelative("name").stringValue         = string.Empty;
                el.FindPropertyRelative("clip").objectReferenceValue = null;
            }

            if (audioClipsProp.arraySize == 0)
                EditorGUILayout.HelpBox("No clips. Click '+ Add Entry' to add an audio clip.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

      

        // ── Mixer structure check ──────────────────────────────────────────────
        private void DrawMixerStructureCheck()
        {
            var mixer = audioMixerProp.objectReferenceValue as AudioMixer;
            if (mixer == null) return;

            var groups = mixer.FindMatchingGroups("");
            bool hasBGM = groups.Any(g => g.name == "BGM");
            bool hasSFX = groups.Any(g => g.name == "SFX");
            mixer.GetFloat("MasterVolume", out float _m);
            mixer.GetFloat("BGMVolume", out float _b);
            mixer.GetFloat("SFXVolume", out float _s);

            bool hasMasterParam = mixer.GetFloat("MasterVolume", out _);
            bool hasBGMParam    = mixer.GetFloat("BGMVolume",    out _);
            bool hasSFXParam    = mixer.GetFloat("SFXVolume",    out _);

            if (hasBGM && hasSFX && hasMasterParam && hasBGMParam && hasSFXParam)
            {
                EditorGUILayout.HelpBox("\u2713 AudioMixer is correctly configured.", MessageType.Info);
                return;
            }

            var sb = new StringBuilder("AudioMixer is missing required setup:\n");
            if (!hasBGM)         sb.AppendLine("  \u2022 Group 'BGM' not found");
            if (!hasSFX)         sb.AppendLine("  \u2022 Group 'SFX' not found");
            if (!hasMasterParam) sb.AppendLine("  \u2022 Exposed param 'MasterVolume' not found");
            if (!hasBGMParam)    sb.AppendLine("  \u2022 Exposed param 'BGMVolume' not found");
            if (!hasSFXParam)    sb.AppendLine("  \u2022 Exposed param 'SFXVolume' not found");

            EditorGUILayout.HelpBox(sb.ToString().TrimEnd(), MessageType.Warning);
        }

       

        private static void ExposeGroupVolume(AudioMixer mixer, Type controllerType, Assembly asm, object group, string exposedName)
        {
            try
            {
                var groupType  = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                var effectType = asm.GetType("UnityEditor.Audio.AudioMixerEffectController");
                if (groupType == null || effectType == null) return;

                // Check if already exposed
                if (mixer.GetFloat(exposedName, out _)) return;

                // Get effects on the group
                var effectsProp  = groupType.GetProperty("effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var effectsValue = effectsProp?.GetValue(group) as System.Collections.IEnumerable;
                if (effectsValue == null) return;

                // Find Attenuation effect
                object attEffect = null;
                var effectNameProp = effectType.GetProperty("effectName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var e in effectsValue)
                {
                    if (effectNameProp?.GetValue(e) as string == "Attenuation") { attEffect = e; break; }
                }
                if (attEffect == null) return;

                // Get the volume parameter GUID
                object paramGUID = null;

                // Try GetGUIDForMixerParameter method
                var getGUIDMethod = effectType.GetMethod("GetGUIDForMixerParameter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (getGUIDMethod != null)
                {
                    foreach (var pName in new[] { "Volume", "Attenuation", "volume" })
                    {
                        try { paramGUID = getGUIDMethod.Invoke(attEffect, new object[] { pName }); } catch { }
                        if (paramGUID != null) break;
                    }
                }

                // Fallback: read from parameters array directly
                if (paramGUID == null)
                {
                    var paramsProp  = effectType.GetProperty("parameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var paramsValue = paramsProp?.GetValue(attEffect) as System.Collections.IEnumerable;
                    if (paramsValue != null)
                    {
                        foreach (var p in paramsValue)
                        {
                            var guidMember = (MemberInfo)p.GetType().GetField("guid",  BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                         ?? p.GetType().GetField("m_GUID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            paramGUID = guidMember is FieldInfo fi ? fi.GetValue(p) : null;
                            if (paramGUID != null) break;
                        }
                    }
                }

                if (paramGUID == null)
                {
                    Debug.LogWarning($"[AudioManager] Could not find GUID for '{exposedName}'. Please expose it manually in the AudioMixer window.");
                    return;
                }

                // Call AddExposedParameter(GUID, string)
                var addExposed = controllerType.GetMethod("AddExposedParameter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (addExposed == null)
                {
                    Debug.LogWarning($"[AudioManager] AddExposedParameter not found. Please expose '{exposedName}' manually.");
                    return;
                }

                addExposed.Invoke(mixer, new[] { paramGUID, (object)exposedName });
                Debug.Log($"[AudioManager] Exposed parameter: {exposedName}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioManager] Could not expose '{exposedName}': {e.InnerException?.Message ?? e.Message}\nPlease expose it manually.");
            }
        }

        // ── Generate AudioConstants.cs ──────────────────────────────────────
        private void GenerateAudioConstants()
        {
            var names = new List<string>();
            for (int i = 0; i < audioClipsProp.arraySize; i++)
            {
                var n = audioClipsProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (!string.IsNullOrEmpty(n) && !names.Contains(n))
                    names.Add(n);
            }

            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("Generate AudioConstants", "No clip names in the list.", "OK");
                return;
            }

            string filePath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var bgm   = names.Where(n => n.StartsWith("BGM", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToList();
            var sfx   = names.Where(n => n.StartsWith("SFX", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToList();
            var other = names.Except(bgm).Except(sfx).OrderBy(n => n).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace AM");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class AudioConstants");
            sb.AppendLine("    {");

            void WriteGroup(string comment, List<string> group, bool addBlankBefore)
            {
                if (group.Count == 0) return;
                if (addBlankBefore) sb.AppendLine();
                sb.AppendLine($"        // {comment}");
                foreach (var n in group)
                    sb.AppendLine($"        public const string {ToConstantName(n)} = \"{n}\";");
            }

            WriteGroup("BGM",   bgm,   false);
            WriteGroup("SFX",   sfx,   bgm.Count > 0);
            WriteGroup("Other", other, bgm.Count > 0 || sfx.Count > 0);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[AudioManager] AudioConstants.cs generated → {filePath}");
        }

        private void DeleteAudioConstants()
        {
            string filePath = Path.GetFullPath(OutputPath);
            if (!File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Delete AudioConstants", "File not found:\n" + OutputPath, "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Delete AudioConstants.cs",
                "Delete file?\n" + OutputPath,
                "Delete", "Cancel");

            if (!confirm) return;

            File.Delete(filePath);
            string metaPath = filePath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
            AssetDatabase.Refresh();
            Debug.Log($"[AudioManager] AudioConstants.cs deleted → {filePath}");
        }

        private static string ToConstantName(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            var result = sb.ToString();
            return char.IsDigit(result[0]) ? "_" + result : result;
        }

        // ── Preview helpers ─────────────────────────────────────────────────
        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            StopAllClips();
            typeof(UnityEditor.Editor).Assembly
                .GetType("UnityEditor.AudioUtil")
                ?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, new object[] { clip, 0, false });
        }

        private void StopAllClips()
        {
            typeof(UnityEditor.Editor).Assembly
                .GetType("UnityEditor.AudioUtil")
                ?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, null);
        }

        // ── Searchable popup ───────────────────────────────────────────────────
        private sealed class SearchablePopup : PopupWindowContent
        {
            private readonly string[]    _options;
            private readonly int         _current;
            private readonly Action<int> _onSelect;

            private string    _search   = "";
            private Vector2   _scroll;
            private List<int> _filtered;
            private bool      _focusSet;

            // Cached styles — built once, reused every repaint
            private GUIStyle _rowNormal;
            private GUIStyle _rowSelected;

            public static void Show(Rect activatorRect, string[] options, int current, Action<int> onSelect)
                => PopupWindow.Show(activatorRect, new SearchablePopup(options, current, onSelect));

            private SearchablePopup(string[] options, int current, Action<int> onSelect)
            {
                _options  = options;
                _current  = current;
                _onSelect = onSelect;
                _filtered = Enumerable.Range(0, options.Length).ToList();
            }

            public override Vector2 GetWindowSize() => new Vector2(300, 320);

            public override void OnOpen()
            {
                int pos = _filtered.IndexOf(_current);
                if (pos > 0) _scroll.y = Mathf.Max(0f, pos * 22f - 120f);
            }

            private void EnsureStyles()
            {
                if (_rowNormal != null) return;
                _rowNormal = new GUIStyle(EditorStyles.label)
                {
                    padding    = new RectOffset(8, 8, 3, 3),
                    fixedHeight = 22,
                    stretchWidth = true,
                };
                _rowSelected = new GUIStyle(_rowNormal)
                {
                    fontStyle  = FontStyle.Bold,
                    normal     = { background = MakeTex(new Color(0.24f, 0.49f, 0.91f, 0.35f)) },
                    hover      = { background = MakeTex(new Color(0.24f, 0.49f, 0.91f, 0.35f)) },
                };
            }

            private static Texture2D MakeTex(Color col)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, col);
                tex.Apply();
                return tex;
            }

            public override void OnGUI(Rect rect)
            {
                EnsureStyles();

                // Search field
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName("ClipSearch");
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Height(22));
                if (EditorGUI.EndChangeCheck())
                {
                    _filtered = Enumerable.Range(0, _options.Length)
                        .Where(i => string.IsNullOrEmpty(_search) ||
                                    _options[i].IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                    _scroll = Vector2.zero;
                }

                if (!_focusSet) { EditorGUI.FocusTextInControl("ClipSearch"); _focusSet = true; }

                // Clip list
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (int idx in _filtered)
                {
                    bool     isCurrent = idx == _current;
                    GUIStyle style     = isCurrent ? _rowSelected : _rowNormal;
                    if (GUILayout.Button(_options[idx], style, GUILayout.Height(22)))
                    {
                        _onSelect(idx);
                        editorWindow.Close();
                    }
                }
                if (_filtered.Count == 0)
                    EditorGUILayout.LabelField("No results", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
