# CRT 상세 참조

간단한 시작 절차는 [README](../README.md), 검증 결과·측정 조건·재현 명령은 [VALIDATION](VALIDATION.md)에서 관리한다.

## 구성과 제작

- `CRTCoordinates.shadersubgraph`: 영역 UV, 곡면 좌표, 가상 픽셀 샘플 좌표, 화면 경계.
- `CRTPhosphor.shadersubgraph`: RGB 패턴, 주사선, 거리별 패턴 완화, 비네트, 밝기.
- 네 `.shadergraph`: 각 출력 타깃의 입력 샘플링·색상/알파·모노와 공통 Sub Graph 연결.
- `CrtSettings`: 16개 공통 설정, 한국어 Tooltip, 머티리얼 반영과 `Clone`/`CopyTo`.
- `CrtProfile`: 공통 설정을 저장하는 ScriptableObject.
- `CrtSurface`: Renderer/RawImage별 머티리얼 인스턴스와 입력 텍스처 연결.
- `CrtVideoSource`: 비동기 준비, 반복·일시정지, 오류 상태, RenderTexture 수명 관리.
- `CrtFullscreen`: 전용 Full Screen Pass와 표시용 Canvas 전환.

설정별 설명은 `Runtime/CrtSettings.cs`의 Tooltip에 한 번만 정의한다.
프로필 Inspector·컴포넌트의 로컬 Settings·미리보기 창이 같은 설명을 사용한다.
전체 적용 강도는 그래프의 상수 1이다. 별도의 숨겨진 조절값은 없다.
`effectEnabled`와 `_CRT_EFFECT_ON` 로컬 키워드가 CRT 우회 변형을 선택한다.
머티리얼을 직접 편집할 때는 CRT Enabled 토글을 사용한다.
Screen Rect 밖과 검정 마스크는 원본을 유지하고, 회색 마스크는 기존 방식대로 원본과 결과를 혼합한다.

생성기는 Shader Graph 17.3 내부 편집 API를 리플렉션으로 사용한다. Player에는 편집 코드가 포함되지 않는다.
그래프를 직접 수정했다면 재생성 전에 보존한다. 생성 후에도 유지할 변경은 생성기에도 반영한다.
`Rebuild Shader Graphs`는 그래프를, `Create or Rebuild Playground`는 그래프·머티리얼·테스트 씬을 덮어쓴다.
기존 기본 프로필은 생성 시 보존하며, `Create Missing Profiles`로 누락 에셋만 추가할 수 있다.

## 적용 상세

### 프로필과 미리보기

Profile이 연결되면 로컬 Settings보다 우선하며, 해제하면 저장된 로컬 값으로 돌아온다.
컴포넌트는 에셋을 읽기만 하고 수정 내용을 다음 갱신에 반영한다.
영역과 마스크도 프로필에 포함되므로 외형은 같고 영역은 다른 화면에는 프로필을 복제한다.
런타임 임시 조절은 `Clone` 또는 `CopyTo`로 분리한다. 데모 슬라이더도 이 방식을 사용한다.

미리보기는 선택한 에셋을 직접 편집하며 Undo/Redo와 에셋 저장을 따른다.
Texture2D를 16:9 화면 안에 비율을 유지해 표시하고 비어 있으면 기본 테스트 패턴을 사용한다.
실제 CRT 표면 셰이더로 공통 효과를 렌더링한다. 실제 픽셀 크기에서는 넘치는 영역을 스크롤한다.
미리보기 장면·메시·머티리얼은 창 종료와 스크립트 재로드 때 정리된다.

### 3D·Sprite·Canvas

3D 화면은 MeshRenderer에 `CrtSurface`와 `CRTSurface.mat`를 연결한다.
여러 머티리얼 슬롯 중 특정 슬롯만 변경하는 기능은 없으므로 화면을 별도 Renderer로 분리한다.
임의 메시의 비율은 Display Aspect로 지정하고, 0일 때만 Quad의 X/Y 스케일에서 추정한다.

Sprite는 `CRTSprite.mat`를 사용하며 Simple 모드의 비회전 사각 UV가 대상이다.
타일·슬라이스·회전 패킹은 지원 범위에 포함하지 않는다.
Source가 없으면 Sprite 자체를 표시하고, 있으면 Sprite 알파 안에 해당 소스를 표시한다.
SpriteRenderer가 `_MainTex`를 관리하므로 별도 영상은 `_SourceTex`로 전달한다.

