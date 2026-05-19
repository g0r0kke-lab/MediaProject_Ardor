# MediaProject_Ardor

> **Poreia** 개발에 사용된 Unity C# 스크립트 모음입니다.  
> 이 레포지토리는 Ardor 팀이 작성한 스크립트 소스만을 포함합니다.

---

## 🎮 프로젝트 개요

**Poreia**는 소포클레스의 *오이디푸스 왕*에서 영감을 받은 SF 디스토피아 배경의 3인칭 3D 퍼즐 어드벤처 게임입니다.

기억을 잃은 고양이 로봇 **소마(Soma)**는 잃어버린 가족을 찾아 떠나는 여정 속에서, AI 존재 **이데아(Idea)**와 로봇이 인간을 괴물로 인식하게 만드는 바이러스 **스키아(Skia)**의 진실을 밝혀나갑니다.

| 항목 | 내용 |
|---|---|
| 장르 | SF 디스토피아 3D 퍼즐 어드벤처 |
| 엔진 | Unity (URP) |
| 플랫폼 | PC (Steam / Stove Indie) |
| 지원 언어 | 한국어 / 영어 (로컬라이징) |
| 팀 | ARDORBIT |

---

## 🧩 핵심 시스템

### 아키텍처
- **멀티씬 아키텍처** — 8인 병렬 개발을 위한 Additive 씬 로딩 (`EditorChildSceneLoader` / `RuntimeChildSceneLoader`)
- **이벤트 드리븐 아키텍처** — 커스텀 `EventBroker`를 통한 문자열 기반 Pub/Sub 시스템
- **FSM 기반 게임 상태 관리** — 제네릭 상태머신 베이스 클래스 (`GameManagerBass<TState>`) 및 `GameManagerRegistry`

### 게임플레이
- **자기력 메커닉** — 인력/척력 시스템 (`MagneticManager`, `LightAnchor`, `HeavyAnchor`, `MagneticAnchor`)
- **적 AI** — FSM + CharacterController 기반 `SkiaController` 및 자식 클래스 (`StorageSkia`, `DeactivateChaseSkia`)
- **대화 시스템** — Yarn Spinner 기반, 한국어/영어 로컬라이징 적용
- **트리거/인터랙션 시스템** — `TriggerBox`, `TriggerConditionHandler`, `WalkDurationTrigger`, `EventActivator`

### 인프라
- **세이브/로드** — `SaveDataManager`
- **입력 관리** — `InputModeManager` (Player / UI / RepelTarget / RepelSource 모드), Unity New Input System 사용
- **카메라** — Cinemachine 3.x, 컷씬용 `TimeLineManager` / `TimelineController`
- **UI** — `GameUIManager`, 패널 스택 시스템
- **사운드** — `SoundManager` (클립별 `AudioClipData`, BGM 크로스페이드, 3D 공간 음향)
- **디버그** — 조건부 컴파일 기반 `DebugLogger` 래퍼 (`ENABLE_DEBUG_LOG`)

---

## 🛠 기술 스택

| 분류 | 내용 |
|---|---|
| 엔진 | Unity (URP) |
| 언어 | C# |
| 주요 패키지 | Cinemachine 3.x, Yarn Spinner, DOTween Pro, Unity New Input System, Highlight Plus, FronkonGames Glitches |
| 버전 관리 | Git / GitHub (Git LFS) |
| 음성 | ElevenLabs TTS, 커스텀 SoundManager |

---

## 📁 레포지토리 구조

```
MediaProject_Ardor/
├── Architecture/          # 멀티씬 로더, EventBroker, GameManager
├── Gameplay/
│   ├── Magnetic/          # MagneticManager, 앵커 클래스
│   ├── Enemy/             # SkiaController, StorageSkia, DeactivateChaseSkia
│   └── Interaction/       # TriggerBox, TriggerConditionHandler, EventActivator
├── UI/                    # GameUIManager, 패널 스택
├── Sound/                 # SoundManager, AudioClipData
├── Dialogue/              # Yarn Spinner 래퍼, 로컬라이징
├── Save/                  # SaveDataManager
├── Input/                 # InputModeManager
├── Camera/                # TimeLineManager, TimelineController
└── Utility/               # DebugLogger, PerformanceMonitor
```

> ⚠️ 이 레포지토리는 **스크립트만** 포함합니다. 모델, 텍스처, 오디오, 씬 등의 에셋은 포함되지 않습니다.

---

## 👥 작성자

- 강희진 gyorm@ajou.ac.kr
- 최지원 jwchoi2004@ajou.ac.kr

**팀**: Ardor  
**소속**: 아주대학교 디지털미디어학과

---

## ⚖️ 라이선스 및 저작권

```
Copyright (c) 2026 Ardor (강희진, 최지원). All Rights Reserved.
```

이 레포지토리 및 포함된 모든 콘텐츠는 저작자의 지식재산입니다.  
**저작권자의 명시적 서면 허가 없이 이 코드의 전부 또는 일부를 사용, 복사, 수정, 배포, 재배포하는 행위를 금지합니다.**

본 프로젝트는 학술적 검토 목적으로만 공개되었습니다.  
상업적·비상업적 목적의 무단 사용은 엄격히 금지됩니다.

---

## 📌 안내

본 프로젝트는 아주대학교 2026년 미디어프로젝트(졸업 프로젝트)로 개발되었습니다.  
전체 게임 빌드, 에셋, 기획 문서는 이 레포지토리에 포함되지 않습니다.  
문의 사항은 작성자에게 직접 연락해 주시기 바랍니다.