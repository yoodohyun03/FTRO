using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Photon.Pun;

public class InGameMenuController : MonoBehaviourPunCallbacks
{
    private static InGameMenuController _instance;
    public static InGameMenuController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindFirstObjectByType<InGameMenuController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InGameMenuController");
                    _instance = go.AddComponent<InGameMenuController>();
                }
            }
            return _instance;
        }
    }

    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject optionsMenuPanel;

    private readonly HashSet<string> inGameScenes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "CityMapScene",
        "CityScene",
        "WesternScene"
    };

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("[InGameMenu] Duplicate instance found, destroying new one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[InGameMenu] Singleton instance initialized.");

        // Canvas 설정 강화: 항상 최상단에 위치하도록
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
        }

        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (pauseMenuPanel == null) pauseMenuPanel = transform.Find("PauseMenuPanel")?.gameObject;
        if (optionsMenuPanel == null) optionsMenuPanel = transform.Find("OptionsMenuPanel")?.gameObject;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsMenuPanel != null) optionsMenuPanel.SetActive(false);

        SetupButtonEvents();
        Debug.Log($"[InGameMenu] References initialized. Pause: {pauseMenuPanel != null}, Options: {optionsMenuPanel != null}");
    }

    private void SetupButtonEvents()
    {
        if (pauseMenuPanel != null)
        {
            var resumeBtn = pauseMenuPanel.transform.Find("ResumeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (resumeBtn != null)
            {
                resumeBtn.onClick.RemoveAllListeners();
                resumeBtn.onClick.AddListener(ResumeGame);
            }

            var optionsBtn = pauseMenuPanel.transform.Find("OptionsButton")?.GetComponent<UnityEngine.UI.Button>();
            if (optionsBtn != null)
            {
                optionsBtn.onClick.RemoveAllListeners();
                optionsBtn.onClick.AddListener(OpenOptions);
            }

            var quitBtn = pauseMenuPanel.transform.Find("QuitButton")?.GetComponent<UnityEngine.UI.Button>();
            if (quitBtn != null)
            {
                quitBtn.onClick.RemoveAllListeners();
                quitBtn.onClick.AddListener(QuitToTitle);
            }
        }

        if (optionsMenuPanel != null)
        {
            var backBtn = optionsMenuPanel.transform.Find("BackButton")?.GetComponent<UnityEngine.UI.Button>();
            if (backBtn != null)
            {
                backBtn.onClick.RemoveAllListeners();
                backBtn.onClick.AddListener(CloseOptions);
            }
        }
    }

    public void ResumeGame()
    {
        Debug.Log("[InGameMenu] ResumeGame requested.");
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsMenuPanel != null) optionsMenuPanel.SetActive(false);
        
        // UI 포커스 해제 (PlayerMove의 이동 차단 방지)
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        if (IsInGameScene())
        {
            // 인게임일 경우 즉시 커서 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[InGameMenu] Cursor locked via ResumeGame.");
        }
    }

    public void OpenOptions()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsMenuPanel != null) optionsMenuPanel.SetActive(true);
        UpdateCursorState();
    }

    public void CloseOptions()
    {
        if (optionsMenuPanel != null) optionsMenuPanel.SetActive(false);
        if (IsInGameScene())
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        }
        UpdateCursorState();
    }

    public void QuitToTitle()
    {
        Debug.Log("[InGameMenu] Quit to Lobby/Login requested.");
        
        // 메뉴 패널 즉시 비활성화 및 커서 해제
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsMenuPanel != null) optionsMenuPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene("TitleScene");
            return;
        }

        // 방장(MasterClient)인 경우: 모든 플레이어를 데리고 대기실(TitleScene의 WaitingRoomPanel)로 이동
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[InGameMenu] Host is returning to Waiting Room. Bringing everyone along.");
            // AutomaticallySyncScene이 true이므로 LoadLevel 호출 시 모든 인원이 함께 TitleScene으로 이동합니다.
            // TitleScene 로드 후 TitleManager.Start()에서 InRoom 체크를 통해 WaitingRoomPanel이 열립니다.
            PhotonNetwork.LoadLevel("TitleScene");
        }
        // 일반 플레이어인 경우: 혼자 방을 나가서 초기 로그인 화면으로 이동
        else
        {
            Debug.Log("[InGameMenu] Client is leaving room and returning to Login.");
            PhotonNetwork.LeaveRoom();
        }
    }

    public override void OnLeftRoom()
    {
        // 일반 플레이어가 방을 나갔을 때 호출 (TitleManager에서 InRoom이 false이므로 로그인 창이 뜹니다)
        SceneManager.LoadScene("TitleScene");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    private void HandleEscapeKey()
    {
        // 1. 채팅 입력 중이면 메뉴를 열지 않고 채팅 포커스만 해제함 (충돌 방지)
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null)
        {
            var inputField = es.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>();
            if (inputField is TMPro.TMP_InputField || inputField is UnityEngine.UI.InputField)
            {
                // 포커스만 해제하고 리턴
                es.SetSelectedGameObject(null);
                Debug.Log("[InGameMenu] ESC ignored: InputField was focused.");
                return;
            }
        }

        if (pauseMenuPanel == null || optionsMenuPanel == null)
        {
            // 참조가 누락된 경우 다시 찾기 시도
            InitializeReferences();
            if (pauseMenuPanel == null || optionsMenuPanel == null) return;
        }

        bool isInGame = IsInGameScene();
        Debug.Log($"[InGameMenu] ESC Pressed. InGame: {isInGame}, OptionsActive: {optionsMenuPanel.activeSelf}");

        if (optionsMenuPanel.activeSelf)
        {
            CloseOptions();
        }
        else
        {
            if (isInGame)
            {
                bool targetState = !pauseMenuPanel.activeSelf;
                pauseMenuPanel.SetActive(targetState);
                Debug.Log($"[InGameMenu] Toggle PausePanel: {targetState}");
            }
            else
            {
                // 인게임이 아닐 때도 ESC로 옵션을 켜고 끌 수 있게 처리
                optionsMenuPanel.SetActive(true);
            }
        }

        // 인게임 여부와 상관없이 커서 상태를 항상 업데이트
        UpdateCursorState();
    }

    private void UpdateCursorState()
    {
        bool isAnyPanelActive = (pauseMenuPanel != null && pauseMenuPanel.activeSelf) || 
                               (optionsMenuPanel != null && optionsMenuPanel.activeSelf);

        if (isAnyPanelActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 모든 메뉴가 닫혔을 때 UI 포커스 강제 해제
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            if (IsInGameScene())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        Debug.Log($"[InGameMenu] Cursor Updated. Lock: {Cursor.lockState}, Visible: {Cursor.visible}");
    }

    private bool IsInGameScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        return inGameScenes.Contains(currentSceneName);
    }
}
