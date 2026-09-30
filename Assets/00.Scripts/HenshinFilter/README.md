# Henshin Filter Demo

`Assets/06.Scenes/HenshinFilterDemo.unity`를 열고 Play를 누릅니다. Python 서버나 웹캠은 필요하지 않습니다.

- 왼쪽 마우스 드래그: 필터 패널 이동
- `R`: 패널 초기 위치 복귀
- `E`: 필터 안의 변신 셰이더 켜기/끄기. 끄면 캐주얼 의상의 원래 색상을 확인할 수 있습니다.

필터 밖에는 비키니 캐릭터, 필터 안에는 캐주얼 의상을 입은 캐릭터가 표시됩니다. 의상은 영구적으로 변경되지 않습니다. 기존 Henshin 씬의 변신 시퀀스와 원본 모델·머티리얼은 수정하지 않습니다.

## 설정과 동작

`Henshin Filter Controller`의 `Base Renderers`는 기본 캐릭터, `Casual Renderers`는 몸·머리카락·캐주얼 상의·하의·신발의 렌더러입니다. 필터용 SkinnedMeshRenderer는 원본 Mesh와 뼈대를 공유하고 BlendShape 값을 따라갑니다. 비활성 상태의 캐주얼 원본도 명시적으로 참조하므로 원본 의상을 활성화할 필요가 없습니다.

보조 카메라는 같은 시점의 배경과 캐주얼 캐릭터를 RenderTexture에 먼저 그립니다. 필터 면은 화면 좌표로 이 결과를 표시합니다. 배경은 원래 모습으로 유지되고 캐릭터만 효과 머티리얼을 사용합니다. `HenshinBase`, `HenshinFiltered`, `HenshinPanel` 레이어로 기본 의상, 필터용 의상, 패널의 재귀 렌더링을 분리합니다.

`MagicalHenshinFilter.shadergraph`는 기존 MagicalHenshin의 색 계산을 유지한 별도 Shader Graph입니다. 의상과 몸의 깊이 관계를 위해 Opaque/ZWrite를 사용하고, 원본 텍스처 알파를 0.5 기준으로 잘라 머리카락·속눈썹 카드의 빈 부분을 유지합니다. 기존 효과의 속도·채도·밝기는 `CharacterEffect` 머티리얼의 `_speed`, `_S`, `_V`에서 조정합니다.

`Resolution Scale` 기본값은 1입니다. GPU 부담을 줄이려면 0.5로 낮출 수 있습니다. 창 또는 카메라 출력 크기가 바뀌면 텍스처가 재생성됩니다. 한 개의 기본 카메라와 하나의 패널을 사용하는 데모이며, 캐주얼 캐릭터는 추가 그림자를 투사하지 않습니다.

## 생성과 검증

- `Tools > Henshin Filter > Create or Rebuild Demo`: 원본 Henshin 씬을 기반으로 데모 생성. 재생성을 하기 전에 데모 씬을 닫아야 합니다. **생성된 데모 씬과 전용 머티리얼의 수동 편집은 재생성 시 대체됩니다.**
- `Tools > Henshin Filter > Validate Saved Demo`: 필수 참조와 셰이더 오류 확인
- `Tools > Henshin Filter > Build Demo`: `Builds/HenshinFilter/HenshinFilterDemo.exe` 생성. 프로젝트의 Build Settings 씬 목록은 변경하지 않습니다.

실행 파일에 `-henshinFilterValidate -henshinFilterOutput <폴더>`를 전달하면 의상 그룹, 뼈대·BlendShape 동기화, 필터 밖 픽셀 보존, 드래그 좌표, 효과 비교, 출력 크기 변경, 리소스 정리와 재활성화를 검증합니다. `-henshinFilterRecord`를 추가하면 같은 드래그 처리 경로로 패널을 이동시키며 30fps 시연 프레임을 저장합니다. 자동 캡처는 숨겨진 창의 백버퍼 대신 실제 Unity 카메라의 렌더 요청 결과를 사용합니다.

검증 코드는 위 인자가 없는 일반 실행에서는 동작하지 않습니다. 실제 마우스 입력 검증과 자동 드래그 경로 검증은 구분해서 기록합니다.
