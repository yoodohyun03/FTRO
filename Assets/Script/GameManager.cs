using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections;   
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviourPunCallbacks
{
private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";
    private const string SurvivorRole = "Survivor";

    public enum GameState { Ready, Setup, Playing, End }
    public GameState currentState = GameState.Ready;

    public static GameManager instance;
    public static GameModeType CurrentGameMode { get; private set; } = GameModeType.Normal;

    [Header("탈출 목표 설정")]
    public int totalObjectives = 3;
    public int completedObjectives = 0;
    public EscapePoint escapePoint;

    [Header("스폰 설정")]
    public GameObject terminalPrefab;
    public GameObject escapeZonePrefab;
    public List<Transform> terminalSpawnPoints = new List<Transform>();
    public List<Transform> escapeSpawnPoints = new List<Transform>();

    void Awake()
{
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    public GameObject gameOverPanel;


    [Header("UI 연결")]
    public TextMeshProUGUI centerText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI objectiveStatusText;
    public TextMeshProUGUI roleIndicatorText;
    public TextMeshProUGUI survivorStatusText;

    [Header("게임 설정")]
    public float playTime = 600f;
    public float bombPassPlayTime = 300f;

    public float ActivePlayTime =>
        BombPassManager.IsModeActive ? bombPassPlayTime : playTime;

    [Header("승리 조건 설정")]
    public int survivorCount = -1; // -1 = 미초기화 (게임 시작 전 검거 오판 방지)
    private int initialSurvivorCount = -1;

    private double gameStartNetworkTime = 0;
    private Coroutine masterTimerRoutine;
    private bool spawnRoutineStarted;

    void Start()
    {
        survivorCount = -1;
        initialSurvivorCount = -1;

        CurrentGameMode = GameModeTypeHelper.FromRoom(PhotonNetwork.CurrentRoom);
        Debug.Log("[GameManager] 게임 모드: " + GameModeTypeHelper.GetDisplayName(CurrentGameMode));

        if (PropDisguiseController.IsModeActive)
            Debug.Log("[GameManager] 사물 변신 모드 — 생존자 [G] 변신 / [R] 회전");
        if (BombPassManager.IsModeActive)
            Debug.Log("[GameManager] 폭탄 돌리기 모드 — 5분 내 폭탄을 넘기세요. 종료 시 보유자 패배");

        EnsureModeManager();

        completedObjectives = 0;
        UpdateObjectiveStatusUI();
        EnsureRoleIndicator();
        if (centerText != null) centerText.text = "";
        if (timerText != null) timerText.text = FormatTime(ActivePlayTime);
        if (!GameModeTypeHelper.UsesObjectives(CurrentGameMode) && objectiveStatusText != null)
            objectiveStatusText.gameObject.SetActive(false);

        StartCoroutine(SetupHudLayoutRoutine());

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(EnsureSurvivorCountWhenReady());
            StartCoroutine(SafeSpawnRoutine());
        }
    }

    void EnsureModeManager()
    {
        if (!BombPassManager.IsModeActive) return;

        if (GetComponent<BombPassManager>() == null)
            gameObject.AddComponent<BombPassManager>();

        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null)
            pv.RefreshRpcMonoBehaviourCache();
    }

    IEnumerator StartBombPassWhenReady()
    {
        float timeout = 3f;
        while (timeout > 0f &&
               RoleAssignmentHelper.ResolveBombHolderActorNumber(PhotonNetwork.CurrentRoom) < 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        float stateTimeout = 2f;
        while (stateTimeout > 0f && currentState != GameState.Playing)
        {
            stateTimeout -= Time.deltaTime;
            yield return null;
        }

        if (!BombPassManager.IsModeActive || currentState != GameState.Playing) yield break;

        BombPassManager mgr = GetComponent<BombPassManager>();
        if (mgr == null) mgr = gameObject.AddComponent<BombPassManager>();
        GetComponent<PhotonView>()?.RefreshRpcMonoBehaviourCache();
        mgr.EnsureBombTimerUi();
        mgr.StartBombMode();
    }

    IEnumerator EnsureSurvivorCountWhenReady()
    {
        float timeout = 6f;
        while (timeout > 0f && survivorCount < 0)
        {
            if (AllPlayersHaveRole())
            {
                InitializeSurvivorCount();
                yield break;
            }

            timeout -= 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (survivorCount < 0 && PhotonNetwork.IsMasterClient)
            InitializeSurvivorCount();
    }

    static bool AllPlayersHaveRole()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.ContainsKey(RoleKey))
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    IEnumerator SafeSpawnRoutine()
    {
        if (spawnRoutineStarted) yield break;
        spawnRoutineStarted = true;

        InitializeSurvivorCount();
        yield return new WaitForSeconds(0.5f);
        
        try 
        {
            if (GameModeTypeHelper.UsesObjectives(CurrentGameMode))
                SpawnObjectives();
            else
                Debug.Log($"[GameManager] {GameModeTypeHelper.GetDisplayName(CurrentGameMode)} — 터미널/탈출구 스폰 생략");
        } 
        catch (System.Exception e) 
        {
            Debug.LogError($"[GameManager] SpawnObjectives 에러: {e.Message}\n{e.StackTrace}");
        }

        // GameFlowRoutine 시작 전 재확인
        if (survivorCount < 0)
            InitializeSurvivorCount();

        StartMasterTimer(ActivePlayTime);
    }

    void StartMasterTimer(float remainingSeconds)
    {
        StopMasterTimer();
        if (!PhotonNetwork.IsMasterClient) return;
        masterTimerRoutine = StartCoroutine(MasterTimerRoutine(remainingSeconds));
    }

    void StopMasterTimer()
    {
        if (masterTimerRoutine != null)
        {
            StopCoroutine(masterTimerRoutine);
            masterTimerRoutine = null;
        }
    }

    IEnumerator MasterTimerRoutine(float remainingSeconds)
    {
        if (currentState != GameState.Playing)
        {
            SetState(GameState.Playing);
            photonView.RPC("SyncRoleIndicator", RpcTarget.All);

            gameStartNetworkTime = PhotonNetwork.Time;
            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new ExitGames.Client.Photon.Hashtable { { "GST", gameStartNetworkTime } });
            }

            photonView.RPC("SyncMessage", RpcTarget.All, "");
            if (BombPassManager.IsModeActive)
                StartCoroutine(StartBombPassWhenReady());
        }

        float currentTime = remainingSeconds;
        photonView.RPC("SyncTimer", RpcTarget.All, FormatTime(currentTime));

        bool finalPhaseTriggered = false;
        if (BombPassManager.IsModeActive && currentTime <= BombPassManager.FinalPhaseThresholdSeconds)
        {
            finalPhaseTriggered = true;
            if (BombPassManager.instance != null)
                BombPassManager.instance.TryActivateFinalPhase();
        }

        while (currentState == GameState.Playing && currentTime > 0f)
        {
            yield return new WaitForSeconds(1f);
            if (!PhotonNetwork.IsMasterClient) yield break;

            currentTime -= 1f;
            photonView.RPC("SyncTimer", RpcTarget.All, FormatTime(currentTime));

            if (BombPassManager.IsModeActive && !finalPhaseTriggered && currentTime <= BombPassManager.FinalPhaseThresholdSeconds)
            {
                finalPhaseTriggered = true;
                if (BombPassManager.instance != null)
                    BombPassManager.instance.TryActivateFinalPhase();
            }
        }

        if (!PhotonNetwork.IsMasterClient) yield break;

        if (currentState == GameState.Playing && currentTime <= 0f)
        {
            if (BombPassManager.IsModeActive && BombPassManager.instance != null)
                BombPassManager.instance.HandleGameTimeExpired();
            else
                EndWithMessage("Time Out!\nSurvivor Victory!");
        }
    }

    public void EndWithMessage(string message)
    {
        StopMasterTimer();
        SetState(GameState.End);
        photonView.RPC("SyncMessage", RpcTarget.All, message);
    }

    void SpawnObjectives()
    {
        Debug.Log("[GameManager] 오브젝트 동적 스폰 시작...");

        // 0. 스폰 포인트 자동 복구
        if (terminalSpawnPoints == null || terminalSpawnPoints.Count == 0)
        {
            Debug.LogWarning("terminalSpawnPoints 리스트가 비어있습니다. 'SpawnPoints' 오브젝트에서 자동 검색을 시도합니다.");
            GameObject spawnRoot = GameObject.Find("ObjectiveSpawnPoints")
                ?? GameObject.Find("SpawnPoints")
                ?? GameObject.Find("SpawnPoints (1)");
            if (spawnRoot != null)
            {
                terminalSpawnPoints = CollectTerminalSpawnPoints(spawnRoot.transform);
                Debug.Log($"[GameManager] {terminalSpawnPoints.Count}개의 터미널 스폰 포인트를 자동으로 찾았습니다.");
            }
        }

        if (escapeSpawnPoints == null || escapeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("escapeSpawnPoints 리스트가 비어있습니다. 'EscapePoints' 오브젝트에서 자동 검색을 시도합니다.");
            GameObject escapeRoot = GameObject.Find("ObjectiveSpawnPoints")
                ?? GameObject.Find("EscapePoints")
                ?? GameObject.Find("EscapeSpawnPoints");
            if (escapeRoot != null)
            {
                escapeSpawnPoints = CollectEscapeSpawnPoints(escapeRoot.transform);
                Debug.Log($"[GameManager] {escapeSpawnPoints.Count}개의 탈출구 스폰 포인트를 자동으로 찾았습니다.");
            }
        }

        // 1. 터미널 스폰 (포인트 없으면 건너뜀 — 탈출구 스폰은 계속 진행)
        if (terminalSpawnPoints != null && terminalSpawnPoints.Count > 0)
        {
            List<Transform> availablePoints = new List<Transform>();
            foreach (var p in terminalSpawnPoints) if (p != null) availablePoints.Add(p);

            List<Vector3> spawnedPositions = new List<Vector3>();
            float minDistanceBetweenTerminals = 15f;
            int countToSpawn = Mathf.Min(totalObjectives, availablePoints.Count);
            int spawnedCount = 0;
            int maxAttempts = 50;

            while (spawnedCount < countToSpawn && availablePoints.Count > 0 && maxAttempts > 0)
            {
                maxAttempts--;
                int randomIndex = Random.Range(0, availablePoints.Count);
                Transform spawnPoint = availablePoints[randomIndex];

                bool tooClose = false;
                foreach (var pos in spawnedPositions)
                {
                    if (Vector3.Distance(spawnPoint.position, pos) < minDistanceBetweenTerminals)
                    { tooClose = true; break; }
                }

                if (!tooClose || availablePoints.Count <= (countToSpawn - spawnedCount))
                {
                    availablePoints.RemoveAt(randomIndex);
                    Vector3 spawnPos = spawnPoint.position + Vector3.up * 0.5f;
                    PhotonNetwork.InstantiateRoomObject("HackingTerminal", spawnPos, spawnPoint.rotation);
                    spawnedPositions.Add(spawnPoint.position);
                    spawnedCount++;
                    Debug.Log($"[GameManager] 터미널 {spawnedCount} 생성됨 at {spawnPos}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] 터미널 스폰 포인트가 없어 터미널 스폰을 건너뜁니다.");
        }

        // 2. 탈출구 스폰 — 터미널 유무와 무관하게 항상 실행
        List<Transform> availableEscapePoints = new List<Transform>();
        foreach (var p in escapeSpawnPoints) if (p != null) availableEscapePoints.Add(p);

        if (availableEscapePoints.Count > 0)
        {
            int randomIndex = Random.Range(0, availableEscapePoints.Count);
            Transform spawnPoint = availableEscapePoints[randomIndex];
            Vector3 spawnPos = spawnPoint.position + Vector3.up * 0.05f;
            GameObject escapeObj = PhotonNetwork.InstantiateRoomObject("EscapeZonePad", spawnPos, spawnPoint.rotation);
            if (escapeObj != null)
            {
                escapePoint = escapeObj.GetComponent<EscapePoint>();
                Debug.Log($"[GameManager] 탈출구 생성됨 at {spawnPos}");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] 탈출구 스폰 포인트가 없습니다.");
        }
    }

    // 게임 시작 시 생존자 수 초기화
    public void InitializeSurvivorCount()
    {
        // 방 안 플레이어 중 Survivor만 카운트
        int count = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey(RoleKey) &&
                (string)player.CustomProperties[RoleKey] == "Survivor")
            {
                count++;
            }
        }
        survivorCount = count;
        initialSurvivorCount = count;
        Debug.Log("초기 생존자 수: " + survivorCount);
        photonView.RPC("SyncSurvivorStatus", RpcTarget.All, survivorCount, initialSurvivorCount);
    }

    IEnumerator GameFlowRoutine()
    {
        // Legacy entry — MasterTimerRoutine 사용
        StartMasterTimer(ActivePlayTime);
        yield break;
    }

    public void SetState(GameState newState)
    {
        photonView.RPC("SyncState", RpcTarget.All, newState);
    }


    public void NotifyTerminalCompleted()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!GameModeTypeHelper.UsesObjectives(CurrentGameMode)) return;

        completedObjectives++;
        UpdateObjectiveStatusUI();

        // RPC_AddProgress 안에서 바로 RPC를 연쇄 호출하면 멈출 수 있어 한 프레임 뒤에 처리
        StartCoroutine(NotifyTerminalCompletedDeferred());
    }

    IEnumerator NotifyTerminalCompletedDeferred()
    {
        yield return null;

        photonView.RPC("SyncObjectiveCount", RpcTarget.All, completedObjectives);

        if (completedObjectives >= totalObjectives)
        {
            if (escapePoint == null) escapePoint = Object.FindFirstObjectByType<EscapePoint>();
            if (escapePoint != null)
                escapePoint.photonView.RPC("RPC_ActivateEscape", RpcTarget.AllBuffered);
            else
                Debug.LogError("[GameManager] 탈출구를 찾을 수 없음!");

            photonView.RPC("SyncMessage", RpcTarget.All, "모든 터미널 활성화! 탈출구로 이동하세요!");
        }
        else
        {
            photonView.RPC("SyncMessage", RpcTarget.All, $"터미널 활성화 ({completedObjectives}/{totalObjectives})");
        }

        StartCoroutine(ClearCenterMessageAfterDelay(3f));
    }

    [PunRPC]
    public void SyncObjectiveCount(int count)
    {
        completedObjectives = count;
        UpdateObjectiveStatusUI();
    }

    [PunRPC]
    public void OnObjectiveCompleted()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!GameModeTypeHelper.UsesObjectives(GameManager.CurrentGameMode)) return;
        NotifyTerminalCompleted();
    }

    void UpdateObjectiveStatusUI()
    {
        if (!GameModeTypeHelper.UsesObjectives(CurrentGameMode))
        {
            if (objectiveStatusText != null)
                objectiveStatusText.gameObject.SetActive(false);
            return;
        }

        if (objectiveStatusText == null)
        {
            GameObject obj = GameObject.Find("ObjectiveStatusText");
            if (obj != null) objectiveStatusText = obj.GetComponent<TextMeshProUGUI>();
        }

        if (objectiveStatusText != null)
        {
            objectiveStatusText.text = $"Terminals: {completedObjectives} / {totalObjectives}";
        }
    }

    IEnumerator ClearCenterMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        photonView.RPC("SyncMessage", RpcTarget.All, "");
    }

    [PunRPC]
    public void RPC_GameOver(string message)
    {
        StopMasterTimer();
        StopAllCoroutines();
        SetState(GameState.End);
        ShowGameOver(message);
    }

    [PunRPC]
    public void OnSurvivorCaught()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (survivorCount < 0) return;

        survivorCount = CountAliveSurvivorsInRoom();
        Debug.Log("생존자 검거! 남은 수: " + survivorCount);
        photonView.RPC("SyncSurvivorStatus", RpcTarget.All, survivorCount, initialSurvivorCount);

        if (survivorCount <= 0 && currentState == GameState.Playing && !BombPassManager.IsModeActive)
            EndWithMessage("All Caught!\nSeeker Victory!");
    }

    public void RefreshSurvivorCount()
    {
        if (!PhotonNetwork.IsMasterClient || survivorCount < 0) return;

        survivorCount = CountAliveSurvivorsInRoom();
        photonView.RPC("SyncSurvivorStatus", RpcTarget.All, survivorCount, initialSurvivorCount);
    }

    int CountAliveSurvivorsInRoom()
    {
        int alive = 0;
        foreach (PlayerMove pm in Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if (RoleAssignmentHelper.IsAliveSurvivor(pm))
                alive++;
        }
        return alive;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || survivorCount < 0) return;

        if (!RoleAssignmentHelper.IsSurvivor(otherPlayer)) return;

        bool wasAlive = true;
        foreach (PlayerMove pm in Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if (pm.photonView != null && pm.photonView.Owner == otherPlayer)
            {
                wasAlive = !pm.isDead;
                break;
            }
        }

        if (!wasAlive) return;

        survivorCount = CountAliveSurvivorsInRoom();
        photonView.RPC("SyncSurvivorStatus", RpcTarget.All, survivorCount, initialSurvivorCount);

        if (survivorCount <= 0 && currentState == GameState.Playing && !BombPassManager.IsModeActive)
            EndWithMessage("All Caught!\nSeeker Victory!");
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        StopMasterTimer();

        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log("[GameManager] 마스터 교체됨 — 게임 진행 인계");

        if (currentState == GameState.Playing)
        {
            float remaining = ActivePlayTime;
            if (PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GST", out object startObj))
            {
                double elapsed = PhotonNetwork.Time - (double)startObj;
                remaining = Mathf.Max(0f, ActivePlayTime - (float)elapsed);
            }

            StartMasterTimer(remaining);

            if (BombPassManager.IsModeActive)
            {
                EnsureModeManager();
                BombPassManager mgr = GetComponent<BombPassManager>();
                if (mgr != null && !mgr.IsInitialized)
                    StartCoroutine(StartBombPassWhenReady());
            }
        }
        else if (currentState == GameState.Ready || currentState == GameState.Setup)
        {
            if (survivorCount < 0 && !spawnRoutineStarted)
                StartCoroutine(SafeSpawnRoutine());
        }
    }

    IEnumerator TakeoverTimerCoroutine()
    {
        // Legacy — MasterTimerRoutine으로 대체
        yield break;
    }

    [PunRPC]
    public void SyncState(GameState newState)
    {
        currentState = newState;
        Debug.Log("현재 게임 상태: " + currentState);
    }

    [PunRPC]
    public void SyncMessage(string msg)
    {
        if (centerText != null) centerText.text = msg;
        if (ShouldShowGameOverPanel(msg))
            ShowGameOver(msg);
    }

    static bool ShouldShowGameOverPanel(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.Contains("Victory") || msg.Contains("Time Out") || msg.Contains("Escaped")
            || msg.Contains("All Caught") || msg.Contains("패배") || msg.Contains("승리");
    }

    void ShowGameOver(string msg)
    {
        if (centerText != null) centerText.text = msg;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    [PunRPC]
    public void SyncTimer(string timeMsg)
    {
        if (timerText != null) timerText.text = timeMsg;
    }

    [PunRPC]
    public void SyncSurvivorStatus(int alive, int total)
    {
        survivorCount = alive;
        initialSurvivorCount = total;
        UpdateSurvivorStatusUI();
    }

    IEnumerator SetupHudLayoutRoutine()
    {
        for (int i = 0; i < 12; i++)
        {
            if (GameObject.Find("MinimapUI_Runtime") != null)
                break;
            yield return null;
        }

        LayoutHudUnderMinimap();
    }

    void LayoutHudUnderMinimap()
    {
        const float defaultLeft = 20f;
        const float defaultTop = 20f;
        const float defaultSize = 310f;
        const float rowGap = 8f;
        const float timerHeight = 48f;
        const float survivorHeight = 34f;

        float left = defaultLeft;
        float topInset = defaultTop;
        float minimapSize = defaultSize;

        GameObject minimapUi = GameObject.Find("MinimapUI_Runtime");
        if (minimapUi != null)
        {
            RectTransform miniRect = minimapUi.GetComponent<RectTransform>();
            if (miniRect != null)
            {
                left = miniRect.anchoredPosition.x;
                topInset = -miniRect.anchoredPosition.y;
                minimapSize = miniRect.sizeDelta.y;
            }
        }

        float y = -(topInset + minimapSize + rowGap);
        float centerX = left + minimapSize * 0.5f;
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        if (timerText != null)
        {
            RectTransform timerRect = timerText.rectTransform;
            if (timerRect.parent != canvas.transform)
                timerRect.SetParent(canvas.transform, false);

            timerRect.anchorMin = new Vector2(0f, 1f);
            timerRect.anchorMax = new Vector2(0f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(centerX, y);
            timerRect.sizeDelta = new Vector2(minimapSize, timerHeight);
            timerText.fontSize = 42f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.raycastTarget = false;
            y -= timerHeight + rowGap;
        }

        EnsureSurvivorStatusText(canvas.transform, centerX, y, minimapSize, survivorHeight);
        UpdateSurvivorStatusUI();

        if (timerText != null) timerText.transform.SetAsLastSibling();
        if (survivorStatusText != null) survivorStatusText.transform.SetAsLastSibling();
    }

    void EnsureSurvivorStatusText(Transform parent, float centerX, float y, float width, float height)
    {
        if (survivorStatusText == null)
        {
            Transform existing = parent.Find("SurvivorStatusText");
            if (existing != null)
                survivorStatusText = existing.GetComponent<TextMeshProUGUI>();
        }

        if (survivorStatusText == null)
        {
            GameObject obj = new GameObject("SurvivorStatusText");
            obj.transform.SetParent(parent, false);
            survivorStatusText = obj.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rect = survivorStatusText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(centerX, y);
        rect.sizeDelta = new Vector2(width, height);

        survivorStatusText.fontSize = 28f;
        survivorStatusText.fontStyle = FontStyles.Bold;
        survivorStatusText.alignment = TextAlignmentOptions.Center;
        survivorStatusText.color = new Color(0.92f, 0.96f, 1f, 1f);
        survivorStatusText.raycastTarget = false;

        if (timerText != null && timerText.font != null)
            survivorStatusText.font = timerText.font;
    }

    void UpdateSurvivorStatusUI()
    {
        if (survivorStatusText == null) return;
        if (initialSurvivorCount < 0)
        {
            survivorStatusText.text = string.Empty;
            return;
        }

        int alive = Mathf.Max(0, survivorCount);
        survivorStatusText.text = $"생존자 : ({alive} / {initialSurvivorCount})";
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int min = total / 60;
        int sec = total % 60;
        return string.Format("{0:00}:{1:00}", min, sec);
    }

    static List<Transform> CollectTerminalSpawnPoints(Transform root)
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child != null && child.name.StartsWith("TerminalSpawn"))
                points.Add(child);
        }
        if (points.Count == 0)
        {
            foreach (Transform child in root)
                if (child != null) points.Add(child);
        }
        return points;
    }

    static List<Transform> CollectEscapeSpawnPoints(Transform root)
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child != null && child.name.StartsWith("EscapeSpawn"))
                points.Add(child);
        }
        if (points.Count == 0)
        {
            foreach (Transform child in root)
                if (child != null) points.Add(child);
        }
        return points;
    }

    [PunRPC]
    public void SyncRoleIndicator()
    {
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
        {
            StartCoroutine(ApplyRoleIndicatorWhenReady());
            return;
        }

        UpdateRoleIndicator();
    }

    IEnumerator ApplyRoleIndicatorWhenReady()
    {
        float timeout = 1.5f;
        float elapsed = 0f;

        while (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        UpdateRoleIndicator();
    }

    void EnsureRoleIndicator()
    {
        if (roleIndicatorText != null) return;

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        GameObject indicatorObject = new GameObject("RoleIndicator");
        indicatorObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = indicatorObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -28f);
        rect.sizeDelta = new Vector2(300f, 80f);

        roleIndicatorText = indicatorObject.AddComponent<TextMeshProUGUI>();
        roleIndicatorText.fontSize = 44f;
        roleIndicatorText.fontStyle = FontStyles.Bold;
        roleIndicatorText.alignment = TextAlignmentOptions.MidlineRight;
        roleIndicatorText.raycastTarget = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Font_1-4Regular SDF");
        if (font != null) roleIndicatorText.font = font;
    }

    void UpdateRoleIndicator()
    {
        EnsureRoleIndicator();
        if (roleIndicatorText == null) return;

        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
        {
            roleIndicatorText.text = string.Empty;
            return;
        }

        string myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey];
        bool hasBomb = BombPassManager.IsModeActive &&
                       BombPassManager.instance != null &&
                       PhotonNetwork.LocalPlayer.ActorNumber == BombPassManager.instance.BombHolderActor;

        if (BombPassManager.IsModeActive && hasBomb)
            roleIndicatorText.text = "<color=#FF6B6B>폭탄 💣</color>";
        else if (myRole == SeekerRole)
            roleIndicatorText.text = "<color=#FF6B6B>술래</color>";
        else
            roleIndicatorText.text = "<color=#66CCFF>생존자</color>";
    }

    static Canvas FindOverlayCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvas;
            }
        }

        return Object.FindFirstObjectByType<Canvas>();
    }

    public void OnClickExit()
    {
        Debug.Log("방을 나갑니다...");

        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("TitleScene");
    }
}