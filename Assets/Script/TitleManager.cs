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

    [Header("4. 대기방 UI (기존 RoomManager)")]
    public Transform playerListGroup;
    public GameObject playerSlotPrefab;
    public TextMeshProUGUI waitingRoomNameText;
    public TextMeshProUGUI selectedMapText;
    public Button startButton;
    public Button readyButton;
    public Button leaveButton;
    private WaitingRoomController waitingRoomController;
    private RoomCreationController roomCreationController;
    private LobbyController lobbyController;
    private MatchStartController matchStartController;

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

        EnsureWaitingRoomController();
        selectedMap = EnsureRoomCreationController().SelectedMap;

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
        if (!EnsureLobbyController().TrySetNickname()) return;

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
        EnsureLobbyController().UpdateRoomList(roomList);
    }

    public void ClickCancelPasswordPopup()
    {
        EnsureLobbyController().CancelPasswordPopup();
    }

    public void ClickJoinPasswordRoom()
    {
        EnsureLobbyController().TryJoinPasswordRoom();
    }

    // 맵 선택/방 생성
    public void SelectCityMap()
    {
        RoomCreationController controller = EnsureRoomCreationController();
        controller.SelectCityMap();
        selectedMap = controller.SelectedMap;
    }

    public void SelectJapanMap()
    {
        RoomCreationController controller = EnsureRoomCreationController();
        controller.SelectJapanMap();
        selectedMap = controller.SelectedMap;
    }

    public void SelectForestMap()
    {
        RoomCreationController controller = EnsureRoomCreationController();
        controller.SelectForestMap();
        selectedMap = controller.SelectedMap;
    }

    public void SelectRandomMap()
    {
        RoomCreationController controller = EnsureRoomCreationController();
        controller.SelectRandomMap();
        selectedMap = controller.SelectedMap;
    }

    public void ClickCreateRoomReal()
    {
        RoomCreationController controller = EnsureRoomCreationController();
        controller.CreateRoom();
        selectedMap = controller.SelectedMap;
    }

    // 대기방
    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공! 대기방으로 화면 전환합니다.");
        ShowWaitingRoom();
    }

    void ShowWaitingRoom()
    {
        SetPanelState(showLogin: false, showRoomList: false, showCreateRoom: false, showPasswordPopup: false, showWaitingRoom: true);
        EnsureWaitingRoomController().InitializeWaitingRoom();
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
        EnsureWaitingRoomController().HandlePlayerPropertiesUpdate(changedProps);
    }

    void UpdatePlayerList()
    {
        EnsureWaitingRoomController().RefreshPlayerList();
    }

    void CheckStartButton()
    {
        EnsureWaitingRoomController().RefreshActionButtons();
    }

    void OnReadyButtonClicked()
    {
        EnsureWaitingRoomController().ToggleReady();
    }

    void OnStartButtonClicked()
    {
        StartCoroutine(EnsureMatchStartController().AssignRolesAndStart());
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

    WaitingRoomController EnsureWaitingRoomController()
    {
        if (waitingRoomController == null)
        {
            waitingRoomController = new WaitingRoomController(
                playerListGroup,
                playerSlotPrefab,
                waitingRoomNameText,
                selectedMapText,
                startButton,
                readyButton,
                IsReadyKey,
                SelectedMapKey);
        }

        return waitingRoomController;
    }

    RoomCreationController EnsureRoomCreationController()
    {
        if (roomCreationController == null)
        {
            roomCreationController = new RoomCreationController(
                roomNameInput,
                isPublicToggle,
                createPwdInput,
                selectedMap,
                mapList,
                PasswordKey,
                SelectedMapKey);
        }

        return roomCreationController;
    }

    LobbyController EnsureLobbyController()
    {
        if (lobbyController == null)
        {
            lobbyController = new LobbyController(
                nameInput,
                roomEntryPrefab,
                roomListContent,
                passwordPopupPanel,
                joinPwdInput,
                PasswordKey);
        }

        return lobbyController;
    }

    MatchStartController EnsureMatchStartController()
    {
        if (matchStartController == null)
        {
            matchStartController = new MatchStartController(RoleKey, SelectedMapKey);
        }

        return matchStartController;
    }
}