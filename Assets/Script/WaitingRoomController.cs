using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed class WaitingRoomController
{
    private readonly Transform playerListGroup;
    private readonly GameObject playerSlotPrefab;
    private readonly TextMeshProUGUI waitingRoomNameText;
    private readonly TextMeshProUGUI selectedMapText;
    private readonly Button startButton;
    private readonly Button readyButton;
    private readonly string isReadyKey;
    private readonly string selectedMapKey;

    public const string SeekerSelectionKey = "SelectedSeeker";

    private bool isReady;

    public WaitingRoomController(
        Transform playerListGroup,
        GameObject playerSlotPrefab,
        TextMeshProUGUI waitingRoomNameText,
        TextMeshProUGUI selectedMapText,
        Button startButton,
        Button readyButton,
        string isReadyKey,
        string selectedMapKey)
    {
        this.playerListGroup = playerListGroup;
        this.playerSlotPrefab = playerSlotPrefab;
        this.waitingRoomNameText = waitingRoomNameText;
        this.selectedMapText = selectedMapText;
        this.startButton = startButton;
        this.readyButton = readyButton;
        this.isReadyKey = isReadyKey;
        this.selectedMapKey = selectedMapKey;
    }

    public void InitializeWaitingRoom()
    {
        if (waitingRoomNameText != null && PhotonNetwork.CurrentRoom != null)
        {
            waitingRoomNameText.text = PhotonNetwork.CurrentRoom.Name;
        }

        if (selectedMapText != null && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(selectedMapKey))
        {
            string mapName = (string)PhotonNetwork.CurrentRoom.CustomProperties[selectedMapKey];
            selectedMapText.text = "맵 · " + mapName;
        }

        Hashtable props = new Hashtable { { isReadyKey, false } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        isReady = false;

        if (readyButton != null)
        {
            TextMeshProUGUI readyLabel = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (readyLabel != null)
            {
                readyLabel.text = "준비";
            }
        }

        RefreshPlayerList();
        RefreshActionButtons();
    }

    public void HandlePlayerPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(isReadyKey))
        {
            return;
        }

        RefreshPlayerList();
        RefreshActionButtons();
    }

    public void SelectSeeker(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 같은 플레이어 다시 클릭 시 선택 해제 (랜덤으로 복귀)
        object current;
        int currentSelected = -1;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SeekerSelectionKey, out current))
            currentSelected = (int)current;

        int newValue = (currentSelected == actorNumber) ? -1 : actorNumber;
        Hashtable props = new Hashtable { { SeekerSelectionKey, newValue } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    public void ToggleReady()
    {
        isReady = !isReady;

        Hashtable props = new Hashtable { { isReadyKey, isReady } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        if (readyButton == null)
        {
            return;
        }

        TextMeshProUGUI readyLabel = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (readyLabel != null)
        {
            readyLabel.text = isReady ? "준비 취소" : "준비";
        }
    }

    public void RefreshPlayerList()
    {
        if (playerListGroup == null)
        {
            Debug.LogError("playerListGroup이 할당되지 않았습니다.");
            return;
        }

        if (playerSlotPrefab == null)
        {
            Debug.LogError("playerSlotPrefab이 할당되지 않았습니다.");
            return;
        }

        foreach (Transform child in playerListGroup)
        {
            Object.Destroy(child.gameObject);
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject slot = Object.Instantiate(playerSlotPrefab, playerListGroup);
            if (slot == null)
            {
                Debug.LogError("playerSlotPrefab을 생성할 수 없습니다.");
                continue;
            }

            Transform nameTextTransform = slot.transform.Find("PlayerNameText");
            Transform readyTextTransform = slot.transform.Find("ReadyStateText");

            if (nameTextTransform == null || readyTextTransform == null)
            {
                Debug.LogError("PlayerSlot의 텍스트 오브젝트를 찾을 수 없습니다.");
                Object.Destroy(slot);
                continue;
            }

            TextMeshProUGUI nameText = nameTextTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readyText = readyTextTransform.GetComponent<TextMeshProUGUI>();

            if (nameText == null || readyText == null)
            {
                Debug.LogError("TextMeshProUGUI 컴포넌트를 찾을 수 없습니다.");
                Object.Destroy(slot);
                continue;
            }

            // 현재 선택된 술래 actorNumber 읽기
            int selectedSeekerActor = -1;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SeekerSelectionKey, out object selObj) && selObj is int selInt)
                selectedSeekerActor = selInt;

            bool isSelectedSeeker = selectedSeekerActor == player.ActorNumber;
            nameText.text = isSelectedSeeker
                ? $"<color=#FF6B6B>술래 · {player.NickName}</color>"
                : player.NickName;

            if (player.IsMasterClient)
            {
                readyText.text = "<color=#FFD54F>방장</color>";
            }
            else
            {
                bool playerReady = false;
                if (player.CustomProperties.TryGetValue(isReadyKey, out object isReadyObj) && isReadyObj is bool ready)
                    playerReady = ready;
                readyText.text = playerReady ? "<color=#7CFC8A>준비 완료</color>" : "<color=#B0BEC5>대기 중</color>";
            }

            ConfigureSeekerButton(slot.transform, player.ActorNumber, isSelectedSeeker);
        }
    }

    void ConfigureSeekerButton(Transform slotTransform, int actorNumber, bool isSelectedSeeker)
    {
        Transform seekerButtonTransform = slotTransform.Find("SeekerButton");
        if (seekerButtonTransform == null)
        {
            return;
        }

        bool showSeekerButton = PhotonNetwork.IsMasterClient;
        seekerButtonTransform.gameObject.SetActive(showSeekerButton);
        if (!showSeekerButton)
        {
            return;
        }

        Button seekerButton = seekerButtonTransform.GetComponent<Button>();
        Image buttonImage = seekerButtonTransform.GetComponent<Image>();
        TextMeshProUGUI buttonLabel = seekerButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonLabel != null)
        {
            buttonLabel.text = isSelectedSeeker ? "선택됨" : "술래 지정";
        }

        if (buttonImage != null)
        {
            buttonImage.color = isSelectedSeeker
                ? new Color(1f, 0.72f, 0.35f, 1f)
                : Color.white;
        }

        if (seekerButton != null)
        {
            seekerButton.onClick.RemoveAllListeners();
            seekerButton.onClick.AddListener(() => SelectSeeker(actorNumber));
        }
    }

    public void RefreshActionButtons()
    {
        if (startButton == null || readyButton == null)
        {
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            readyButton.gameObject.SetActive(false);
            startButton.gameObject.SetActive(true);
            startButton.interactable = AreAllNonMasterPlayersReady();
            return;
        }

        startButton.gameObject.SetActive(false);
        readyButton.gameObject.SetActive(true);
    }

    private bool AreAllNonMasterPlayersReady()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient)
            {
                continue;
            }

            if (!player.CustomProperties.TryGetValue(isReadyKey, out object isReadyObj) || !(isReadyObj is bool ready) || !ready)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class RoomCreationController
{
    private readonly TMP_InputField roomNameInput;
    private readonly Toggle isPublicToggle;
    private readonly TMP_InputField createPwdInput;
    private readonly string[] mapList;
    private readonly string passwordKey;
    private readonly string selectedMapKey;

    private string selectedMap;

    public string SelectedMap => selectedMap;

    public RoomCreationController(
        TMP_InputField roomNameInput,
        Toggle isPublicToggle,
        TMP_InputField createPwdInput,
        string initialSelectedMap,
        string[] mapList,
        string passwordKey,
        string selectedMapKey)
    {
        this.roomNameInput = roomNameInput;
        this.isPublicToggle = isPublicToggle;
        this.createPwdInput = createPwdInput;
        this.mapList = mapList;
        this.passwordKey = passwordKey;
        this.selectedMapKey = selectedMapKey;
        selectedMap = string.IsNullOrEmpty(initialSelectedMap) ? "CityScene" : initialSelectedMap;
    }

    public void SelectCityMap()
    {
        selectedMap = "CityScene";
        Debug.Log("CityScene 선택");
    }

    public void SelectJapanMap()
    {
        selectedMap = "WesternScene";
        Debug.Log("WesternScene 선택");
    }

    public void SelectForestMap()
    {
        selectedMap = "CityMapScene";
        Debug.Log("CityMapScene 선택");
    }

    public void SelectRandomMap()
    {
        if (mapList == null || mapList.Length == 0)
        {
            selectedMap = "CityScene";
            Debug.LogWarning("맵 목록이 비어 있어서 기본 맵으로 설정됩니다.");
            return;
        }

        int randomIndex = Random.Range(0, mapList.Length);
        selectedMap = mapList[randomIndex];
        Debug.Log("랜덤 맵 선택: " + selectedMap);
    }

    public void CreateRoom()
    {
        if (roomNameInput == null || string.IsNullOrEmpty(roomNameInput.text))
        {
            return;
        }

        RoomOptions options = new RoomOptions { MaxPlayers = 8 };
        options.CustomRoomPropertiesForLobby = new[] { passwordKey, selectedMapKey };

        Hashtable roomProps = new Hashtable();
        bool isPublic = isPublicToggle != null && isPublicToggle.isOn;
        string password = isPublic ? string.Empty : (createPwdInput != null ? createPwdInput.text : string.Empty);

        roomProps.Add(passwordKey, password);
        roomProps.Add(selectedMapKey, selectedMap);

        options.CustomRoomProperties = roomProps;
        PhotonNetwork.CreateRoom(roomNameInput.text, options);
    }
}

