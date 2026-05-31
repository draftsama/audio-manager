using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace AM
{
    public class BGMController : MonoBehaviour
    {
        private AudioMixer _audioMixer;
        private AudioPoolManager _poolManager;
        private float _fadeDuration;

        private AudioInstance _currentBGM;
        public AudioInstance CurrentBGM => _currentBGM;
        private AudioInstance _nextBGM;
        private Coroutine _fadeCoroutine;

        public void Initialize(AudioMixer mixer, AudioPoolManager poolManager, float fadeDuration)
        {
            _audioMixer = mixer;
            _poolManager = poolManager;
            _fadeDuration = fadeDuration;

            _currentBGM = poolManager.BgmPool[0];
            _nextBGM = poolManager.BgmPool[1];
        }

        public AudioInstance PlayBGM(string clipName, AudioClip clip, float volume = 1f, bool loop = true)
        {
            AudioSource currentSource = _currentBGM.GetAudioSource();
            if (currentSource.clip == clip && currentSource.isPlaying)
            {
                currentSource.volume = Mathf.Clamp01(volume);
                currentSource.loop = loop;
                return _currentBGM;
            }

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(CrossFadeBGM(clip, volume, loop));
            return _nextBGM;
        }

        public void StopBGM()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeOutBGM());
        }

        public void StopBGMImmediate()
        {
            AudioSource currentSource = _currentBGM.GetAudioSource();
            AudioSource nextSource = _nextBGM.GetAudioSource();

            currentSource.Stop();
            _poolManager.LastStopTime[_currentBGM] = Time.time;
            int currentIndex = _poolManager.BgmPool.IndexOf(_currentBGM);
            currentSource.gameObject.name = $"BGM Source {currentIndex}";
            currentSource.gameObject.SetActive(false);

            nextSource.Stop();
            _poolManager.LastStopTime[_nextBGM] = Time.time;
            int nextIndex = _poolManager.BgmPool.IndexOf(_nextBGM);
            nextSource.gameObject.name = $"BGM Source {nextIndex}";
            nextSource.gameObject.SetActive(false);
        }

        public void SetBGMVolume(float volume)
        {
            if (_audioMixer == null) return;
            float dbVolume = LinearToDecibel(volume);
            bool success = _audioMixer.SetFloat("BGMVolume", dbVolume);
            if (!success)
            {
                Debug.LogWarning("Failed to set BGMVolume. Make sure 'BGMVolume' parameter exists in AudioMixer and is exposed.");
            }
        }

        private IEnumerator CrossFadeBGM(AudioClip newClip, float targetVolume, bool loop)
        {
            AudioSource nextSource = _nextBGM.GetAudioSource();
            AudioSource currentSource = _currentBGM.GetAudioSource();

            nextSource.gameObject.SetActive(true);
            nextSource.clip = newClip;
            nextSource.volume = 0f;
            nextSource.loop = loop;

            int index = _poolManager.BgmPool.IndexOf(_nextBGM);
            nextSource.gameObject.name = $"BGM Source {index} - {newClip.name}";

            nextSource.Play();

            float elapsed = 0f;
            float currentVolume = currentSource.volume;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _fadeDuration;

                currentSource.volume = Mathf.Lerp(currentVolume, 0f, t);
                nextSource.volume = Mathf.Lerp(0f, Mathf.Clamp01(targetVolume), t);

                yield return null;
            }

            currentSource.volume = 0f;
            nextSource.volume = Mathf.Clamp01(targetVolume);

            currentSource.Stop();
            _poolManager.LastStopTime[_currentBGM] = Time.time;
            int oldIndex = _poolManager.BgmPool.IndexOf(_currentBGM);
            currentSource.gameObject.name = $"BGM Source {oldIndex}";
            currentSource.gameObject.SetActive(false);

            AudioInstance temp = _currentBGM;
            _currentBGM = _nextBGM;
            _nextBGM = temp;

            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutBGM()
        {
            AudioSource currentSource = _currentBGM.GetAudioSource();
            float elapsed = 0f;
            float currentVolume = currentSource.volume;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _fadeDuration;

                currentSource.volume = Mathf.Lerp(currentVolume, 0f, t);

                yield return null;
            }

            currentSource.volume = 0f;
            currentSource.Stop();

            _poolManager.LastStopTime[_currentBGM] = Time.time;
            int index = _poolManager.BgmPool.IndexOf(_currentBGM);
            currentSource.gameObject.name = $"BGM Source {index}";
            currentSource.gameObject.SetActive(false);

            _fadeCoroutine = null;
        }

        private static float LinearToDecibel(float volume) =>
            volume > 0 ? Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f : -80f;
    }
}