Canvas에서는 RawImage에 `CRTCanvas.mat`를 연결한다.
RawImage 비율을 기준으로 소스 종횡비를 유지하고 남는 부분은 검게 채운다.
색상·투명도·uGUI 마스킹을 유지한다. 개별 Text/TMP 글자에 직접 적용하는 기능은 아니다.

RGB 패턴과 주사선은 화면 미분값으로 표현 가능한 밀도를 판단해 평균값으로 전환한다.
멀리 있는 표면에서 패턴이 약해지는 것은 무늬 간섭을 줄이기 위한 처리다.
화면 휘어짐은 메시를 변형하지 않는다. 프레임 잔상·노이즈·깜빡임·흐르는 띠는 현재 범위에 포함하지 않는다.

### 전체 화면

카메라의 Universal Additional Camera Data에서 `CRT_Renderer`를 선택하고 `CrtFullscreen`을 추가한다.
해당 Renderer의 `CRT Fullscreen` Feature와 `CRTFullscreen.mat`를 연결한다.
Display Canvases에는 표시용 루트 Canvas만 등록한다.
Include UI가 켜지면 등록 Canvas를 해당 카메라의 Screen Space–Camera로, 꺼지면 Screen Space–Overlay로 전환한다.
클릭해야 하는 조작 UI는 별도 Overlay Canvas에 둔다. 곡면 왜곡의 입력 좌표 역변환은 구현하지 않는다.
컴포넌트를 비활성화하면 등록한 Canvas와 Renderer Feature 설정을 복원한다.
하나의 CRT Renderer/Feature는 하나의 전체 화면 컨트롤러가 소유해야 한다.

기본 PC Renderer는 유지하고 PC RP Asset의 Renderer 목록 끝에 CRT 전용 Renderer를 등록한다.
데모는 실행 중 PC 파이프라인을 사용하고 종료 시 이전 Quality 파이프라인 선택을 복원한다.

### 영상 입력

Source에는 Texture 또는 RenderTexture를 전달한다.
`CrtVideoSource`의 `Texture`는 비동기 영상 준비 후 제공되므로 준비 전에는 정지 이미지를 사용한다.
별도 씬에서는 이 텍스처를 원하는 `CrtSurface.source`에 연결하는 코드를 작성한다. 데모의 갱신 코드가 연결 예시다.
기본 MP4는 1280×720, 30fps, 4초짜리 무음 H.264다. Unity VideoPlayer로 재생하며 외부 플러그인은 필요 없다.
원본 패턴 재생성은 `Tools/CRT/generate_pattern.py`를 사용한다. Python NumPy/Pillow와 FFmpeg는 재생성에만 필요하다.

## 예시 영상 녹화와 변환

```powershell
Build/CRT/CRTPreview.exe -force-d3d12 -crtRecord -crtOutput '<새 출력 폴더의 절대 경로>'
```

실제 Game 화면을 1920×1080, 목표 30fps로 36초 동안 최고 품질 JPEG 연속 프레임으로 저장한다.
원본, CRT 동시 적용, 픽셀화 조절, 부분 UV, 3D, 2D, 전체 화면(UI 제외/포함), Canvas 순서로 각 4초씩 재생한다.
설정 변경은 데모의 슬라이더 콜백을 사용하며 화면 상단에 현재 단계가 표시된다.
일반 실행에서는 녹화 컴포넌트를 추가하지 않는다. `frames` 하위 폴더가 이미 있는 출력 위치는 사용하지 않는다.

FFmpeg 변환 명령:

```powershell
ffmpeg -safe 0 -f concat -i '<출력 폴더>/frames.ffconcat' -c:v libx264 -preset slow -crf 15 -pix_fmt yuv420p -r 30 -fps_mode cfr -t 36 -movflags +faststart '<출력 폴더>/CRT-demo.mp4'
```

`recording.json`에는 프레임 수·해상도·영상 프레임 진행 횟수·실행 오류가 기록된다.
`frames.ffconcat`의 실제 시간 간격으로 저장 지연이 생겨도 출력 영상의 재생 속도를 유지한다.
VideoPlayer는 Unscaled Game Time으로 재생한다. 녹화 부하와 캡처 타이밍 관련 관찰은 [검증 기록](VALIDATION.md#recording-conditions)을 참고한다.
