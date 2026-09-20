# CRT 검증 결과

## 매개변수 정리·툴팁·문서 재편 — 2026-09-20

Unity 6000.3.23f1, URP/Shader Graph 17.3.0, Windows x64 Development Build,
NVIDIA GeForce RTX 3070 Ti, 1920×1080에서 검증했다. 열린 Unity 편집기는 DX11이었다.

| 검증 | 결과 |
|---|---|
| 그래프 재생성·C# 컴파일·Windows 빌드 | 통과, CRT 셰이더 4개·설정 검사 7개 |
| DX12 / DX11 Player 실행 | 각각 PASS, 수집한 런타임 오류 0 |
| 기본 캡처 비교 | API별 56/56 |
| 프로필·우회·마스크·알파·미리보기 비교 | API별 82/82; 공통 편집기 검사 5개 포함 |
| 기존 Strength 1과 출력 영역 비교 | API별 102/102, 모든 비교 영역의 RGB 차이 0 |
| 정적·문서·기존 저장값 검사 | 75/75; 기존 에셋 GUID 44개 보존 포함 |
| 편집기 데이터 검사 | 설정 16개와 Tooltip, 복사·경계값·Undo/Redo·저장·기존 프로필 보존 통과 |
| 편집기 미리보기 | 생성·렌더·해제 9회, 장면 변경과 임시 리소스 누수 없음 |
| 실제 GUI | 프로필 Inspector·로컬 Settings·미리보기의 한국어 호버, 값 조절, Undo 확인 |
| README | 공백 포함 80줄; 제작·프로필 연결·네 출력·미리보기·데모·도구 절차와 문서 링크 확인 |

전체 Strength 필드·속성·UI·저장값을 제거하고 그래프 상수 1로 대체했다.
개별 RGB/주사선 강도와 나머지 설정은 유지했다. 모든 그래프 속성이 출력까지 연결되는 것을 검사했다.
영상 입력·비율·Sprite UV 속성과 Unity 키워드 속성을 유지하고 VALIDATION의 기존 `.meta` GUID도 보존했다.
기존 프로필·머티리얼·씬은 삭제한 저장값 이외의 내용이 변경 전과 일치한다.

00–33 캡처에서 3D·Sprite·Canvas 표시 영역을 동일 API의 `Logs/CRTProfiles/DX12`, `DX11`과 비교했다.
전체 화면 캡처도 포함되며, 배치가 달라진 조작 UI는 비교 영역에서 제외했다.
부분 영역과 마스크 밖 원본, Sprite 모서리 알파, Canvas 스텐실/CanvasGroup 알파,
프로필 교체·공유·해제·재적용, 원본 에셋 보존을 함께 확인했다.
새 34·35 캡처와 상태 검사는 전체 화면 끄기/다시 켜기, Feature 활성 상태와 우회 키워드를 확인한다.

GUI에서는 RGB Density·Virtual Resolution의 한국어 설명을 실제로 표시했다.
미리보기 Monochrome 1→0.01→Undo 1, 프로필 Monochrome 1→0.393→Undo 1,
로컬 RGB Density 240→595→Undo 240을 확인했다. GUI 조절에 사용한 에셋 값은 모두 복원했다.
로컬 설정 검사용 오브젝트는 임시 Preview Scene에 생성하고 검사 후 정리했다.

### 비교 중 발견한 검증 조건

첫 실행은 사용자가 조절한 Classic 프로필(녹색 모노, 가상 세로 해상도 480)을 사용해 과거 기본 컬러 캡처와 달랐다.
프로필 튜닝은 그대로 보존하고, 자동 검증 시작 시 런타임 복사본만 고정 기본값으로 초기화하도록 수정했다.
수정 후 빌드와 두 API 검사를 다시 실행한 결과가 위 표다. 최초 실행 자료는 `DX12-initial-custom-preset`, `DX11-initial-custom-preset`에 남겼다.

동일 프로젝트의 열린 편집기로 인해 배치 빌드 시작이 거절되어 기존 편집기에서 빌드했다.
임시 검증 드라이버에서 컴파일 전 명령 호출 및 명령 파일 읽기 경합 오류가 각각 있었으나,
드라이버를 다시 컴파일하고 파일을 완성한 후 이름을 바꾸어 전달해 검사를 완료했다. 이는 Player 오류 집계에 포함되지 않는다.
검증 드라이버는 작업 후 제거했다. 빌드로 자동 직렬화된 ProjectSettings와 URP 설정은 작업 전 값으로 복원했다.

