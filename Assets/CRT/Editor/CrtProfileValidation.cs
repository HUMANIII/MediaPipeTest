using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MediaPipeTest.CRT.Editor
{
    public static class CrtProfileValidation
    {
        static bool buildAfterPreview;
        public static void PreviewAndBuild() { buildAfterPreview = true; ValidatePreview(); }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("CRT profile validation: " + message); }

        public static void ValidateData()
        {
            var profiles = CrtProjectSetup.CreateProfiles();
            using (var serialized = new SerializedObject(profiles[0]))
            {
                var fields = typeof(CrtSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                Check(fields.Length == 16, "Expected 16 adjustable settings");
                foreach (var field in fields)
                    Check(!string.IsNullOrWhiteSpace(serialized.FindProperty("settings." + field.Name).tooltip), "Missing tooltip: " + field.Name);
            }
            string[] before = profiles.Select(p => EditorJsonUtility.ToJson(p)).ToArray();
            var copy = profiles[0].settings.Clone();
            copy.monochrome = .75f; copy.monoTint = Color.magenta; copy.vignetteRadius = .5f; copy.vignetteSoftness = 1.5f;
            var destination = new CrtSettings(); copy.CopyTo(destination);
            Check(JsonUtility.ToJson(copy) == JsonUtility.ToJson(destination), "Settings copy omitted fields");
            Check(!ReferenceEquals(copy, profiles[0].settings), "Runtime settings alias the profile");
            var defaults = new CrtSettings();
            Check(defaults.monochrome == 0 && defaults.monoTint == Color.white && defaults.vignetteRadius == 0 && defaults.vignetteSoftness == 1, "Legacy defaults");
            foreach (string path in Directory.GetFiles("Assets/CRT/Shaders", "*.shadergraph"))
            {
                var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(path.Replace('\\','/')));
                try
                {
                    Check(!material.HasProperty("_Strength"), "Global blend property still exposed: " + path);
                    foreach (string property in new[] { "_Monochrome", "_MonoTint", "_VignetteRadius", "_VignetteSoftness" })
                        Check(material.HasProperty(property), path + " missing " + property);
                    copy.monochrome = 2; copy.vignetteRadius = -1; copy.vignetteSoftness = 0;
                    copy.Apply(material);
                    Check(material.GetFloat("_Monochrome") == 1 && material.GetFloat("_VignetteRadius") == 0 && material.GetFloat("_VignetteSoftness") == .05f, "Boundary clamp");
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            }
            string scratch = AssetDatabase.GenerateUniqueAssetPath("Assets/CRT/Editor/ProfileValidation.asset");
            var asset = ScriptableObject.CreateInstance<CrtProfile>();
            try
            {
                asset.settings = destination;
                AssetDatabase.CreateAsset(asset, scratch);
                Undo.RegisterCompleteObjectUndo(asset, "CRT validation edit");
                asset.settings.monochrome = .125f;
                EditorUtility.SetDirty(asset);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(asset.settings.monochrome == .75f, "Undo");
                Undo.PerformRedo();
                Check(asset.settings.monochrome == .125f, "Redo");
                AssetDatabase.SaveAssetIfDirty(asset);
                AssetDatabase.ImportAsset(scratch, ImportAssetOptions.ForceUpdate);
                Check(AssetDatabase.LoadAssetAtPath<CrtProfile>(scratch).settings.monochrome == .125f, "Asset persistence");
                string serialized = File.ReadAllText(scratch);
                Check(serialized.Contains("monochrome: 0.125"), "Serialized value");
                CrtProjectSetup.CreateProfiles();
                for (int i = 0; i < profiles.Length; i++)
                    Check(before[i] == EditorJsonUtility.ToJson(profiles[i]), "Preset was modified by copy or regeneration");
            }
            finally { Undo.ClearUndo(asset); AssetDatabase.DeleteAsset(scratch); }
            Debug.Log("CRT_PROFILE_DATA PASS");
        }

        public static void ValidatePreview()
        {
            var routine = PreviewSteps();
            double next = EditorApplication.timeSinceStartup + .2;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .2;
                try
                {
                    if (routine.MoveNext()) return;
                    EditorApplication.update -= tick;
                    if (buildAfterPreview) CrtProjectSetup.BuildWindows();
                    buildAfterPreview = false;
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
                catch (Exception error)
                {
                    EditorApplication.update -= tick;
                    (routine as IDisposable)?.Dispose();
                    Debug.LogException(error);
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            };
            EditorApplication.update += tick;
        }

        static IEnumerator PreviewSteps()
        {
            ValidateData();
            string[] arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "-crtOutput");
            string output = outputIndex >= 0 && outputIndex + 1 < arguments.Length ? arguments[outputIndex + 1] : "Logs/CRTProfiles/Editor";
            Directory.CreateDirectory(output);
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CRT/Media/CRTTestPattern.png");
            var settings = new CrtSettings();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            int previewScenes = EditorSceneManager.previewSceneCount;
            using (var renderer = new CrtPreviewRenderer())
            {
                yield return null;
                Save(renderer.Render(settings, source, false), output + "/01-classic.png");
                yield return null;
                Save(renderer.Render(settings, source, true), output + "/00-original.png");
                yield return null;
                settings.monochrome = 1;
                Save(renderer.Render(settings, source, false), output + "/02-mono.png");
                yield return null;
                settings.monoTint = new Color(.2f,1,.3f);
                Save(renderer.Render(settings, source, false), output + "/03-green.png");
                yield return null;
                settings.vignette = 1; settings.vignetteRadius = .5f; settings.vignetteSoftness = 1.5f;
                Save(renderer.Render(settings, source, false), output + "/04-vignette.png");
            }
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                using (var renderer = new CrtPreviewRenderer()) renderer.Render(settings, source, false, 320,180);
            }
            Check(EditorSceneManager.previewSceneCount == previewScenes, "Preview scene leak");
            Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m => m.name == "CRT editor preview"), "Preview material leak");
            Check(!Resources.FindObjectsOfTypeAll<Mesh>().Any(m => m.name == "CRT preview quad"), "Preview mesh leak");
            Check(scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() && wasDirty == scene.isDirty, "Preview changed the active scene");
            File.WriteAllText(output + "/result.json", "{\"passed\":true,\"renderAndDisposeCycles\":9,\"sceneUnchanged\":true}");
            Debug.Log("CRT_PROFILE_PREVIEW PASS");
        }
        static void Save(Texture image, string path)
        {
            Check(image is RenderTexture, "Preview did not return a render texture");
            var previous = RenderTexture.active;
            var texture = new Texture2D(image.width,image.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active = (RenderTexture)image;
                texture.ReadPixels(new Rect(0,0,image.width,image.height),0,0); texture.Apply();
                var pixels = texture.GetPixels32();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Check(pixels.Count(p => p.r > 40 || p.g > 40 || p.b > 40) > pixels.Length / 10, "Black preview: " + path);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
