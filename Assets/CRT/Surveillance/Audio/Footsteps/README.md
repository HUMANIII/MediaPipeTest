# A 실녹음 발소리

Fantozzi의 Stone L1~L3/R1~R3를 qubodup이 단발음으로 정리한 CC0 녹음이 원자료다.
배포: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone
라이선스: https://creativecommons.org/publicdomain/zero/1.0/

사용자가 승인한 `AudioPreviews/CRT_Footsteps/A_Recorded_Original.wav`의 첫 여섯 발걸음을 각각 0.43초씩 원본 PCM 그대로 추출했다. 원래 적용된 DC/극저역 정리·음량·페이드를 유지한다. 이 파일들에는 CRT 효과를 미리 굽지 않았다.

재현: 프로젝트 루트에서 `python Tools/CRT/extract_approved_footsteps.py`.
`extraction.json`은 원본 시작 프레임·길이·PCM SHA256을 기록한다. 원본 8초 비교 파일은 변경하지 않는다.

재생 순서는 L1, R1, L2, R2, L3, R3다. `SurveillanceFootstepAudio`가 선택한 설정 에셋으로 실행 중 처리하고, 추가된 잡음까지 앞 2ms·뒤 18ms 페이드한다.
