# Poreia-Scripts

> **Poreia** 개발에 참여한 Unity C# 스크립트 모음입니다.
> 직접 설계·작성한 스크립트와 팀원 원본 코드에 기여(기능 추가·버그 수정·리팩토링)한 스크립트를 구분해 README를 작성했습니다.

---

## 🎮 프로젝트 개요

**Poreia**는 SF 디스토피아 배경의 3인칭 3D 퍼즐 어드벤처 게임입니다.

기억을 잃은 고양이 로봇 소마(Soma)는 잃어버린 가족을 찾아 떠나는 여정 속에서,  
AI 존재 이데아(Idea)와 로봇이 인간을 괴물로 인식하게 만드는 바이러스 스키아(Skia)의 진실을 밝혀나갑니다.

| 항목 | 내용 |
|---|---|
| 장르 | SF 디스토피아 3D 퍼즐 어드벤처 |
| 엔진 | Unity (URP) |
| 플랫폼 | PC (Steam / Stove Indie) |
| 지원 언어 | 한국어 / 영어 (로컬라이징) |
| 팀 | ARDORBIT (8인) |
| 역할 | Team Lead, Project Manager, Lead Programmer |

---

## 🏗 아키텍처 & 디자인 패턴

게임 매니저 시스템 설계 시 아래 패턴들을 의도적으로 적용했습니다.

| # | 패턴 | 적용 위치 | 설명 |
|---|---|---|---|
| 1 | **싱글턴 패턴** | `SaveDataManager`, `LoadingManager`, `SoundManager`, `GameUIManager`, `MagneticManager`, `SettingsManager` | 씬 전환 시 파괴되지 않는 전역 매니저를 단일 인스턴스로 유지 |
| 2 | **템플릿 메서드 패턴** | `GameManagerBass<TState>` | 기반 클래스에서 게임 매니저 초기화 순서를 고정하고, 자식 클래스는 맵별 세부 구현만 담당 |
| 3 | **빌드 타임 컴파일** | `SaveDataManager` | 개발 모드에서는 평문 저장 전략, 릴리즈 모드에서는 암호화 저장 전략을 컴파일 시점에 선택 |
| 4 | **옵저버 패턴** | `EventBroker` | 타임라인 종료 시 `EventBroker`를 통해 구독 중인 모든 시스템(GameManager, SoundManager 등)에 자동 알림 |
| 5 | **Enum 기반 FSM** | `Map1~4GameManager`, `SkiaController` | 게임 진행 단계를 명시적 상태로 정의하고, 상태 변경 시 다음 단계로 자동 전환 관리 |
| 6 | **지연 이벤트 콜백** | `GameManagerBass<TState>` | 타임라인 완료 시 실행할 액션을 UnityEvent에 미리 등록해 두고 일괄 실행 |
| 7 | **레지스트리 패턴** | `GameManagerRegistry` | 제네릭 타입이 다른 GameManager들을 단일 저장소에서 씬별로 관리하고, 리플렉션으로 공통 메서드 호출 |

---

## ✍️ 직접 작성한 시스템

### 아키텍처
- **FSM 기반 게임 상태 관리** — 제네릭 추상 기반 클래스 `GameManagerBass<TState>` 및 씬 전환용 `GameManagerRegistry`  
  → Intro / Map1~4 GameManager 구현, 플레이어 스폰·세이브포인트·씬 로드를 단일 클래스에서 처리
- **이벤트 드리븐 아키텍처** — 문자열 키 기반 Pub/Sub `EventBroker`, 조건 분리 `TriggerConditionHandler` / `ITriggerCondition`  
  → 씬 간 결합도 최소화, `GameStateCondition`·`InventoryCondition` 조건 조합 가능
- **멀티씬 아키텍처** — `EditorChildSceneLoader` / `RuntimeChildSceneLoader`로 Additive 씬 로딩  
  → 8인 병렬 개발 환경 지원

### 플레이어
- `ThirdPersonController` / `PlayerMovementController` — CharacterController 기반 이동·점프·웅크리기
- `SomaShieldController` — 방어막 기믹
- `PlayerDialogueHandler` — 대화 시 입력 모드 전환 및 NPC 방향 회전 연동
- `BasicRigidBodyPush` — Rigidbody 오브젝트 밀기

