using UnityEngine;
using UnityEngine.InputSystem;
using AM;

namespace AM.Demo
{
    /// <summary>
    /// AudioManager Demo — Keyboard Controls
    /// ─────────────────────────────────────
    /// BGM:
    ///   [1]  Play BGM slot 1
    ///   [2]  Play BGM slot 2
    ///   [3]  Play BGM slot 3
    ///   [S]  Stop current BGM
    ///   [P]  Pause / Resume current BGM
    ///
    /// SFX:
    ///   [Q]  Play SFX slot 1
    ///   [W]  Play SFX slot 2
    ///   [E]  Play SFX slot 3
    ///
    /// Volume:
    ///   [↑] / [↓]  Master volume +/- 0.1
    ///   [B]  Log current BGM name
    /// </summary>
    public class AudioManagerDemo : MonoBehaviour
    {
        [Header("BGM Clip Names (must match AudioManager list)")]
        [SerializeField] private string bgm1 = "BGM_1";
        [SerializeField] private string bgm2 = "BGM_2";
        [SerializeField] private string bgm3 = "BGM_3";

        [Header("SFX Clip Names (must match AudioManager list)")]
        [SerializeField] private string sfx1 = "SFX_1";
        [SerializeField] private string sfx2 = "SFX_2";
        [SerializeField] private string sfx3 = "SFX_3";

        [Header("Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float volumeStep = 0.1f;

        private float _masterVolume = 1f;
        private bool _bgmPaused = false;
        private string _currentBGMName = string.Empty;

        private void Update()
        {
            if (Keyboard.current == null) return;

            HandleBGMInput();
            HandleSFXInput();
            HandleVolumeInput();
            HandleUtilityInput();
        }

        private void HandleBGMInput()
        {
            var kb = Keyboard.current;

            if (kb.digit1Key.wasPressedThisFrame) PlayBGM(bgm1, "BGM 1");
            if (kb.digit2Key.wasPressedThisFrame) PlayBGM(bgm2, "BGM 2");
            if (kb.digit3Key.wasPressedThisFrame) PlayBGM(bgm3, "BGM 3");

            if (kb.sKey.wasPressedThisFrame)
            {
                AudioManager.StopBGM();
                _currentBGMName = string.Empty;
                _bgmPaused = false;
                Debug.Log("[AudioDemo] BGM Stopped");
            }

            if (kb.sKey.wasPressedThisFrame && kb.leftShiftKey.isPressed)
            {
                AudioManager.StopBGMImmediate();
                _currentBGMName = string.Empty;
                _bgmPaused = false;
                Debug.Log("[AudioDemo] BGM Stopped Immediately");

            }

           



            if (kb.pKey.wasPressedThisFrame)
            {
                var current = AudioManager.Instance?.CurrentBGM;
                if (current == null) return;

                if (_bgmPaused)
                {
                    AudioManager.ResumeAudio(current);
                    _bgmPaused = false;
                    Debug.Log("[AudioDemo] BGM Resumed");
                }
                else
                {
                    AudioManager.PauseAudio(current);
                    _bgmPaused = true;
                    Debug.Log("[AudioDemo] BGM Paused");
                }
            }
        }

        private void HandleSFXInput()
        {
            var kb = Keyboard.current;

            if (kb.qKey.wasPressedThisFrame) PlaySFX(sfx1, "SFX 1");
            if (kb.wKey.wasPressedThisFrame) PlaySFX(sfx2, "SFX 2");
            if (kb.eKey.wasPressedThisFrame) PlaySFX(sfx3, "SFX 3");
        }

        private void HandleVolumeInput()
        {
            var kb = Keyboard.current;

            if (kb.upArrowKey.wasPressedThisFrame)
            {
                _masterVolume = Mathf.Clamp01(_masterVolume + volumeStep);
                AudioManager.SetMasterVolume(_masterVolume);
                Debug.Log($"[AudioDemo] Master Volume: {_masterVolume:F1}");
            }

            if (kb.downArrowKey.wasPressedThisFrame)
            {
                _masterVolume = Mathf.Clamp01(_masterVolume - volumeStep);
                AudioManager.SetMasterVolume(_masterVolume);
                Debug.Log($"[AudioDemo] Master Volume: {_masterVolume:F1}");
            }
        }

        private void HandleUtilityInput()
        {
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                var info = string.IsNullOrEmpty(_currentBGMName) ? "None" : _currentBGMName;
                Debug.Log($"[AudioDemo] Current BGM: {info}  |  Paused: {_bgmPaused}  |  Master Vol: {_masterVolume:F1}");
            }
        }

        private void PlayBGM(string clipName, string label)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning($"[AudioDemo] {label} clip name is empty.");
                return;
            }

            _bgmPaused = false;
            _currentBGMName = clipName;
            AudioManager.PlayBGM(clipName, bgmVolume);
            Debug.Log($"[AudioDemo] Playing {label}: \"{clipName}\"");
        }

        private void PlaySFX(string clipName, string label)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning($"[AudioDemo] {label} clip name is empty.");
                return;
            }

            AudioManager.PlaySFX(clipName, sfxVolume);
            Debug.Log($"[AudioDemo] Playing {label}: \"{clipName}\"");
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 10, 10)
            };

            var content =
                "<b>AudioManager Demo</b>\n" +
                "──────────────────────\n" +
                "[1] Play BGM 1\n" +
                "[2] Play BGM 2\n" +
                "[3] Play BGM 3\n" +
                "[S] Stop BGM\n" +
                "[P] Pause / Resume BGM\n" +
                "──────────────────────\n" +
                "[Q] Play SFX 1\n" +
                "[W] Play SFX 2\n" +
                "[E] Play SFX 3\n" +
                "──────────────────────\n" +
                "[↑/↓] Master Volume\n" +
                "[B]  Log BGM Info\n" +
                $"──────────────────────\n" +
                $"BGM: {(_currentBGMName == string.Empty ? "None" : _currentBGMName)}\n" +
                $"Vol: {_masterVolume:F1}  Paused: {_bgmPaused}";

            GUI.Box(new Rect(10, 10, 220, 310), content, style);
        }
    }
}
