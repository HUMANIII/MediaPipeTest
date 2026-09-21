# CRT 감시실 데모

`Scenes/SurveillanceDemo.unity`를 열고 Play한다. 병원 안의 흰색 더미를 감시실 CRT 한 대로 관찰하는 데모다.
씬의 병원·감시실·가구·카메라·착석 지점·참조는 모두 저장되어 있으며 Inspector에서 직접 수정할 수 있다.
Editor에서는 Game 뷰를 클릭해 입력 포커스를 준 뒤 조작한다.

| 상태 | 조작 | 동작 |
|---|---|---|
| 자유 이동 | WASD / 마우스 | 감시실 이동 / 둘러보기 |
| 착석 지점 1.5m 이내 | E | 약 0.25초에 걸쳐 고정 시점으로 착석 |
| 착석 중 | ← / →, PREVIOUS / NEXT | CCTV 4채널 순환 |
| 착석 중 | C, CRT / ORIGINAL | 화면 효과 전체 전환 |
| 착석 중 | E / Esc, STAND UP | 착석 직전 위치·시점·커서 상태로 복귀 |
| 자유 이동 | Esc / 화면 클릭 | 커서 해제 / 다시 잠금 |

앉으면 커서를 사용할 수 있고, 이동과 마우스 시점 회전이 멈춘다. HUD의 CAM 번호는 실제 표시 중인 채널이다.
전환 중 추가 채널 입력은 무시하며 일어나기는 가능하다. 캐릭터의 배회는 착석과 채널 선택에 관계없이 계속된다.

## 구성과 편집

- `Hospital - Map_Hosp1 Copy`: Hospital Horror Pack의 배치 사본. 문은 열린 상태다. 통로를 막던 침대 한 개는 빈 병실로 옮겼다.
- `Surveillance Room - HorrorPackFBX`: 원본 방 모델로 만든 감시실, 책상, CRT, 스툴과 착석 기준점.
- `CCTV Controller`: `SurveillanceFeed`, 4개의 고정 CinemachineCamera. 위치·회전·Lens에서 구도를 수정한다.
- `CCTV Output Camera`: 전용 CinemachineBrain. Channel02만 받고 Cut으로 전환한다.
- `Player - Surveillance Room Only`: CharacterController와 독립 플레이어 카메라.
- `White Dummy - Random Patrol`: HumanDummy_M White와 Cameron의 Humanoid Idle/Walk 애니메이션. Root Motion은 사용하지 않는다.
- `Navigation/HospitalNavigation.asset`: 병원 Collider로 생성한 NavMesh. 벽·침대·가구와 열린 문 위치를 반영한다.
- `Profiles/SurveillanceCRT.asset`: 흑백·주사선·픽셀화·곡면·비네트 설정. 공유 프로필에는 전환 진행 상태가 없다.
- `Meshes/CRTScreenNormalized.asset`: 원본 Screen 메시를 복제하고 정면 기준 UV를 0~1로 보정한 사본.
- `Prefabs`: CRT, 스툴, 배회 더미. 원본 모델과 병원 패키지는 변경하지 않는다.
- `Audio/ChannelStatic.wav`: 자체 생성한 44.1kHz 모노 PCM, 0.20초. 앞뒤 페이드 포함.

병원은 `HospitalWorld`, 감시실은 `SurveillanceRoom` 렌더링 레이어다. 두 카메라의 Culling Mask는 분리되어 있다.
데모 전용 URP 설정은 씬을 사용할 때만 적용하고 종료하면 이전 Quality 파이프라인을 복원한다.

## 화면과 전환

`CinemachineCamera 4대 → CCTV Camera + Brain → RenderTexture 1024×768 → CrtSurface`

RenderTexture는 실행 중 하나만 생성해 재사용한다. 씬 종료 시 카메라·화면 연결을 끊고 메모리를 해제한다.
머티리얼은 기존 `CrtSurface`가 화면마다 복제하고 정리한다. 정적 음원 에셋은 AudioSource 하나에서 재생한다.

전환은 0~0.05초 잡음 상승, 0.05초에 채널 Cut, 0.20초까지 감소 순서다.
흑백 잡음·가로 띠 UV 이동·밝기 흔들림은 Shader Graph 노드로 구성했고 `CrtGraphBuilder` 재생성에도 유지된다.
`CrtSurface.transitionStrength`와 `transitionTime`은 저장하지 않는 런타임 값이다. 강도가 0이면 기존 CRT 출력이 유지된다.

ORIGINAL은 동일한 실시간 입력에서 `_CRT_EFFECT_ON`을 끄는 우회 모드다. 기본 CRT 효과와 전환 왜곡을 모두 제거한다.
채널 전환 지연과 소리는 유지하므로 영상 효과만 비교할 수 있다.

