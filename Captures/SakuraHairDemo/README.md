> 현재 버전은 [머리 내부의 독립 낙하](InteriorFall/README.md)입니다. [새 영상 보기](InteriorFall/SakuraHairInteriorFall.mp4). 아래 내용은 이전 UV 기반 구현의 기록입니다.

# Sakura Hair Demo — 실행 확인

- [6초 재생 영상](SakuraHairDemo.mp4): Unity 6000.3.23f1 Play Mode에서 RTX 3070 Ti로 렌더링한 180프레임, 1280×1280, H.264, 30fps.
- [효과 끄기](front-off.png) / [효과 켜기](front-on.png) / [측면](side-on.png) / [후면](back-on.png).
- [영상과 비교 화면](index.html): 로컬 HTML로 열 수 있습니다. 로컬 파일을 지원하지 않는 뷰어에서는 이 폴더만 HTTP 서버로 제공해 열면 됩니다.

영상은 앞 3초 동안 카메라를 고정해 꽃잎 움직임을 보여 주고, 뒤 3초 동안 카메라를 조금 회전합니다. 정지 이미지에 이동을 준 영상이 아니라 Unity의 Time 노드로 동작하는 셰이더를 실제 Play Mode에서 렌더링한 결과입니다. `Time.captureFramerate=30`으로 시뮬레이션 간격을 고정했으므로 이 영상은 실시간 성능 벤치마크가 아닙니다. Blender는 Unity가 만든 PNG 프레임의 H.264 인코딩에만 사용했습니다.

## 확인 결과

| 검사 | 결과 |
|---|---|
| URP Shader Graph 컴파일 및 최종 재임포트 | 오류 없음 |
| Play Mode 실행 중 오류 | 없음 |
| 시작 직후 효과 및 2초 후 변화 | 실제 렌더 이미지에서 확인 |
| 효과 강도 0 / 발광 1 | 효과를 끈 기준 이미지와 픽셀 일치 |
| 밀도 0 | 효과를 끈 기준 이미지와 픽셀 일치 |
| 얼굴과 의상 표본 영역 | 전후 픽셀 변화 없음 |
| 정면·측면·후면 | 꽃잎 윤곽 확인, 사각 배경 없음. 밀도와 UV 비율 조정 완료 |
| 저장된 씬 다시 열기 | 머티리얼, 텍스처, 기본값 유지 |
| 변신 스크립트 / 파티클 | 데모 씬에 없음 |
| 원본 씬·프리팹·모델·공유 머티리얼·제공 리소스 | 기준 178개 파일 SHA-256 일치 |
| MP4 | 180프레임, 6초, 30fps, H.264 확인 |
| 앱 브라우저 | 전체 재생, 처음부터, 0.5배속, 일시 정지 확인. 재생 오류 없음 |

머리카락의 기존 UV를 그대로 사용하므로 곡면과 끝부분에서는 약간의 무늬 변형이 있습니다. 원래 머티리얼은 Unlit/Texture이며 새 셰이더는 Lit이므로 기본 머릿결의 조명 반응은 다릅니다. 독립 플레이어 빌드 및 장시간 성능 측정은 수행하지 않았습니다.

## 근거 파일

- `runtime-validation.json`: Unity 버전, GPU, Play Mode 시간, 프레임, 오류와 셰이더 메시지.
- `saved-scene-validation.json`: 별도 Unity 실행에서 최종 그래프를 재임포트하고 저장된 씬을 다시 연 결과.
- `pixel-and-preservation-validation.json`: 이미지 비교 수치와 보호 대상 파일 해시 비교.
- `media-validation.json`: MP4 컨테이너의 코덱·길이·해상도·프레임 수.
- `browser-playback.png`: 실제 영상 재생 후 일시 정지한 앱 브라우저 화면.
- `Frames/`: Unity가 출력한 180개의 원본 프레임.

사용법과 파라미터는 `Assets/SakuraHairDemo/README.md`를 참조합니다. 기존 Henshin 씬과 다른 진행 중 작업은 변경 범위에 포함하지 않았습니다.

개별 PNG 시퀀스인 "Frames/"는 로컬 캡처 산출물로 보존하며 Git 커밋에서는 제외합니다. 비교 이미지, MP4 영상과 검증 JSON은 함께 보관합니다.
