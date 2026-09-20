using System;
using UnityEngine;

namespace MediaPipeTest.CRT
{
    [Serializable]
    public sealed class CrtSettings
    {
        [Tooltip("가로 방향으로 반복되는 RGB 발광 패턴의 수입니다. 높이면 무늬가 작고 촘촘해집니다. 영상의 픽셀 크기는 Virtual Resolution으로 별도 조절합니다.")]
        [Range(1, 1920)] public float rgbDensity = 240;
        [Tooltip("RGB 발광 무늬의 색상 대비를 조절합니다. 0이면 무늬가 사라지고, 높이면 빨강·초록·파랑의 구분이 뚜렷해집니다.")]
        [Range(0, 1)] public float rgbStrength = .35f;
        [Tooltip("화면 세로 방향에 배치되는 가로 주사선의 수입니다. 높이면 선 사이의 간격이 좁아집니다.")]
        [Range(1, 1080)] public float scanlineCount = 180;
        [Tooltip("각 주사선에서 어두운 띠의 두께를 조절합니다. 높이면 어두운 부분이 넓어집니다.")]
        [Range(.02f, .95f)] public float scanlineWidth = .35f;
        [Tooltip("주사선의 어두운 정도를 조절합니다. 0이면 주사선이 사라지고, 높이면 선이 진해집니다.")]
        [Range(0, 1)] public float scanlineStrength = .3f;
        [Tooltip("영상을 나눌 가상의 가로·세로 픽셀 수입니다. 낮추면 영상의 픽셀 덩어리가 커집니다. Pixelation이 0이면 적용되지 않으며 RGB 발광 패턴의 밀도와는 독립적입니다.")]
        public Vector2 virtualResolution = new Vector2(640, 360);
        [Tooltip("영상 샘플 좌표를 가상 픽셀 중심에 맞추는 정도입니다. 0이면 적용하지 않고, 1이면 Virtual Resolution으로 정한 격자에 완전히 맞춥니다.")]
        [Range(0, 1)] public float pixelation;
        [Tooltip("영상 좌표를 휘어 볼록한 CRT 화면처럼 보이게 합니다. 0이면 평평하고, 높이면 왜곡이 커집니다. 메시 자체는 변형하지 않습니다.")]
        [Range(0, .5f)] public float curvature = .08f;
        [Tooltip("화면 가장자리를 어둡게 만드는 강도입니다. 0이면 비네트를 끄고, 높이면 가장자리가 더 어두워집니다.")]
        [Range(0, 1)] public float vignette = .25f;
        [Tooltip("비네트로 어두워지지 않는 중앙 영역의 크기입니다. 높이면 밝은 중앙 영역이 넓어집니다. Vignette가 0이면 영향이 없습니다.")]
        [Range(0, 1)] public float vignetteRadius;
        [Tooltip("중앙에서 가장자리로 어두워지는 변화 곡선입니다. 낮으면 바깥쪽에서 급하게, 높으면 넓은 범위에서 점진적으로 어두워집니다. Vignette가 0이면 영향이 없습니다.")]
        [Range(.05f, 2)] public float vignetteSoftness = 1;
        [Tooltip("컬러를 단색으로 바꾸는 정도입니다. 0은 컬러, 1은 완전한 단색이며 중간값은 색을 일부 남깁니다. 1에서는 RGB 발광 무늬에도 색이 남지 않습니다.")]
        [Range(0, 1)] public float monochrome;
        [Tooltip("단색 화면에 사용할 색입니다. 흰색이면 흑백, 녹색이면 녹색 모니터처럼 표현됩니다. Monochrome이 0이면 영향이 없습니다.")]
        [ColorUsage(false)] public Color monoTint = Color.white;
        [Tooltip("CRT 결과의 밝기 배율입니다. 1은 배율 변경 없음, 0은 검정, 2는 두 배입니다. 주사선과 비네트로 줄어든 밝기를 보정할 때 사용합니다.")]
        [Range(0, 2)] public float brightness = 1.12f;
        [Tooltip("효과를 적용할 사각형을 (시작 X, 시작 Y, 너비, 높이) 순서로 지정합니다. 로컬 UV의 0~1 좌표를 사용하며 (0, 0, 1, 1)은 전체 화면입니다. 영역 밖은 원본을 유지합니다.")]
        public Vector4 screenRect = new Vector4(0, 0, 1, 1);
        [Tooltip("텍스처의 빨간 채널로 적용 영역을 지정합니다. 흰색은 CRT, 검정은 원본이며 비우면 Screen Rect 전체에 적용됩니다. 회색은 두 결과를 혼합하므로 곡면 왜곡이 있으면 윤곽이 겹쳐 보일 수 있습니다.")]
        public Texture2D effectMask;

