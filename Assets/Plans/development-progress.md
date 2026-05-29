# FTRO - 개발 진행 기록

> 마지막 업데이트: 2026-05-28

---

## 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 장르 | 숨바꼭질 멀티플레이어 (Hide & Seek) |
| 네트워크 | Photon PUN2 |
| 플랫폼 | PC (Windows) / WebGL |
| 렌더 파이프라인 | Built-in |

**기본 구조**
- **술래(Seeker)**: 일반 플레이어와 AI 더미 사이에서 진짜 플레이어를 찾아 잡음
- **생존자(Survivor)**: AI 더미처럼 행동하며 숨음
- **AI 더미**: 맵을 배회하며 술래의 탐색을 방해

---

## 씬 구성

| 씬 이름 | 설명 | 상태 |
|---|---|---|
| `TitleScene` | 메인 메뉴, 로비, 방 생성 | ✅ 완료 |
| `CityMapScene` | 기존 도시 맵 (원본) | ✅ 완료 |
| `CityScene` | 새로 추가된 도시 씬 | ✅ 추가 완료 |
| `WesternScene` | 새로 추가된 서부 씬 | ✅ 추가 완료 |

**타이틀 씬 맵 선택 UI 순서**
```
1번 토글 → CityScene
2번 토글 → WesternScene
3번 토글 → CityMapScene
4번 토글 → 랜덤 맵
```

---

## 완료된 작업 목록

### 1. 씬 추가 및 맵 선택 연동
- `TitleManager.cs`: `selectedMap` 기본값 `"CityScene"`, `mapList` = `{CityScene, WesternScene, CityMapScene}` 로 변경
- `WaitingRoomController.cs`: 맵 선택 메서드 이름 및 초기값 업데이트
- `TitleScene.unity`: Toggle GameObject 이름·라벨·기본 선택값 수정
- 새 씬에 `GameManager`, `NetworkManager`, NavMesh 설정 필요 (수동 작업)

---

### 2. Unity 시작 시 프리즈 문제 해결
**원인**: 새 씬에 `GameManager`, `NetworkManager`, NavMesh가 없어 AI 스포너가 무한 루프  
**해결**: `CityMapScene`에서 필수 오브젝트 복사, 각 씬에서 NavMesh 베이크

---

### 3. NavMesh 관련 오류 수정
**오류**: `"GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh`  
**파일**: `RandomRoam.cs`  
**해결**: `Update()` 상단에 NavMesh 유효성 체크 추가
```csharp
if (agent == null) return;
if (!agent.isOnNavMesh) return;
```

**NavMesh 장애물 팁**: 차량 등 동적 장애물은 `NavMeshObstacle` 컴포넌트 + `Carve` 옵션 활성화

---

### 4. 연결 끊김 문제 해결 (AppOutOfFocus / TimeoutDisconnect)

| 원인 | 해결 방법 | 적용 파일 |
|---|---|---|
| 에디터 포커스 잃을 때 연결 끊김 | `Application.runInBackground = true` | `TitleManager.cs`, `NetworkManager.cs` |
| UDP 방화벽/NAT 문제 | 프로토콜 UDP→TCP 변경 (`Protocol: 1`) | `PhotonServerSettings.asset` |
| 네트워크 불안정 | `SendRate=30`, `SerializationRate=15` | `TitleManager.cs`, `NetworkManager.cs` |

---

### 5. 플레이어/AI 스폰 및 동기화 수정
**파일**: `NetworkManager.cs`

- `OnConnectedToMaster` / `OnJoinedRoom`에서 강제로 "TestRoom" 조인하는 로직 제거 (다른 방에 있을 때 튕기는 버그)
- `WaitAndSpawn()` 코루틴 추가: 방에 없으면 5초 대기 후 타이틀로 복귀
- `FindSafeSpawnPosition()` 구현: `sharedSpawnPoints` 없을 때 NavMesh에서 랜덤 위치 탐색
- `OnGUI()` 디버그 오버레이: 방 이름, 플레이어 수, 리전, 스폰 상태 실시간 표시
- `using System.Collections;` 누락 추가 (컴파일 에러 수정)

---

### 6. 애니메이션 동기화 수정
**원인**: `PhotonAnimatorView`의 모든 파라미터가 `SynchronizeType: 0` (Disabled)  
**해결**: `SynchronizeType: 1` (Discrete)로 변경  
**대상 파일**:
- `Assets/Resources/playerPrefab.prefab`
- `Assets/Resources/AI_Dummy.prefab`