### 이번 성능 기록

각 모드 1초 준비·3초 측정, 영상 재생, VSync/프레임 제한 해제 조건이다.
Unity GUI 편집기가 함께 열려 있었으므로 이전 측정 대비 성능 향상·회귀 판단에 사용하지 않는다.

| API | 모드 | 평균 ms | 95백분위 ms | 평균 GPU ms |
|---|---|---:|---:|---:|
| DX12 | 원본 + 영상 | 0.734 | 2.964 | 0.100 |
| DX12 | 세 표면 CRT | 0.731 | 2.835 | 무효 |
| DX12 | 전체 화면, UI 제외 | 0.731 | 2.475 | 0.151 |
| DX12 | 전체 화면, UI 포함 | 0.727 | 2.477 | 0.152 |
| DX11 | 원본 + 영상 | 0.715 | 2.871 | 0.337 |
| DX11 | 세 표면 CRT | 0.764 | 3.407 | 무효 |
| DX11 | 전체 화면, UI 제외 | 0.708 | 2.364 | 무효 |
| DX11 | 전체 화면, UI 포함 | 0.704 | 2.425 | 0.328 |

무효로 표시한 세 측정은 GPU 타임스탬프 무효 샘플이 각각 1개이며 `report.json`의 GPU 값은 -1이다.

### 이번 근거 위치

저장소 루트의 `Logs/CRTRefinement` 기준:

- `editor-build.log`: 그래프 재생성·최종 빌드·에셋/프로필 데이터·미리보기 자동 검사.
- `DX12`, `DX11`: 00–35 PNG, `report.json`, `capture-checks.json`, `profile-checks.json`, `refinement-checks.json`.
- `Editor`: 00–04 미리보기 PNG와 `result.json`.
- `audit.py`, `static-checks.json`: 연결·잔여 참조·저장값·GUID·README 줄 수·링크·과거 기록 보존 검사.
- `baseline/CRT`: 작업 전 코드·그래프·에셋·문서. 이전 로그와 성능 기록은 아래 이력의 경로에 그대로 보존했다.
- `unsaved-CRTCanvas.shadergraph`: 열려 있던 CRTCanvas의 미저장 편집본을 별도 보존한 뒤 해당 창을 재생성된 그래프로 갱신했다.

## 프로필·모노·비네트 확장 — 2026-09-20

Unity 6000.3.23f1, URP/Shader Graph 17.3.0, Windows x64 Development Build,
NVIDIA GeForce RTX 3070 Ti, 1920×1080에서 검증했다.

| 검증 | 결과 |
|---|---|
| Windows 빌드·Shader Graph 컴파일 | 통과 |
| DX12 / DX11 자동 실행 | 각각 PASS, 런타임 오류 0 |
| 기본 캡처 비교 | 각각 54/54 |
| 프로필 캡처·상태 검사 | 각각 71/71 |
| 공통 편집기 미리보기 픽셀·정리 검사 | 5/5, 위 검사와 합쳐 76/76 |
| 에셋 복사·경계값·Undo/Redo·저장·기존 프리셋 보존 | 통과 |
| 미리보기 생성·렌더링·해제 | 9회, 장면/메시/머티리얼 누수 및 활성 씬 변경 없음 |

실제 렌더링한 00–33 PNG를 비교했다. 3D·Sprite·Canvas와 전체 화면의 완전 모노·중간 모노·녹색·호박색,
비네트 강도·반경·부드러움 변화, 부분 영역과 마스크 밖 원본 보존, Canvas 스텐실·알파 단일 적용을 확인했다.
기본 컬러 설정은 확장 전 캡처와 비교해 기존 외형을 유지했다.
프로필 공유·실시간 변경·교체·해제, 데모 재적용, 저장된 영역 복원, 런타임 조작 후 원본 에셋 보존도 통과했다.

