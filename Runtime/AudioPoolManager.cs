using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace AM
{
    public class AudioPoolManager : MonoBehaviour
    {
        private const float CooldownDuration = 0.2f;

        private AudioMixer _audioMixer;

        public List<AudioInstance> BgmPool { get; private set; }
        public List<AudioInstance> SfxPool { get; private set; }
        public Dictionary<AudioInstance, float> LastStopTime { get; } = new Dictionary<AudioInstance, float>();

        public void Initialize(AudioMixer audioMixer, int bgmPoolSize, int sfxPoolSize)
        {
            _audioMixer = audioMixer;
            int safeBgmPoolSize = Mathf.Max(2, bgmPoolSize);

            BgmPool = new List<AudioInstance>();
            GameObject bgmPoolParent = new GameObject("BGM Pool");
            bgmPoolParent.transform.SetParent(transform);

            for (int i = 0; i < safeBgmPoolSize; i++)
            {
                AudioInstance audioInstance = CreateAudioSource($"BGM Source {i}", bgmPoolParent.transform, AudioType.BGM);
                BgmPool.Add(audioInstance);
            }

            SfxPool = new List<AudioInstance>();
            GameObject sfxPoolParent = new GameObject("SFX Pool");
            sfxPoolParent.transform.SetParent(transform);

            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioInstance audioInstance = CreateAudioSource($"SFX Source {i}", sfxPoolParent.transform, AudioType.SFX);
                SfxPool.Add(audioInstance);
            }
        }

        public AudioInstance GetAvailableSFXSource()
        {
            float currentTime = Time.time;

            foreach (var audioInstance in SfxPool)
            {
                if (!audioInstance.IsPlaying())
                {
                    if (LastStopTime.TryGetValue(audioInstance, out float stopTime))
                    {
                        if (currentTime - stopTime < CooldownDuration)
                        {
                            continue;
                        }
                    }

                    audioInstance.gameObject.SetActive(true);
                    return audioInstance;
                }
            }

            AudioInstance newInstance = CreateAudioSource(
                $"SFX Source {SfxPool.Count}",
                SfxPool[0].GetAudioSource().transform.parent,
                AudioType.SFX);
            SfxPool.Add(newInstance);
            newInstance.gameObject.SetActive(true);
            return newInstance;
        }

        public AudioInstance CreateAudioSource(string sourceName, Transform parent, AudioType audioType)
        {
            GameObject obj = new GameObject(sourceName);
            obj.transform.SetParent(parent);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = audioType == AudioType.BGM;

            AudioInstance audioInstance = obj.AddComponent<AudioInstance>();

            if (_audioMixer != null)
            {
                var groups = _audioMixer.FindMatchingGroups(audioType.ToString());
                if (groups.Length > 0)
                {
                    source.outputAudioMixerGroup = groups[0];
                }
            }

            return audioInstance;
        }

        public void Configure3DAudio(AudioSource source, Vector3 position, float minDistance = 1f, float maxDistance = 500f)
        {
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 1f;
            source.spread = 0f;
        }
    }
}