        public void Apply(Material material, bool enabled = true)
        {
            if (!material) return;
            if (enabled) material.EnableKeyword("_CRT_EFFECT_ON");
            else material.DisableKeyword("_CRT_EFFECT_ON");
            material.SetFloat("_RGBDensity", Mathf.Clamp(rgbDensity, 1, 1920));
            material.SetFloat("_RGBStrength", Mathf.Clamp01(rgbStrength));
            material.SetFloat("_ScanlineCount", Mathf.Clamp(scanlineCount, 1, 1080));
            material.SetFloat("_ScanlineWidth", Mathf.Clamp(scanlineWidth, .02f, .95f));
            material.SetFloat("_ScanlineStrength", Mathf.Clamp01(scanlineStrength));
            material.SetVector("_VirtualResolution", new Vector4(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y), 0, 0));
            material.SetFloat("_Pixelation", Mathf.Clamp01(pixelation));
            material.SetFloat("_Curvature", Mathf.Clamp(curvature, 0, .5f));
            material.SetFloat("_Vignette", Mathf.Clamp01(vignette));
            material.SetFloat("_VignetteRadius", Mathf.Clamp01(vignetteRadius));
            material.SetFloat("_VignetteSoftness", Mathf.Clamp(vignetteSoftness, .05f, 2));
            material.SetFloat("_Monochrome", Mathf.Clamp01(monochrome));
            material.SetColor("_MonoTint", monoTint);
            material.SetFloat("_Brightness", Mathf.Clamp(brightness, 0, 2));
            material.SetVector("_ScreenRect", new Vector4(screenRect.x, screenRect.y, Mathf.Max(.0001f, screenRect.z), Mathf.Max(.0001f, screenRect.w)));
            material.SetTexture("_EffectMask", effectMask ? effectMask : Texture2D.whiteTexture);
        }

        // Copy values into an existing object, retaining bindings held by surfaces and UI.
        public void CopyTo(CrtSettings destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.rgbDensity = rgbDensity; destination.rgbStrength = rgbStrength;
            destination.scanlineCount = scanlineCount; destination.scanlineWidth = scanlineWidth; destination.scanlineStrength = scanlineStrength;
            destination.virtualResolution = virtualResolution; destination.pixelation = pixelation; destination.curvature = curvature;
            destination.vignette = vignette; destination.vignetteRadius = vignetteRadius; destination.vignetteSoftness = vignetteSoftness;
            destination.monochrome = monochrome; destination.monoTint = monoTint; destination.brightness = brightness;
            destination.screenRect = screenRect; destination.effectMask = effectMask;
        }

        public CrtSettings Clone()
        {
            var copy = new CrtSettings(); CopyTo(copy); return copy;
        }

        public static Vector2 FitScale(float sourceAspect, float displayAspect)
        {
            sourceAspect = Mathf.Max(.0001f, sourceAspect);
            displayAspect = Mathf.Max(.0001f, displayAspect);
            return displayAspect > sourceAspect
                ? new Vector2(displayAspect / sourceAspect, 1)
                : new Vector2(1, sourceAspect / displayAspect);
        }
    }
}
