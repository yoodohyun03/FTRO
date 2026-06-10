# FTRO — 프로젝트 문서 (통합)

> 마지막 업데이트: 2026-06-06

---

## 1. 프로젝트 개요


| 항목       | 내용                        |
| -------- | ------------------------- |
| 장르       | 숨바꼭질 멀티플레이어 (Hide & Seek) |
| 네트워크     | Photon PUN2               |
| Unity    | 6000.3.10f1               |
| 렌더 파이프라인 | URP 17.x                  |
| 플랫폼      | PC (Windows) / WebGL      |


**역할 구조**

- **술래(Seeker)**: AI 더미와 섞인 맵에서 생존자를 찾아 잡음
- **생존자(Survivor)**: AI처럼 행동하며 숨거나 목표(터미널/탈출) 수행
- **AI 더미**: 맵을 배회하며 술래의 판별을 어렵게 함

---

## 2. 씬 구성


| 씬              | 설명                | 상태  |
| -------------- | ----------------- | --- |
| `TitleScene`   | 로그인, 로비, 방 생성/대기  | ✅   |
| `CityScene`    | 메인 도시 맵 (UI 기준 씬) | ✅   |
| `WesternScene` | 서부 맵              | ✅   |
| `CityMapScene` | 도시 맵 변형           | ✅   |


**타이틀 맵 선택 순서 (캐러셀)**

```
0 → CityScene
1 → WesternScene
2 → CityMapScene
3 → 랜덤
```

---

## 3. 완료된 작업 (최신)

### 3.1 로비 / 대기방

- 대기방 **술래 지정**: 방장이 플레이어 목록에서 술래 선택 (`SelectedSeeker` 룸 프로퍼티)
- `MatchStartController`: 선택된 술래 우선, 없으면 랜덤
- `TitleManager.OnRoomPropertiesUpdate`: 술래 선택 변경 시 UI 갱신
- **Photon 로비 복구**: 게임 종료 후 타이틀 복귀 시 GameServer 잔류 상태에서 `CreateRoom` 실패하던 문제 수정 (`EnsureLobbyReady`, `RunWhenLobbyReady`)

### 3.2 타이틀 씬 UI

- 대기방 채팅 패널 레이아웃/폰트 크기 조정 (`ChatManager.ApplyLobbyChatLayout`, `TitleScene` 스케일 수정)
- 채팅 입력·로그 영역 분리, 클리핑 해소

### 3.3 인게임 UI (CityScene 기준 통일)

- `WesternScene`, `CityMapScene`에 CityScene UI 동기화 완료
  - Canvas (타이머, 게임오버, 채팅, ExitButton 등)
  - ChatManager, EventSystem, MinimapSystem
  - GameManager UI 참조 자동 연결
- **동기화 도구**
  - Unity 메뉴: `FTRO → Sync Game UI From CityScene` (`Assets/Editor/SyncGameUIFromCityScene.cs`) — **권장**
  - CLI: `node tools/sync_game_ui.mjs` (검증 포함, YAML 헤더·LightingSettings·ExitButton 자동 패치)
  - 씬 수리: `node tools/repair_scenes.mjs` (`m_LightingSettings: {fileID: 0}`)

### 3.4 씬 손상 이슈 해결 (중요)

UI YAML 동기화 과정에서 반복되던 오류와 해결:


| 증상                               | 원인                                      | 해결                                  |
| -------------------------------- | --------------------------------------- | ----------------------------------- |
| `File may be corrupted`          | `%YAML 1.1` 헤더 삭제                       | `sync_game_ui.mjs` preamble 보존 + 검증 |
| 위 오류 (Western/CityMap)           | 64비트 `m_LightingSettings` 외부 참조         | `{fileID: 0}` 로 통일                  |
| `Broken text PPtr ... 566308976` | ExitButton이 CityScene GameManager ID 참조 | 각 씬 GameManager ID로 재연결             |
| 중복 오브젝트 ID                       | JS `Number`로 64비트 ID 파싱                 | 문자열 ID 처리로 변경                       |


