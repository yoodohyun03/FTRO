using UnityEngine;
using Photon.Pun;
using TMPro;

public class TitleManager : MonoBehaviourPunCallbacks
{
    [Header("UI 연결")]
    public TMP_InputField nameInput;

    // 유저가 무슨 버튼을 눌렀는지 기억하는 메모장
    private string myChoice = "";

    // 1. [createroom] 버튼을 누르면 실행!
    public void ClickCreateRoom()
    {
        if (string.IsNullOrEmpty(nameInput.text)) return; // 이름 없으면 컷

        PhotonNetwork.NickName = nameInput.text;
        myChoice = "create"; // "난 방 만들 거임" 메모
        PhotonNetwork.ConnectUsingSettings(); // 서버 접속!
    }

    // 2. [entryroom] 버튼을 누르면 실행!
    public void ClickEntryRoom()
    {
        if (string.IsNullOrEmpty(nameInput.text)) return;

        PhotonNetwork.NickName = nameInput.text;
        myChoice = "join"; // "난 남의 방 갈 거임" 메모
        PhotonNetwork.ConnectUsingSettings(); // 서버 접속!
    }

    // 3. 서버 접속이 성공하면 유니티가 자동으로 실행하는 곳
    public override void OnConnectedToMaster()
    {
        if (myChoice == "create")
        {
            // 방 만들기 (일단 테스트용으로 방 이름 고정)
            PhotonNetwork.CreateRoom("FTRO_Room");
        }
        else if (myChoice == "join")
        {
            // 만들어진 방 들어가기
            PhotonNetwork.JoinRoom("FTRO_Room");
        }
    }

    // 4. 방에 무사히 들어갔을 때 실행되는 곳
    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공! 대기실로 이동합니다.");
        // 포톤 전용 씬 이동 코드 (멀티게임 필수!)
        PhotonNetwork.LoadLevel("LobbyScene");
    }
}