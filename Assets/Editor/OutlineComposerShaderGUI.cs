using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Fullscreen.ShaderGraph;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class OutlineComposerShaderGUI : FullscreenShaderGUI
{
    // Shader Graph Reference 이름을 기준으로 툴팁을 등록합니다.
    // 툴팁을 추가하려면 이 Dictionary에 항목만 더하면 됩니다.
    private static readonly IReadOnlyDictionary<string, string> Tooltips =
        new Dictionary<string, string>
        {
            ["_FLEXIBLETHICKNESS"] =
                "활성화하면 Thickness 값에 따라 주변 마스크 픽셀을 추가로 샘플링합니다. " +
                "\n 비활성화하면 기본 1픽셀 외곽선 검출을 사용합니다." +
                "\n 비활성화 시 성능이 향상될 수 있으나 품질이 낮아집니다.",
            ["_Thickness"] =
                "Flexible Thickness가 활성화된 경우 사용할 외곽선 탐색 거리입니다.",
            ["_OutlineColor"] =
                "검출된 외곽선에 적용할 색상입니다."
        };

    public override void OnGUI(
        MaterialEditor materialEditor,
        MaterialProperty[] properties)
    {
        EditorGUILayout.LabelField("Surface Inputs", EditorStyles.boldLabel);

        foreach (MaterialProperty property in properties)
        {
            if ((property.propertyFlags &
                 (ShaderPropertyFlags.HideInInspector |
                  ShaderPropertyFlags.PerRendererData)) != 0)
            {
                continue;
            }

            Tooltips.TryGetValue(property.name, out string tooltip);

            materialEditor.ShaderProperty(
                property,
                new GUIContent(property.displayName, tooltip));
        }
    }
}