public sealed class LobbyController
{
    private const string PublicRoomLabel = "[public]";
    private const string PrivateRoomLabel = "[private]";

    private readonly TMP_InputField nameInput;
    private readonly GameObject roomEntryPrefab;
    private readonly Transform roomListContent;
    private readonly GameObject passwordPopupPanel;
    private readonly TMP_InputField joinPwdInput;
    private readonly string passwordKey;

    private string targetRoomName = string.Empty;
    private string targetRoomPassword = string.Empty;

    public LobbyController(
        TMP_InputField nameInput,
        GameObject roomEntryPrefab,
        Transform roomListContent,
        GameObject passwordPopupPanel,
        TMP_InputField joinPwdInput,
        string passwordKey)
    {
        this.nameInput = nameInput;
        this.roomEntryPrefab = roomEntryPrefab;
        this.roomListContent = roomListContent;
        this.passwordPopupPanel = passwordPopupPanel;
        this.joinPwdInput = joinPwdInput;
        this.passwordKey = passwordKey;
    }

    public bool TrySetNickname()
    {
        if (nameInput == null || string.IsNullOrEmpty(nameInput.text))
        {
            return false;
        }

        PhotonNetwork.NickName = nameInput.text;
        return true;
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        if (roomListContent == null || roomEntryPrefab == null)
        {
            Debug.LogError("roomListContent 또는 roomEntryPrefab이 할당되지 않았습니다.");
            return;
        }

        foreach (Transform child in roomListContent)
        {
            Object.Destroy(child.gameObject);
        }

        foreach (RoomInfo room in roomList)
        {
            if (!room.IsOpen || !room.IsVisible || room.RemovedFromList)
            {
                continue;
            }

            GameObject entry = Object.Instantiate(roomEntryPrefab, roomListContent, false);
            entry.transform.localScale = Vector3.one;

            bool hasPassword = false;
            string roomPassword = string.Empty;
            if (room.CustomProperties.ContainsKey(passwordKey))
            {
                roomPassword = (string)room.CustomProperties[passwordKey];
                hasPassword = !string.IsNullOrEmpty(roomPassword);
            }

            Transform roomNameTransform = entry.transform.Find("RoomNameText");
            Transform roomInfoTransform = entry.transform.Find("RoomInfoText");
            if (roomNameTransform == null || roomInfoTransform == null)
            {
                Object.Destroy(entry);
                continue;
            }

            TextMeshProUGUI nameText = roomNameTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI infoText = roomInfoTransform.GetComponent<TextMeshProUGUI>();
            if (nameText == null || infoText == null)
            {
                Object.Destroy(entry);
                continue;
            }

            nameText.text = room.Name;
            infoText.text = hasPassword
                ? $"{PrivateRoomLabel} {room.PlayerCount}/{room.MaxPlayers}"
                : $"{PublicRoomLabel} {room.PlayerCount}/{room.MaxPlayers}";

            Button joinBtn = entry.GetComponent<Button>();
            if (joinBtn == null)
            {
                continue;
            }

            joinBtn.onClick.AddListener(() =>
            {
                if (hasPassword)
                {
                    targetRoomName = room.Name;
                    targetRoomPassword = roomPassword;
                    if (passwordPopupPanel != null)
                    {
                        passwordPopupPanel.SetActive(true);
                    }
                }
                else
                {
                    PhotonNetwork.JoinRoom(room.Name);
                }
            });
        }
    }

