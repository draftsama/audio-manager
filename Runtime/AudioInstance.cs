using UnityEngine;

namespace AM
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioInstance : MonoBehaviour
    {
        private AudioSource audioSource;
        private Transform followTransform;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (followTransform != null && audioSource != null && audioSource.isPlaying)
            {
                transform.position = followTransform.position;
            }
        }

        /// <summary>
        /// Set the transform to follow
        /// </summary>
        public void SetFollowTransform(Transform target)
        {
            followTransform = target;
        }

        /// <summary>
        /// Stop following transform
        /// </summary>
        public void StopFollowing()
        {
            followTransform = null;
        }

        /// <summary>
        /// Play the assigned audio clip
        /// </summary>
        public void Play()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Play();
            }
        }

        /// <summary>
        /// Stop the audio
        /// </summary>
        public void Stop()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
            StopFollowing();
        }

        /// <summary>
        /// Pause the audio
        /// </summary>
        public void Pause()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        /// <summary>
        /// Resume the audio
        /// </summary>
        public void Resume()
        {
            if (audioSource != null)
            {
                audioSource.UnPause();
            }
        }

        /// <summary>
        /// Check if audio is currently playing
        /// </summary>
        public bool IsPlaying()
        {
            return audioSource != null && audioSource.isPlaying;
        }

        /// <summary>
        /// Get the AudioSource component
        /// </summary>
        public AudioSource GetAudioSource()
        {
            return audioSource;
        }

        private void OnDisable()
        {
            // Clear follow transform when disabled
            StopFollowing();
        }
    }
}
