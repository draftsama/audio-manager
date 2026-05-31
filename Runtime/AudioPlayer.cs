using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
namespace AM
{
    public class AudioPlayer : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField, ValueDropdown("@AudioConstants.GetPropertiesName()")] private string clipName;
        [SerializeField] private AudioType audioType = AudioType.SFX;
        [SerializeField][Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool isLooping = false;
        [FormerlySerializedAs("playOnStart")]
        [SerializeField] private bool playOnEnable = true;

        [Header("3D Audio Settings")]
        [SerializeField] private bool uses3DAudio = false;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 500f;
        [SerializeField] private Transform followTransform = null;

        [SerializeField, ReadOnly] private AudioInstance audioInstance;

         private void OnEnable() {
   
            if (playOnEnable && !string.IsNullOrEmpty(clipName))
            {
                Play();
            }
        }

        /// <summary>
        /// Play the configured audio
        /// </summary>
        public void Play()
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning($"AudioPlayer on {gameObject.name}: No clip name set!");
                return;
            }

            Stop(); // Stop any currently playing audio

            if (audioType == AudioType.BGM)
            {
                audioInstance = AudioManager.PlayBGM(clipName, volume, isLooping);
            }
            else // SFX
            {
                if (uses3DAudio)
                {
                    // Pass followTransform to AudioManager
                    audioInstance = AudioManager.PlaySFX(clipName, transform.position, volume, isLooping, minDistance, maxDistance, followTransform);
                }
                else
                {
                    audioInstance = AudioManager.PlaySFX(clipName, volume, isLooping);
                }
            }
        }

        /// <summary>
        /// Stop the audio
        /// </summary>
        public void Stop()
        {
            if (audioInstance != null)
            {
                AudioManager.StopAudio(audioInstance);
                audioInstance = null;
            }
        }

        /// <summary>
        /// Pause the audio
        /// </summary>
        public void Pause()
        {
            if (audioInstance != null)
            {
                AudioManager.PauseAudio(audioInstance);
            }
        }

        /// <summary>
        /// Resume the audio
        /// </summary>
        public void Resume()
        {
            if (audioInstance != null)
            {
                AudioManager.ResumeAudio(audioInstance);
            }
        }

        /// <summary>
        /// Set the volume of the playing audio
        /// </summary>
        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (audioInstance != null)
            {
                AudioSource source = audioInstance.GetAudioSource();
                if (source != null)
                {
                    source.volume = volume;
                }
            }
        }

        /// <summary>
        /// Change the clip name and optionally play it
        /// </summary>
        public void SetClip(string newClipName, bool autoPlay = false)
        {
            clipName = newClipName;
            if (autoPlay)
            {
                Play();
            }
        }

        /// <summary>
        /// Check if the audio is currently playing
        /// </summary>
        public bool IsPlaying()
        {
            return audioInstance != null && audioInstance.IsPlaying();
        }

        void OnDestroy()
        {
            Stop();
        }

        void OnDisable()
        {
            if (audioInstance != null && audioInstance.IsPlaying())
            {
                Stop();
            }
        }
    }


}
