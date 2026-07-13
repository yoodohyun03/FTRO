using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class ObjectivePoint : MonoBehaviourPunCallbacks, IPunObservable
{
    public float interactionTime = 10f; 
    public bool isCompleted = false;
    public float currentProgress = 0f;

    private SpriteRenderer minimapIcon;
    private Transform minimapCamTransform;

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
        // Find Minimap Camera to match rotation
        MinimapFollow mf = Object.FindFirstObjectByType<MinimapFollow>();
        if (mf != null) minimapCamTransform = mf.transform;

        CreateMinimapIcon();
    }

    void Update()
    {
        // Sync rotation with Minimap Camera so it's always "upright" on the map
        if (minimapCamTransform != null && minimapIcon != null)
        {
            float camY = minimapCamTransform.eulerAngles.y;
            minimapIcon.transform.rotation = Quaternion.Euler(90f, camY, 0f);
        }
    }

    void CreateMinimapIcon()
    {
        GameObject iconObj = new GameObject("MinimapIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.transform.localPosition = Vector3.up * 20f; // Higher up to be above buildings
        iconObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        iconObj.layer = 7; 

        minimapIcon = iconObj.AddComponent<SpriteRenderer>();
        minimapIcon.sprite = Resources.Load<Sprite>("TerminalIcon_New");
        minimapIcon.color = Color.white; // Keep neon colors
        iconObj.transform.localScale = Vector3.one * 20f; 
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

            if (PhotonNetwork.IsMasterClient && GameManager.instance != null &&
                GameModeTypeHelper.UsesObjectives(GameManager.CurrentGameMode))
            {
                GameManager.instance.NotifyTerminalCompleted();

                SurvivorItemType[] pool = Random.value < 0.6f ? CommonItems : RareItems;
                SurvivorItemType grant = pool[Random.Range(0, pool.Length)];
                StartCoroutine(GrantItemNextFrame(info.Sender, grant));
            }
        }
    }

    IEnumerator GrantItemNextFrame(Player target, SurvivorItemType grant)
    {
        yield return null;
        photonView.RPC("RPC_GrantItem", target, (int)grant);
        Debug.Log($"[Objective] 아이템 지급 → {target.NickName}: {grant}");
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
