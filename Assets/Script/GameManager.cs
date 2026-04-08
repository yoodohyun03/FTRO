using UnityEngine;
using Photon.Pun;
using TMPro;
using System.Collections;   
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviourPunCallbacks
{
    public enum GameState { Ready, Setup, Playing, End }
    public GameState currentState = GameState.Ready;

    public static GameManager instance;

    void Awake()
    {
        if (instance == null) instance = this;
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

    // [핵심] 게임 시작 시 생존자 숫자를 세팅하는 함수
    public void InitializeSurvivorCount()
    {
        // 방 안에 있는 모든 플레이어 중 'Survivor'인 사람만 카운트!
        int count = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey("Role") &&
                (string)player.CustomProperties["Role"] == "Survivor")
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

        // 형님 요청대로 생존자/술래 멘트를 직관적으로 바꿨습니다.
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
        photonView.RPC("SyncMessage", RpcTarget.All, ""); // 화면 텍스트 깔끔하게 지우기

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
        // 마스터 클라이언트(방장)만 숫자를 관리하게 합니다. (동기화 꼬임 방지)
        if (!PhotonNetwork.IsMasterClient) return;

        survivorCount--;
        Debug.Log("생존자 검거! 남은 수: " + survivorCount);

        // 만약 생존자가 0명이면? 즉시 술래 승리로 게임 종료!
        if (survivorCount <= 0 && currentState == GameState.Playing)
        {
            StopAllCoroutines(); // 타이머 멈추기
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
                gameOverPanel.SetActive(true); // 판넬 등장!

                // 마우스 커서도 풀어줍니다 (나가기 버튼 눌러야 하니까요!)
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

        string myRole = "Survivor";
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
        {
            myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties["Role"];
        }

        if (myRole == "Seeker") centerText.text = seekerMsg;
        else centerText.text = survivorMsg;
    }



    public void OnClickExit()
    {
        Debug.Log("방을 나갑니다...");

        // 1. 먼저 포톤 서버의 '방'에서 퇴장합니다. (중요!)
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        // 2. 방에서 완전히 나간 것이 확인되면, 타이틀 씬으로 이동합니다.
        // 씬 이름이 "TitleScene"이 맞는지 확인해 보시고 이름에 맞춰 수정하세요!
        SceneManager.LoadScene("TitleScene");
    }
}