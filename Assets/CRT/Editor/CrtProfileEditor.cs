using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.Editor
{
    [CustomEditor(typeof(CrtProfile))]
    public sealed class CrtProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("이 프리셋을 연결한 화면들이 설정을 공유합니다. 개별 설정은 에셋을 복제해서 연결하세요.", MessageType.Info);
            DrawSettings(serializedObject);
            if (GUILayout.Button("CRT 미리보기 열기")) CrtPreviewWindow.Open((CrtProfile)target);
        }

        internal static bool DrawSettings(SerializedObject serialized)
        {
            serialized.Update();
            var settings = serialized.FindProperty("settings");
            Group(settings, "밝기", "brightness");
            Group(settings, "RGB · 주사선", "rgbDensity", "rgbStrength", "scanlineCount", "scanlineWidth", "scanlineStrength");
            Group(settings, "픽셀 · 곡면", "pixelation", "virtualResolution", "curvature");
            Group(settings, "모노", "monochrome", "monoTint");
            Group(settings, "비네트", "vignette", "vignetteRadius", "vignetteSoftness");
            Group(settings, "부분 적용", "screenRect", "effectMask");
            return serialized.ApplyModifiedProperties();
        }

        static void Group(SerializedProperty settings, string label, params string[] fields)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            foreach (var field in fields)
            {
                var property = settings.FindPropertyRelative(field);
                EditorGUILayout.PropertyField(property, new GUIContent(property.displayName, property.tooltip));
            }
        }
    }

    [CustomEditor(typeof(CrtSurface)), CanEditMultipleObjects]
    public sealed class CrtSurfaceEditor : CrtComponentEditor { }
    [CustomEditor(typeof(CrtFullscreen)), CanEditMultipleObjects]
    public sealed class CrtFullscreenEditor : CrtComponentEditor { }

    public abstract class CrtComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var profile = serializedObject.FindProperty("profile");
            bool shared = profile.objectReferenceValue != null && !profile.hasMultipleDifferentValues;
            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                using (new EditorGUI.DisabledScope(iterator.name == "m_Script" || (shared && iterator.name == "settings")))
                    EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();
            if (shared)
            {
                EditorGUILayout.HelpBox("Profile 값이 적용됩니다. 아래 로컬 Settings는 Profile을 해제했을 때 사용합니다.", MessageType.Info);
                if (GUILayout.Button("프로필 편집 / 미리보기")) CrtPreviewWindow.Open((CrtProfile)profile.objectReferenceValue);
            }
        }
    }
}
