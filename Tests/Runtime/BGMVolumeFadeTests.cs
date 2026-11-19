using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AM.Tests
{
    public class BGMVolumeFadeTests
    {
        private const string MixerGuid = "6dd9c6add5d75441690ad4a06fe762b1";

        private GameObject _ManagerObject;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(MixerGuid));
            Assert.That(mixer, Is.Not.Null, "AudioMixer asset not found");

            _ManagerObject = new GameObject("AudioManager (test)");
            _ManagerObject.SetActive(false);
            var manager = _ManagerObject.AddComponent<AudioManager>();
            var so = new UnityEditor.SerializedObject(manager);
            so.FindProperty("audioMixer").objectReferenceValue = mixer;
            so.ApplyModifiedPropertiesWithoutUndo();
            _ManagerObject.SetActive(true);
#else
            Assert.Ignore("Needs the Editor to load the AudioMixer asset.");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_ManagerObject != null) Object.Destroy(_ManagerObject);
        }

        [UnityTest]
        public IEnumerator SetBGMVolume_with_fade_moves_through_intermediate_values_to_the_target() => UniTask.ToCoroutine(async () =>
        {
            AudioManager.SetBGMVolume(1f);

            var fade = AudioManager.SetBGMVolume(0.5f, 1f);
            await UniTask.Delay(TimeSpan.FromSeconds(0.2), ignoreTimeScale: true);

            float midway = ReadBGMVolume();
            Assert.That(midway, Is.LessThan(1f).And.GreaterThan(0.5f));

            await fade;

            Assert.That(ReadBGMVolume(), Is.EqualTo(0.5f).Within(0.001f));
        });

        private static float ReadBGMVolume()
        {
            Assert.That(AudioManager.Instance.AudioMixer.GetFloat("BGMVolume", out float db), Is.True);
            return Mathf.Pow(10f, db / 20f);
        }
    }
}
