using UnityEngine;
using UnityEngine.Audio;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace AM
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    // Must initialise after AudioManager (order -100) but before audio-playing components
    [DefaultExecutionOrder(-50)]
    public class AudioOutput : MonoBehaviour
    {
        [SerializeField] private AudioType audioType;

        private async UniTaskVoid Awake()
        {
            var audioSource = GetComponent<AudioSource>();

            // Wait for AudioManager instance to be ready
            await UniTask.WaitUntil(() => AudioManager.Instance != null, cancellationToken: this.GetCancellationTokenOnDestroy());

            // Get audioMixer from AudioManager instance
            var audioMixer = AudioManager.Instance.AudioMixer;

            // Assign to audio mixer group based on audio type
            if (audioMixer != null)
            {
                var groups = audioMixer.FindMatchingGroups(audioType.ToString());
                if (groups.Length > 0)
                {
                    audioSource.outputAudioMixerGroup = groups[0];
                }
            }
        }

       
      
    }
}

