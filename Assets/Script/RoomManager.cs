using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
// 🌟 [핵심] 포톤 전용 해시테이블을 쓰기 위한 선언입니다!
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("UI 연결 (패널/텍스트)")]
    public GameObject roomPanel;        // 대기방 전체 화면
    public Transform playerListGroup;   // 명단이 꽂힐 빈 공간 (Vertical Layout Group 달린 곳)
    public GameObject playerSlotPrefab; // 아까 만든 1칸짜리 프리팹
    public TextMeshProUGUI roomNameText;// 방 제목

    [Header("버튼 연결")]
    public Button startButton;          // 방장용
    public Button readyButton;          // 손님용
    public Button leaveButton;

    private bool isReady = false;       // 내 레디 상태

    void Start()
    {
        if (roomPanel != null) roomPanel.SetActive(false);

        startButton.onClick.AddListener(OnStartButtonClicked);
        readyButton.onClick.AddListener(OnReadyButtonClicked);
        leaveButton.onClick.AddListener(OnLeaveButtonClicked);

        // 🌟 [핵심 해결책 추가] 
        // 씬이 시작될 때 '이미 방에 들어와 있는 상태'라면?!
        // OnJoinedRoom()이 안 터지니까, 여기서 수동으로 바로 세팅을 켜줍니다!
        if (PhotonNetwork.InRoom)
        {
            roomPanel.SetActive(true);
            if (roomNameText != null) roomNameText.text = PhotonNetwork.CurrentRoom.Name;

            isReady = false;

            // 명단 그리고 버튼 검사하기!
            UpdatePlayerList();
            CheckStartButton();
        }
    }

    // ==========================================
    // 🚪 1. 방 입장 / 퇴장
    // ==========================================
    public override void OnJoinedRoom()
    {
        roomPanel.SetActive(true); // 대기방 화면 켜기!
        roomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // 방에 들어오면 내 레디 상태를 'false'로 포톤 서버에 등록합니다.
        Hashtable props = new Hashtable() { { "IsReady", false } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        isReady = false;

        UpdatePlayerList();
        CheckStartButton();
    }

    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false); // 방 나가면 대기방 화면 끄기
    }

    // ==========================================
    // 🔄 2. 명단 갱신 (누가 들어오고, 나가고, 레디할 때마다 실행)
    // ==========================================
    public override void OnPlayerEnteredRoom(Player newPlayer) { UpdatePlayerList(); CheckStartButton(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { UpdatePlayerList(); CheckStartButton(); }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // 누군가 'IsReady' 상태를 바꿨을 때만 화면을 새로고침합니다.
        if (changedProps.ContainsKey("IsReady"))
        {
            UpdatePlayerList();
            CheckStartButton();
        }
    }

    // 방장이 튕겨서 방장이 바뀌었을 때
    public override void OnMasterClientSwitched(Player newMasterClient) { UpdatePlayerList(); CheckStartButton(); }

    // 명단 다시 그리기 핵심 함수
    void UpdatePlayerList()
    {
        // 1. 기존에 그려진 명단을 싹 청소합니다.
        foreach (Transform child in playerListGroup)
        {
            Destroy(child.gameObject);
        }

        // 2. 지금 방에 있는 사람 수만큼 새로 그립니다.
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject slot = Instantiate(playerSlotPrefab, playerListGroup);

            // 🌟 주의: 프리팹 안의 텍스트 이름이 정확히 일치해야 찾을 수 있습니다!
            TextMeshProUGUI nameText = slot.transform.Find("PlayerNameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readyText = slot.transform.Find("ReadyStateText").GetComponent<TextMeshProUGUI>();

            nameText.text = $"[{player.NickName}]";

            if (player.IsMasterClient)
            {
                readyText.text = "<color=yellow>[방장]</color>";
            }
            else
            {
                // 포톤 서버에서 이 사람의 IsReady 값을 가져옵니다.
                bool playerReady = false;
                if (player.CustomProperties.TryGetValue("IsReady", out object isReadyObj))
                {
                    playerReady = (bool)isReadyObj;
                }

                if (playerReady) readyText.text = "<color=green>Ready!</color>";
                else readyText.text = "<color=gray>대기 중...</color>";
            }
        }
    }

    // ==========================================
    // 👑 3. 권한 관리 및 버튼 클릭
    // ==========================================
    void CheckStartButton()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 방장이면: 레디 숨기고, 스타트 보이기
            readyButton.gameObject.SetActive(false);
            startButton.gameObject.SetActive(true);

            // 다른 손님들이 모두 레디했는지 검사
            bool allReady = true;
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                {
                    player.CustomProperties.TryGetValue("IsReady", out object isReadyObj);
                    if (isReadyObj == null || !(bool)isReadyObj)
                    {
                        allReady = false;
                        break; // 한 명이라도 레디 안 했으면 컷!
                    }
                }
            }
            // 모두 레디했을 때만 시작 버튼 불이 켜집니다! (상호작용 가능)
            startButton.interactable = allReady;
        }
        else
        {
            // 손님이면: 스타트 숨기고, 레디 보이기
            startButton.gameObject.SetActive(false);
            readyButton.gameObject.SetActive(true);
        }
    }

    void OnReadyButtonClicked()
    {
        isReady = !isReady; // 클릭할 때마다 상태 뒤집기 (토글)

        // 내 바뀐 상태를 포톤 서버로 쏩니다!
        Hashtable props = new Hashtable() { { "IsReady", isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // 버튼 글씨 변경
        TextMeshProUGUI btnText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        btnText.text = isReady ? "Cancel" : "Ready";
    }

    void OnStartButtonClicked()
    {
        // 🌟 방장이 시작 누름! 더 이상 사람 못 들어오게 방을 잠급니다.
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // 🌟 형님의 진짜 게임 씬(숨바꼭질 씬) 이름으로 꼭 바꿔주십쇼!
        PhotonNetwork.LoadLevel("GameScene");
    }

    void OnLeaveButtonClicked()
    {
        PhotonNetwork.LeaveRoom();
    }
}