---

### 7. 스킨 랜덤화 및 네트워크 동기화 (`RandomSkin.cs`)

#### 최종 구현 방식: `PhotonNetwork.LocalPlayer.CustomProperties` (ViewID 기반 고유 키)

| 방식 | 문제 | 결과 |
|---|---|---|
| `AllBuffered` RPC | 씬 전환 타이밍에 따라 미수신 | ❌ 불안정 |
| `SetCustomProperties("SkinIdx")` | AI 더미 50개가 마스터의 같은 키 덮어씀 → 전부 동일 스킨 | ❌ 버그 |
| `SetCustomProperties("S_" + ViewID)` | 오브젝트마다 고유 키 → 충돌 없음 | ✅ 채택 |

**현재 동작 흐름**
1. `photonView.IsMine == true` → 랜덤 스킨 선택 → `SetCustomProperties({"S_ViewID": index})` 저장
2. 상대방 기기에서 오브젝트 생성 시 → `Start()`에서 `TryApplySkinFromOwner()` 즉시 읽어 적용
3. 프로퍼티가 늦게 도착하면 → `OnPlayerPropertiesUpdate()` 콜백에서 자동 적용
4. `SyncCharacterSkin(int index)`: HideAllModels → BuildBoneMap → 해당 모델 활성화 → 본 리매핑

**씬별 스킨 구성**: `MapSkinSet[]` 배열로 씬 이름별 모델 목록 지정, 없으면 `defaultModels` 사용

**RPC 목록** (`PhotonServerSettings.asset`의 `RpcList`에 등록):
- `SyncCharacterSkin`
- `RPC_ShowSkeletonSource`

---

## 수동으로 해야 할 Unity 에디터 작업

| 항목 | 설명 | 씬 |
|---|---|---|
| NavMesh 베이크 | Window → AI → Navigation → Bake | CityScene, WesternScene |
| 스폰 포인트 연결 | `Point1~5` GameObject를 `NetworkManager`의 `Shared Spawn Points` 배열에 드래그 | CityScene, WesternScene |
| 스폰 포인트 위치 | Y좌표가 땅 위에 있는지 확인 (카메라가 땅 아래로 내려가는 버그 원인) | 전체 씬 |
| `RandomSkin` 프리팹 설정 | `mapSkinSets`에 씬 이름과 모델 배열 연결 | playerPrefab, AI_Dummy |

---

## 현재 확인된 동작 상태

| 기능 | 상태 |
|---|---|
| 씬 전환 (타이틀 → 게임) | ✅ 정상 |
| Photon 방 생성/참가 | ✅ 정상 |
| 플레이어 스폰 | ✅ 정상 |
| AI 더미 스폰 및 배회 | ✅ 정상 |
| 플레이어 스킨 랜덤화 | ✅ 정상 |
| 스킨 네트워크 동기화 | ✅ 수정 완료 |
| 애니메이션 동기화 | ✅ 수정 완료 |
| AI 스킨 동기화 | ✅ 수정 완료 (ViewID 키) |
| 연결 안정성 (TCP 전환) | ✅ 개선됨 |

---

## 기획 문서 목록

| 파일 | 내용 |
|---|---|
| `Assets/Plans/seeker-item-system.md` | 술래 전용 아이템 시스템 기획 (Freeze Mode / Swarm Mode) |
| `Assets/Plans/development-progress.md` | 이 파일 |

---

## 관련 주요 파일

```
Assets/
├── Script/
│   ├── TitleManager.cs         # 타이틀/로비/방 생성
│   ├── WaitingRoomController.cs# 대기방 맵 선택
│   ├── NetworkManager.cs       # Photon 연결, 스폰, 디버그
│   ├── RandomSkin.cs           # 스킨 랜덤화 & 동기화
│   ├── RandomRoam.cs           # AI NavMesh 배회
│   └── AISpawner.cs            # AI 더미 스폰
├── Resources/
│   ├── playerPrefab.prefab     # 플레이어 프리팹
│   └── AI_Dummy.prefab         # AI 더미 프리팹
├── Scenes/
│   ├── TitleScene.unity
│   ├── CityScene.unity
│   ├── WesternScene.unity
│   └── CityMapScene.unity
└── Photon/PhotonUnityNetworking/Resources/
    └── PhotonServerSettings.asset  # Photon 설정 (TCP, RPC 목록)
```