실제 Unity 편집기 창(DX11, Play 정지 상태)에서 모노 슬라이더의 즉시 반영과 Undo 복원,
원본 비교 버튼, 실제 픽셀 크기 및 스크롤 영역을 직접 확인했다.
편집기 미리보기는 정지 이미지용이며 동영상·출력별 알파/마스크 검증은 Player에서 수행했다.
GUI 편집기 종료 중에는 VFX Graph 패키지의 `VFXViewWindow.CloseIfNotLast`에서 별도 예외가 기록됐다.
CRT 호출 스택은 없었으며, 위 런타임 오류 0개는 Player 검사의 수집 결과를 뜻한다.

원본 증거는 저장소 루트 기준 다음 경로에 있다.

- 빌드·편집기 자동 검사: `Logs/CRTProfiles/final-build.log`
- DX12 / DX11: `Logs/CRTProfiles/DX12`, `Logs/CRTProfiles/DX11`의 `report.json`, `capture-checks.json`, `profile-checks.json`, PNG
- 편집기 렌더링: `Logs/CRTProfiles/Editor`의 00–04 PNG와 `result.json`
- 실제 편집기 세션: `Logs/CRTProfiles/editor-ui.log`

## 최초 구현 — 2026-09-19

환경: Unity 6000.3.23f1, URP/Shader Graph 17.3.0, Windows x64 Development Build,
NVIDIA GeForce RTX 3070 Ti, 1920×1080. 테스트 씬은 CRTPlayground만 사용했다.

### 결과

- 네 Shader Graph / 두 Sub Graph 임포트 및 Windows 셰이더 컴파일: 통과, 셰이더 오류 없음.
- 에셋·설정 경계값 및 실제 CRT 우회 변형 검사: 셰이더 4개, 설정 검사 8개 통과.
- DX11 / DX12 실행 검사: 각각 PASS, 수집한 런타임 오류 0개.
- 영상 준비·프레임 진행·반복·일시정지/재개·재준비·씬 재진입: 모두 통과.
- 영상용 RenderTexture: 씬 재진입 전 1개 / 후 1개.
- 캡처 직접 비교: DX11 30/30, DX12 30/30 통과.

캡처 검사는 검은 화면 여부, 세 표면의 효과와 픽셀화, 부분 UV·마스크 밖 원본 보존,
전체 화면의 UI 포함·제외, 조작 UI 보존, 스프라이트 투명 모서리,
1024×768 출력, UI 스텐실 클리핑, CanvasGroup 알파의 단일 적용을 확인한다.
거리·각도 변경 화면도 캡처해서 확인했다. 시간에 따른 모든 카메라 움직임의 무늬 간섭을 보증하는 검사는 아니다.

### 성능

각 모드에서 1초 준비 후 3초간 측정했다. 동영상은 재생 중이고 VSync 및 프레임 제한은 해제했다.
원본 모드에서는 로컬 키워드로 CRT 계산을 생략한다. 전체 화면 모드에서는 표면별 CRT를 끈다.

DX12 결과:

| 모드 | 평균 프레임 ms | 95백분위 프레임 ms | 평균 GPU ms |
|---|---:|---:|---:|
| 원본 + 영상 | 0.497 | 0.724 | 0.118 |
| 3D + Sprite + Canvas CRT | 0.499 | 0.724 | 0.126 |
| 전체 화면 CRT, UI 제외 | 0.533 | 0.731 | 0.145 |
| 전체 화면 CRT, UI 포함 | 0.535 | 0.714 | 0.146 |

DX12의 GPU 타임스탬프 무효 샘플은 0개다. 전체 화면+UI 모드의 GPU 시간 증가는 원본 대비 약 0.028ms였다.
DX11 평균 프레임은 모드별 0.504–0.541ms, 95백분위는 0.598–0.660ms였다.
DX11의 세 표면 모드에서는 GPU 타임스탬프 이상 1개가 검출되어 해당 GPU 평균을 무효 처리했다.

두 API 모두 이 테스트 씬에서 60fps 목표의 프레임 예산 16.67ms 이내였다.
측정값은 이 장비와 단순 테스트 씬의 결과이며 기존 MediaPipe 씬 전체의 성능을 의미하지 않는다.

### 재현과 원본 증거

저장소 루트 기준 경로:

- Windows 실행 파일: `Build/CRT/CRTPreview.exe`
- 빌드 로그: `Logs/CRT-build.log`
- DX12: `Logs/CRTValidation/report.json`, `capture-checks.json`, 00–09 PNG
- DX11: `Logs/CRTValidationD3D11/report.json`, `capture-checks.json`, 00–09 PNG
- 실행 로그: `Logs/CRT-player.log`, `Logs/CRT-player-d3d11.log`

