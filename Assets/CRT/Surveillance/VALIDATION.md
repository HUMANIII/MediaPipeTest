# 감시실 데모 검증 결과

2026-09-21 CRT 발소리 적용 후 검증은 [오디오 연결 검증](VALIDATION-AUDIO.md)에 별도로 기록한다. 아래 수치는 최초 영상 데모 검증 기록이다.

Unity 6000.3.23f1, Windows x64 Development Player, Direct3D 11, NVIDIA RTX 3070 Ti에서 실행했다.
2026-09-20 빌드·자동 실행 결과와 2026-09-21 Editor 확인을 기록한다.
실행 파일은 `Build/Surveillance/SurveillanceDemo.exe`, 저장된 씬은 `Scenes/SurveillanceDemo.unity`다.

## 실행 결과

| 검사 | 결과와 근거 |
|---|---|
| 에셋·빌드 | 저장된 참조, 누락 스크립트, Humanoid, UV, 렌더링 레이어, 실제 CRT 크기·책상 위 위치 검사 통과. Windows 빌드 성공. |
| NavMesh | 저장된 데이터로 서쪽 복도에서 동쪽 복도 및 두 병실까지 완전한 경로 생성 확인. |
| 런타임 | **50/50 통과**, 기록된 오류·예외 0개. |
| 입력 | Input System에 키 이벤트를 넣고 실제 Player.Update에서 처리: E 착석·복귀, Esc 복귀, 좌우 채널, C 양방향 토글, 이동 확인. |
| Editor 화면 버튼 | Computer Use로 NEXT를 눌러 CAM 01→02, PREVIOUS로 02→01, CRT/ORIGINAL 양방향 전환, STAND UP으로 자유 시점 복귀를 화면에서 확인. |
| 이동·착석 | 실제 CharacterController를 벽과 책상으로 이동시켜 통과하지 않음을 확인. 세 차례 착석·복귀에서 위치·시점·커서 복원, 착석 중 이동 차단 확인. |
| 배회 | **65.006초**, **49.50m**, 목적지 8개, 도착 7회, 정체 복구 0회, 벽 겹침 감지 0회. |
| 애니메이션 | Walk 우세 샘플 185개, Idle 우세 샘플 63개, 실제 다리 뼈 움직임 샘플 199개. |
| 채널·글리치 | 4개 고정 시점과 앞뒤 순환 확인. 연타에도 채널·음원 각 1회. 착석 해제 중에도 전환 완료. |
| 전환 시간 | 관측 Cut **0.0617초**, 당시 강도 **0.9221**. 종료 **0.2035초**, 강도 0. 프레임 단위로 0.05/0.20초 임계값을 처리한다. |
| 음원 | 실제 AudioSource 출력의 최대 RMS **0.01852**, 모노 PCM **0.20초**, 시작·끝 페이드 확인. |
| 씬 재진입 | 두 차례 재진입 후 RenderTexture·화면 머티리얼·해당 AudioSource가 각각 하나. 전환 강도·오디오 재생 상태 초기화. |

원본 기록: [runtime-results.json](Validation/runtime-results.json), [build-result.txt](Validation/build-result.txt), [asset-result.txt](Validation/asset-result.txt).
검증한 소스·주요 에셋·빌드 DLL의 SHA256은 [source-build-hashes.json](Validation/source-build-hashes.json)에 남겼다.

## 독립 픽셀·음원 검사

런타임 내부 단언과 별도로 `Tools/CRT/check_surveillance.py`가 실제 PNG 및 WAV 데이터를 검사했다. **28/28 통과**.

- CCTV 입력을 같은 프레임으로 고정한 뒤 ORIGINAL, ORIGINAL+최대 전환 강도, CRT, GLITCH를 캡처했다.
- 베젤의 깊이 가림을 유지한 화면 마스크에서 보이는 CRT 영역 99,039픽셀을 검사했다.
- ORIGINAL에서 전환 강도 0과 1의 픽셀 차이는 **0**이었다.
- CRT 및 GLITCH 적용 시 화면 외부 픽셀의 최대 차이는 각각 **0**이었다. 경계의 안티앨리어싱 오차를 제외하기 위해 외부 판정에 3픽셀 여유를 둔다.
- 평상시 CRT와 GLITCH의 화면 내 평균 차이는 RGB 0~255 기준 **110.08**이었다.
- 네 채널 모두 1024×768, 서로 다른 실제 병원 영상, 오류 셰이더 색 없음.
- 음원 길이와 앞뒤 RMS 감소를 PCM 샘플에서 확인했다.

기록: [visual-audio-results.json](Validation/visual-audio-results.json).
원본 PNG와 플레이어 로그는 프로젝트 `Logs/Surveillance/DX11` 및 `Logs/Surveillance/player-dx11.log`에 있다.

## 기존 CRT 회귀

수정된 Shader Graph를 포함해 기존 CRTPlayground를 새로 빌드하고 Direct3D 11로 실행했다.

| 검사기 | 결과 |
|---|---|
| `check_captures.py` | 56/56 |
| `check_profiles.py` | 77/77 |
| `check_refinement.py` | 102/102, 기존 DX11 기준 영상과 비교한 영역의 최대 평균 오차 0 |

3D·Sprite·Canvas·전체 화면, 동영상 갱신·루프·재열기·일시정지/재개, UI 포함/제외, 프로필 유지, 씬 재진입 모두 통과했다.
RenderTexture 수는 전후 각각 1개, 기록된 오류는 0개다. 전환 강도 0에서 기존 출력이 유지됨을 확인했다.
전체 기록은 `Logs/Surveillance/CRTRegression`, 요약은 [regression-summary.json](Validation/regression-summary.json)에 있다.

## 재현

1. `Tools > CRT > Surveillance > Validate Saved Demo`로 저장된 참조를 검사한다.
2. `Build Windows Demo`로 실행 파일을 생성한다.
3. README의 `-surveillanceValidate` 옵션으로 약 75초 실행한다. 그래픽 캡처를 위해 실행 창을 표시해야 한다.
4. `python Tools/CRT/check_surveillance.py <결과 폴더> --audio Assets/CRT/Surveillance/Audio/ChannelStatic.wav`를 실행한다.

## 확인 범위

- Windows/DX11 한 환경에서 검사했다. 다른 그래픽 API·플랫폼·장시간 실행은 이번 검증에 포함하지 않았다.
- 키 바인딩은 실제 Input System 처리 경로를 자동 검증했다. Computer Use의 Editor 키 주입에서는 게임 입력을 재현하지 못했다. 물리 키보드로 직접 조작하는 시험은 미확인이다.
- 오디오는 실제 출력 샘플과 음원 데이터로 검증했다. 스피커에서의 주관적인 음량·음색 청취 평가는 수행하지 않았다.
- URP가 많은 점광원 그림자를 아틀라스에 맞춰 축소하는 안내 로그가 있다. 런타임 오류는 아니며 영상 검사를 통과했다.
- 초기 실패 및 수정 전 캡처는 `Logs/Surveillance/Initial*`, `Before*`에 보존했다. 위 수치는 최종 `DX11` 결과 기준이다.
