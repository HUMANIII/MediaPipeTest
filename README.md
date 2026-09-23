실시간 연출 데모는 TestScenc.unity를 통해 확인 가능하며

변신 연출 데모는 Henshin.unity를 통해 확인 가능합니다.

## CRT 감시실 실행 준비

감시실 데모를 처음 실행할 때는 아래 외부 에셋을 직접 다운로드하고 임포트해야 합니다. 모델 원본은 저장소에 포함되어 있지 않으며, 프로젝트를 여는 것만으로 자동 복원되지 않습니다.

- [PBR - Hospital Horror Pack. Free](https://assetstore.unity.com/packages/3d/environments/pbr-hospital-horror-pack-free-80117) — 병원 맵, 버전 1.2.
- [CRT TV and Remote Models](https://nailfighter.itch.io/crt-tv-and-remote-models) — `Complete CRT TV Set` 다운로드.
- [Abandoned Room – Free Horror Asset Pack](https://blackgearstudio.itch.io/abandoned-room-free-horror-asset-pack) — `HorrorPackFBX.zip` 다운로드.

Cinemachine의 걷기·대기 샘플도 필요합니다. [외부 에셋 설치 순서와 씬 참조 복원](Assets/CRT/Surveillance/README.md#외부-에셋-설치)에 따라 준비한 뒤 감시실 씬을 실행합니다.

## CRT 사운드

`Tools > CRT > Audio Preview`의 Audio Lab에서 클립의 원본/CRT 음색을 비교하고 간편·상세 조절 결과를 설정 에셋으로 저장할 수 있습니다.
감시실 데모는 승인한 A 발소리에 `DefaultCRT.asset`의 음색을 적용해 CRT에서 재생하며, 채널 전환 시 별도의 치지직 소리를 재생합니다.

- [CRT 사운드 설정과 적용 방법](Assets/CRT/README.md#사운드)
- [Audio Lab 상세 안내](Assets/CRT/AudioPreview/README.md)
- [감시실 데모 실행 및 조작](Assets/CRT/Surveillance/README.md)