    public void CancelPasswordPopup()
    {
        if (passwordPopupPanel != null)
        {
            passwordPopupPanel.SetActive(false);
        }

        if (joinPwdInput != null)
        {
            joinPwdInput.text = string.Empty;
        }
    }

    public void TryJoinPasswordRoom()
    {
        if (joinPwdInput == null)
        {
            return;
        }

        if (joinPwdInput.text == targetRoomPassword)
        {
            PhotonNetwork.JoinRoom(targetRoomName);
            if (passwordPopupPanel != null)
            {
                passwordPopupPanel.SetActive(false);
            }

            joinPwdInput.text = string.Empty;
            return;
        }

        Debug.LogWarning("비밀번호가 일치하지 않습니다.");
        joinPwdInput.text = string.Empty;
    }
}

public sealed class MatchStartController
{
    private readonly string roleKey;
    private readonly string selectedMapKey;

    public MatchStartController(string roleKey, string selectedMapKey)
    {
        this.roleKey = roleKey;
        this.selectedMapKey = selectedMapKey;
    }

    private bool isStarting = false;

    public System.Collections.IEnumerator AssignRolesAndStart()
    {
        if (PhotonNetwork.CurrentRoom == null) yield break;
        if (isStarting) yield break; // 중복 호출 방지
        isStarting = true;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        Player[] players = PhotonNetwork.PlayerList;

        // 방장이 술래를 직접 선택했는지 확인
        int selectedSeekerActor = -1;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(WaitingRoomController.SeekerSelectionKey, out object selObj) && selObj is int selInt)
            selectedSeekerActor = selInt;

        // 선택된 술래가 없거나 유효하지 않으면 랜덤
        bool validSelection = false;
        foreach (var p in players) if (p.ActorNumber == selectedSeekerActor) { validSelection = true; break; }
        if (!validSelection) selectedSeekerActor = players[Random.Range(0, players.Length)].ActorNumber;

        for (int i = 0; i < players.Length; i++)
        {
            Hashtable props = new Hashtable();
            props.Add(roleKey, players[i].ActorNumber == selectedSeekerActor ? "Seeker" : "Survivor");
            players[i].SetCustomProperties(props);
        }

        yield return new WaitForSeconds(0.5f);

        string mapToLoad = "CityScene"; // 기본 폴백
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(selectedMapKey, out object mapObj) && mapObj != null)
            mapToLoad = (string)mapObj;
        else
            Debug.LogWarning("[MatchStart] 맵 키를 찾지 못했습니다. 기본 맵(CityScene)으로 로드합니다.");
        PhotonNetwork.LoadLevel(mapToLoad);
    }
}
