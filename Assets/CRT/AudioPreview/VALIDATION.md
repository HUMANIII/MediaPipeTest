# CRT Audio Lab 검증 기록

검증일: 2026-09-21. 환경: Windows, Unity 6000.3.23f1.
열린 사용자 프로젝트는 유지하고 동일 소스를 복사한 `Logs/CRTAudioRefinement/UnityProject`에서 검사했다.
실행 전 모든 Editor·Runtime C# 및 어셈블리 정의 파일의 SHA256 일치를 확인했다.
CRT 발소리 연결을 위해 설정·DSP를 런타임 어셈블리로 분리한 뒤 아래 검사를 다시 실행해 모두 통과했다.

## 결과

- 기존 DSP·실제 Editor 오디오 미리듣기 검사: **19개 통과**.
- 간편/상세 조절·설정 에셋·클립 복원 검사: **31개 통과**.
- 실제 도메인 재로드: 편집 중인 상세 설정·간편 값·클립 유지, 재생 정지, 임시 클립 한 개만 존재하는 것을 확인했다.
- 실제 새 Unity 프로세스: 마지막 재생 클립을 복원하고 간편 0·볼륨 0dB로 시작하며 자동 재생하지 않는 것을 확인했다.
- 독립 PCM 검사: **7개 통과**. Python WAV 디코더와 NumPy로 이번 실행의 검사 음원을 다시 읽었다.

## 주요 확인 사항

| 항목 | 확인 결과 |
|---|---|
| 간편 −100 / 0 / +100 | 기존 약하게 / 기본 CRT / 강하게 설정으로 처리한 실제 PCM과 일치 |
| 중간값 | 주파수 로그 보간, 나머지 선형 보간 확인. 전체 구간에서 요소 변화 방향 유지 |
| 상세 진입 | 진입 전후 효과 설정 동일. 상세 요소 수정은 실제 PCM을 변경 |
| 간편 복귀 | 마지막 간편 값 복원. 상세 재진입은 현재 간편 음색에서 시작 |
| 공통 설정 | 모드·음색 버튼 전환이 출력 볼륨, 피크 보호를 변경하지 않음. −40/+12dB 범위 유지 |
| 재생 중 조절 | 같은 엔진을 유지하며 간편 음색 변경과 상세 진입 중 재생 위치 계속 진행 |
| 설정 에셋 저장 | 클립 없이 저장 가능. 원본 비교 중이어도 CRT 설정 저장 |
| 설정 에셋 내용 | 실제 효과 값·모드·간편 값 저장. 클립·원본 비교 상태 미포함 |
| 설정 에셋 재로드 | 메모리에서 내려 다시 읽은 뒤 간편/상세 모드와 설정 복원 |
| 자동 저장 방지 | 슬라이더 편집만으로 기존 에셋 메모리와 파일이 변경되지 않음 |
| 이전 프리셋 | 상세 모드로 읽고 음색 보존. 읽는 것만으로 파일을 다시 쓰지 않음 |
| 마지막 클립 | 선택만 하면 기록하지 않고 실제 창의 재생 경로에서 GUID 기록 |
| GUID 복원 | 파일 이동 후 복원, 삭제 후 빈 상태, 프로젝트별 기록 분리 |
| 클립 교체 | 다른 클립에서도 같은 효과 설정 유지 |
| 정리 | 검사 창을 정리한 뒤 임시 AudioClip 수가 실행 전 값으로 복귀 |

기존 검사에는 강도(`strength`, 상세의 효과 혼합) 0에서 원본 PCM 일치, -6dB 진폭 비율, 잡음 제어, 스테레오 분리, 피크 보호, 압축/Streaming 클립 읽기, 일시정지·계속·구간 이동·반복·정지가 포함된다.
**간편 음색 강도 0은 기본 CRT이며 원본 바이패스를 뜻하지 않는다.**

## 재실행과 증거

Unity 메뉴: `Tools > CRT > Validate Audio Preview`, `Tools > CRT > Validate Audio Controls`.

전체 격리 검사 및 실제 재로드·재시작 검사:

```powershell
& .\Tools\CRT\Test-AudioRefinement.ps1
python Tools/CRT/check_audio_preview.py
```

PowerShell 검사는 설치된 Unity와 라이선스 서비스 접근이 필요하다. Python 검사는 NumPy가 필요하다.
검사 중 짧게 오디오를 재생한다. 검사 창은 정리하고 클립 기록은 복원한다.

- `Logs/CRTAudioPreview/validation.json`: 기존 19개 검사
- `Logs/CRTAudioPreview/controls-validation.json`: 새 조절·저장·복원 검사
- `Logs/CRTAudioPreview/reload-validation.json`, `restart-validation.json`: 실제 재로드 및 새 프로세스 검사
- `Logs/CRTAudioPreview/independent-pcm-checks.json`: 독립 PCM 검사
- `Logs/CRTAudioRefinement/verified-source-hashes.json`: 실행 소스 해시
- `Logs/CRTAudioRefinement/editor-run.log`, `editor-restart.log`: Unity 컴파일 및 실행 로그

검사용 WAV 출력은 자동 검증에만 사용한다. 사용자용 WAV 저장 버튼은 제거했다.
이전 도구의 WAV 저장·마우스 조작 검증 기록은 `Logs/CRTAudioRefinement/before/VALIDATION.md`에 보존했다.

## 검증 범위

이번 검사는 Unity 배치 실행에서 실제 Editor API·오디오 콜백·저장 파일·창 상태·도메인 재로드·프로세스 재시작을 확인했다.
새 UI의 마우스 조작과 화면 배치를 직접 관찰한 검사, 스피커 출력에 대한 사람의 청감 평가는 이번 실행에 포함되지 않는다.
모든 MP3/OGG/AIFF 파일과 모든 다채널 배치를 개별 시험하지는 않았다. 게임의 발걸음 타이밍·거리 감쇠·AudioSource 연결은 [감시실 오디오 검증](../Surveillance/VALIDATION-AUDIO.md)에 별도로 기록한다.
