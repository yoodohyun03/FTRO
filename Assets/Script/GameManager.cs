using UnityEngine;
using Photon.Pun;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviourPunCallbacks
{
    public enum GameState { Ready, Setup, Playing, End }
    public GameState currentState = GameState.Ready;

    [Header("UI 연결")]
    public TextMeshProUGUI centerText;
    public TextMeshProUGUI timerText;

    [Header("게임 설정")]
    public float playTime = 180f;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(GameFlowRoutine());
        }
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
            "<color=red>GOGO! Find!!</color>",
            "<color=#00BFFF>Survive!</color>");

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
    public void SyncState(GameState newState)
    {
        currentState = newState;
        Debug.Log("현재 게임 상태: " + currentState);
    }

    [PunRPC]
    public void SyncMessage(string msg)
    {
        if (centerText != null) centerText.text = msg;
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
}