# CRT 발소리 청취 비교

Unity 적용 전에 음색을 비교하기 위해 제작한 8초 WAV 네 개다. 사용자는 A 실녹음을 선택했으며 현재 감시실에도 연결되어 있다. CRT 처리 강도는 Editor 미리듣기 도구에서 조절하고 설정 에셋으로 저장한다.

| 파일 | 내용 |
|---|---|
| A_Recorded_Original.wav | 실녹음: 단단한 돌바닥 발소리. CRT 처리 없음. |
| A_Recorded_CRT.wav | 같은 실녹음에 오래된 스피커의 음역 제한·약한 왜곡·잡음 적용. |
| B_Synthesized_Original.wav | 자체 합성: 뒤꿈치 타격, 앞꿈치 타격, 짧은 밑창 마찰음. CRT 처리 없음. |
| B_Synthesized_CRT.wav | 같은 합성음에 A와 동일한 CRT 처리 적용. |

## 비교 조건

- 모두 44,100Hz, 모노, 16-bit PCM WAV, 정확히 8초다.
- 첫 발걸음은 0.45초, 이후 0.55초 간격으로 총 13걸음이다. 마지막 발걸음은 7.05초다.
- 좌우 발과 세 가지 변형을 번갈아 사용한다. A/B는 같은 타이밍과 상대 강약을 사용한다.
- 네 파일의 전체 RMS를 약 -26.731dBFS로 맞췄다. 음색별로 체감 음량은 다를 수 있다. 피크 헤드룸을 확보하며 하드 클리핑으로 음량을 맞추지 않았다.
- CRT 처리: 250Hz 하이패스, 3,500Hz 로우패스, 낮은 비율의 소프트 포화, 4,200Hz 마무리 로우패스, 작은 대역 제한 잡음과 험. 시작·끝에는 페이드를 적용했다.
- 채널 전환의 치지직 소리는 이번 비교 파일에 섞지 않았다. 발소리와 지속적인 스피커 음색만 비교한다.
- 가까운 거리의 고정 음량 예시다. 현재 감시실은 CCTV 거리 감쇠와 이동 거리 기반 발걸음 재생을 별도로 적용한다. 아래 비교 파일 자체에는 거리 감쇠가 없다.

## 실녹음 출처 및 편집

- 제목: Fantozzi's Footsteps (Grass/Sand & Stone)
- 원작자: Fantozzi / OpenGameArt 배포·단발음 정리: qubodup
- 라이선스: CC0 1.0, 배포 페이지에서 2026-09-21 확인.
- 배포 페이지: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone
- 라이선스: https://creativecommons.org/publicdomain/zero/1.0/
- 원본 팩: https://www.freesound.org/people/Fantozzi/packs/10338/
- 다운로드: https://opengameart.org/sites/default/files/Fantozzi-footsteps.7z
- 사용 파일: Fantozzi-StoneL1/L2/L3.flac, Fantozzi-StoneR1/R2/R3.flac.
- 원본 아카이브와 사용한 FLAC 여섯 개는 `Source`에 보존했다. 다른 바닥 소리는 사용하지 않았다.
- 편집: 모노 변환, DC 및 극저역 정리, 짧은 끝단 페이드, 단발음 기준 음량 조정, 좌우 순서 배치, 출력 음량 조정. CRT 파일에는 추가 스피커 처리를 적용했다.
- 배포 설명은 단단한 바닥의 발소리이며 신발 종류를 특정하지 않는다. 이 샘플이 원하는 구두 음색에 맞는지는 실제 청취 후 선택한다.

B는 고정 난수 시드로 직접 합성했으며 외부 녹음을 섞지 않았다.

## 검증과 재생성

`validation.json`에 저장한 WAV를 다시 읽은 길이·포맷·음량·클리핑·시작/끝·발걸음 스케줄·CRT 음역 검사를 기록한다.
`generation.json`에는 파라미터와 원본·결과 파일 SHA256을 기록한다. 소리의 선호도와 자연스러움은 사용자 청취로 확정한다.

생성: `make_previews.py` (Python, NumPy, SoundFile 필요)
검사: `check_previews.py` (Python, NumPy 필요)

변환 도구 의존성은 이 폴더의 `.tools`에만 설치했다. 최초 비교 파일 제작 단계에서는 Unity Assets, 씬, 프리팹, 코드, 프로젝트 설정을 변경하지 않았다.

## 선택

A 실녹음을 선택했다. `Tools > CRT > Audio Preview` 도구에 A 원본 사본을 제공하며, 원본 네 파일은 보존한다.
음색 강도·음역·찌그러짐·잡음·출력 볼륨을 조절하고 설정 에셋으로 저장할 수 있다. 최신 도구는 WAV 내보내기를 제공하지 않는다.
도구 안내: [CRT Audio Lab](../../Assets/CRT/AudioPreview/README.md).
현재 [감시실 데모](../../Assets/CRT/Surveillance/README.md)에는 A 원본에서 추출한 여섯 단발음이 연결되어 있다. `DefaultCRT.asset`으로 음색을 처리해 CRT에서 재생하며, 이후 강도 변경은 Audio Lab에서 해당 에셋에 저장한다.
`Exports`의 파일은 이전 도구 조작 검증 중 만든 출력이며 현재 감시실에서 사용하는 음원이 아니다.