### 적 AI
- `SkiaController` — NavMesh + FSM 기반 베이스 컨트롤러 (이동·부드러운 회전·애니메이션 전환)
- `StorageSkia` / `JailSkia` — `SkiaController` 상속, 맵별 행동 패턴 특화
- `StorageSkiaManager` — 다수 스키아 풀 관리·활성화 조율
- `WalkDurationTrigger` — 거리·시간 조건 기반 패트롤 트리거

### UI 시스템
- `PanelManager` / `GuideManager` — 단일 패널 보장 스택 관리
- 패널 상태 패턴 — `IPanelState` 인터페이스, `PanelStateFactory`, HUD / Dialogue / Player / UI 모드 4종
- `BlackScreenFader` / `LoadingManager` / `VideoManager` / `EscapeKeyHandler` / `DeathEffectManager`
- `ButtonHover` / `UIBillboard` / `DisableButtonOnClick`

### 사운드
- `SoundManager` — BGM 크로스페이드, 인덱스 기반 SFX 재생(중복 방지), PlayerPrefs 볼륨 설정
- `LoopSound3D` / `Simple3DSound` — 3D 공간 음향
- `BoxCollisionSound` / `TargetCollisionEffect` / `TimelineAudioVolumeSync`

### 입력 관리
- `InputModeManager` — Player / UI / RepelTarget / RepelSource 4모드 전환 (Unity New Input System)
- `EditorDialogueSkip` / `DevBuildMode` — 에디터·개발 빌드 전용 입력 유틸

### 인벤토리
- `InventoryUI` / `ItemData` / `ItemSlot` / `ItemPickup` / `ItemConsumer`

### 세이브·설정
- `SaveDataManager` — 씬별 세이브/로드
- `SavePoint` / `SettingsManager` — 세이브 트리거, 그래픽·볼륨 설정

### 애니메이션
- `FootstepHandlerBase` / `PlayerFootstepHandler` / `MonsterFootstepHandler` — 발소리 이벤트 처리
- `PullStateBehaviour` / `PushStateBehaviour` — 당기기·밀기 Animator State Behaviour
- `EyeBlinkAnimator` — 눈 깜빡임 애니메이션

### 카메라
- `MainCam` — Cinemachine 3.x 메인 카메라 제어
- `CameraAspectRatioFitter` — 해상도별 종횡비 보정 (레터박스)
- `SomaCollisionHider` — 카메라-오브젝트 충돌 시 메시 은닉 (셰이더 연동)

### 인프라·헬퍼
- `DebugLogger` — `ENABLE_DEBUG_LOG` 조건부 컴파일 로그 래퍼
- `ObjectPool` / `PerformanceMonitor` / `InteractionObjectNotifier` / `StateConditionalTrigger`
- `SteamManager` — Steamworks.NET 래퍼

### 기믹
- `DeathZone` / `LaserRotator` / `RigidbodyGroupController`

### 에디터 도구 (빌드 미포함)
- `ThumbnailGenerator` — 인게임 썸네일 자동 캡처
- `AudioListenerFinder` / `ArdorbitHelper` / `ChildSceneLoaderInspectorGUI` / `DialogueConfigAutoSetup` / `ItemDatabase`

---

## 🤝 팀원 코드에 기여한 내역

> 기존 개발 인원(팀원1, 팀원2)은 프로젝트 장기화에 따른 역할 조정으로 개발 외 업무로 전환. 디자인 팀원(팀원3)의 희망으로 일부 기능 서브 개발 위임. 기여도는 git blame 기준 라인 수로 산출.

### 팀원1 - 자기력 메커닉 시스템

