using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ChatManager : MonoBehaviourPun
{
    [Header("UI 연결")]
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatLog;

    [Header("채팅 설정")]
    public int maxMessages = 8;
    [SerializeField] private int lobbyChatFontSize = 34;
    [SerializeField] private int gameChatFontSize = 30;
    [SerializeField] private int lobbyInputFontSize = 30;
    [SerializeField] private int gameInputFontSize = 28;
    private List<string> messageList = new List<string>();

    [Header("로비 채팅 레이아웃")]
    [SerializeField] private float lobbyInputHeight = 48f;
    [SerializeField] private float lobbyInputBottomMargin = 24f;
    [SerializeField] private float lobbyInputSideMargin = 28f;
    [SerializeField] private float lobbyLogGapAboveInput = 12f;
    [SerializeField] private float lobbyLogSideMargin = 10f;
    [SerializeField] private float lobbyLogTopMargin = 8f;

    [Header("씬 설정")]
    public bool isLobbyScene = false; // 로비 씬일 때만 체크

    // 엔터키 중복 인식 방지
    private bool justSent = false;

    void Start()
    {
        if (chatLog != null) chatLog.text = "";
        ApplyChatTypography();

        if (chatInput != null)
        {
            chatInput.onSubmit.AddListener(delegate { SendChatMessage(); });
        }
    }

    void ApplyChatTypography()
    {
        if (isLobbyScene)
        {
            ApplyLobbyChatLayout();
        }

        int logSize = isLobbyScene ? lobbyChatFontSize : gameChatFontSize;
        int inputSize = isLobbyScene ? lobbyInputFontSize : gameInputFontSize;

        if (chatLog != null)
        {
            chatLog.fontSize = logSize;
            chatLog.enableAutoSizing = false;
            if (isLobbyScene)
            {
                chatLog.overflowMode = TextOverflowModes.Overflow;
            }
        }

        if (chatInput == null) return;

        if (chatInput.textComponent != null)
        {
            chatInput.textComponent.fontSize = inputSize;
            chatInput.textComponent.enableAutoSizing = false;
        }

        if (chatInput.placeholder is TMP_Text placeholderText)
        {
            placeholderText.fontSize = inputSize;
            placeholderText.enableAutoSizing = false;
        }
    }

    void ApplyLobbyChatLayout()
    {
        if (chatLog == null || chatInput == null) return;

        RectTransform panel = chatLog.transform.parent as RectTransform;
        if (panel == null) return;

        panel.localScale = Vector3.one;

        float logBottom = lobbyInputBottomMargin + lobbyInputHeight + lobbyLogGapAboveInput;

        RectTransform logRect = chatLog.rectTransform;
        logRect.anchorMin = Vector2.zero;
        logRect.anchorMax = Vector2.one;
        logRect.pivot = new Vector2(0.5f, 0.5f);
        logRect.offsetMin = new Vector2(lobbyLogSideMargin, logBottom);
        logRect.offsetMax = new Vector2(-lobbyLogSideMargin, -lobbyLogTopMargin);

        RectTransform inputRect = chatInput.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.anchoredPosition = new Vector2(0f, lobbyInputBottomMargin);
        inputRect.sizeDelta = new Vector2(-lobbyInputSideMargin * 2f, lobbyInputHeight);

        if (chatInput.textViewport != null)
        {
            RectTransform viewport = chatInput.textViewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(10f, 6f);
            viewport.offsetMax = new Vector2(-10f, -6f);
        }
    }

    void Update()
    {
        if (chatInput == null) return;

        // 로비 씬에서는 커서를 항상 표시
        if (isLobbyScene)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 엔터키를 눌렀을 때
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (justSent) return;

            // 채팅창이 꺼져있을 때 엔터를 누르면?
            if (!chatInput.isFocused)
            {
                chatInput.ActivateInputField();

                // 본 게임에서만 마우스 상태 변경
                if (!isLobbyScene)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }
    }

    public void SendChatMessage()
    {
        if (chatInput == null || chatLog == null) return;

        string msg = chatInput.text;

        if (!string.IsNullOrWhiteSpace(msg))
        {
            // 방 미입장 상태에서 전송 시 안내 메시지 출력
            if (!PhotonNetwork.InRoom)
            {
                string sysMsg = "<color=red>[시스템] 방에 입장해야 채팅이 가능합니다.</color>";
                messageList.Add(sysMsg);
                if (messageList.Count > maxMessages) messageList.RemoveAt(0);
                chatLog.text = string.Join("\n", messageList);

                chatInput.text = "";
                chatInput.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            // 정상 전송 로직
            string nickName = PhotonNetwork.NickName;
            if (string.IsNullOrEmpty(nickName)) nickName = "무명";

            photonView.RPC("RPC_ReceiveChat", RpcTarget.All, nickName, msg);
        }

        // 전송 후 입력창 종료
        chatInput.text = "";
        chatInput.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);

        // 본 게임에서만 마우스를 다시 잠금
        if (!isLobbyScene)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 전송 직후 짧은 쿨타임 적용
        justSent = true;
        Invoke("ResetJustSent", 0.1f);
    }

    // 0.1초 뒤에 다시 엔터키를 쓸 수 있게 풀어주는 함수
    void ResetJustSent()
    {
        justSent = false;
    }

    [PunRPC]
    void RPC_ReceiveChat(string sender, string message)
    {
        string newMsg = $"<color=yellow>[{sender}]</color> : {message}";
        messageList.Add(newMsg);

        if (messageList.Count > maxMessages)
        {
            messageList.RemoveAt(0);
        }

        chatLog.text = string.Join("\n", messageList);
    }
}