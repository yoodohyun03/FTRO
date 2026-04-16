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
    private List<string> messageList = new List<string>();

    [Header("씬 설정")]
    public bool isLobbyScene = false; // 로비 씬일 때만 체크

    // 엔터키 중복 인식 방지
    private bool justSent = false;

    void Start()
    {
        if (chatLog != null) chatLog.text = "";

        if (chatInput != null)
        {
            chatInput.onSubmit.AddListener(delegate { SendChatMessage(); });
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