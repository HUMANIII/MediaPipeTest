# CRT 감시실·Audio Lab 커밋 검토안

작성일: 2026-09-21  
상태: **사용자 승인 완료 — 아래 범위의 로컬 커밋 두 개를 승인받음**  
저장소: `E:\02.Unity\MediaPipeTest_0`  
현재 브랜치: `main`  
기준 커밋: `c946fd04da23a943d7a1fa5d8030319519cd1a7d` — `[기능] CRT Shader Graph 효과와 프로필 편집 도구 추가`

사운드 README 보강 요청을 반영했다. CRT README와 프로젝트 최상위 README에 사용 안내를 추가하고, Audio Lab·비교 음원 README의 연결 상태를 현재 구현에 맞춰 수정했다. 사용자는 사운드 README를 포함한 이 안에 대해 `커밋 올려줘`로 실행을 승인했다.

## 1. 승인받은 범위

이번 대화에서 만든 **Audio Lab, 비교 음원, 감시실 데모, 채널 전환 글리치, A 발소리 연결**을 두 개의 로컬 커밋으로 정리한다. 마지막 발소리 연결뿐 아니라 그 연결에 필요한 Audio Lab과 감시실도 현재 Git에는 추가되지 않은 상태이므로 함께 포함한다.

| 순서 | 커밋 제목 | 포함 규모 |
|---|---|---|
| 1 | `[기능] CRT Audio Lab 조절 도구와 설정 에셋 저장 추가` | 53개 파일, 약 4.13 MiB |
| 2 | `[기능] CRT 감시실과 채널 글리치 및 발소리 연동 추가` | 구현·검증·안내 126개 파일, 약 3.14 MiB + 이 검토 문서와 파일 목록 2개 |

총 181개 파일이 승인 범위다. 크기는 현재 파일 내용의 합계이며 Git 압축 후 저장 크기는 아니다. 2번은 1번의 공용 오디오 어셈블리를 사용하므로 순서대로 커밋한다. 원격 push는 포함하지 않는다.

전체 경로와 현재 SHA256은 [커밋 대상 파일 목록](2026-09-21-commit-files.json)에 기록했다. `commits[].files`가 구현 파일 목록이며, 검토 문서 2개는 2번의 `additionalReviewDocuments`에 따로 명시했다.

## 2. 커밋 1 — Audio Lab과 비교 음원

### 변경 내용

- Unity Editor에서 AudioClip을 넣고 원본/CRT를 비교 재생하는 Audio Lab을 추가한다.
- 마지막으로 실제 재생한 클립을 프로젝트별 GUID로 복원한다. 기록이 없으면 빈 상태로 시작하며 자동 재생하지 않는다.
- 간편 음색 강도 -100~+100, 상세 조절, 독립 출력 볼륨 -40~+12dB, 피크 보호를 제공한다. 간편 0은 기본 CRT 음색이다.
- 설정 에셋에 음색·볼륨·피크 보호·간편/상세 상태를 저장한다. 클립·재생 위치·원본 비교 상태는 저장하지 않는다. 사용자용 WAV 저장 UI는 제공하지 않는다.
- 설정·간편 조절 계산·DSP를 `MediaPipeTest.CRT.Audio` 공용 런타임 어셈블리에 두고 Editor 창에서 참조한다.
- A 실녹음/B 합성 비교 WAV 네 개, 제작·검사 스크립트, 기존 출처 기록에서 CC0로 확인한 A 원자료를 보존한다.

### 포함 파일

| 경로 | 포함 내용 |
|---|---|
| `Assets/CRT/AudioPreview/**`, 폴더 `.meta` | Editor 창·미리듣기·설정 저장·이력·검증 코드, 공용 런타임 코드, A 예시, DefaultCRT 프리셋, 안내 문서 |
| `AudioPreviews/CRT_Footsteps/` | 비교 WAV 4개, README, 제작·검사 Python, generation/validation JSON, Source 원본 아카이브와 FLAC 6개 |
| `Tools/CRT/AudioRefinementHarness.cs.txt` | 격리 Unity 프로젝트에서 재로드·재시작 검사 |
| `Tools/CRT/Test-AudioRefinement.ps1` | 검증 소스 복사·해시 확인·Unity 실행 |
| `Tools/CRT/check_audio_preview.py` | 독립 PCM 검사 |