| 스크립트 | 기여 내용 | 기여도 |
|---|---|---|
| `Magnetic/MagneticAnchor.cs` | R키 씬 재로드 후 미작동 버그 수정 / 부착 VFX 교체 / 카메라 정중앙 방향 발사 로직 | **51%** |
| `Magnetic/MagneticManager.cs` | 감지 UI 연동 / 감지 우선순위 정리 / 앵커 초기화 완료 대기 로딩 / 참조 주입 | **42%** |
| `Magnetic/LightAnchor.cs` | Push·Pull 애니메이션 연동 / 조작 가이드 UI 활성·비활성화 / 리스폰 후 마우스 조작 불가 버그 수정 / 사망 시 `_hasExploded` 초기화 / 조준 시 박스 투명화 | **36%** |
| `NPC/SkiaStateType.cs` | 창고 스키아용 상태 타입 추가 | **29%** |
| `Magnetic/HeavyAnchor.cs` | VFX 구현 / 사운드·부착 애니메이션·Look 기능 추가 / 씬 전환 후 재부착 시 VFX만 발생하는 버그 수정 / 드랍·던지기 속도 상속 튕김 수정 / 홀드 중 벽 클리핑·진동 개선 | **23%** |
| `Magnetic/RayDetector.cs` | occlusion 체크 추가 (장애물 뒤 앵커 감지 차단) | **18%** |
| `Camera/BackViewCameraController.cs` | 이동 입력 감지 시 백뷰 자동 해제 | **11%** |

### 팀원2 - UI · 타임라인 · 대화 시스템

| 스크립트 | 기여 내용 | 기여도 |
|---|---|---|
| `UI/Components/SomaAtion.cs` | 소마 눈 뜨기·깜빡임 등 기능 추가로 대부분 재작성 | **77%** |
| `Event/TriggerBox.cs` | 커스텀 UnityEvent 슬롯 추가 / 게임 상태·인벤토리 조건 결합 / 트리거 시 NPC 방향 플레이어 회전 / 상태 리셋 기능 | **57%** |
| `Timeline/TimeLineManager.cs` | `OnSkipButtonPressed()` 엔터 연타 스킵 구현 / SoundManager 볼륨 동기화 / 타임라인 이벤트 시스템 / 우선순위 버그 수정 / 백뷰캠 후 고장 버그 수정 | **58%** |
| `UI/Panels/GameUIManager.cs` | 언어 변경 즉시 로컬라이제이션 적용 / 상태 진행 대화 연동 / 맵2 대사 연결 | **54%** |
| `UI/Panels/GuideUIManager.cs` | 물체 내려놓기 가이드 UI / 의도적으로 닫은 패널 복원 방지 버그 수정 | **52%** |
| `Dialogue/DialogueCommands.cs` | 대사 스킵 커맨드 / 보이스 전환 / 언어 suffix 처리 `StartDialogueImmediate`로 통합 | **38%** |
| `UI/Components/KeyboardUI.cs` | 인벤토리 버튼 키보드 UI 연결 / XBOX 컨트롤러 입력 매핑 | **32%** |
| `Timeline/TimelineController.cs` | 로컬라이제이션 suffix 버그 수정 / 리턴 조건 추가 | **30%** |
| `UI/Panels/QuestUIManager.cs` | 아이템 이미지 할당 / 퀘스트 UI 인풋 버그 수정 | **18%** |
| `Helpers/UIClickDebugger.cs` | 디버그 로그 정리 | **18%** |
| `UI/Utils/UIAnimationManager.cs` | UI 마커 이펙트 연동 | **12%** |
| `UI/Components/InteractableObject.cs` | 상호작용 UI 카메라 충돌 시 항상 표시되도록 수정 | **7%** |

### 팀원3 - 맵2 창고 기믹

| 스크립트 | 기여 내용 | 기여도 |
|---|---|---|
| `Gimmick/TriggerBoxCJW.cs` | 창고 트리거 로직 전반 작성 (원본 틀만 팀원3 작성) | **75%** |
| `Gimmick/DoorRotate.cs` | Skia2 연동 / 리스폰 시 문 상태 초기화 | **43%** |

---

## 🛠 기술 스택

| 분류 | 내용 |
|---|---|
| 엔진 | Unity (URP) |
| 언어 | C# |
| 주요 패키지 | Cinemachine 3.x, Yarn Spinner, DOTween Pro, Unity New Input System, Highlight Plus, FronkonGames Glitches |
| 버전 관리 | Git / GitHub (Git LFS) |
| 음성 | ElevenLabs TTS |

