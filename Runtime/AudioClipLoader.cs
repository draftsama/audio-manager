using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AM
{
    // Runtime loader for external audio files, mirroring the TextureLoader pattern:
    // async UnityWebRequest load, in-flight dedup, and a static cache keyed by path.
    public class AudioClipLoader : MonoBehaviour
    {
        [SerializeField] private string m_Path;
        [SerializeField] private AudioClip m_AudioClip;

        private enum State { Idle, Loading, Done, Failed }
        private State _State = State.Idle;

        // Task (not UniTask) because a UniTask allows only one pending awaiter,
        // and callers can join the same in-flight load concurrently.
        private System.Threading.Tasks.Task<AudioClip> _LoadingTask;

        public UniTask<AudioClip> LoadAudioClipAsync(string path, CancellationToken cancellationToken = default)
        {
            // dedup: reuse the in-flight load instead of firing a second request
            if (_State == State.Loading && _LoadingTask != null)
                return _LoadingTask.AsUniTask().AttachExternalCancellation(cancellationToken);

            _LoadingTask = LoadAudioClipInternalAsync(path, cancellationToken).AsTask();
            return _LoadingTask.AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        private async UniTask<AudioClip> LoadAudioClipInternalAsync(string path, CancellationToken cancellationToken)
        {
            _State = State.Loading;
            try
            {
                using (var request = UnityWebRequestMultimedia.GetAudioClip(path, InferAudioType(path)))
                {
                    await request.SendWebRequest().WithCancellation(cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new System.Exception($"Failed to load audio ({path}): {request.error}");

                    if (m_AudioClip) DestroyImmediate(m_AudioClip);
                    m_AudioClip = DownloadHandlerAudioClip.GetContent(request);
                    m_AudioClip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    _State = State.Done;
                    return m_AudioClip;
                }
            }
            catch (System.Exception ex)
            {
                // fault the task so waiters get the exception instead of hanging forever
                _State = State.Failed;
                // wrap so the failing path (e.g. 404 / missing file) is visible in the message
                throw new System.Exception($"<color=red>Failed to load audio ({path})</color> : {ex.Message} ", ex);
            }
        }

        // map file extension to the codec Unity needs; UnityEngine.AudioType, not AM.AudioType
        private static UnityEngine.AudioType InferAudioType(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".ogg": return UnityEngine.AudioType.OGGVORBIS;
                case ".wav": return UnityEngine.AudioType.WAV;
                case ".mp3": return UnityEngine.AudioType.MPEG;
                default: return UnityEngine.AudioType.UNKNOWN;
            }
        }


        //static list of loaders
        public static List<AudioClipLoader> m_Loaders = new List<AudioClipLoader>();
        public static Transform m_Parent = null;

        // local file paths without a scheme get file:// so UnityWebRequest can read them
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.Contains("://")) return path; // already has a scheme (http/https/file/jar...)
            return "file://" + path;
        }

        public static async UniTask<AudioClip> GetAudioClipAsync(string path, bool reload = false, CancellationToken cancellationToken = default)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            path = NormalizePath(path);

            //find in list
            var loader = m_Loaders.Find(x => x.m_Path == path);
            if (loader != null)
            {
                //in-flight → join the same load (dedup, also covers reload-while-loading)
                if (loader._State == State.Loading && loader._LoadingTask != null)
                    return await loader._LoadingTask.AsUniTask().AttachExternalCancellation(cancellationToken);

                //already loaded and not forcing reload → return cached clip
                if (loader._State == State.Done && !reload)
                    return loader.m_AudioClip;

                //idle / failed / reload → start a fresh load (also retries after a failure)
                return await loader.LoadAudioClipAsync(path, cancellationToken);
            }

            //create new loader
            var uuid = System.Guid.NewGuid().ToString();
            var newLoader = new GameObject($"{fileName}_{uuid}").AddComponent<AudioClipLoader>();
            newLoader.m_Path = path;

            if (m_Parent == null)
            {
                var parentGo = new GameObject("AudioClipLoaders");
                m_Parent = parentGo.transform;
            }
            newLoader.transform.SetParent(m_Parent);

            m_Loaders.Add(newLoader);

            return await newLoader.LoadAudioClipAsync(path, cancellationToken);
        }

        public static void RemoveLoader(string path, bool destroyClip = true)
        {
            path = NormalizePath(path);
            var loader = m_Loaders.Find(x => x.m_Path == path);
            if (loader != null)
                RemoveLoader(loader, destroyClip);
        }

        public static void RemoveLoader(AudioClipLoader loader, bool destroyClip = true)
        {
            if (loader.m_AudioClip != null && destroyClip)
                DestroyImmediate(loader.m_AudioClip);

            m_Loaders.Remove(loader);

            if (loader.gameObject != null)
                DestroyImmediate(loader.gameObject);
        }

        public static void RemoveAllLoaders(bool destroyClips = true)
        {
            foreach (var loader in m_Loaders)
            {
                if (loader.m_AudioClip != null && destroyClips)
                    DestroyImmediate(loader.m_AudioClip);
                if (loader.gameObject != null)
                    DestroyImmediate(loader.gameObject);
            }

            m_Loaders.Clear();
        }
    }
}