`AudioPreviews` 안의 `.tools`, `__pycache__`, `Exports`는 제외한다. Python 제작·검사에는 별도로 NumPy/SoundFile이 필요하며 도구 설치본을 커밋에 넣지 않는다.

### 승인된 커밋 메시지

```text
[기능] CRT Audio Lab 조절 도구와 설정 에셋 저장 추가

AudioClip을 선택해 원본과 CRT 음색을 비교하는 Editor 도구를 추가한다.
간편 음색 강도와 상세 조절을 전환하고 출력 볼륨·피크 보호를 독립적으로 유지한다.
마지막 재생 클립을 GUID로 복원하며 현재 효과 설정은 재사용 가능한 .asset으로 저장한다.

- 설정·조절 계산·DSP를 MediaPipeTest.CRT.Audio 런타임 어셈블리로 공유
- A 실녹음/B 합성 비교 WAV와 CC0 원자료·출처·제작 도구 보존
- 기존 프리셋 호환, 재컴파일 상태 유지, 미리듣기 임시 클립 정리

사용: Tools > CRT > Audio Preview에서 클립과 설정 에셋을 선택한다.
조절 후 '설정 에셋에 저장' 또는 '새 설정 에셋 저장…'을 사용한다.

검증: DSP/Editor 재생 19개, 조절·저장·복원 31개, 독립 PCM 7개 통과.
실제 도메인 재로드와 새 Unity 프로세스의 마지막 클립 복원도 통과했다.
안내·검증: Assets/CRT/AudioPreview/README.md, VALIDATION.md
```

## 3. 커밋 2 — 감시실·전환 글리치·발소리

### 변경 내용

- 감시실 1개, CRT 1대, 고정 CCTV 4채널, 무작위로 배회하는 흰색 더미 1명을 구성한 씬과 관련 생성 도구를 추가한다.
- CharacterController 이동, 착석/복귀, 고정 시점 전환, 이전/다음 채널, 원본 영상 비교를 제공한다.
- 채널 전환 시 약 0.2초 화면 잡음과 치지직 소리를 재생하고 0.05초에 카메라를 즉시 전환한다. Shader Graph와 재생성 코드를 함께 반영한다.
- 승인한 A 음원의 여섯 단발음을 이동 거리 약 0.58m마다 번갈아 재생한다. 대기·정체 중에는 새 발소리를 만들지 않는다.
- `DefaultCRT.asset`을 읽어 CRT 전용 AudioSource의 발소리에 효과를 적용한다. 카메라와 더미 사이 거리, 플레이어와 CRT 사이 거리에 따라 음량을 조절한다.
- Play 중 저장된 오디오 설정 변경을 약 0.5초 간격으로 감지한다. 원본 클립은 유지하고 임시 처리 클립은 종료 시 정리한다.
- `C` 키는 영상 비교 기능을 유지한다. 발소리 효과는 AudioListener 전체나 채널 전환 치지직 소리에 적용하지 않는다.

### 포함 파일

| 경로 | 포함 내용 |
|---|---|
| `Assets/CRT/Surveillance/**`, 폴더 `.meta` | 런타임·Editor 도구, 씬, 프리팹, 프로필, 머티리얼, NavMesh, 애니메이터, 전용 렌더링 설정, 음원, 사용 안내·검증 기록 |
| `Assets/CRT/Runtime/CrtSurface.cs` | 화면별 임시 글리치 상태와 원본 모드 우회 |
| `Assets/CRT/Editor/CrtGraphBuilder.cs` | 전환 잡음·UV 어긋남·밝기 흔들림 노드 생성 |
| `Assets/CRT/Shaders/`의 변경된 그래프 6개 | 재생성된 Surface/Sprite/Canvas/Fullscreen 및 공통 Sub Graph |
| `Assets/CRT/README.md` | 감시실 안내, 사운드 조절·저장·적용 절차, 영상/오디오 비교 구분 |
| `README.md` | 프로젝트 첫 화면의 CRT 사운드 소개와 상세 안내 링크 |
| `ProjectSettings/TagManager.asset` | `HospitalWorld`(8), `SurveillanceRoom`(9) 두 레이어 추가만 포함 |
| `Tools/CRT/check_surveillance.py` | 기존 감시실 영상·전환 음원 검사 |
| `Tools/CRT/check_surveillance_footsteps.py` | 승인 음원 슬라이스와 실제 런타임 PCM 검사 |
| `Tools/CRT/extract_approved_footsteps.py` | A 비교 파일에서 단발음 여섯 개 추출 |
| `Docs/CRT/2026-09-21-commit-review.md`, `2026-09-21-commit-files.json` | 승인용 변경 설명 및 파일 목록 |

