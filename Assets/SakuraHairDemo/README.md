# Sakura Hair Demo

[SakuraHairDemo.unity](../06.Scenes/SakuraHairDemo.unity)를 열고 **Play**를 누르면 머리카락 윤곽 안에서 꽃잎이 독립적으로 떨어집니다. 월드 아래 방향 `(0, -1, 0)`은 유지됩니다.

## 지금까지의 구현 과정

| 단계 | 적용한 내용 | 실행 기록 |
|---|---|---|
| 1. 전용 씬과 표면 무늬 | Henshin의 시작 의상·자세를 복사한 독립 씬, 상반신 카메라·배경·조명, URP Lit Shader Graph와 Hair1 전용 머티리얼을 구성했습니다. SakuraPetal 리소스로 색상·마스크를 준비하고 UV 기반의 여러 꽃잎 층을 합성했습니다. | [초기 영상](../../Captures/SakuraHairDemo/SakuraHairDemo.mp4) |
| 2. 월드 아래 방향 | 꽃잎 좌표를 월드 XY·ZY 평면에 투영해 회전된 UV와 관계없이 위에서 아래로 흐르게 했습니다. 캐릭터를 25도 기울인 상태도 확인했습니다. | [월드 좌표 버전](../../Captures/SakuraHairDemo/WorldDown/README.md) |
| 3. 머리 안의 독립 낙하 — 현재 | 표면 투영을 시선과 가상 꽃잎 평면의 교차 계산으로 교체했습니다. 꽃잎마다 별도 3D 위치·속도·회전을 부여하고 깊이에 따른 시차·음영을 추가했습니다. 스킨드 메시의 실제 월드 경계를 공급하는 컴포넌트를 연결했습니다. | [최신 영상과 검증](../../Captures/SakuraHairDemo/InteriorFall/README.md) |

현재 씬과 머티리얼에는 3단계가 적용되어 있습니다. 이전 단계의 영상은 비교를 위해 보관한 기록이며 현재 동작과 다릅니다.

## 표현 방식

머리카락을 작은 내부 공간이 보이는 창처럼 사용합니다. 셰이더가 96개의 후보 꽃잎에 서로 다른 3차원 위치, 속도와 회전을 부여하고, 시선과 각 꽃잎 평면의 교차점을 계산합니다. 머리카락 UV와 표면 법선은 꽃잎 위치·모양에 관여하지 않습니다. 꽃잎에는 별도 음영을 사용하므로 머릿결의 골을 따라 휘거나 어두워지지 않습니다.

가까운 꽃잎과 먼 꽃잎의 크기·밝기·시차가 다르게 보입니다. 이는 머리카락 영역 안에 표시하는 가상 내부 표현입니다. 실제 ParticleSystem이나 외부로 방출되는 오브젝트는 생성하지 않습니다. 가려진 얼굴 안쪽까지 물리적인 투명 렌더링을 하는 방식은 아니며, 꽃잎끼리의 반투명 겹침은 색상 가중 혼합으로 근사합니다.

기존 갈색 머릿결은 UV0와 원본 텍스처를 유지합니다. 원본은 Unlit/Texture였고 데모는 URP Lit이므로 조명 반응은 다릅니다. 효과는 새 씬의 `Hair1`에만 적용합니다.

## 조절

[SakuraHair.mat](Materials/SakuraHair.mat)을 선택합니다.

| 항목 | 기본값 | 동작 |
|---|---:|---|
| Effect Strength | 0.78 | 꽃잎 표시 강도. 0이면 기존 머릿결만 표시합니다. |
| Petal Tint | 옅은 분홍 | 꽃잎 색상입니다. |
| Petal Size | 0.64 | 꽃잎 크기입니다. |
| Petal Density | 0.7 | 96개 후보 중 표시할 비율입니다. 0이면 꽃잎을 모두 숨깁니다. |
| Flow Speed (meters per second) | 0.065 | 기준 속도. 꽃잎마다 0.7~1.4배로 다르게 떨어집니다. |
| Flow Direction (World XYZ) | (0, -1, 0) | 월드 기준 이동 방향입니다. |
| Flutter Amount | 0.75 | 좌우 흔들림, 회전, 납작해지는 움직임입니다. |
| Petal Glow | 0.08 | 꽃잎의 추가 밝기입니다. |
| Petal Scale Divisor | 26 | 높이면 꽃잎이 작아집니다. |
| Interior Depth | 1 | 내부 꽃잎의 앞뒤 간격. 0이면 깊이를 한 평면으로 모읍니다. |
| Distant Petal Shading | 0.65 | 먼 꽃잎이 어두워지는 정도입니다. |

`Volume Center / Half Size`는 `SakuraHairVolumeBounds`가 실제 머리카락 렌더러의 월드 경계로 매 프레임 공급합니다. 스킨드 메시의 본 좌표와 오브젝트 좌표 차이를 피하고, 캐릭터를 기울여도 월드 아래로 떨어지도록 합니다. 이 컴포넌트는 전용 데모의 Hair1에만 추가되며 공유 머티리얼을 수정하지 않습니다.

