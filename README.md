실시간 연출 데모는 TestScenc.unity를 통해 확인 가능하며

변신 연출 데모는 Henshin.unity를 통해 확인 가능합니다.

## 의상 전환 필터 데모

필터 밖에는 비키니 차림이 표시되고, 필터 안에는 캐주얼 의상을 입은 캐릭터 전체에 무지개색 변신 셰이더가 적용됩니다. 패널을 옮기면 겹치는 영역이 즉시 바뀌며, 필터를 벗어난 부분은 다시 비키니 차림으로 보입니다.

기본 캐릭터와 동일한 뼈대를 사용하는 캐주얼 캐릭터를 보조 카메라로 렌더링하고, 그 결과를 3D 패널 안에 표시하는 방식입니다.

[HenshinFilterDemo.unity](Assets/06.Scenes/HenshinFilterDemo.unity)를 열고 **Play**를 누르면 실행됩니다. Python 서버나 웹캠은 필요하지 않습니다.

| 조작 | 기능 |
| --- | --- |
| 왼쪽 마우스 드래그 | 필터 패널 이동 |
| `R` | 패널 초기 위치 복귀 |
| `E` | 변신 셰이더 켜기/끄기. 끄면 필터 안의 캐주얼 의상을 원래 색상으로 확인 |

설정·재생성·빌드·검증 방법은 [필터 데모 상세 안내](Assets/00.Scripts/HenshinFilter/README.md)를 참조합니다.

## 머리카락 벚꽃 데모

Henshin 캐릭터의 갈색 머릿결을 유지하면서, 머리카락 안의 별도 공간에서 꽃잎이 떨어지는 전용 데모입니다. 꽃잎마다 깊이·속도·회전이 다르며, 머리 표면의 굴곡을 따라 휘지 않고 월드 아래 방향 `(0, -1, 0)`으로 움직입니다. 효과는 `Hair1`에만 적용됩니다.

[SakuraHairDemo.unity](Assets/06.Scenes/SakuraHairDemo.unity)를 열고 **Play**를 누르면 바로 재생됩니다. [SakuraHair 머티리얼](Assets/SakuraHairDemo/Materials/SakuraHair.mat)에서 강도·색·크기·밀도·속도·흔들림·발광·내부 깊이를 조절합니다. `Effect Strength`를 0으로 설정하면 효과를 끈 상태를 비교할 수 있습니다.

- [구현 과정·구조·조절 항목](Assets/SakuraHairDemo/README.md)
- [최신 6초 재생 영상](Captures/SakuraHairDemo/InteriorFall/SakuraHairInteriorFall.mp4) · [효과 끄기](Captures/SakuraHairDemo/InteriorFall/front-off.png) / [켜기](Captures/SakuraHairDemo/InteriorFall/front-on.png)
- [Unity 실행·표면 독립성·원본 보존 검증 결과](Captures/SakuraHairDemo/InteriorFall/README.md)

원본 Henshin 씬·캐릭터 프리팹·공유 머티리얼은 보존했습니다. 공간으로 방출되는 파티클, 디졸브와 변신 연동은 포함하지 않습니다.

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