Shader Graph 재생성 결과는 직렬화된 노드 식별자 변경도 포함하므로 텍스트 차이가 크다. 그래프 생성 코드와 실행 검증 기록을 함께 검토한다.

### 승인된 커밋 메시지

```text
[기능] CRT 감시실과 채널 글리치 및 발소리 연동 추가

병원을 배회하는 더미를 고정 CCTV 4채널로 관찰하는 감시실 데모를 추가한다.
채널 전환 시 0.2초 잡음·치지직 소리를 재생하고 잡음 정점에서 시점을 전환한다.
승인한 A 발소리를 Audio Lab의 저장 프리셋으로 처리해 CRT 스피커에서 재생한다.

- CharacterController 이동, 착석·복귀, NavMesh 배회와 Humanoid 애니메이션
- 화면 글리치 Shader Graph와 재생성 코드, 원본 영상 우회
- 이동 거리 기반 발걸음, 선택한 CCTV 기준 거리 감쇠, 설정 변경 감지
- 임시 RenderTexture·머티리얼·오디오 클립 정리와 전용 레이어

사용: SurveillanceDemo 씬에서 Play. WASD/마우스 이동, E 착석·복귀,
좌우 키로 채널 변경, C로 영상 효과 비교. Audio Lab의 DefaultCRT 저장값이 발소리에 반영된다.
Hospital Horror Pack, HorrorPackFBX, CRT 모델과 Cinemachine Cameron 샘플이 필요하다.
외부 모델 팩은 이 구현 커밋에 포함하지 않는다.

검증: 발소리 적용 후 65초 실행·4채널·씬 재진입 등 70개, 독립 PCM 12개 통과.
기존 영상 데모와 CRTPlayground 회귀 결과는 해당 시점의 검증 문서에 별도 보존했다.
안내·검증: Assets/CRT/Surveillance/README.md, VALIDATION-AUDIO.md, VALIDATION.md
```

## 4. 제외할 변경과 외부 의존성

기존에 스테이징된 `Assets/03.Shader` 관련 13개 항목은 다른 작업이다. 해당 인덱스 상태와 작업 폴더의 삭제 상태를 모두 유지한다.

다음 항목도 포함하지 않는다.

- 외부 모델·텍스처 원본: `Assets/CRT/Resource/**`, `Assets/CRT/Resource.meta`, `Assets/Dnk_Dev/**`, `Assets/Dnk_Dev.meta`.
- 무관한 임시 코드·씬·VFX·머티리얼·IDE 변경 및 `Assets/Live2D.meta`.
- `Assets/Settings/PC_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`과 무관한 전역 프로젝트 설정 변경. `TagManager.asset`의 위 두 레이어만 이번 범위다.
- `Build`, `Library`, `Logs`, `Temp`, 사용자별 설정, Python 설치 패키지와 캐시. 기존 검증 문서에 참조된 `Logs` 원본은 로컬 기록으로 남는다.

**이 커밋만 새로 받은 저장소에서 감시실을 바로 실행할 수 있는 것은 아니다.** 현재 로컬에 설치된 다음 외부 자료도 필요하다.

