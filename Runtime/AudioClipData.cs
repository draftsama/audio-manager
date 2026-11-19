using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.IO;
#endif

namespace AM
{
    public enum ClipSource
    {
        Internal,
        External
    }

    [System.Serializable]
    [InlineProperty(LabelWidth = 60)]
    public class AudioClipData
    {
        [ValueDropdown("GetAudioClipNames"), OnValueChanged("OnNameChanged")]
        public string name;

        // Internal = Unity AudioClip asset; External = file loaded from Resources at runtime.
        public ClipSource source = ClipSource.Internal;

        // Internal source
        public AudioClip clip;

        // External source — Resource Path, relative to ExternalResourcePath.Resolve()
        public string externalPath;

        // External source — true: preload at init (keeps sync Play working);
        // false: lazy-load on first PlaySFXAsync/PlayBGMAsync call.
        public bool preload = true;

#if UNITY_EDITOR
        private static IEnumerable<string> GetAudioClipNames()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            var names = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                names.Add(Path.GetFileNameWithoutExtension(path));
            }
            names.Sort();
            return names;
        }

        private void OnNameChanged()
        {
            if (string.IsNullOrEmpty(name)) return;

            var guids = AssetDatabase.FindAssets($"t:AudioClip {name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    return;
                }
            }
        }
#endif
    }
}