> **주의**: UI 동기화는 Unity 에디터 메뉴 사용을 권장. CLI 스크립트 실행 후 Unity에서 **Reload** 필수.

### 3.5 오브젝트 스폰 포인트 (Western / CityMap)

- `ObjectiveSpawnPoints` 루트 추가: 터미널 10 (`TerminalSpawn_0~9`) + 탈출구 3 (`EscapeSpawn_0~2`)
- `GameManager`에 스폰 포인트·프리팹 참조 연결 (CityScene과 동일)
- 도구: `node tools/add_objective_spawn_points.mjs` 또는 `FTRO → Add Objective Spawn Points`
- 스폰 위치는 에디터에서 맵에 맞게 수동 조정 필요

### 3.6 Western 맵 캐릭터 스킨

- `playerPrefab` / `AI_Dummy`의 `RandomSkin.mapSkinSets`에 `WesternScene` + Synty Western 프리팹 8종 연결
  - 경로: `Assets/Synty/PolygonWestern/Prefabs/Characters/SM_Chr_`*
- City 캐릭터는 프리팹 **자식**으로 들어 있고, Western은 **프로젝트 프리팹 참조**만 연결 (Hierarchy에 City만 보이는 것은 정상)
- `skeletonSource`는 애니메이션용 City 뼈대 유지 → Western 메시는 `RemapBones`로 City 뼈에 붙임
- `RandomSkin.cs`: 외부 프리팹 참조 시 런타임 `Instantiate` + `runtimeModelCache` (`ResolveModelInstance`)
- **투명 캐릭터 이슈**: Unity가 Western 프리팹을 재저장하면서 `SkinnedMeshRenderer.m_Bones`가 `{fileID: 0}`으로 깨짐 → git 원본 복구 필요
  - 복구: `git checkout HEAD -- Assets/Synty/PolygonWestern/Prefabs/Characters/`

### 3.7 게임 타이머 & 터미널 상호작용

- **10분 타이머 즉시 시작** (`GameManager.cs`)
  - 기존: Setup 5초 + 추가 2초 대기 후 타이머 시작, 첫 표시가 `09:59`
  - 변경: 씬 진입 후 바로 `Playing` 상태 + `**10:00` 표시** 후 1초 단위 카운트다운
  - `FormatTime()` 헬퍼로 표시 통일, 마스터 교체 시 `TakeoverTimerCoroutine`도 동일 포맷 사용
- **생존자 터미널 해킹(E) 상호작용 수정** (`PlayerMove.cs`, `HackingTerminal.prefab`)
  - 채팅 입력창 포커스 시 이동만 차단, **E 해킹은 계속 가능** (기존엔 `Update` 전체 return)
  - 터미널 탐색: 콜라이더 OverlapSphere + **거리 기반 `ObjectivePoint` 탐색** (6m) 병행
  - 역할 프로퍼티 늦게 도착 시 `EnsureRoleAssigned()`로 `SurvivorItemHandler` 등 보완
  - `Playing` 상태에서만 상호작용 허용
  - `HackingTerminal` 콜라이더: 3×3×3 트리거, 중심 `(0, 1.5, 0)`으로 확대
- **터미널 스폰 포인트 자동 탐색 보강** (`GameManager.cs`)
  - `ObjectiveSpawnPoints` 루트 인식, `TerminalSpawn_`* / `EscapeSpawn_*` 이름 필터

### 3.8 게임플레이 UI/연출

- 미니맵 크기 280 → 310 (`MinimapFollow.cs`)
- **게임 시작 역할 안내 배너** (`PlayerMove.cs`, `playerPrefab.prefab`)
  - `whBtn._0` 스프라이트(`Assets/Sprites/whBtn..png`) 배경 + 중앙 상단 문구
  - 술래: "당신은 술래입니다" / 생존자: "당신은 생존자입니다"
  - `ScreenSpaceOverlay` Canvas에만 붙임 (`FindOverlayCanvas`) — 플레이어 월드스페이스 이름표 Canvas와 분리
  - 위치 `anchoredPosition (0, 140)`, 크기 `680×180`, 글자 `48pt`, **3초** 후 자동 제거
  - 기존 술래 **BlindPanel 5초** 전체 가림 제거
  - 터미널 상호작용 UI(`Press [E] to Hack`)도 동일 Overlay Canvas 사용으로 수정