| 자료 | 현재 경로·조건 |
|---|---|
| Hospital Horror Pack | `Assets/Dnk_Dev/HospitalHorrorPack/Map_Hosp1.unity` 및 해당 팩 |
| HorrorPackFBX | `Assets/CRT/Resource/HorrorPackFBX/` |
| Complete CRT TV Set | `Assets/CRT/Resource/Complete CRT TV Set/` |
| Cameron Idle/Walk | `Assets/Samples/Cinemachine/3.1.7/Shared Assets/Cameron/Animations/`. `Assets/Samples/*`는 기존 Git 제외 규칙 대상이다. |
| HumanDummy_M White | `Assets/Kevin Iglesias/Human Character Dummy/`. 기존 추적 에셋이며 이번 커밋에서 추가 변경하지 않는다. |

원본 `.meta` GUID를 유지해야 저장된 씬과 프리팹 참조가 연결된다. 패키지는 현재 `manifest.json`의 Cinemachine 3.1.7, Input System 1.20.0, URP/Shader Graph 17.3.0을 사용하며 이번 제안에 패키지 변경은 없다.

## 5. 검증 근거와 확인 범위

이번 문서 작성에서는 테스트를 다시 실행하지 않았다. 저장된 결과를 읽고 현재 소스·씬·프리셋·빌드 DLL의 해시를 대조했다. 발소리 적용 검증 기록에 포함된 11개 경로가 모두 현재 파일과 일치한다.

| 검증 | 기록된 결과 | 근거 |
|---|---|---|
| Audio Lab | DSP·Editor 재생 19개, 조절·저장·복원 31개, 독립 PCM 7개 통과. 도메인 재로드·프로세스 재시작 통과 | [Audio Lab 검증 기록](../../Assets/CRT/AudioPreview/VALIDATION.md), 로컬 `Logs/CRTAudioPreview/*.json` |
| 발소리 적용 후 실행 | 70/70, 오류 0. 65.006초 배회 중 98회 발소리, 대기 중 추가 재생 0. -6dB 변경 시 RMS 0.501187배 | [실행 결과](../../Assets/CRT/Surveillance/Validation/audio-runtime-results.json) |
| 발소리 PCM | 12/12. 승인 원본 보존, 실제 처리 음역 축소, 길이 유지, 클리핑 없음 | [PCM 결과](../../Assets/CRT/Surveillance/Validation/audio-pcm-results.json) |
| 기존 영상 데모 | 당시 런타임 50/50, 독립 영상·전환 음원 28/28 | [기존 검증 기록](../../Assets/CRT/Surveillance/VALIDATION.md) |
| 기존 CRTPlayground | 당시 검사기별 56/56, 77/77, 102/102 | [회귀 요약](../../Assets/CRT/Surveillance/Validation/regression-summary.json) |

최신 발소리 자동 실행은 숨김 창에서 진행되어 PNG가 검정으로 저장됐다. 그 PNG로 새 영상 픽셀 회귀 통과를 주장하지 않는다. 별도 일반 실행 창에서 감시실과 실제 CCTV 영상이 표시되는 것은 확인했다. 물리 스피커의 주관적인 청감, 다른 플랫폼, 장시간 실행은 미확인이다. 발걸음 타이밍은 이동 거리 기준이며 발 접촉 애니메이션 이벤트·벽 차폐·병원 잔향은 구현하지 않았다.

## 6. 승인 후 실행 절차

1. 현재 HEAD와 파일 목록의 변경 여부를 다시 확인한다. 승인 이후 새로운 변경이 생겼다면 내용과 범위를 먼저 대조한다.
2. 승인된 정확한 경로만 대상으로 순서대로 로컬 커밋한다. 기존 스테이징 13개 항목을 섞지 않는다.
3. 각 커밋의 파일 목록·메시지와 남은 작업 폴더·인덱스 상태를 확인한다.
4. 커밋 해시 두 개와 포함 범위를 보고한다. 원격 push는 하지 않는다.

**승인 기록:** 2026-09-21 사용자의 `커밋 올려줘` 요청에 따라 위 범위와 메시지로 두 로컬 커밋을 진행한다. 원격 push는 승인 범위에 포함하지 않는다.
