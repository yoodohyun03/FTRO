using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
// 🌟 [핵심] 포톤 전용 해시테이블!
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class TitleManager : MonoBehaviourPunCallbacks
{
    [Header("1. 화면 패널들")]
    public GameObject loginPanel;
    public GameObject roomListPanel;
    public GameObject createRoomPanel;
    public GameObject passwordPopupPanel;
    public GameObject waitingRoomPanel; // 🌟 새로 추가된 대기방 패널!

    [Header("2. 입력칸 & 토글")]
    public TMP_InputField nameInput;
    public TMP_InputField roomNameInput;
    public Toggle isPublicToggle;
    public TMP_InputField createPwdInput;
    public TMP_InputField joinPwdInput;

    [Header("3. 방 목록 UI")]
    public GameObject roomEntryPrefab;
    public Transform roomListContent;
    private string targetRoomName = "";
    private string targetRoomPassword = "";

    [Header("4. 대기방 UI (기존 RoomManager)")]
    public Transform playerListGroup;
    public GameObject playerSlotPrefab;
    public TextMeshProUGUI waitingRoomNameText; // 대기방 안의 방 제목 텍스트
    public TextMeshProUGUI selectedMapText;     // 🌟 대기방에 띄울 "선택된 맵: OOO" 텍스트
    public Button startButton;
    public Button readyButton;
    public Button leaveButton;
    private bool isReady = false;

    [Header("5. 맵 선택 시스템")]
    public string selectedMap = "JapanMapScene"; // 기본값
    public string[] mapList = { "JapanMapScene", "CityMapScene" }; // 랜덤 추첨용 배열

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        // 🌟 혹시 게임 끝내고 로비로 돌아왔을 때를 대비한 방어막
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        // 처음 켰을 때 패널 초기화
        loginPanel.SetActive(true);
        roomListPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        passwordPopupPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);

        // 버튼 이벤트 연결
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyButtonClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveButtonClicked);

        // 🌟 게임 끝나고 대기방으로 돌아왔을 때 바로 대기방 켜주기
        if (PhotonNetwork.InRoom)
        {
            ShowWaitingRoom();
        }
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
        Debug.Log("서버 접속 및 로비 진입 완료!");
    }

    // ==========================================
    // 🚪 [로그인 & 방 목록 시스템]
    // ==========================================
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

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (Transform child in roomListContent) Destroy(child.gameObject);

        foreach (RoomInfo room in roomList)
        {
            if (!room.IsOpen || !room.IsVisible || room.RemovedFromList) continue;

            GameObject entry = Instantiate(roomEntryPrefab, roomListContent, false);
            entry.transform.localScale = Vector3.one;

            bool hasPassword = false;
            string roomPassword = "";
            if (room.CustomProperties.ContainsKey("Password"))
            {
                roomPassword = (string)room.CustomProperties["Password"];
                hasPassword = !string.IsNullOrEmpty(roomPassword);
            }

            TextMeshProUGUI nameText = entry.transform.Find("RoomNameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI infoText = entry.transform.Find("RoomInfoText").GetComponent<TextMeshProUGUI>();
            nameText.text = room.Name;
            infoText.text = hasPassword ? $"[private] {room.PlayerCount}/{room.MaxPlayers}" : $"[public] {room.PlayerCount}/{room.MaxPlayers}";

            Button joinBtn = entry.GetComponent<Button>();
            joinBtn.onClick.AddListener(() =>
            {
                if (hasPassword)
                {
                    targetRoomName = room.Name;
                    targetRoomPassword = roomPassword;
                    passwordPopupPanel.SetActive(true);
                }
                else
                {
                    PhotonNetwork.JoinRoom(room.Name);
                }
            });
        }
    }

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

    // ==========================================
    // 🗺️ [맵 선택 & 방 생성 시스템]
    // ==========================================

    // 🌟 방 만들기 UI에 맵 버튼 3개 만들고 각각 연결해주세요!
    public void SelectJapanMap() { selectedMap = "JapanMapScene"; Debug.Log("일본 맵 선택"); }
    public void SelectCityMap() { selectedMap = "CityMapScene"; Debug.Log("도시 맵 선택"); }
    public void SelectRandomMap()
    {
        int r = Random.Range(0, mapList.Length);
        selectedMap = mapList[r];
        Debug.Log("랜덤 맵 선택됨: " + selectedMap);
    }

    public void ClickCreateRoomReal()
    {
        if (string.IsNullOrEmpty(roomNameInput.text)) return;

        RoomOptions options = new RoomOptions { MaxPlayers = 8 };
        // 🌟 로비에서 비번이랑 맵 정보를 읽을 수 있게 등록
        options.CustomRoomPropertiesForLobby = new string[] { "Password", "SelectedMap" };

        Hashtable roomProps = new Hashtable();
        roomProps.Add("Password", isPublicToggle.isOn ? "" : createPwdInput.text);
        roomProps.Add("SelectedMap", selectedMap); // 포스트잇에 맵 이름 붙이기!

        options.CustomRoomProperties = roomProps;
        PhotonNetwork.CreateRoom(roomNameInput.text, options);
    }

    // ==========================================
    // 🛋️ [대기방 (Waiting Room) 시스템]
    // ==========================================
    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공! 대기방으로 화면 전환합니다.");
        ShowWaitingRoom();
    }

    void ShowWaitingRoom()
    {
        // 다른 패널 싹 끄고 대기방만 켜기!
        loginPanel.SetActive(false);
        roomListPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        passwordPopupPanel.SetActive(false);
        waitingRoomPanel.SetActive(true);

        if (waitingRoomNameText != null) waitingRoomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // 🌟 맵 이름 포스트잇 읽어와서 화면에 띄워주기
        if (selectedMapText != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("SelectedMap"))
        {
            string mapName = (string)PhotonNetwork.CurrentRoom.CustomProperties["SelectedMap"];
            selectedMapText.text = "현재 맵: " + mapName;
        }

        // 내 레디 상태 초기화
        Hashtable props = new Hashtable() { { "IsReady", false } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        isReady = false;
        if (readyButton != null) readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";

        UpdatePlayerList();
        CheckStartButton();
    }

    public override void OnLeftRoom()
    {
        // 방 나가면 다시 방 목록 패널로!
        waitingRoomPanel.SetActive(false);
        roomListPanel.SetActive(true);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) { UpdatePlayerList(); CheckStartButton(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { UpdatePlayerList(); CheckStartButton(); }
    public override void OnMasterClientSwitched(Player newMasterClient) { UpdatePlayerList(); CheckStartButton(); }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            UpdatePlayerList();
            CheckStartButton();
        }
    }

    void UpdatePlayerList()
    {
        if (playerListGroup == null) return;

        foreach (Transform child in playerListGroup) Destroy(child.gameObject);

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject slot = Instantiate(playerSlotPrefab, playerListGroup);
            TextMeshProUGUI nameText = slot.transform.Find("PlayerNameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readyText = slot.transform.Find("ReadyStateText").GetComponent<TextMeshProUGUI>();

            nameText.text = $"[{player.NickName}]";

            if (player.IsMasterClient)
            {
                readyText.text = "<color=yellow>[방장]</color>";
            }
            else
            {
                bool playerReady = false;
                if (player.CustomProperties.TryGetValue("IsReady", out object isReadyObj))
                {
                    playerReady = (bool)isReadyObj;
                }
                readyText.text = playerReady ? "<color=green>Ready!</color>" : "대기 중...";
            }
        }
    }

    void CheckStartButton()
    {
        if (startButton == null || readyButton == null) return;

        if (PhotonNetwork.IsMasterClient)
        {
            readyButton.gameObject.SetActive(false);
            startButton.gameObject.SetActive(true);

            bool allReady = true;
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                {
                    player.CustomProperties.TryGetValue("IsReady", out object isReadyObj);
                    if (isReadyObj == null || !(bool)isReadyObj)
                    {
                        allReady = false;
                        break;
                    }
                }
            }
            startButton.interactable = allReady;
        }
        else
        {
            startButton.gameObject.SetActive(false);
            readyButton.gameObject.SetActive(true);
        }
    }

    void OnReadyButtonClicked()
    {
        isReady = !isReady;
        Hashtable props = new Hashtable() { { "IsReady", isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        readyButton.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? "Cancel" : "Ready";
    }

    void OnStartButtonClicked()
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        StartCoroutine(AssignRolesAndStart());
    }

    System.Collections.IEnumerator AssignRolesAndStart()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int seekerIndex = Random.Range(0, players.Length);

        for (int i = 0; i < players.Length; i++)
        {
            Hashtable props = new Hashtable();
            props.Add("Role", i == seekerIndex ? "Seeker" : "Survivor");
            players[i].SetCustomProperties(props);
        }

        yield return new WaitForSeconds(0.5f);

        // 🌟 [최종 하이라이트] 방장이 고른 맵 이름표를 읽어와서 그 씬으로 텔레포트!!
        string mapToLoad = (string)PhotonNetwork.CurrentRoom.CustomProperties["SelectedMap"];
        PhotonNetwork.LoadLevel(mapToLoad);
    }

    void OnLeaveButtonClicked()
    {
        PhotonNetwork.LeaveRoom();
    }
}