using System;
using System.IO;
using System.Linq;
using MediaPipeTest.CRT.AudioPreview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MediaPipeTest.CRT.Surveillance.Editor
{
    public static class SurveillanceAudioSetup
    {
        const string PresetPath = "Assets/CRT/AudioPreview/Presets/DefaultCRT.asset";
        const string AudioRoot = SurveillanceSceneBuilder.Root + "/Audio/Footsteps/";

        [MenuItem("Tools/CRT/Surveillance/Apply CRT Footstep Audio")]
        public static void ApplyToSavedDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before applying scene audio.");
            var scene = SceneManager.GetSceneByPath(SurveillanceSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            var previous = SceneManager.GetActiveScene();
            if (opened) scene = EditorSceneManager.OpenScene(SurveillanceSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                T One<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                Configure(One<SurveillanceFeed>(), One<HospitalWanderer>());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("SURVEILLANCE_AUDIO_SETUP_PASS saved preset=DefaultCRT clips=6");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static SurveillanceFootstepAudio Configure(SurveillanceFeed feed, HospitalWanderer walker)
        {
            var preset = AssetDatabase.LoadAssetAtPath<CrtAudioPreset>(PresetPath);
            if (!preset) throw new InvalidOperationException("Missing saved CRT audio preset: " + PresetPath);
            var clips = new[] { "L1", "R1", "L2", "R2", "L3", "R3" }.Select(name =>
            {
                string path = AudioRoot + "A_Stone_" + name + ".wav";
                if (!File.Exists(path)) throw new FileNotFoundException("Run Tools/CRT/extract_approved_footsteps.py first.", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings; importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }).ToArray();
            var audio = feed.staticAudio.GetComponentInChildren<SurveillanceFootstepAudio>(true);
            if (!audio)
            {
                var speaker = new GameObject("CCTV Footstep Speaker");
                Undo.RegisterCreatedObjectUndo(speaker, "Add CRT footsteps");
                speaker.transform.SetParent(feed.staticAudio.transform, false);
                speaker.layer = feed.staticAudio.gameObject.layer;
                // The imported CRT root carries axis/unit conversion; use world position.
                speaker.transform.position = feed.screen.GetComponent<Renderer>().bounds.center;
                audio = Undo.AddComponent<SurveillanceFootstepAudio>(speaker);
                var source = speaker.GetComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 1;
                source.dopplerLevel = 0; source.minDistance = 1.5f; source.maxDistance = 8;
                source.rolloffMode = AudioRolloffMode.Linear; source.volume = 1;
            }
            Undo.RecordObject(audio, "Configure CRT footsteps");
            audio.feed = feed; audio.walker = walker; audio.footsteps = clips;
            if (!audio.preset) audio.preset = preset;
            EditorUtility.SetDirty(audio);
            PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
            return audio;
        }
    }
}