---

## 📁 레포지토리 구조

```
Scripts/
├── GameManager/            # GameManagerBass, GameManagerRegistry, Map1~4·IntroGameManager
│   └── Debugger/           # Map1~2 StateDebugger
├── Event/                  # EventBroker, EventActivator, TriggerConditionHandler
│                           # ITriggerCondition, GameStateCondition, InventoryCondition, PartsCheckTrigger
│                           # TriggerBox ★팀원2
├── Player/                 # ThirdPersonController, PlayerMovementController
│                           # SomaShieldController, PlayerDialogueHandler, BasicRigidBodyPush
├── NPC/                    # SkiaController, StorageSkia, JailSkia
│                           # StorageSkiaManager, WalkDurationTrigger
│                           # SkiaStateType ★팀원1
├── Magnetic/               # MagneticManager, LightAnchor, HeavyAnchor ★팀원1
│                           # MagneticAnchor, RayDetector ★팀원1
├── Gimmick/                # DeathZone, LaserRotator, RigidbodyGroupController
│                           # DoorRotate, TriggerBoxCJW ★팀원3
├── UI/
│   ├── Core/               # IPanelState, PanelStateFactory
│   ├── Panels/             # PanelManager, GuideManager
│   │   │                   # GameUIManager, GuideUIManager, QuestUIManager ★팀원2
│   │   └── States/         # HUD / Dialogue / Player / UI 모드 패널 상태
│   ├── Components/         # ButtonHover, UIBillboard, DisableButtonOnClick
│   │                       # KeyboardUI, InteractableObject, SomaAtion ★팀원2
│   └── Utils/              # BlackScreenFader, LoadingManager, VideoManager
│                           # EscapeKeyHandler, DeathEffectManager
│                           # UIAnimationManager ★팀원2
├── Timeline/               # TimeLineManager, TimelineController ★팀원2
├── Dialogue/               # PlayerDialogueHandler
│                           # DialogueCommands ★팀원2
├── Sound/                  # SoundManager, LoopSound3D, Simple3DSound
│                           # BoxCollisionSound, TargetCollisionEffect, TimelineAudioVolumeSync
├── Animation/              # FootstepHandlerBase, PlayerFootstepHandler, MonsterFootstepHandler
│                           # PullStateBehaviour, PushStateBehaviour, EyeBlinkAnimator
├── Camera/                 # MainCam, CameraAspectRatioFitter, SomaCollisionHider
│                           # BackViewCameraController ★팀원1
├── InputMode/              # InputModeManager, EditorDialogueSkip, DevBuildMode, InputModeTester
├── Inventory/              # InventoryUI, ItemData, ItemSlot, ItemPickup, ItemConsumer
├── SaveSystem/             # SaveDataManager, SavePoint, SettingsManager
├── Helpers/                # DebugLogger, EditorChildSceneLoader, RuntimeChildSceneLoader
│                           # ObjectPool, PerformanceMonitor, InteractionObjectNotifier
│                           # StateConditionalTrigger, SteamManager, GamepadTester
│                           # UIClickDebugger ★팀원2
└── Editor/                 # ThumbnailGenerator, AudioListenerFinder, ArdorbitHelper (빌드 미포함)
                            # ChildSceneLoaderInspectorGUI, DialogueConfigAutoSetup, ItemDatabase
```

> ★ 표시는 팀원 원본 코드에 기여한 스크립트  
> ⚠️ 이 레포지토리는 **스크립트만** 포함합니다. 모델, 텍스처, 오디오, 씬 등의 에셋은 포함되지 않습니다.

---

## 👤 작성자

- **강희진** · jiniandjuni@gmail.com

**팀**: ARDORBIT  

---

## ⚖️ 라이선스 및 저작권

```
Copyright (c) 2026 강희진. All Rights Reserved.
```

저작권자의 명시적 서면 허가 없이 이 코드의 전부 또는 일부를 사용, 복사, 수정, 배포하는 행위를 금지합니다.  
본 프로젝트는 학술적 검토 목적으로만 공개되었습니다.
