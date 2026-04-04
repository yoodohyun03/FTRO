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
    public bool isLobbyScene = false; // 🌟 유니티에서 로비 씬일 때만 체크(V) 하십쇼!

    // 🌟 [핵심 해결책] 엔터키 중복 인식(레이스 컨디션)을 막는 방어막
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
        // 🌟 [절대 방어막] 인스펙터에서 로비라고 체크를 해뒀다면?
        // 다른 스크립트가 마우스를 훔쳐 가려 해도, 매 프레임마다 강제로 마우스를 끄집어냅니다!
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

                // 로비가 아닐 때(본 게임)만 마우스 상태를 변경합니다.
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
        string msg = chatInput.text;

        if (!string.IsNullOrWhiteSpace(msg))
        {
            // 로비에서 아직 방에 안 들어갔는데 타자를 쳤다면? (에러 방지)
            if (!PhotonNetwork.InRoom)
            {
                string sysMsg = "<color=red>[시스템] 방에 입장해야 채팅이 가능합니다.</color>";
                messageList.Add(sysMsg);
                if (messageList.Count > maxMessages) messageList.RemoveAt(0);
                chatLog.text = string.Join("\n", messageList);

                chatInput.text = "";
                chatInput.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                return; // 여기서 멈춤!
            }

            // 정상 전송 로직
            string nickName = PhotonNetwork.NickName;
            if (string.IsNullOrEmpty(nickName)) nickName = "무명";

            photonView.RPC("RPC_ReceiveChat", RpcTarget.All, nickName, msg);
        }

        // 전송 끝! 창 닫기
        chatInput.text = "";
        chatInput.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null); // UI 선택 완벽 해제

        // 🌟 로비가 아닐 때(게임 중일 때)만 마우스를 다시 화면에 가둡니다!
        if (!isLobbyScene)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 🌟 [핵심 방어막] 전송 완료 직후, 0.1초 동안 엔터키를 무시하도록 쿨타임을 줍니다!
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