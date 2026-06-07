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

    [Header("게임 설정")]
    public float playTime = 600f;

    [Header("승리 조건 설정")]
    public int survivorCount = -1; // -1 = 미초기화 (게임 시작 전 검거 오판 방지)

    private double gameStartNetworkTime = 0;

    void Start()
    {
        completedObjectives = 0;
        UpdateObjectiveStatusUI();
        EnsureRoleIndicator();
        if (centerText != null) centerText.text = "";

        if (PhotonNetwork.IsMasterClient)
{
            StartCoroutine(SafeSpawnRoutine());
        }
    }

    IEnumerator SafeSpawnRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        try 
        {
            SpawnObjectives();
        } 
        catch (System.Exception e) 
        {
            Debug.LogError($"[GameManager] SpawnObjectives 에러: {e.Message}\n{e.StackTrace}");
        }

        // GameFlowRoutine 시작 전 미리 초기화 → Setup 중 검거돼도 카운트 오류 방지
        InitializeSurvivorCount();

        StartCoroutine(GameFlowRoutine());
    }

    void SpawnObjectives()
    {
        Debug.Log("[GameManager] 오브젝트 동적 스폰 시작...");

        // 0. 스폰 포인트 자동 복구
        if (terminalSpawnPoints == null || terminalSpawnPoints.Count == 0)
        {
            Debug.LogWarning("terminalSpawnPoints 리스트가 비어있습니다. 'SpawnPoints' 오브젝트에서 자동 검색을 시도합니다.");
            GameObject spawnRoot = GameObject.Find("SpawnPoints") ?? GameObject.Find("SpawnPoints (1)");
            if (spawnRoot != null)
            {
                terminalSpawnPoints = new List<Transform>();
                foreach (Transform child in spawnRoot.transform)
                    terminalSpawnPoints.Add(child);
                Debug.Log($"[GameManager] {terminalSpawnPoints.Count}개의 터미널 스폰 포인트를 자동으로 찾았습니다.");
            }
        }

        if (escapeSpawnPoints == null || escapeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("escapeSpawnPoints 리스트가 비어있습니다. 'EscapePoints' 오브젝트에서 자동 검색을 시도합니다.");
            GameObject escapeRoot = GameObject.Find("EscapePoints") ?? GameObject.Find("EscapeSpawnPoints");
            if (escapeRoot != null)
            {
                escapeSpawnPoints = new List<Transform>();
                foreach (Transform child in escapeRoot.transform)
                    escapeSpawnPoints.Add(child);
                Debug.Log($"[GameManager] {escapeSpawnPoints.Count}개의 탈출구 스폰 포인트를 자동으로 찾았습니다.");
            }
        }

        // 1. 터미널 스폰 (포인트 없으면 건너뜀 — 탈출구 스폰은 계속 진행)
        if (terminalSpawnPoints != null && terminalSpawnPoints.Count > 0)
        {
            List<Transform> availablePoints = new List<Transform>();
            foreach (var p in terminalSpawnPoints) if (p != null) availablePoints.Add(p);

            List<Vector3> spawnedPositions = new List<Vector3>();
            float minDistanceBetweenTerminals = 20f;
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
        Debug.Log("초기 생존자 수: " + survivorCount);
    }

    IEnumerator GameFlowRoutine()
    {
        // 1. [Setup] 5초 대기 (가운데 안내 멘트 없음, 직업은 우측 상단 표시)
        SetState(GameState.Setup);
        photonView.RPC("SyncRoleIndicator", RpcTarget.All);

        yield return new WaitForSeconds(5f);

        // 2. [Playing] 본 게임 시작
        SetState(GameState.Playing);
        photonView.RPC("SyncRoleIndicator", RpcTarget.All);

        // 마스터 교체 시 타이머 인계를 위해 시작 시간 저장
        gameStartNetworkTime = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { { "GST", gameStartNetworkTime } });

        yield return new WaitForSeconds(2f);
        photonView.RPC("SyncMessage", RpcTarget.All, "");

        // 3. 타이머 시작
        float currentTime = playTime;

        while (currentState == GameState.Playing && currentTime > 0)
        {
            yield return new WaitForSeconds(1f);
            currentTime -= 1f;

            int min = Mathf.FloorToInt(currentTime / 60f);
            int sec = Mathf.FloorToInt(currentTime % 60f);
            string timeString = string.Format("{0:00}:{1:00}", min, sec);

            photonView.RPC("SyncTimer", RpcTarget.All, timeString);
        }

        // 4. 게임 끝!
        if (currentState == GameState.Playing && currentTime <= 0)
        {
            SetState(GameState.End);
            photonView.RPC("SyncMessage", RpcTarget.All, "Time Out!\nSurvivor Victory!");
        }
    }

    public void SetState(GameState newState)
    {
        photonView.RPC("SyncState", RpcTarget.All, newState);
    }


    public void NotifyTerminalCompleted()
    {
        if (!PhotonNetwork.IsMasterClient) return;

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
        // 구버전 호환용 — 실제 처리는 NotifyTerminalCompleted에서만 수행
        if (!PhotonNetwork.IsMasterClient) return;
        NotifyTerminalCompleted();
    }

    void UpdateObjectiveStatusUI()
    {
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
        StopAllCoroutines();
        SetState(GameState.End);
        photonView.RPC("SyncMessage", RpcTarget.All, message);
    }

    [PunRPC]
    public void OnSurvivorCaught()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 아직 초기화 전이면(Setup 단계) 무시
        if (survivorCount < 0) return;

        survivorCount--;
        Debug.Log("생존자 검거! 남은 수: " + survivorCount);

        if (survivorCount <= 0 && currentState == GameState.Playing)
        {
            StopAllCoroutines();
            SetState(GameState.End);
            photonView.RPC("SyncMessage", RpcTarget.All, "All Caught!\nSeeker Victory!");
        }
    }

    // 마스터 클라이언트 교체 시 게임 진행 인계
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log("[GameManager] 마스터 교체됨 — 게임 진행 인계");

        if (currentState == GameState.Playing)
            StartCoroutine(TakeoverTimerCoroutine());
        else if (currentState == GameState.Ready || currentState == GameState.Setup)
            StartCoroutine(SafeSpawnRoutine());
    }

    IEnumerator TakeoverTimerCoroutine()
    {
        float remaining = playTime;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GST", out object startObj))
        {
            double elapsed = PhotonNetwork.Time - (double)startObj;
            remaining = Mathf.Max(0, playTime - (float)elapsed);
        }

        while (currentState == GameState.Playing && remaining > 0)
        {
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
            int min = Mathf.FloorToInt(remaining / 60f);
            int sec = Mathf.FloorToInt(remaining % 60f);
            photonView.RPC("SyncTimer", RpcTarget.All, string.Format("{0:00}:{1:00}", min, sec));
        }

        if (currentState == GameState.Playing && remaining <= 0)
        {
            SetState(GameState.End);
            photonView.RPC("SyncMessage", RpcTarget.All, "Time Out!\nSurvivor Victory!");
        }
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
        if (msg.Contains("Victory") || msg.Contains("Time Out"))
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    [PunRPC]
    public void SyncTimer(string timeMsg)
    {
        if (timerText != null) timerText.text = timeMsg;
    }

    [PunRPC]
    public void SyncRoleIndicator()
    {
        if (centerText != null) centerText.text = "";

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
        roleIndicatorText.text = myRole == SeekerRole
            ? "<color=#FF6B6B>술래</color>"
            : "<color=#66CCFF>생존자</color>";
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