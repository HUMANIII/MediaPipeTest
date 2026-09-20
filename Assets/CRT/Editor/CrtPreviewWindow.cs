using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MediaPipeTest.CRT.Editor
{
    // Owns only a preview scene, mesh, and material instance; never edits a scene or renderer asset.
    internal sealed class CrtPreviewRenderer : IDisposable
    {
        readonly PreviewRenderUtility preview;
        readonly Mesh quad;
        readonly Material material;
        readonly Transform surface;
        public CrtPreviewRenderer()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/CRT/Shaders/CRTSurface.shadergraph");
            if (!shader) throw new InvalidOperationException("CRTSurface Shader Graph를 불러올 수 없습니다.");
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/CRT/Materials/CRTSurface.mat");
            material = template ? new Material(template) : new Material(shader);
            material.name = "CRT editor preview"; material.hideFlags = HideFlags.HideAndDontSave;
            quad = new Mesh { name = "CRT preview quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0,2,1,0,3,2 };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quad.tangents = new[] { new Vector4(1,0,0,-1),new Vector4(1,0,0,-1),new Vector4(1,0,0,-1),new Vector4(1,0,0,-1) };
            quad.RecalculateBounds();
            preview = new PreviewRenderUtility();
            var display = EditorUtility.CreateGameObjectWithHideFlags("CRT preview surface", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            preview.AddSingleGO(display);
            display.GetComponent<MeshFilter>().sharedMesh = quad;
            display.GetComponent<MeshRenderer>().sharedMaterial = material;
            surface = display.transform;
            var camera = preview.camera;
            camera.transform.position = new Vector3(0,0,-2);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true; camera.orthographicSize = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = false; camera.allowMSAA = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
        }

        internal Texture Render(CrtSettings settings, Texture source, bool original, int width = 1280, int height = 720)
        {
            settings.Apply(material, !original);
            material.SetTexture("_MainTex", source);
            float aspect = (float)width / height;
            material.SetVector("_FitScale", source ? CrtSettings.FitScale((float)source.width / source.height, aspect) : Vector2.one);
            preview.camera.aspect = aspect;
            surface.localScale = new Vector3(aspect,1,1);
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            preview.BeginPreview(new Rect(0,0,width/pixelsPerPoint,height/pixelsPerPoint), GUIStyle.none);
            // Batch validation has no GUI event, so PreviewRenderUtility cannot set this itself.
            preview.camera.pixelRect = new Rect(0,0,width,height);
            Texture result = null;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ShaderUtil.CompilePass(material, 0, true);
                // SubmitRenderRequest is URP's supported explicit camera rendering path.
                RenderPipeline.SubmitRenderRequest(preview.camera, new UniversalRenderPipeline.SingleCameraRequest {
                    destination = preview.camera.targetTexture
                });
            }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; result = preview.EndPreview(); }
            return result;
        }
        public void Dispose()
        {
            preview.Cleanup();
            UnityEngine.Object.DestroyImmediate(quad);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    public sealed class CrtPreviewWindow : EditorWindow
    {
        [SerializeField] CrtProfile profile;
        [SerializeField] Texture2D source;
        [SerializeField] bool original, actualPixels;
        Vector2 settingsScroll, imageScroll;
        SerializedObject profileObject;
        CrtPreviewRenderer renderer;
        Texture previewImage;
        string lastSettings;
        bool dirty = true;
        const string PatternPath = "Assets/CRT/Media/CRTTestPattern.png";

        [MenuItem("Tools/CRT/Profile Preview")]
        public static void OpenWindow() => Open(Selection.activeObject as CrtProfile);
        public static void Open(CrtProfile selected)
        {
            var window = GetWindow<CrtPreviewWindow>("CRT Preview");
            window.minSize = new Vector2(850, 540);
            if (selected) window.SetProfile(selected);
            else if (!window.profile) window.SetProfile(AssetDatabase.LoadAssetAtPath<CrtProfile>("Assets/CRT/Profiles/Classic.asset"));
            window.Show();
        }
        void SetProfile(CrtProfile selected)
        {
            profileObject?.Dispose();
            profile = selected;
            profileObject = profile ? new SerializedObject(profile) : null;
            dirty = true;
        }
        void OnEnable() { Undo.undoRedoPerformed += Changed; dirty = true; }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= Changed;
            profileObject?.Dispose(); profileObject = null;
            renderer?.Dispose(); renderer = null; previewImage = null;
        }
        void Changed() { dirty = true; Repaint(); }
        void OnInspectorUpdate()
        {
            string current = profile ? JsonUtility.ToJson(profile.settings) : "";
            if (current != lastSettings) { lastSettings = current; Changed(); }
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("프리셋 에셋을 직접 편집합니다. 이미지로 공통 효과를 확인하며, 동영상과 출력별 동작은 CRT Playground의 Play 모드에서 확인하세요.", MessageType.Info);
            var selected = (CrtProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(CrtProfile), false);
            if (selected != profile) SetProfile(selected);
            if (!profile) { EditorGUILayout.HelpBox("Assets > Create > CRT > Profile로 만든 에셋을 지정하세요.", MessageType.Info); return; }
            if (profileObject == null || profileObject.targetObject != profile) SetProfile(profile);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(310));
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            if (CrtProfileEditor.DrawSettings(profileObject)) dirty = true;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            source = (Texture2D)EditorGUILayout.ObjectField("이미지 (비우면 테스트 패턴)", source, typeof(Texture2D), false);
            EditorGUILayout.BeginHorizontal();
            original = GUILayout.Toggle(original, "원본 보기", "Button");
            actualPixels = GUILayout.Toggle(actualPixels, "실제 픽셀 크기", "Button");
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) dirty = true;
            var area = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            try
            {
                if (Event.current.type == EventType.Repaint && (dirty || !previewImage))
                {
                    if (renderer == null) renderer = new CrtPreviewRenderer();
                    previewImage = renderer.Render(profile.settings, source ? source : AssetDatabase.LoadAssetAtPath<Texture2D>(PatternPath), original);
                    dirty = false;
                }
                if (previewImage)
                {
                    if (actualPixels)
                    {
                        float scale = EditorGUIUtility.pixelsPerPoint;
                        var full = new Rect(0,0,previewImage.width/scale,previewImage.height/scale);
                        imageScroll = GUI.BeginScrollView(area, imageScroll, full);
                        GUI.DrawTexture(full, previewImage, ScaleMode.StretchToFill, false);
                        GUI.EndScrollView();
                    }
                    else GUI.DrawTexture(area, previewImage, ScaleMode.ScaleToFit, false);
                }
            }
            catch (Exception error)
            {
                GUI.Label(area, "미리보기 렌더링 실패: " + error.Message, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}
