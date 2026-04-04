using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic; // 🌟 [추가] 리스트(List) 기능을 쓰기 위해 필수!

public class ChatManager : MonoBehaviourPun
{
    [Header("UI 연결")]
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatLog;

    // 🌟 [추가] 채팅 줄 수 제한 설정! (회색 패널 크기에 맞춰서 숫자 조절하십쇼)
    [Header("채팅 설정")]
    public int maxMessages = 8; // 최대 8줄까지만 표시!
    private List<string> messageList = new List<string>(); // 채팅 내역을 담을 바구니

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
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!chatInput.isFocused)
            {
                chatInput.ActivateInputField();
            }
        }
    }

    public void SendChatMessage()
    {
        string msg = chatInput.text;

        if (!string.IsNullOrWhiteSpace(msg))
        {
            string nickName = PhotonNetwork.NickName;
            if (string.IsNullOrEmpty(nickName)) nickName = "무명";

            photonView.RPC("RPC_ReceiveChat", RpcTarget.All, nickName, msg);
        }

        chatInput.text = "";
        chatInput.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);
    }

    // ==========================================
    // 📡 [수정됨] 메시지 수신 및 '밀어내기' 로직
    // ==========================================
    [PunRPC]
    void RPC_ReceiveChat(string sender, string message)
    {
        // 1. 새 메시지를 예쁘게 포장해서 바구니(List)에 담습니다.
        string newMsg = $"<color=yellow>[{sender}]</color> : {message}";
        messageList.Add(newMsg);

        // 2. 만약 바구니에 담긴 메시지가 최대 개수(8개)를 넘었다면?
        if (messageList.Count > maxMessages)
        {
            // 3. 제일 오래된 첫 번째 메시지(Index 0)를 가차 없이 버립니다!
            messageList.RemoveAt(0);
        }

        // 4. 바구니에 남은 메시지들을 줄바꿈(\n)으로 쫙 이어서 텍스트창에 뿌려줍니다!
        chatLog.text = string.Join("\n", messageList);
    }
}