`Shaders/SakuraHair.shadergraph`에서 URP Lit과 속성 연결을 편집하고, `Shaders/SakuraSurface.hlsl`에서 내부 꽃잎 계산을 편집할 수 있습니다. 원래 머리카락 텍스처는 읽기 전용 참조입니다.

## 주요 파일

| 파일 | 역할 |
|---|---|
| [SakuraHair.shadergraph](Shaders/SakuraHair.shadergraph) | URP Lit 출력과 머티리얼 속성을 Custom Function에 연결합니다. |
| [SakuraSurface.hlsl](Shaders/SakuraSurface.hlsl) | 가상 꽃잎 위치, 낙하·흔들림·회전, 시선 교차와 색상 합성을 계산합니다. |
| [SakuraHairVolumeBounds.cs](SakuraHairVolumeBounds.cs) | Hair1의 월드 경계를 MaterialPropertyBlock으로 전달합니다. |
| [SakuraHairDemoBuilder.cs](Editor/SakuraHairDemoBuilder.cs) | 전용 씬·머티리얼 생성과 꽃잎 마스크 생성을 담당합니다. |
| [SakuraHairCaptureTools.cs](Editor/SakuraHairCaptureTools.cs) · [SakuraHairRuntimeCapture.cs](SakuraHairRuntimeCapture.cs) | 배치 실행, 저장 상태 확인, 실제 Play Mode 캡처를 담당합니다. |
| [색상 텍스처](Textures/SakuraPetal_BaseColor.png) · [마스크](Textures/SakuraPetal_Mask.png) · [정적 모델](Editor/Source/SakuraPetal_Static.fbx) | 제공 리소스의 복사본과 모델 윤곽에서 생성한 마스크입니다. |

## 리소스와 원본 보존

제공된 `E:/999.temp/SakuraPetal`의 정적 FBX와 색상 PNG를 복사해 사용합니다. 실행에 필요한 복사본은 이 폴더에 포함되어 있습니다. 마스크는 FBX의 실제 UV 삼각형 영역에서 생성했습니다. 원본 Henshin 씬, 캐릭터 프리팹, 모델, 공유 머티리얼과 제공 리소스는 수정하지 않습니다. 데모 캐릭터의 변신 컴포넌트는 제거하고 Animator 실행을 껐습니다.

씬이 없을 때 `Tools > Sakura Hair > Create Demo Scene`으로 생성할 수 있습니다. 기존 데모 씬을 덮어쓰지 않습니다.

## 실행 근거

구현 시 실행한 Unity 6000.3.23f1 / RTX 3070 Ti 검증 기록은 다음과 같습니다.

- 셰이더 컴파일·Play Mode 실행 오류가 없고, 별도 실행에서 씬을 다시 열어 설정 유지를 확인했습니다.
- 일반 자세와 25도 기울인 자세에서 월드 아래로 이동하는 것을 렌더 프레임으로 확인했습니다.
- 표면 깊이만 바꾼 평면 두 개에서도 같은 꽃잎이 보이는지 비교해 표면 위치와의 독립성을 확인했습니다.
- 효과 강도 0·발광 1 또는 밀도 0에서 효과 끄기 이미지와 픽셀이 일치했습니다. 얼굴·의상 표본 영역도 전후 변화가 없었습니다.
- 보호 대상 원본 178개 파일의 SHA-256이 일치했습니다. 실제 180프레임을 6초·30fps H.264 영상으로 남겼습니다.

[최신 영상](../../Captures/SakuraHairDemo/InteriorFall/SakuraHairInteriorFall.mp4), [효과 끄기](../../Captures/SakuraHairDemo/InteriorFall/front-off.png) / [켜기](../../Captures/SakuraHairDemo/InteriorFall/front-on.png), [상세 검증과 근거 파일](../../Captures/SakuraHairDemo/InteriorFall/README.md)을 참조합니다.

플레이어 빌드, 모바일·여러 캐릭터 환경, 장시간 성능 측정은 검증하지 않았습니다.

`MediaPipeTest.SakuraHair.Editor.SakuraHairCaptureTools.CaptureBatch`는 별도 Unity 배치 실행용이며, 씬 재열기 → Play Mode → 비교 이미지·180프레임 캡처 → 종료 순서로 동작합니다. 영상 앞 3초는 카메라를 고정하고 뒤 3초는 28도 회전합니다. 캡처용 컴포넌트와 테스트용 기울임·평면은 저장되지 않습니다. 일반 작업 중인 에디터에서 이 종료용 진입점을 실행하지 않습니다.

96개 후보의 시선 교차를 픽셀마다 검사하는 데모 셰이더입니다. 여러 캐릭터 동시 사용이나 모바일 성능은 별도 검증이 필요합니다.
