using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
// Photon 전용 해시테이블
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class TitleManager : MonoBehaviourPunCallbacks
{
    private const string PasswordKey = "Password";
    private const string SelectedMapKey = "SelectedMap";
    private const string IsReadyKey = "IsReady";
    private const string RoleKey = "Role";

    [Header("1. 화면 패널들")]
    public GameObject loginPanel;
    public GameObject roomListPanel;
    public GameObject createRoomPanel;
    public GameObject passwordPopupPanel;
    public GameObject waitingRoomPanel;

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
    public TextMeshProUGUI waitingRoomNameText;
    public TextMeshProUGUI selectedMapText;
    public Button startButton;
    public Button readyButton;
    public Button leaveButton;
    private bool isReady = false;

    private const string PublicRoomLabel = "[public]";
    private const string PrivateRoomLabel = "[private]";

    [Header("5. 맵 선택 시스템")]
    public string selectedMap = "CityMapScene";
    public string[] mapList = { "CityMapScene", "JapanMapScene", "ForestMapScene" };

    [Header("6. 맵 선택 토글")]
    public Toggle cityMapToggle;
    public Toggle japanMapToggle;
    public Toggle forestMapToggle;
    public Toggle randomMapToggle;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        // 연결이 끊긴 상태면 재연결
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        // 처음 켰을 때 패널 초기화
        SetPanelState(showLogin: true, showRoomList: false, showCreateRoom: false, showPasswordPopup: false, showWaitingRoom: false);

        // 버튼 이벤트 연결
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyButtonClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveButtonClicked);

        // 맵 선택 토글 이벤트 연결
        if (cityMapToggle != null) cityMapToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectCityMap(); });
        if (japanMapToggle != null) japanMapToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectJapanMap(); });
        if (forestMapToggle != null) forestMapToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectForestMap(); });
        if (randomMapToggle != null) randomMapToggle.onValueChanged.AddListener(isOn => { if (isOn) SelectRandomMap(); });

        // 이미 방 안이면 대기방 UI 표시
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

    // 로그인/방 목록
    public void ClickPlay()
    {
        if (string.IsNullOrEmpty(nameInput.text)) return;
        PhotonNetwork.NickName = nameInput.text;

        SetPanelState(showLogin: false, showRoomList: true, showCreateRoom: false, showPasswordPopup: false, showWaitingRoom: false);
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
        if (roomListContent == null || roomEntryPrefab == null)
        {
            Debug.LogError("roomListContent 또는 roomEntryPrefab이 할당되지 않았습니다.");
            return;
        }

        foreach (Transform child in roomListContent) Destroy(child.gameObject);

        foreach (RoomInfo room in roomList)
        {
            if (!room.IsOpen || !room.IsVisible || room.RemovedFromList) continue;

            GameObject entry = Instantiate(roomEntryPrefab, roomListContent, false);
            entry.transform.localScale = Vector3.one;

            bool hasPassword = false;
            string roomPassword = "";
            if (room.CustomProperties.ContainsKey(PasswordKey))
            {
                roomPassword = (string)room.CustomProperties[PasswordKey];
                hasPassword = !string.IsNullOrEmpty(roomPassword);
            }

            Transform roomNameTransform = entry.transform.Find("RoomNameText");
            Transform roomInfoTransform = entry.transform.Find("RoomInfoText");
            if (roomNameTransform == null || roomInfoTransform == null)
            {
                Destroy(entry);
                continue;
            }

            TextMeshProUGUI nameText = roomNameTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI infoText = roomInfoTransform.GetComponent<TextMeshProUGUI>();
            if (nameText == null || infoText == null)
            {
                Destroy(entry);
                continue;
            }

            nameText.text = room.Name;
            infoText.text = hasPassword ? $"{PrivateRoomLabel} {room.PlayerCount}/{room.MaxPlayers}" : $"{PublicRoomLabel} {room.PlayerCount}/{room.MaxPlayers}";

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
            Debug.LogWarning("비밀번호가 일치하지 않습니다.");
            joinPwdInput.text = "";
        }
    }

    // 맵 선택/방 생성
    public void SelectCityMap() { selectedMap = "CityMapScene"; Debug.Log("도시 맵 선택"); }
    public void SelectJapanMap() { selectedMap = "JapanMapScene"; Debug.Log("일본 맵 선택"); }
    public void SelectForestMap() { selectedMap = "ForestMapScene"; Debug.Log("숲 맵 선택"); }
    public void SelectRandomMap()
    {
        int r = Random.Range(0, mapList.Length);
        selectedMap = mapList[r];
        Debug.Log("랜덤 맵 선택: " + selectedMap);
    }

    public void ClickCreateRoomReal()
    {
        if (string.IsNullOrEmpty(roomNameInput.text)) return;

        RoomOptions options = new RoomOptions { MaxPlayers = 8 };
        // 로비에서 비밀번호/맵 정보를 확인할 수 있도록 등록
        options.CustomRoomPropertiesForLobby = new string[] { PasswordKey, SelectedMapKey };

        Hashtable roomProps = new Hashtable();
        roomProps.Add(PasswordKey, isPublicToggle.isOn ? "" : createPwdInput.text);
        roomProps.Add(SelectedMapKey, selectedMap);

        options.CustomRoomProperties = roomProps;
        PhotonNetwork.CreateRoom(roomNameInput.text, options);
    }

    // 대기방
    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공! 대기방으로 화면 전환합니다.");
        ShowWaitingRoom();
    }

    void ShowWaitingRoom()
    {
        // 다른 패널 싹 끄고 대기방만 켜기!
        SetPanelState(showLogin: false, showRoomList: false, showCreateRoom: false, showPasswordPopup: false, showWaitingRoom: true);

        if (waitingRoomNameText != null) waitingRoomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // 선택된 맵 이름 표시
        if (selectedMapText != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(SelectedMapKey))
        {
            string mapName = (string)PhotonNetwork.CurrentRoom.CustomProperties[SelectedMapKey];
            selectedMapText.text = "현재 맵: " + mapName;
        }

        // 내 레디 상태 초기화
        Hashtable props = new Hashtable() { { IsReadyKey, false } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        isReady = false;
        if (readyButton != null) readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";

        UpdatePlayerList();
        CheckStartButton();
    }

    public override void OnLeftRoom()
    {
        // 방 나가면 다시 방 목록 패널로!
        SetPanelState(showLogin: false, showRoomList: true, showCreateRoom: false, showPasswordPopup: false, showWaitingRoom: false);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) { UpdatePlayerList(); CheckStartButton(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { UpdatePlayerList(); CheckStartButton(); }
    public override void OnMasterClientSwitched(Player newMasterClient) { UpdatePlayerList(); CheckStartButton(); }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(IsReadyKey))
        {
            UpdatePlayerList();
            CheckStartButton();
        }
    }

    void UpdatePlayerList()
    {
        // 필수 요소 확인
        if (playerListGroup == null)
        {
            Debug.LogError("playerListGroup이 할당되지 않았습니다!");
            return;
        }

        if (playerSlotPrefab == null)
        {
            Debug.LogError("playerSlotPrefab이 할당되지 않았습니다!");
            return;
        }

        // 기존 플레이어 슬롯 삭제
        foreach (Transform child in playerListGroup) Destroy(child.gameObject);

        // 새 플레이어 슬롯 생성
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject slot = Instantiate(playerSlotPrefab, playerListGroup);

            if (slot == null)
            {
                Debug.LogError("playerSlotPrefab을 생성할 수 없습니다!");
                continue;
            }

            Transform nameTextTransform = slot.transform.Find("PlayerNameText");
            Transform readyTextTransform = slot.transform.Find("ReadyStateText");

            // 자식 객체 확인
            if (nameTextTransform == null)
            {
                Debug.LogError($"PlayerSlot에서 'PlayerNameText'를 찾을 수 없습니다!");
                Destroy(slot);
                continue;
            }

            if (readyTextTransform == null)
            {
                Debug.LogError($"PlayerSlot에서 'ReadyStateText'를 찾을 수 없습니다!");
                Destroy(slot);
                continue;
            }

            TextMeshProUGUI nameText = nameTextTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readyText = readyTextTransform.GetComponent<TextMeshProUGUI>();

            if (nameText == null || readyText == null)
            {
                Debug.LogError("TextMeshProUGUI 컴포넌트를 찾을 수 없습니다!");
                Destroy(slot);
                continue;
            }

            nameText.text = $"[{player.NickName}]";

            if (player.IsMasterClient)
            {
                readyText.text = "<color=yellow>[방장]</color>";
            }
            else
            {
                bool playerReady = false;
                if (player.CustomProperties.TryGetValue(IsReadyKey, out object isReadyObj))
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
                    player.CustomProperties.TryGetValue(IsReadyKey, out object isReadyObj);
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
        Hashtable props = new Hashtable() { { IsReadyKey, isReady } };
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
        // 방장이 역할을 먼저 확정한 뒤 씬을 로드해야 클라이언트별 스폰/안내 메시지 동기화가 안정적입니다.
        Player[] players = PhotonNetwork.PlayerList;
        int seekerIndex = Random.Range(0, players.Length);

        for (int i = 0; i < players.Length; i++)
        {
            Hashtable props = new Hashtable();
            props.Add(RoleKey, i == seekerIndex ? "Seeker" : "Survivor");
            players[i].SetCustomProperties(props);
        }

        yield return new WaitForSeconds(0.5f);

        // 방장이 선택한 맵으로 씬 전환
        string mapToLoad = (string)PhotonNetwork.CurrentRoom.CustomProperties[SelectedMapKey];
        PhotonNetwork.LoadLevel(mapToLoad);
    }

    void OnLeaveButtonClicked()
    {
        PhotonNetwork.LeaveRoom();
    }

    void SetPanelState(bool showLogin, bool showRoomList, bool showCreateRoom, bool showPasswordPopup, bool showWaitingRoom)
    {
        if (loginPanel != null) loginPanel.SetActive(showLogin);
        if (roomListPanel != null) roomListPanel.SetActive(showRoomList);
        if (createRoomPanel != null) createRoomPanel.SetActive(showCreateRoom);
        if (passwordPopupPanel != null) passwordPopupPanel.SetActive(showPasswordPopup);
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(showWaitingRoom);
    }
}