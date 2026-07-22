using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace AM
{
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance => instance;

        [SerializeField] private AudioMixer audioMixer;
        public AudioMixer AudioMixer => audioMixer;

        [Min(2)]
        [SerializeField] private int initialBGMPoolSize = 2;
        [SerializeField] private int initialSFXPoolSize = 10;

        [SerializeField] private float bgmFadeDuration = 1f;

        [SerializeField] private List<AudioClipData> audioClips = new List<AudioClipData>();

        private AudioPoolManager _poolManager;
        private BGMController _bgmController;
        private SFXController _sfxController;

        // clips ready to play: internal + preloaded external + lazily-loaded external once resolved.
        // shared by reference with SFXController, so lazily-added clips become playable there too.
        private Dictionary<string, AudioClip> audioDictionary;

        // every declared entry by name, used to resolve lazy/external clips on demand.
        private Dictionary<string, AudioClipData> _entries;

        // System Task (not UniTask) so multiple callers can await readiness concurrently.
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> _readySource =
            new System.Threading.Tasks.TaskCompletionSource<bool>();

        // true once external preloading has finished (success or per-clip failure).
        public bool IsReady { get; private set; }

        public AudioInstance CurrentBGM => _bgmController?.CurrentBGM;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);

                _poolManager = new GameObject("AudioPoolManager").AddComponent<AudioPoolManager>();
                _poolManager.transform.SetParent(transform);

                _bgmController = new GameObject("BGMController").AddComponent<BGMController>();
                _bgmController.transform.SetParent(transform);

                _sfxController = new GameObject("SFXController").AddComponent<SFXController>();
                _sfxController.transform.SetParent(transform);

                InitializeAudioDictionaries();
                _poolManager.Initialize(audioMixer, initialBGMPoolSize, initialSFXPoolSize);
                _bgmController.Initialize(audioMixer, _poolManager, bgmFadeDuration);
                _sfxController.Initialize(audioMixer, _poolManager, audioDictionary);

                PreloadExternalAsync().Forget();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static AudioManager GetInstance()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<AudioManager>();
                if (instance == null)
                {
                    var prefab = Resources.Load<AudioManager>("AudioManager");
                    if (prefab != null)
                    {
                        var go = Instantiate(prefab.gameObject);
                        instance = go.GetComponent<AudioManager>();
                    }
                }
            }
            return instance;
        }

        private void InitializeAudioDictionaries()
        {
            audioDictionary = new Dictionary<string, AudioClip>();
            _entries = new Dictionary<string, AudioClipData>();

            foreach (var clipData in audioClips)
            {
                if (string.IsNullOrEmpty(clipData.name) || _entries.ContainsKey(clipData.name))
                    continue;

                _entries.Add(clipData.name, clipData);

                // internal clips are ready immediately; external clips are loaded async
                if (clipData.source == ClipSource.Internal && clipData.clip != null)
                    audioDictionary.Add(clipData.name, clipData.clip);
            }
        }

        // load every external+preload entry into audioDictionary, then flag ready.
        private async UniTaskVoid PreloadExternalAsync()
        {
            try
            {
                foreach (var kv in _entries)
                {
                    var data = kv.Value;
                    if (data.source != ClipSource.External || !data.preload)
                        continue;

                    if (string.IsNullOrEmpty(data.externalPath))
                    {
                        Debug.LogWarning($"External audio '{data.name}' has an empty path; skipped.");
                        continue;
                    }

                    try
                    {
                        var fullPath = AudioResourcePath.GetAudioFolderPath(data.externalPath);
                        var clip = await AudioClipLoader.GetAudioClipAsync(fullPath);
                        if (clip != null)
                            audioDictionary[data.name] = clip;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed preloading external audio '{data.name}': {e.Message}");
                    }
                }
            }
            finally
            {
                IsReady = true;
                _readySource.TrySetResult(true);
            }
        }

        // load one external entry and publish it into the live dictionary.
        private async UniTaskVoid PreloadOneAsync(AudioClipData data)
        {
            try
            {
                var fullPath = AudioResourcePath.GetAudioFolderPath(data.externalPath);
                var clip = await AudioClipLoader.GetAudioClipAsync(fullPath);
                if (clip != null)
                    audioDictionary[data.name] = clip;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed preloading external audio '{data.name}': {e.Message}");
            }
        }

        // returns a cached clip, or loads an external clip on demand (lazy).
        private async UniTask<AudioClip> ResolveClipAsync(string clipName)
        {
            if (audioDictionary.TryGetValue(clipName, out var cached))
                return cached;

            if (_entries != null && _entries.TryGetValue(clipName, out var data) && data.source == ClipSource.External)
            {
                if (string.IsNullOrEmpty(data.externalPath))
                {
                    Debug.LogWarning($"External audio '{clipName}' has an empty path.");
                    return null;
                }

                var fullPath = AudioResourcePath.GetAudioFolderPath(data.externalPath);
                var clip = await AudioClipLoader.GetAudioClipAsync(fullPath);
                if (clip != null)
                    audioDictionary[clipName] = clip;
                return clip;
            }

            Debug.LogWarning($"Audio clip '{clipName}' not found (no internal clip, no external entry).");
            return null;
        }

        // true when the name is a declared external clip not yet loaded — hints to use the async API.
        private bool IsUnloadedExternal(string clipName) =>
            !audioDictionary.ContainsKey(clipName)
            && _entries != null
            && _entries.TryGetValue(clipName, out var d)
            && d.source == ClipSource.External;

        // await until external preloading has completed. Safe to call from multiple places.
        public static UniTask WaitUntilReadyAsync()
        {
            var mgr = GetInstance();
            if (mgr == null) return UniTask.CompletedTask;
            // cast to non-generic Task so AsUniTask() resolves to UniTask (not UniTask<bool>)
            return ((System.Threading.Tasks.Task)mgr._readySource.Task).AsUniTask();
        }

        // Register an external audio file at runtime so it becomes playable by name
        // through the normal PlaySFX/PlayBGM lookup. Intended for components (e.g.
        // AudioButtonView) that carry their own clip independent of the inspector list.
        // Safe to call before the manager exists — GetInstance() bootstraps it from
        // Resources/AudioManager if there is no instance in the scene.
        public static void RegisterExternalClip(string clipName, string externalPath, bool preload = false)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning("RegisterExternalClip: clip name is empty; skipped.");
                return;
            }

            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning($"RegisterExternalClip('{clipName}'): AudioManager instance not found (no scene object and no Resources/AudioManager prefab).");
                return;
            }

            if (mgr._entries == null)
            {
                Debug.LogWarning($"RegisterExternalClip('{clipName}'): AudioManager not initialized yet.");
                return;
            }

            // already declared (inspector list or a previous registration) — don't clobber it
            if (mgr._entries.ContainsKey(clipName))
                return;

            if (string.IsNullOrEmpty(externalPath))
            {
                Debug.LogWarning($"RegisterExternalClip('{clipName}'): external path is empty; skipped.");
                return;
            }

            var data = new AudioClipData
            {
                name = clipName,
                source = ClipSource.External,
                externalPath = externalPath,
                preload = preload
            };
            mgr._entries[clipName] = data;

            // preload publishes into audioDictionary now; otherwise it lazy-loads on first PlaySFXAsync/PlayBGMAsync.
            if (preload)
                mgr.PreloadOneAsync(data).Forget();
        }

        public static AudioInstance PlayBGM(string clipName, float volume = 1f, bool loop = true)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }

            if (mgr.audioDictionary.TryGetValue(clipName, out AudioClip clip))
            {
                return mgr._bgmController.PlayBGM(clipName, clip, volume, loop);
            }

            if (mgr.IsUnloadedExternal(clipName))
                Debug.LogWarning($"BGM '{clipName}' is an external clip not yet loaded — use PlayBGMAsync().");
            else
                Debug.LogWarning($"BGM clip '{clipName}' not found!");
            return null;
        }

        public static async UniTask<AudioInstance> PlayBGMAsync(string clipName, float volume = 1f, bool loop = true)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }

            var clip = await mgr.ResolveClipAsync(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"BGM clip '{clipName}' not found!");
                return null;
            }
            return mgr._bgmController.PlayBGM(clipName, clip, volume, loop);
        }

        public static void StopBGM()
        {
            var mgr = GetInstance();
            mgr?._bgmController.StopBGM();
        }

        public static void StopBGMImmediate()
        {
            var mgr = GetInstance();
            mgr?._bgmController.StopBGMImmediate();
        }

        public static void SetBGMVolume(float volume)
        {
            var mgr = GetInstance();
            mgr?._bgmController.SetBGMVolume(volume);
        }

        public static AudioInstance PlaySFX(string clipName, float volume = 1f, bool loop = false)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }
            if (mgr.IsUnloadedExternal(clipName))
            {
                Debug.LogWarning($"SFX '{clipName}' is an external clip not yet loaded — use PlaySFXAsync().");
                return null;
            }
            return mgr._sfxController.PlaySFX(clipName, volume, loop);
        }

        public static AudioInstance PlaySFX(string clipName, Vector3 position, float volume = 1f, bool loop = false,
            float minDistance = 1f, float maxDistance = 500f, Transform followTransform = null)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }
            if (mgr.IsUnloadedExternal(clipName))
            {
                Debug.LogWarning($"SFX '{clipName}' is an external clip not yet loaded — use PlaySFXAsync().");
                return null;
            }
            return mgr._sfxController.PlaySFX(clipName, position, volume, loop, minDistance, maxDistance, followTransform);
        }

        public static async UniTask<AudioInstance> PlaySFXAsync(string clipName, float volume = 1f, bool loop = false)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }
            var clip = await mgr.ResolveClipAsync(clipName);
            if (clip == null) return null;
            return mgr._sfxController.PlaySFX(clipName, volume, loop);
        }

        public static async UniTask<AudioInstance> PlaySFXAsync(string clipName, Vector3 position, float volume = 1f,
            bool loop = false, float minDistance = 1f, float maxDistance = 500f, Transform followTransform = null)
        {
            var mgr = GetInstance();
            if (mgr == null)
            {
                Debug.LogWarning("AudioManager instance not found!");
                return null;
            }
            var clip = await mgr.ResolveClipAsync(clipName);
            if (clip == null) return null;
            return mgr._sfxController.PlaySFX(clipName, position, volume, loop, minDistance, maxDistance, followTransform);
        }

        public static void StopAudio(AudioInstance audioInstance)
        {
            var mgr = GetInstance();
            mgr?._sfxController.StopAudio(audioInstance);
        }

        public static void PauseAudio(AudioInstance audioInstance)
        {
            var mgr = GetInstance();
            mgr?._sfxController.PauseAudio(audioInstance);
        }

        public static void ResumeAudio(AudioInstance audioInstance)
        {
            var mgr = GetInstance();
            mgr?._sfxController.ResumeAudio(audioInstance);
        }

        public static void SetSFXVolume(float volume)
        {
            var mgr = GetInstance();
            mgr?._sfxController.SetSFXVolume(volume);
        }

        public static void SetMasterVolume(float volume)
        {
            var mgr = GetInstance();
            mgr?._sfxController.SetMasterVolume(volume);
        }
    }
}
