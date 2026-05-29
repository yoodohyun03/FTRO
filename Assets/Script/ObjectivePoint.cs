using UnityEngine;
using Photon.Pun;

public class ObjectivePoint : MonoBehaviourPunCallbacks, IPunObservable
{
    public float interactionTime = 10f; 
    public bool isCompleted = false;
    public float currentProgress = 0f;

    private SpriteRenderer minimapIcon;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentProgress);
            stream.SendNext(isCompleted);
        }
        else
        {
            currentProgress = (float)stream.ReceiveNext();
            isCompleted = (bool)stream.ReceiveNext();
            
            if (isCompleted && minimapIcon != null)
            {
                minimapIcon.color = Color.gray;
            }
        }
    }

    void Start()
    {
        CreateMinimapIcon();
    }

    void CreateMinimapIcon()
    {
        GameObject iconObj = new GameObject("MinimapIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.transform.localPosition = Vector3.up * 7f; 
        iconObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        iconObj.layer = 7; 

        minimapIcon = iconObj.AddComponent<SpriteRenderer>();
        minimapIcon.sprite = Resources.Load<Sprite>("TerminalIcon_New");
        minimapIcon.color = Color.green;
        iconObj.transform.localScale = Vector3.one * 15f; 
    }

    // 일반 등급 아이템 (60% 확률)
    static readonly SurvivorItemType[] CommonItems = { SurvivorItemType.Sprint, SurvivorItemType.Smoke, SurvivorItemType.MarkerJam };
    // 희귀 등급 아이템 (40% 확률)
    static readonly SurvivorItemType[] RareItems   = { SurvivorItemType.EMP, SurvivorItemType.Hack, SurvivorItemType.Decoy };

    [PunRPC]
    public void RPC_AddProgress(float amount, PhotonMessageInfo info)
    {
        if (isCompleted) return;

        currentProgress += amount;
        if (currentProgress >= interactionTime)
        {
            currentProgress = interactionTime;
            isCompleted = true;
            if (minimapIcon != null) minimapIcon.color = Color.gray;
            Debug.Log("[Objective] Terminal Completed!");

            if (PhotonNetwork.IsMasterClient && GameManager.instance != null)
            {
                GameManager.instance.photonView.RPC("OnObjectiveCompleted", RpcTarget.All);

                // 터미널을 완료한 생존자에게 랜덤 아이템 지급
                SurvivorItemType[] pool = Random.value < 0.6f ? CommonItems : RareItems;
                SurvivorItemType   grant = pool[Random.Range(0, pool.Length)];
                photonView.RPC("RPC_GrantItem", info.Sender, (int)grant);
                Debug.Log($"[Objective] 아이템 지급 → {info.Sender.NickName}: {grant}");
            }
        }
    }

    [PunRPC]
    public void RPC_GrantItem(int itemTypeInt)
    {
        // 이 RPC는 완료한 플레이어의 클라이언트에서만 실행됨
        SurvivorItemHandler handler = null;
        foreach (var h in Object.FindObjectsByType<SurvivorItemHandler>(FindObjectsSortMode.None))
        {
            if (h.GetComponent<PhotonView>() != null && h.GetComponent<PhotonView>().IsMine)
            {
                handler = h;
                break;
            }
        }
        if (handler != null)
            handler.ReceiveItem(itemTypeInt);
        else
            Debug.LogWarning("[Objective] SurvivorItemHandler를 찾지 못했습니다.");
    }

    [PunRPC]
    public void RPC_SetCompleted(bool state)
    {
        isCompleted = state;
        if (state) 
        {
            currentProgress = interactionTime;
            if (minimapIcon != null) minimapIcon.color = Color.gray;
        }
    }
}
