using UnityEngine;
using Photon.Pun;
using TMPro;
using System.Collections;   
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviourPunCallbacks
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";
    private const string SurvivorRole = "Survivor";

    public enum GameState { Ready, Setup, Playing, End }
    public GameState currentState = GameState.Ready;

    public static GameManager instance;

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

    [Header("게임 설정")]
    public float playTime = 180f;

    [Header("승리 조건 설정")]
    public int survivorCount = 0; // 현재 살아있는 생존자 수

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(GameFlowRoutine());
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