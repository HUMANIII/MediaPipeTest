# CRT 발소리 연결 검증

검증일: 2026-09-21. Windows, Unity 6000.3.23f1, Development Player, Direct3D 11.

## 변경 범위

- 승인한 A 실녹음의 여섯 발걸음을 무손실 PCM으로 추출하고 기존 감시실 씬의 CRT에 전용 AudioSource를 추가했다.
- Audio Lab의 설정·간편 조절 계산·DSP를 `MediaPipeTest.CRT.Audio` 런타임 어셈블리로 분리했다. 기존 스크립트 GUID와 프리셋을 보존했으며 기본 설정 에셋을 덮어쓰지 않았다.
- 저장된 `DefaultCRT.asset`을 참조해 더미의 실제 이동 거리에 따라 재생한다. 선택한 CCTV 카메라와 더미의 거리를 음량에 반영한다.
- 채널 전환 치지직 소리, C 키 영상 비교, 방·병원 배치·캐릭터 경로는 기존 동작을 유지했다.

## 실제 실행 결과

`Build/Surveillance/SurveillanceDemo.exe`를 새로 빌드해 실행했다. 저장된 씬 참조·기존 프리셋 로드·컴파일·Windows 빌드를 통과했다.

| 검사 | 결과 |
|---|---|
| 실행 단언 | **70/70 통과**, 오류·예외 0개 |
| 배회 | 65.006초, 55.277m, 목적지 8개, 도착 7회 |
| 발소리 | **98회** 재생, 실제 `AudioSource.GetOutputData` 최대 RMS **0.07784** |
| 대기 | 대기 중 새 발소리 0회, 마지막 소리 종료 후 잔여 재생·유의미한 출력 0프레임 |
| 채널 | 4채널의 실제 표시 번호와 발소리 마이크 위치 일치 |
| 설정 변경 | 런타임 사본 프리셋을 -6dB로 바꾸자 처리 PCM RMS가 **0.501187배**로 변경. 원본 에셋은 변경하지 않음 |
| 영상 비교 | C 키로 원본 영상을 봐도 독립 오디오 프리셋 유지 |
| 정리 | 비활성화 시 재생 정지·임시 클립 0개, 다시 켜면 6개. 두 차례 씬 재진입에서도 6개만 존재 |
| 기존 동작 | 착석·복귀·충돌·Input System 키 처리·채널 순환·전환 연타·치지직 출력 통과 |

결과 원본: [audio-runtime-results.json](Validation/audio-runtime-results.json).
실행 로그 및 처리 PCM: 프로젝트 `Logs/Surveillance/AudioIntegration`.

## 독립 PCM 및 기존 도구 회귀

- Python WAV 디코더·NumPy 검사 **12/12 통과**. 여섯 단발음 모두 승인 파일의 해당 PCM 구간과 바이트 단위로 일치했다.
- 실제 플레이어가 읽은 원본 PCM과 임포트 클립의 차이는 PCM16 재기록에 따른 최대 1단계였다.
- 실제 처리 클립은 44.1kHz 모노 0.43초를 유지하며 피크 0.52209, 첫·끝 샘플 0으로 클리핑과 끝단 불연속이 없었다.
- 250~3500Hz 바깥의 에너지 비율은 원본 0.37727에서 CRT 0.07178로 감소했다.
- 원래 A 비교 파일과 Audio Lab 예시 파일의 해시는 유지됐다.
- 공유 코드 이동 후 Audio Lab의 기존 DSP·미리듣기 19개, 조절·저장·복원 31개, 실제 도메인 재로드·Unity 프로세스 재시작, 독립 PCM 7개 검사를 다시 통과했다.

기록: [audio-pcm-results.json](Validation/audio-pcm-results.json), [Audio Lab 검증](../AudioPreview/VALIDATION.md).
실행한 코드·저장 씬·프리셋·빌드 DLL의 해시는 [audio-source-build-hashes.json](Validation/audio-source-build-hashes.json)에 기록한다.

## 관찰 범위와 제한

- 숨김 상태의 자동 실행은 오디오·이동·참조 검증에 사용했다. 이 실행의 PNG는 검정으로 캡처되어 픽셀 회귀 통과 근거로 사용하지 않았다.
- 별도로 일반 실행 창을 전면에 표시해 감시실·CRT·실시간 병원 영상·채널 안내가 정상 렌더링되는 것을 Computer Use로 확인했다. 이번 변경은 셰이더나 영상 처리 코드를 바꾸지 않았다.
- 실제 오디오 데이터와 출력 샘플을 검사했다. 물리 스피커에서의 주관적인 음색·음량 평가는 수행하지 않았다.
- 발걸음은 실제 이동 거리 기준이다. 애니메이션 발 접촉 이벤트·벽 차폐·병원 잔향은 구현 범위에 포함하지 않는다.

## 재현

1. `Tools > CRT > Surveillance > Validate Saved Demo`, `Build Windows Demo`를 실행한다.
2. README의 `-surveillanceValidate` 옵션으로 빌드를 실행한다.
3. `python Tools/CRT/check_surveillance_footsteps.py <실행 결과 폴더>`로 실제 처리 PCM을 확인한다.
4. Audio Lab 회귀는 `Tools/CRT/Test-AudioRefinement.ps1`, `Tools/CRT/check_audio_preview.py`를 실행한다.
