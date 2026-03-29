using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic; // 리스트(List) 쓰려면 필수!
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class TitleManager : MonoBehaviourPunCallbacks
{
    [Header("화면 패널들")]
    public GameObject loginPanel;
    public GameObject roomListPanel;
    public GameObject createRoomPanel;
    public GameObject passwordPopupPanel;

    [Header("입력칸들 (InputFields & Toggle)")]
    public TMP_InputField nameInput;
    public TMP_InputField roomNameInput;
    public Toggle isPublicToggle;
    public TMP_InputField createPwdInput;
    public TMP_InputField joinPwdInput;

    [Header("방 목록 전용 (방금 추가됨!)")]
    public GameObject roomEntryPrefab; // 아까 만든 파란색 방 버튼 프리팹
    public Transform roomListContent;  // 방 버튼들이 생성될 부모 폴더 (Scroll View 안의 Content)

    private string targetRoomName = "";
    private string targetRoomPassword = "";

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();

        loginPanel.SetActive(true);
        roomListPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        passwordPopupPanel.SetActive(false);
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
        Debug.Log("서버 접속 및 로비 진입 완료!");
    }

    public void ClickPlay()
    {
        if (string.IsNullOrEmpty(nameInput.text)) return;
        PhotonNetwork.NickName = nameInput.text;

        loginPanel.SetActive(false);
        roomListPanel.SetActive(true);
    }

    public void OpenCreateRoomPanel()
    {
        roomListPanel.SetActive(false);
        createRoomPanel.SetActive(true);
    }

    public void CloseCreateRoomPanel()
    {
        createRoomPanel.SetActive(false);
        roomListPanel.SetActive(true);
    }

    public void ClickCreateRoomReal()
    {
        if (string.IsNullOrEmpty(roomNameInput.text)) return;

        RoomOptions options = new RoomOptions { MaxPlayers = 10 };
        options.CustomRoomPropertiesForLobby = new string[] { "Password" };

        Hashtable roomProps = new Hashtable();
        if (isPublicToggle.isOn) roomProps.Add("Password", "");
        else roomProps.Add("Password", createPwdInput.text);

        options.CustomRoomProperties = roomProps;
        PhotonNetwork.CreateRoom(roomNameInput.text, options);
    }

    // ==========================================
    // 👑 [대망의 2탄 핵심★] 방 목록이 갱신될 때마다 자동으로 실행되는 마법!
    // ==========================================
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // 1. 기존에 떠있던 방 목록 싹 다 청소! (안 그러면 중복돼서 쌓임)
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        // 2. 서버가 준 방 목록을 하나씩 까봅니다.
        foreach (RoomInfo room in roomList)
        {
            // 꽉 찼거나 삭제된 방은 화면에 안 띄움!
            if (!room.IsOpen || !room.IsVisible || room.RemovedFromList) continue;

            // 3. 아까 만든 '방 버튼 프리팹'을 복사해서 화면에 생성!
            GameObject entry = Instantiate(roomEntryPrefab, roomListContent, false);

            entry.transform.localScale = Vector3.one;
            entry.transform.localRotation = Quaternion.identity;

            // 비밀번호 확인 로직
            bool hasPassword = false;
            string roomPassword = "";
            if (room.CustomProperties.ContainsKey("Password"))
            {
                roomPassword = (string)room.CustomProperties["Password"];
                if (!string.IsNullOrEmpty(roomPassword)) hasPassword = true;
            }

            // 4. 글씨(텍스트) 덮어씌우기
            TextMeshProUGUI nameText = entry.transform.Find("RoomNameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI infoText = entry.transform.Find("RoomInfoText").GetComponent<TextMeshProUGUI>();

            nameText.text = room.Name;
            infoText.text = hasPassword ? $"[private] {room.PlayerCount}/{room.MaxPlayers}" : $"[public] {room.PlayerCount}/{room.MaxPlayers}";

            // 5. 이 방 버튼을 [클릭] 했을 때의 행동 강령 세팅!
            Button joinBtn = entry.GetComponent<Button>();
            joinBtn.onClick.AddListener(() =>
            {
                if (hasPassword)
                {
                    targetRoomName = room.Name;
                    targetRoomPassword = roomPassword;
                    passwordPopupPanel.SetActive(true); // 비번 창 띄우기!
                }
                else
                {
                    PhotonNetwork.JoinRoom(room.Name); // 공개방은 프리패스!
                }
            });
        }
    }

    // ==========================================
    // 팝업창 및 씬 이동 로직
    // ==========================================
    public void ClickCancelPasswordPopup()
    {
        passwordPopupPanel.SetActive(false);
        joinPwdInput.text = "";
    }

    public void ClickJoinPasswordRoom()
    {
        if (joinPwdInput.text == targetRoomPassword)
        {
            PhotonNetwork.JoinRoom(targetRoomName);
            passwordPopupPanel.SetActive(false);
            joinPwdInput.text = "";
        }
        else
        {
            Debug.LogWarning("🚨 비밀번호 불일치!");
            joinPwdInput.text = "";
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공! 대기실로 갑니다.");
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel("LobbyScene");
    }
}