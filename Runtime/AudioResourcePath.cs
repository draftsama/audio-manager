using System;
using System.IO;

namespace AM
{
    // Self-contained resource-path helper so the audio-manager submodule stays decoupled
    // from project-specific helpers (ResourceHelper lives in Assembly-CSharp, which an
    // asmdef cannot reference). Base folder mirrors ResourceHelper.GetResourceFolderPath():
    // {CurrentDirectory}/Resources.
    public static class AudioResourcePath
    {
        public const string RootFolderName = "Resources";

        public static string GetAudioFolderPath(string combine = "")
        {
            var root = Path.Combine(Environment.CurrentDirectory, RootFolderName);
            if (!string.IsNullOrEmpty(combine))
                root = Path.Combine(root, combine);
            return root;
        }

#if UNITY_EDITOR
        private static readonly string[] AudioExtensions = { "*.wav", "*.mp3", "*.ogg" };

        // relative paths (root prefix stripped) of every audio file under Resources.
        // returns empty when the folder is missing — never throws.
        public static string[] GetAudioResourcePaths()
        {
            var root = GetAudioFolderPath();
            if (!Directory.Exists(root))
                return Array.Empty<string>();

            var results = new System.Collections.Generic.List<string>();
            foreach (var pattern in AudioExtensions)
            {
                var files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
                foreach (var path in files)
                    results.Add(path.Substring(root.Length + 1).Replace('\\', '/'));
            }
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results.ToArray();
        }
#endif
    }
}
