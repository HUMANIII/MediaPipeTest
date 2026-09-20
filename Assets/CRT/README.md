# CRT Shader Graph

Unity 6000.3.23f1 · URP / Shader Graph 17.3.0용 CRT 효과.
RGB 발광 패턴, 주사선, 픽셀화, 곡면 왜곡, 비네트와 흑백·단색 모노를 제공한다.

## 제작법

1. `Tools > CRT > Create or Rebuild Playground`로 그래프·머티리얼·테스트 씬을 생성한다.
2. `Assets/CRT/Demo/CRTPlayground.unity`를 열고 Play로 실행한다.
3. 그래프만 다시 만들 때는 `Tools > CRT > Rebuild Shader Graphs`를 사용한다.

두 생성 메뉴는 해당 그래프를 덮어쓴다. Playground 생성은 머티리얼·씬도 덮어쓰므로 직접 수정했다면 먼저 보존한다.
기본 프로필은 없는 에셋만 생성하며 기존 값은 보존한다.

그래프는 `CRTCoordinates`에서 영역·곡면·픽셀 좌표를 만들고, 입력을 샘플링한 뒤
`CRTPhosphor`에서 RGB·주사선·비네트·밝기를 계산한다. 네 출력 그래프가 색상·알파와 모노를 연결한다.
직접 그래프를 편집할 때는 `Shaders`의 Sub Graph부터 수정한다.
재생성 후에도 유지할 변경은 `Editor/CrtGraphBuilder.cs`에도 반영한다.

## 사용법

### 프로필 연결

1. Project 창의 `Create > CRT > Profile`로 설정 에셋을 만들거나 `Profiles`의 기본 에셋을 복제한다.
2. Profile Inspector에서 값을 조절한다. 각 설정 이름에 마우스를 올리면 한국어 설명이 표시된다.
3. 대상의 `CrtSurface` 또는 `CrtFullscreen` 컴포넌트에 **Profile**을 연결한다.
4. Profile이 있으면 에셋 값을 사용한다. 비우면 컴포넌트의 **Settings** 값을 사용한다.

같은 프로필을 연결한 화면은 설정을 공유한다. 개별 조절은 프로필을 복제해서 사용한다.
이미지·영상 입력과 대상 연결은 컴포넌트에서 지정한다. 효과 켜기/끄기는 **Effect Enabled**로 조절한다.

### 네 출력 대상

| 대상 | 적용 절차 |
|---|---|
| 3D 표면 | 화면 MeshRenderer에 `CrtSurface` 추가 → Template에 `CRTSurface.mat` → Source에 이미지/RenderTexture 지정. |
| 2D Sprite | SpriteRenderer에 `CrtSurface` 추가 → `CRTSprite.mat` 지정. Source를 비우면 원래 스프라이트, 지정하면 스프라이트 알파 안에 입력 영상을 표시한다. |
| Canvas UI | RawImage에 `CrtSurface` 추가 → `CRTCanvas.mat` → Source 지정. Canvas 색상·알파와 uGUI 마스크를 사용할 수 있다. |
| 전체 화면 | 카메라의 Renderer를 `CRT_Renderer`로 선택 → `CrtFullscreen` 추가 → Feature에 해당 Renderer의 `CRT Fullscreen`, Template에 `CRTFullscreen.mat` 연결. |

- 일부 영역에 적용하려면 Profile 또는 로컬 Settings의 **Screen Rect / Effect Mask**를 사용한다.
- 3D의 Display Aspect는 화면 가로/세로 비율이다. 0은 Quad의 X/Y 스케일로 추정한다.
- Sprite는 Simple 모드의 비회전 사각 UV를 사용한다. 여러 머티리얼 슬롯의 3D 화면은 별도 Renderer로 분리한다.
- 전체 화면의 Display Canvases에는 표시용 루트 Canvas를 등록한다. Include UI로 화면 효과 포함 여부를 바꾼다.
- 조작 UI는 별도 Overlay Canvas에 둔다. 하나의 전체 화면 Feature는 하나의 `CrtFullscreen`이 관리한다.
- 영상은 Unity VideoPlayer 기반 `CrtVideoSource`가 준비한 `Texture`를 Source로 전달한다. 별도 플러그인은 필요 없다.

### 미리보기

1. Profile Inspector의 **CRT 미리보기 열기** 또는 `Tools > CRT > Profile Preview`를 선택한다.
2. 프로필과 이미지(Texture2D)를 선택한다. 이미지를 비우면 기본 테스트 패턴을 표시한다.
3. 왼쪽에서 프로필을 직접 편집한다. 설정 호버 설명과 Undo/Redo를 사용할 수 있다.
4. **원본 보기**로 비교하고 **실제 픽셀 크기**로 발광 패턴을 확인한다.

미리보기는 정지 이미지용이다. 동영상과 각 출력의 알파·마스크·UI 포함 동작은 Play 데모에서 확인한다.

### 데모

1. `CRTPlayground`에서 Play를 누르고 전체 비교 / 전체 화면 / 3D / 2D / Canvas 모드를 선택한다.
2. 원본 비교, 정지 이미지·동영상, UI 포함, 부분 UV, 영상 일시정지를 버튼으로 전환한다.
3. COLOR / MONO / GREEN / AMBER로 프리셋을 선택하고 왼쪽 슬라이더를 조절한다. 아래 설정은 스크롤해서 확인한다.
4. **REAPPLY**로 선택한 프리셋 값을 다시 불러온다. 데모 조절은 임시 복사본에만 적용되며 에셋에 저장되지 않는다.

## 도구 설명

메뉴 위치: `Tools > CRT`.

| 도구 | 용도 |
|---|---|
| Create or Rebuild Playground | 그래프·머티리얼·데모 씬 생성. |
| Rebuild Shader Graphs | 네 출력 그래프와 공통 Sub Graph 재생성. |
| Create Missing Profiles | 없는 Classic·Monochrome·Green·Amber 기본 프로필 생성. |
| Profile Preview | Play 없이 프로필 편집과 이미지 미리보기. |
| Validate Assets | 현재 CRT 에셋 구성 검사. |
| Build Windows Preview | 데모를 `Build/CRT/CRTPreview.exe`로 빌드. |

알려진 오류: Unity 편집기를 닫을 때 VFX Graph의 `VFXViewWindow.CloseIfNotLast` 예외가 발생할 수 있다.
CRT Player 동작에는 관찰된 영향이 없다. [상세 기록](RawData/VALIDATION.md#known-errors).

상세 제작·적용·녹화 절차는 [RawData/REFERENCE.md](RawData/REFERENCE.md)를 참고한다.