- `CornerRoleText`는 계속 기본 비활성 (`[Core_Systems].prefab`)
- `NetworkManager`: 방 미입장 5초 후 타이틀 복귀 시 `LeaveRoom`/`Disconnect` 처리

### 3.9 Photon Voice (PR #16, #17)

- **PR #16** (`voice-test`, `cb42b01`): Photon Voice 2 패키지·에셋 통합, `VoiceManager.cs`, `USE_PHOTON_VOICE` 심볼
- **PR #17** (`voice-last-test`, `d169753`): 보이스 수정
  - `playerPrefab`에 `PhotonVoiceView` + `Recorder` + `PhotonVoiceSpeaker` 연결
  - `TitleScene`에 Voice UI/설정 보강
  - `VoiceManager.cs` Recorder 바인딩·재시도 로직 조정
- `VoiceManager`: `TitleScene` 전용 오브젝트 + `PunVoiceClient`, 씬 전환 시 DDOL
  - Push-to-Talk 기본 `V` 키 (`pushToTalk` 옵션, **현재 TitleScene 설정은 꺼짐** → 오픈 마이크)
  - 인게임 씬 로드 후 Recorder 자동 바인딩 (최대 10초 재시도)
- `PhotonServerSettings`: `AppIdVoice` 설정 필요 (Photon 대시보드)
- 에디터: `PhotonVoiceDefineSync.cs`가 Voice 패키지 유무에 따라 심볼 자동 동기화

### 3.14 인게임 보이스 상태 UI

- **`VoiceStatusUI.cs`**: 마이크 연결·송신 상태를 인게임에서 확인하는 간단한 HUD
  - **좌하단** 고정 (미니맵 좌상단과 겹치지 않음)
  - 기존 **Overlay Canvas**에만 패널을 붙임 — 별도 Canvas를 만들지 않아 다른 UI 레이아웃에 영향 없음
  - `raycastTarget = false` — 클릭·터치 방해 없음
  - 씬 전환 시 패널은 게임 씬 Canvas와 함께 제거, TitleScene에서는 표시 안 함
- 표시 내용
  - 상태 점 + 문구: `연결 대기...` / `켜짐` / `송신 중` / `대기 (V키)` 등
  - 하단 **마이크 레벨 게이지**: `Recorder.LevelMeter.CurrentPeakAmp` 기반 (말할 때 채워짐)
  - `송신 중` 문구는 Photon이 실제 음성 패킷 송신 중일 때 표시
- `VoiceManager.cs`: 상태 조회 API (`Instance`, `HasRecorder`, `IsTransmitting`, `MicPeakLevel` 등) + `VoiceStatusUI` 자동 부착

### 3.10 맵 스폰·위치 조정 (PR #15)

- **PR #15** (`bsm`, `068705a` / `08e3323`): 탈출구·터미널·플레이어 스폰 포인트 위치 조정
  - `WesternScene.unity`, `CityMapScene.unity` 스폰 Transform 이동
  - `CityMapScene` NavMesh 에셋 갱신

### 3.11 타이틀·오디오 에셋

- 타이틀 배경 이미지 경량화 (`Background_Title.png`, `9b62c56`)
- BGM 에셋 추가: `Assets/Audio/Casual & Relaxing Game Music/Happy.wav` (`b6a182a`)
  - 아직 씬/스크립트에 자동 재생 연결은 없음 — 에디터에서 AudioSource 연결 필요

### 3.13 스킬창 UI 통일 (술래·생존자)

- **`RoleSkillPanelUI.cs`**: 화면 하단 Overlay 스킬 슬롯 공통 UI (이름·설명·키·상태)
- **술래** (`SeekerItemHandler.cs`): Q/R 슬롯 항상 표시
  - AI 프리즈: "모든 AI 정지, 움직이는 대상 추적"
  - AI 스웜: "AI가 생존자 방향으로 집단 이동"
  - `ItemData.description` + `FreezeItem`/`SwarmItem` 에셋 한글화