```csharp
feed.NextChannel();
feed.PreviousChannel();
feed.SetCrtEnabled(false); // 원본 영상
feed.SetCrtEnabled(true);  // CRT 영상
```

## CRT 발소리와 오디오 설정

승인한 A 실녹음에서 추출한 왼발·오른발 각 3가지 단발음을 사용한다. 더미가 실제 이동한 거리 약 0.58m마다 번갈아 재생하며, 대기·정체 중에는 새 발소리를 내지 않는다. 마지막 발소리는 짧은 잔향을 마치고 정지한다. 타이밍은 이동 거리 기준이며 애니메이션 발 접촉 이벤트 기반은 아니다.

`CRT Monitor > CCTV Footstep Speaker`의 `SurveillanceFootstepAudio`가 `AudioPreview/Presets/DefaultCRT.asset`을 참조한다. Audio Lab과 동일한 DSP로 좁은 음역·찌그러짐·잡음·출력 볼륨·피크 보호를 적용한다. 원본 클립은 보존하고, 실행 중에 생성한 여섯 처리 클립은 비활성화·씬 종료 시 해제한다.

1. `Tools > CRT > Audio Preview`에서 `DefaultCRT` 설정 에셋을 선택한다.
2. A 예시나 다른 클립을 넣고 조절한 뒤 **설정 에셋에 저장**을 누른다.
3. 감시실을 Play한다. Play 중 같은 에셋을 저장해도 약 0.5초 이내에 처리 클립을 갱신해 이후 발걸음에 적용한다. 저장하지 않은 Audio Lab 값은 적용되지 않는다.

다른 설정을 쓰려면 `SurveillanceFootstepAudio.preset`에 해당 `.asset`을 지정한다. 설정 에셋의 출력 볼륨 이후에 CCTV와 더미 사이 거리 감쇠(기본 2~22m), CRT와 플레이어 사이의 3D 거리 감쇠가 적용되므로 Audio Lab의 청취 크기와는 차이가 날 수 있다. 벽의 차폐나 잔향은 시뮬레이션하지 않는다.

표시 중인 채널의 카메라를 마이크 위치로 사용한다. 실제 채널이 바뀌는 0.05초 시점에 듣는 위치도 바뀐다. 발소리는 CRT의 별도 AudioSource에서 나오며 전체 AudioListener나 채널 전환 치지직 소리에는 이 효과를 적용하지 않는다. 착석 여부와 관계없이 배회·발소리가 계속되며 CRT에서 멀어지면 작게 들린다. **C / CRT·ORIGINAL은 기존대로 영상만 비교한다.**

`Tools > CRT > Surveillance > Apply CRT Footstep Audio`는 기존 배치를 유지하면서 오디오 참조만 설치·갱신하는 메뉴다. 이미 지정한 오디오 프리셋은 보존한다.

## 생성·빌드·검증 도구

`Tools > CRT > Surveillance` 메뉴:

- **Create or Rebuild Demo**: 데모 배치·프로필·프리팹·NavMesh·음원을 초기 구성으로 재생성한다. 수동 배치 편집을 보존하려면 재생성 전에 씬을 별도로 복제한다.
- **Validate Saved Demo**: 저장된 참조·레이어·Humanoid·UV·NavMesh·셰이더를 검사한다.
- **Bake Navigation in Open Demo**: 현재 데모의 병원 배치를 기준으로 NavMesh만 다시 생성한다.
- **Build Windows Demo**: `Build/Surveillance/SurveillanceDemo.exe`를 만든다.

그래프를 재생성하려면 기존 `Tools > CRT > Rebuild Shader Graphs`를 사용한다.
병원 벽이나 가구를 직접 옮겼다면 Bake Navigation 메뉴로 NavMesh를 다시 굽고 씬을 저장한다.

Windows 실행 파일에 다음 옵션을 붙이면 자동 실행 검증과 PNG 캡처를 남긴다.

```text
-surveillanceValidate -surveillanceOutput "검증 결과를 저장할 절대 경로"
-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11
```

실행 검증은 65초 배회, 착석·복귀, 충돌, 채널 타이밍·연타, 고정 시점, 원본 모드와 두 차례 씬 재진입을 확인한다.
같은 CCTV 프레임을 고정한 원본·CRT·글리치 캡처와 실제 화면 메시 마스크를 함께 저장한다.
`Tools/CRT/check_surveillance.py`가 픽셀 차이의 범위, 원본 우회, 각 채널 영상, 음원 길이·페이드를 별도로 검사한다.
기존 영상 데모 결과와 제한은 [VALIDATION.md](VALIDATION.md), 새 발소리 연결 결과는 [VALIDATION-AUDIO.md](VALIDATION-AUDIO.md)에 기록한다.
