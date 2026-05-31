using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
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

        private Dictionary<string, AudioClip> audioDictionary;

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
            foreach (var clipData in audioClips)
            {
                if (clipData.clip != null && !string.IsNullOrEmpty(clipData.name) && !audioDictionary.ContainsKey(clipData.name))
                {
                    audioDictionary.Add(clipData.name, clipData.clip);
                }
            }
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
            else
            {
                Debug.LogWarning($"BGM clip '{clipName}' not found!");
                return null;
            }
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