- **생존자** (`SurvivorItemHandler.cs`): F 슬롯 1개
  - 보유 없음: "아이템 없음 / 터미널 해킹 시 획득"
  - 보유 시: 아이템명(일반·희귀) + 효과 설명 + `[F] 사용 가능`
- **연막탄 시야 차단** 강화: 술래 전체 화면 오버레이 알파 `0.97`, 연막 구체 불투명도 상향

### 3.12 이전에 완료된 핵심 기능 (요약)

- Photon TCP 전환, `runInBackground`, SendRate/SerializationRate 조정
- `NetworkManager` 스폰/NavMesh 폴백, `RandomRoam` NavMesh 가드
- `RandomSkin` ViewID 기반 스킨 동기화
- `PhotonAnimatorView` Discrete 동기화
- NavMesh 베이크 및 스폰 포인트 연결 (각 맵)

---

## 4. 아이템 시스템 기획

### 4.1 술래 아이템 (Seeker Tactical Device)


| 모드         | 효과                             | 지속  |
| ---------- | ------------------------------ | --- |
| **Freeze** | 모든 AI 더미 정지 → 움직이는 대상 = 생존자 후보 | 5초  |
| **Swarm**  | AI 더미가 생존자 쪽으로 집단 이동           | 5초  |


**구현 상태** (✅ 기본 동작 + 스킬창 설명 UI)

- `SeekerItemHandler.cs`: Q/R 사용, 90초 쿨다운, `RandomRoam.RPC_SetAIState`
- `RoleSkillPanelUI.cs`: 하단 슬롯에 스킬명·설명·키·쿨타임 표시
- `Items/FreezeItem.asset`, `Items/SwarmItem.asset`: 한글 이름·설명

### 4.2 생존자 아이템 (터미널 해제 보상)

**습득**: 맵 터미널 해제 시 등급 가중치 랜덤 지급


| 등급     | 아이템      | 효과                | 지속    |
| ------ | -------- | ----------------- | ----- |
| Common | 스프린트 부스터 | 이동속도 2배           | 5초    |
| Common | 연막탄      | 반경 5m 시야 차단       | 3초    |
| Common | 마커 교란기   | 술래 HUD 가짜 위치 2~3개 | 5초    |
| Rare   | EMP      | 술래 감속 + 화면 노이즈    | 3초    |
| Rare   | 해킹 툴     | 다음 터미널 해제 50% 단축  | 1회    |
| Rare   | 디코이      | 잔상 생성 + 반대 방향 이동  | 잔상 5초 |


**구현 상태** (✅ 터미널 보상 지급 + F 사용 + 스킬창 설명)

- `ObjectivePoint.cs` → `SurvivorItemHandler.ReceiveItem` (6종)
- 스킬창: 보유 아이템명·등급·효과 설명 표시 (`RoleSkillPanelUI`)
- 연막탄: 술래 15m 이내 전체 화면 시야 차단 (불투명도 조정 완료)
- EMP·연막·디코이 등 RPC 효과 구현, 밸런스·연출 추가 조정 가능

---

## 5. 현재 동작 상태


