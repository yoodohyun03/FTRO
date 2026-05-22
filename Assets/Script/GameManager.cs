using UnityEngine;
using Photon.Pun;
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

    [Header("게임 설정")]
    public float playTime = 600f;

    [Header("승리 조건 설정")]
    public int survivorCount = 0; // 현재 살아있는 생존자 수

    void Start()
    {
        completedObjectives = 0;
        UpdateObjectiveStatusUI();

        if (PhotonNetwork.IsMasterClient)
{
            StartCoroutine(SafeSpawnRoutine());
        }
    }

    IEnumerator SafeSpawnRoutine()
    {
        // 맵 로딩 후 포톤 네트워크 안정화를 위해 대기
        yield return new WaitForSeconds(0.5f);
        
        try 
        {
            SpawnObjectives();
        } 
        catch (System.Exception e) 
        {
            Debug.LogError($"[GameManager] SpawnObjectives 에러: {e.Message}\n{e.StackTrace}");
        }

        StartCoroutine(GameFlowRoutine());
    }

    void SpawnObjectives()
    {
        Debug.Log("[GameManager] 오브젝트 동적 스폰 시작...");

        if (terminalSpawnPoints == null || terminalSpawnPoints.Count == 0)
        {
            Debug.LogWarning("terminalSpawnPoints 리스트가 비어있습니다. 인스펙터를 확인하세요.");
            return;
        }

        // 1. 터미널 스폰 (랜덤 위치 선택, 거리 고려)
        List<Transform> availablePoints = new List<Transform>();
        foreach(var p in terminalSpawnPoints) if(p != null) availablePoints.Add(p);

        List<Vector3> spawnedPositions = new List<Vector3>();
        float minDistanceBetweenTerminals = 20f; // 최소 거리 설정

        int countToSpawn = Mathf.Min(totalObjectives, availablePoints.Count);
        int spawnedCount = 0;
        int maxAttempts = 50; // 무한 루프 방지

        while (spawnedCount < countToSpawn && availablePoints.Count > 0 && maxAttempts > 0)
        {
            maxAttempts--;
            int randomIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[randomIndex];
            
            // 거리 체크
            bool tooClose = false;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(spawnPoint.position, pos) < minDistanceBetweenTerminals)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose || availablePoints.Count <= (countToSpawn - spawnedCount))
            {
                // 충분히 멀거나, 남은 포인트가 부족해서 어쩔 수 없이 뽑아야 할 때
                availablePoints.RemoveAt(randomIndex);
                Vector3 spawnPos = spawnPoint.position + Vector3.up * 0.5f;
                PhotonNetwork.InstantiateRoomObject("HackingTerminal", spawnPos, spawnPoint.rotation);
                spawnedPositions.Add(spawnPoint.position);
                spawnedCount++;
                Debug.Log($"[GameManager] 터미널 {spawnedCount} 생성됨 at {spawnPos}");
            }
            else
            {
                // 너무 가까우면 리스트에서 잠시 제외했다가 나중에 다시 고려하거나 그냥 스킵
                // 여기서는 그냥 스킵하고 다른 포인트를 찾음
                continue;
            }
        }

            // 2. 탈출구 스폰
            List<Transform> availableEscapePoints = new List<Transform>();
            foreach(var p in escapeSpawnPoints) if(p != null) availableEscapePoints.Add(p);

            if (availableEscapePoints.Count > 0)
            {
            int randomIndex = Random.Range(0, availableEscapePoints.Count);
            Transform spawnPoint = availableEscapePoints[randomIndex];
            
            // Apply small Y-offset (+0.05f) for floor pad
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
                Debug.LogWarning("escapeSpawnPoints 리스트가 비어있습니다.");
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
        // 1. [Setup] 시작하자마자 직업 확인 + 5초 대기 (멘트 통합!)
        SetState(GameState.Setup);

        photonView.RPC("SyncRoleMessage", RpcTarget.All,
            "<color=red>You Are Seeker!</color>\n생존자들이 숨고 있습니다... (5초)",
            "<color=#00BFFF>You Are Surviver!</color>\n술래가 눈을 감고 있습니다...");

        yield return new WaitForSeconds(5f);

        // 2. [Playing] 본 게임 시작
        SetState(GameState.Playing);
        photonView.RPC("SyncRoleMessage", RpcTarget.All,
            "<color=red>생존자를 찾으십시오.</color>",
            "<color=#00BFFF>완벽히 연기하여 살아남으십시오.</color>");

        InitializeSurvivorCount();

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


    [PunRPC]
    public void OnObjectiveCompleted()
    {
        completedObjectives++;
        UpdateObjectiveStatusUI();
        
        if (completedObjectives >= totalObjectives)
        {
            if (escapePoint == null) escapePoint = Object.FindFirstObjectByType<EscapePoint>();
            if (escapePoint != null)
            {
                escapePoint.photonView.RPC("RPC_ActivateEscape", RpcTarget.All);
                photonView.RPC("SyncMessage", RpcTarget.All, "모든 터미널 활성화! 탈출구로 이동하세요!");
            }
        }
        else
        {
            photonView.RPC("SyncMessage", RpcTarget.All, $"터미널 활성화 ({completedObjectives}/{totalObjectives})");
        }

        // 3초 후 중앙 메세지 삭제 (작은 텍스트는 유지)
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(ClearCenterMessageAfterDelay(3f));
        }
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
        // 마스터 클라이언트만 생존자 수를 관리
        if (!PhotonNetwork.IsMasterClient) return;

        survivorCount--;
        Debug.Log("생존자 검거! 남은 수: " + survivorCount);

        // 생존자가 0명이면 술래 승리로 종료
        if (survivorCount <= 0 && currentState == GameState.Playing)
        {
            StopAllCoroutines();
            SetState(GameState.End);
            photonView.RPC("SyncMessage", RpcTarget.All, "All Caught!\nSeeker Victory!");
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
    public void SyncRoleMessage(string seekerMsg, string survivorMsg)
    {
        if (centerText == null) return;

        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
        {
            StartCoroutine(ApplyRoleMessageWhenReady(seekerMsg, survivorMsg));
            return;
        }

        string myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey];

        if (myRole == SeekerRole) centerText.text = seekerMsg;
        else centerText.text = survivorMsg;
    }

    IEnumerator ApplyRoleMessageWhenReady(string seekerMsg, string survivorMsg)
    {
        float timeout = 1.5f;
        float elapsed = 0f;

        while (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        string myRole = SurvivorRole;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
        {
            myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey];
        }

        if (centerText == null) yield break;

        if (myRole == SeekerRole) centerText.text = seekerMsg;
        else centerText.text = survivorMsg;
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