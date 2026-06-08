# FTRO — 프로젝트 문서 (통합)

> 마지막 업데이트: 2026-06-09

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
- 인게임 **Seeker** 텍스트가 계속 보이던 문제: `CornerRoleText` 기본 비활성, 블라인드 시퀀스만 사용 (`PlayerMove.cs`, `[Core_Systems].prefab`)
- `NetworkManager`: 방 미입장 5초 후 타이틀 복귀 시 `LeaveRoom`/`Disconnect` 처리

### 3.9 이전에 완료된 핵심 기능 (요약)

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


**구현 방향**

- `RandomRoam.cs`: `RPC_SetAIState(state, targetViewID, duration)`
- `SeekerItemHandler.cs` / `ItemSystem`: 수집·사용·RPC
- `PlayerMove.cs`: 사용 입력 (Q 등)
- UI: 하단 슬롯 + 모드 표시

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


**설계 원칙**

- 터미널 해제 리스크에 대한 보상
- 직접 대미지 없음 — 생존·위장 중심
- `SurvivorItemHandler.cs`, `TerminalController`, `ItemData` ScriptableObject

---

## 5. 현재 동작 상태


| 기능                            | 상태                                                    |
| ----------------------------- | ----------------------------------------------------- |
| 타이틀 로비 / 방 생성·입장              | ✅                                                     |
| Photon 재접속·로비 복구              | ✅                                                     |
| City / Western / CityMap 씬 로드 | ✅                                                     |
| City 기준 인게임 UI (3맵)           | ✅                                                     |
| 술래 선택 대기방                     | ✅                                                     |
| 플레이어·AI 스폰 / 스킨 동기화           | ✅                                                     |
| Western 맵 캐릭터 스킨 (RandomSkin) | ✅                                                     |
| Western/CityMap 터미널·탈출 스폰 포인트 | ✅ (위치 수동 조정)                                          |
| 인게임 10분 타이머 즉시 시작             | ✅                                                     |
| 생존자 터미널 해킹(E) 상호작용            | ✅                                                     |
| 술래·생존자 아이템                    | 🔧 부분 구현 (`SeekerItemHandler`, `SurvivorItemHandler`) |


---

## 6. Unity 에디터 수동 작업 (필요 시)


| 항목          | 설명                                                          |
| ----------- | ----------------------------------------------------------- |
| NavMesh 베이크 | 맵 수정 후 Window → AI → Navigation → Bake                      |
| 스폰 포인트      | `NetworkManager.sharedSpawnPoints` 연결·Y좌표 확인                |
| 터미널/탈출 스폰   | `ObjectiveSpawnPoints` 하위 Transform 위치를 맵에 맞게 이동            |
| Western 스킨  | `RandomSkin` WesternScene 목록 + Synty `SM_Chr_`* 프리팹 (파란 큐브) |
| 터미널 해킹 테스트  | 생존자로 터미널 6m 이내 접근 → `Press [E] to Hack` → 프로그레스 바 확인        |
| UI 재동기화     | `FTRO → Sync Game UI From CityScene`                        |
| 씬 외부 편집 후   | Unity **Reload** (`.unity` 디스크 변경 반영)                       |


---

## 7. 주요 파일

```
Assets/
├── Editor/
│   ├── SyncGameUIFromCityScene.cs   # UI 동기화 (권장)
│   └── AddObjectiveSpawnPoints.cs   # 터미널/탈출 스폰 포인트 추가
├── Resources/
│   ├── playerPrefab.prefab          # RandomSkin (City + Western mapSkinSets)
│   ├── AI_Dummy.prefab
│   └── HackingTerminal.prefab       # ObjectivePoint + 넓은 트리거 콜라이더
├── Script/
│   ├── TitleManager.cs              # 로비, Photon 콜백, 로비 복구
│   ├── WaitingRoomController.cs     # 대기방, 술래 선택, 방 생성
│   ├── RandomSkin.cs                # 맵별 캐릭터 스킨, 외부 프리팹 Instantiate
│   ├── ObjectivePoint.cs            # 터미널 해킹 진행·완료·아이템 지급
│   ├── ChatManager.cs               # 로비/인게임 채팅 레이아웃
│   ├── GameManager.cs               # 게임 흐름, 10분 타이머, 오브젝트 스폰
│   ├── NetworkManager.cs            # 스폰, Photon
│   ├── PlayerMove.cs                # 이동, 터미널 E 상호작용, 역할 연출
│   └── Items/
│       ├── SeekerItemHandler.cs
│       └── SurvivorItemHandler.cs
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
- `ObjectiveSpawnPoints` 기본 좌표는 임시 그리드 — 맵마다 에디터에서 위치 조정 필요 (터미널이 땅 밑/맵 밖이면 상호작용 불가)
- **병렬 작업 중**: 로컬 변경(`GameManager`, `PlayerMove`, `HackingTerminal` 등)은 친구 브랜치와 합칠 때 충돌 가능 → PR 전 `git pull`/머지 권장