| 기능                                | 상태                          |
| --------------------------------- | --------------------------- |
| 타이틀 로비 / 방 생성·입장                  | ✅                           |
| Photon 재접속·로비 복구                  | ✅                           |
| City / Western / CityMap 씬 로드     | ✅                           |
| City 기준 인게임 UI (3맵)               | ✅                           |
| 술래 선택 대기방                         | ✅                           |
| 플레이어·AI 스폰 / 스킨 동기화               | ✅                           |
| Western 맵 캐릭터 스킨 (RandomSkin)     | ✅                           |
| Western/CityMap 터미널·탈출 스폰 포인트     | ✅ (위치 수동 조정)                |
| 인게임 10분 타이머 즉시 시작                 | ✅                           |
| 생존자 터미널 해킹(E) 상호작용                | ✅                           |
| 게임 시작 역할 안내 배너 (whBtn)            | ✅                           |
| Photon Voice (마이크/스피커)            | ✅ (AppIdVoice·마이크 권한 확인 필요) |
| 인게임 보이스 상태 UI (좌하단 게이지)        | ✅                           |
| Western/CityMap 스폰·터미널 위치 (PR#15) | ✅ (추가 미세 조정 가능)             |
| 술래·생존자 스킬창 (설명 UI)                | ✅                           |
| 술래·생존자 아이템 효과                     | ✅ (밸런스·연출 미세 조정 가능)         |
| BGM (`Happy.wav`) 자동 재생           | ⏳ 에셋만 추가, 씬 연결 미완           |


---

## 6. Unity 에디터 수동 작업 (필요 시)


| 항목          | 설명                                                          |
| ----------- | ----------------------------------------------------------- |
| NavMesh 베이크 | 맵 수정 후 Window → AI → Navigation → Bake                      |
| 스폰 포인트      | `NetworkManager.sharedSpawnPoints` 연결·Y좌표 확인                |
| 터미널/탈출 스폰   | `ObjectiveSpawnPoints` 하위 Transform 위치를 맵에 맞게 이동            |
| Western 스킨  | `RandomSkin` WesternScene 목록 + Synty `SM_Chr_`* 프리팹 (파란 큐브) |
| 터미널 해킹 테스트  | 생존자로 터미널 6m 이내 접근 → `Press [E] to Hack` → 프로그레스 바 확인        |
| 역할 배너 테스트   | 게임 시작 시 화면 중앙 상단 배너 3초 표시 확인 (월드스페이스 말풍선 X)                 |
| 스킬창 테스트     | 술래 Q/R 설명·쿨타임, 생존자 F 슬롯·연막탄 술래 시야 차단 확인                     |
| Voice 테스트   | TitleScene→방 입장→인게임: 좌하단 보이스 HUD, 말할 때 게이지·`송신 중` 확인, 상대 음성 수신 |
| BGM 연결      | `Happy.wav`를 TitleScene 또는 인게임 AudioSource에 드래그·Loop 설정     |
| UI 재동기화     | `FTRO → Sync Game UI From CityScene`                        |
| 씬 외부 편집 후   | Unity **Reload** (`.unity` 디스크 변경 반영)                       |


---

## 7. 주요 파일

```
Assets/
├── Editor/
│   ├── SyncGameUIFromCityScene.cs   # UI 동기화 (권장)
│   └── AddObjectiveSpawnPoints.cs   # 터미널/탈출 스폰 포인트 추가
├── Audio/
│   └── Casual & Relaxing Game Music/Happy.wav   # BGM (씬 연결 미완)
├── Resources/
│   ├── playerPrefab.prefab          # RandomSkin + PhotonVoiceView/Recorder/Speaker + 역할 배너
│   ├── AI_Dummy.prefab
│   ├── HackingTerminal.prefab       # ObjectivePoint + 넓은 트리거 콜라이더
│   └── Items/FreezeItem.asset, SwarmItem.asset
├── Script/
│   ├── TitleManager.cs              # 로비, Photon 콜백, 로비 복구
│   ├── WaitingRoomController.cs     # 대기방, 술래 선택, 방 생성
│   ├── VoiceManager.cs              # Photon Voice 2, PTT, Recorder 바인딩
│   ├── VoiceStatusUI.cs             # 인게임 보이스 상태 HUD (좌하단, Overlay Canvas)
│   ├── RandomSkin.cs                # 맵별 캐릭터 스킨, 외부 프리팹 Instantiate
│   ├── ObjectivePoint.cs            # 터미널 해킹 진행·완료·아이템 지급
│   ├── ChatManager.cs               # 로비/인게임 채팅 레이아웃
│   ├── GameManager.cs               # 게임 흐름, 10분 타이머, 오브젝트 스폰
│   ├── NetworkManager.cs            # 스폰, Photon
│   ├── PlayerMove.cs                # 이동, 터미널 E, 역할 안내 배너(whBtn)
│   └── Items/
│       ├── ItemData.cs              # itemName, description
│       ├── RoleSkillPanelUI.cs      # 술래·생존자 공통 스킬창 UI
│       ├── SeekerItemHandler.cs
│       └── SurvivorItemHandler.cs
├── Sprites/
│   └── whBtn..png                   # 역할 안내 배너 스프라이트 (whBtn._0)
├── Scripts/
│   └── MinimapFollow.cs
├── Scenes/
│   ├── TitleScene.unity
│   ├── CityScene.unity              # UI 기준
│   ├── WesternScene.unity
│   └── CityMapScene.unity
└── Plans/
    └── development-progress.md      # 이 문서

tools/
├── sync_game_ui.mjs                 # CLI UI 동기화
├── repair_scenes.mjs                # LightingSettings 수리
└── add_objective_spawn_points.mjs   # 터미널/탈출 스폰 포인트 CLI
```

---

## 8. 알려진 이슈 / 메모

- `UISettingsManager.sliderValue` (SlimUI): 미사용 경고 — 무시 가능
- 씬 파일은 Unity 열린 상태에서 CLI 동기화 시 잠금 오류 가능 → Unity 닫거나 Reload
- git `c7b98b3`~`9cacc05` 커밋에 WesternScene 바이너리 손상 이력 있음 — 현재 YAML 버전 사용 중
- Synty Western 캐릭터 프리팹을 Unity에서 저장/업그레이드하면 bone 참조가 null로 깨져 투명해질 수 있음 → `git checkout`으로 `Prefabs/Characters/` 복구
- `ObjectiveSpawnPoints` 기본 좌표는 임시 그리드 — PR #15로 Western/CityMap 위치 조정됨, 추가 미세 조정은 에디터에서
- 역할 배너가 캐릭터 위 말풍선처럼 보이면 → Overlay Canvas 미탐지. `FindOverlayCanvas()` 확인
- Photon Voice: `AppIdVoice` 미설정·마이크 권한 거부 시 음성 불가 — Photon 대시보드·Windows 마이크 설정 확인
- 보이스 HUD 게이지는 **로컬 마이크 입력** 기준 — 바가 안 움직이면 Windows 마이크 장치·권한 확인
- Synty Package Helper 팝업(Shader Graph): `manifest.json`에 이미 포함됨 — **Install** 한 번 또는 `ShaderGraph.asset`의 `hasPromptedUser`를 1로 설정
- GitHub PR #16/#17은 로컬 fast-forward 머지 후 푸시됨 — 웹에서 open으로 남아 있으면 Close 처리

---

## 9. 데스크탑 이어서 작업 (핸드오프)

### 9.1 저장소 상태

```text
브랜치: main (= origin/main)
최근:   인게임 보이스 상태 UI (좌하단 HUD)
        bdc7f60  스킬창 UI 통일 + 연막탄 시야차단 강화
        1ec2b7d  development-progress (PR handoff)
        b6a182a  role reveal banner + BGM
```

데스크탑에서 시작:

```bash
git pull origin main
```

### 9.2 최근 머지 요약


| PR  | 브랜치               | 내용                                        |
| --- | ----------------- | ----------------------------------------- |
| #15 | `bsm`             | Western/CityMap 스폰·터미널·탈출구 위치             |
| #16 | `voice-test`      | Photon Voice 2 통합                         |
| #17 | `voice-last-test` | playerPrefab Voice 컴포넌트·TitleScene 보이스 UI |


### 9.3 우선 확인할 것 (Unity 플레이 테스트)

1. **역할 배너** — 게임 시작 3초, 화면 중앙 상단, 술래/생존자 문구
2. **10분 타이머** — 즉시 `10:00` 시작
3. **터미널 E** — 생존자 6m 이내 해킹
4. **Voice** — 좌하단 HUD, 말할 때 게이지·`송신 중`, 2인 이상 상대 음성 수신
5. **Western 스킨** — 캐릭터 투명 여부
6. **스킬창** — 술래 Q/R·생존자 F 설명, 연막탄 술래 시야 차단

### 9.4 다음 작업 후보

- `Happy.wav` BGM을 TitleScene/인게임에 AudioSource 연결
- 아이템 밸런스·연출 미세 조정 (쿨타임, 연막 범위 등)
- 대기방/맵 선택 후 인게임 진입 전체 플로우 재검증

