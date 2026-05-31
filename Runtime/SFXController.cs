using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace AM
{
    public class SFXController : MonoBehaviour
    {
        private AudioMixer _audioMixer;
        private AudioPoolManager _poolManager;
        private Dictionary<string, AudioClip> _audioDictionary;

        public void Initialize(AudioMixer audioMixer, AudioPoolManager poolManager, Dictionary<string, AudioClip> audioDictionary)
        {
            _audioMixer = audioMixer;
            _poolManager = poolManager;
            _audioDictionary = audioDictionary;
        }

        public AudioInstance PlaySFX(string clipName, float volume = 1f, bool loop = false)
        {
            if (!_audioDictionary.TryGetValue(clipName, out AudioClip clip))
            {
                Debug.LogWarning($"SFX clip '{clipName}' not found!");
                return null;
            }

            if (loop)
            {
                foreach (var i in _poolManager.SfxPool)
                {
                    AudioSource existingSource = i.GetAudioSource();
                    if (existingSource.isPlaying && existingSource.loop && existingSource.clip == clip && existingSource.spatialBlend == 0f)
                    {
                        existingSource.volume = Mathf.Clamp01(volume);
                        return i;
                    }
                }
            }

            AudioInstance audioInstance = _poolManager.GetAvailableSFXSource();
            AudioSource source = audioInstance.GetAudioSource();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.loop = loop;
            source.spatialBlend = 0f;

            int index = _poolManager.SfxPool.IndexOf(audioInstance);
            source.gameObject.name = $"SFX Source {index} - {clipName}";

            source.Play();

            if (!loop)
            {
                WatchAndReturnSFX(audioInstance, source).Forget();
            }

            return audioInstance;
        }

        public AudioInstance PlaySFX(string clipName, Vector3 position, float volume = 1f, bool loop = false,
            float minDistance = 1f, float maxDistance = 500f, Transform followTransform = null)
        {
            if (!_audioDictionary.TryGetValue(clipName, out AudioClip clip))
            {
                Debug.LogWarning($"SFX clip '{clipName}' not found!");
                return null;
            }

            if (loop)
            {
                foreach (var i in _poolManager.SfxPool)
                {
                    AudioSource existingSource = i.GetAudioSource();
                    if (existingSource.isPlaying && existingSource.loop && existingSource.clip == clip && existingSource.spatialBlend > 0f)
                    {
                        existingSource.volume = Mathf.Clamp01(volume);
                        _poolManager.Configure3DAudio(existingSource, position, minDistance, maxDistance);
                        if (followTransform != null)
                        {
                            i.SetFollowTransform(followTransform);
                        }
                        return i;
                    }
                }
            }

            AudioInstance audioInstance = _poolManager.GetAvailableSFXSource();
            AudioSource source = audioInstance.GetAudioSource();
            _poolManager.Configure3DAudio(source, position, minDistance, maxDistance);
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.loop = loop;

            int index = _poolManager.SfxPool.IndexOf(audioInstance);
            source.gameObject.name = $"SFX Source {index} - {clipName}";

            if (followTransform != null)
            {
                audioInstance.SetFollowTransform(followTransform);
            }

            source.Play();

            if (!loop)
            {
                WatchAndReturnSFX(audioInstance, source).Forget();
            }

            return audioInstance;
        }

        public void StopAudio(AudioInstance audioInstance)
        {
            if (audioInstance == null)
            {
                Debug.LogWarning("AudioInstance is null!");
                return;
            }

            if (audioInstance.IsPlaying())
            {
                AudioSource source = audioInstance.GetAudioSource();
                audioInstance.Stop();

                if (source != null)
                {
                    source.loop = false;
                }

                _poolManager.LastStopTime[audioInstance] = Time.time;

                int sfxIndex = _poolManager.SfxPool.IndexOf(audioInstance);
                if (sfxIndex >= 0 && source != null)
                {
                    source.gameObject.name = $"SFX Source {sfxIndex}";
                    source.gameObject.SetActive(false);
                }
                else if (source != null)
                {
                    int bgmIndex = _poolManager.BgmPool.IndexOf(audioInstance);
                    if (bgmIndex >= 0)
                    {
                        source.gameObject.name = $"BGM Source {bgmIndex}";
                        source.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void PauseAudio(AudioInstance audioInstance)
        {
            if (audioInstance == null)
            {
                Debug.LogWarning("AudioInstance is null!");
                return;
            }

            AudioSource audioSource = audioInstance.GetAudioSource();
            if (audioSource != null && audioSource.isPlaying)
            {
                audioInstance.Pause();
            }
        }

        public void ResumeAudio(AudioInstance audioInstance)
        {
            if (audioInstance == null)
            {
                Debug.LogWarning("AudioInstance is null!");
                return;
            }

            AudioSource audioSource = audioInstance.GetAudioSource();
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioInstance.Resume();
            }
        }

        public void SetSFXVolume(float volume)
        {
            if (_audioMixer == null) return;
            float dbVolume = LinearToDecibel(volume);
            bool success = _audioMixer.SetFloat("SFXVolume", dbVolume);
            if (!success)
            {
                Debug.LogWarning("Failed to set SFXVolume. Make sure 'SFXVolume' parameter exists in AudioMixer and is exposed.");
            }
        }

        public void SetMasterVolume(float volume)
        {
            if (_audioMixer == null) return;
            float dbVolume = LinearToDecibel(volume);
            bool success = _audioMixer.SetFloat("MasterVolume", dbVolume);
            if (!success)
            {
                Debug.LogWarning("Failed to set MasterVolume. Make sure 'MasterVolume' parameter exists in AudioMixer and is exposed.");
            }
        }

        private async UniTaskVoid WatchAndReturnSFX(AudioInstance audioInstance, AudioSource source)
        {
            await UniTask.WaitUntil(
                () => !source.isPlaying || !source.gameObject.activeSelf,
                cancellationToken: source.GetCancellationTokenOnDestroy());

            if (source != null && !source.loop)
            {
                _poolManager.LastStopTime[audioInstance] = Time.time;
                int index = _poolManager.SfxPool.IndexOf(audioInstance);
                if (index >= 0) source.gameObject.name = $"SFX Source {index}";
                source.gameObject.SetActive(false);
            }
        }

        private static float LinearToDecibel(float volume) =>
            volume > 0 ? Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f : -80f;
    }
}