자동 실행 인수:

```text
-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d12 -crtValidate -crtOutput <절대 결과 경로>
```

DX11은 `-force-d3d12`를 `-force-d3d11`로 바꾼다.
캡처 검사는 `python Tools/CRT/check_captures.py <결과 폴더>`로 재실행한다.
Build와 Logs는 기존 .gitignore 대상이며 이 문서에는 검증 시점의 결과를 기록했다.

<a id="known-errors"></a>
## 알려진 오류

- Unity GUI 편집기 종료 시 VFX Graph 패키지의 `VFXViewWindow.CloseIfNotLast` 예외가 기록된 이력이 있다.
  CRT 호출 스택은 없었고 CRT Player 동작에 관찰된 영향은 없었다. 근거: `Logs/CRTProfiles/editor-ui.log`.

<a id="recording-conditions"></a>
## 녹화 관련 관찰과 측정 조건

기존 녹화는 `Logs/CRTRecording/CRT-demo.mp4`에 보관한다.
VideoPlayer는 Unscaled Game Time으로 재생한다. 이 환경에서는 동기식 캡처 프레임레이트를 사용하면 반복 재생 경계에서 지연이 발생했다.
JPEG 프레임 저장 부하가 추가되므로 녹화 결과는 실시간 성능 측정 자료가 아니다.
녹화 및 FFmpeg 변환 명령은 [REFERENCE](REFERENCE.md#예시-영상-녹화와-변환)에 있다.

## 재현 도구와 결과 해석

- 에셋: `Tools > CRT > Validate Assets`
- 빌드: `Tools > CRT > Build Windows Preview`
- 그래프 재생성 후 빌드: Unity `-batchmode -executeMethod MediaPipeTest.CRT.Editor.CrtProjectSetup.RebuildAndBuild -quit`
- 편집기 데이터·미리보기: Unity `-batchmode -executeMethod MediaPipeTest.CRT.Editor.CrtProfileValidation.ValidatePreview -crtOutput <절대 결과 경로>`

Unity 명령에는 `-projectPath <프로젝트 경로>`와 `-logFile <로그 경로>`를 함께 지정한다.
미리보기 검사는 그래픽 장치가 필요하며 프레임을 나눠 렌더링한 뒤 자체 종료하므로 `-quit`, `-nographics`를 사용하지 않는다.
열린 편집기가 같은 프로젝트를 점유하고 있으면 배치 모드를 중복 실행하지 않는다.

```text
CRTPreview.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d12 -crtValidate -crtOutput <절대 결과 경로>
python Tools/CRT/check_captures.py <결과 폴더>
python Tools/CRT/check_profiles.py <결과 폴더> --baseline <기존 CRTValidation 폴더> --editor <편집기 결과 폴더>
python Tools/CRT/check_refinement.py <결과 폴더> --baseline <동일 API의 기존 CRTProfiles 폴더>
```

DX11은 `-force-d3d11`로 실행한다. 각 실행은 서로 다른 결과 폴더를 사용한다.
자동 실행은 원본·네 출력·부분 적용·비율·거리·각도·UI 마스크/투명도 캡처,
영상 준비·진행·반복·일시정지·재준비·씬 재진입 검사와 성능 측정을 수행한다.
프로필의 컬러·중간/완전 모노·녹색·호박색, 비네트 변화, 프로필 교체·해제·공유·원본 값 보존도 포함한다.
이번 버전부터 전체 화면 끄기/다시 켜기 캡처 34·35도 포함한다.

`report.json`에서 영상 상태·UI 모드·오류·프레임 시간·GPU 시간을 확인한다.
GPU 시간이 -1이면 측정 불가 또는 타임스탬프 이상으로 무효 처리한 값이다. 유효·무효 샘플 수도 기록한다.
성능 측정은 VSync/프레임 제한을 해제한 개발 빌드 기준이며 GPU와 출력 해상도를 함께 기록한다.
정적인 검사와 실제 픽셀 비교의 결과를 구분하며, 편집기 호버의 실제 표시는 GUI에서 별도로 확인한다.
