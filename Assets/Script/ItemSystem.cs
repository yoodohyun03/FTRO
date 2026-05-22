using UnityEngine;
using Photon.Pun;
using System.Collections;

public enum ItemType { Flashbang, Spray, Invisibility }

public class ItemSystem : MonoBehaviourPun
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";

    public ItemType currentItem;
    public bool hasItem = false;

    private PlayerMove playerMove;

    void Start()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (hasItem && Input.GetKeyDown(KeyCode.Q))
        {
            UseItem();
        }
    }

    [PunRPC]
    public void RPC_GiveItem(ItemType type)
    {
        currentItem = type;
        hasItem = true;
        Debug.Log($"[ItemSystem] Received item: {type}");
    }

    void UseItem()
    {
        switch (currentItem)
        {
            case ItemType.Flashbang:
                StartCoroutine(UseFlashbang());
                break;
            case ItemType.Spray:
                UseSpray();
                break;
            case ItemType.Invisibility:
                StartCoroutine(UseInvisibility());
                break;
        }

        hasItem = false;
    }

    IEnumerator UseFlashbang()
    {
        Debug.Log("[ItemSystem] Using Flashbang!");
        photonView.RPC("RPC_FlashbangEffect", RpcTarget.All, transform.position);
        yield return null;
    }

    void UseSpray()
    {
        Debug.Log("[ItemSystem] Using Spray!");
        PhotonNetwork.Instantiate("SprayArea", transform.position, Quaternion.identity);
    }

    IEnumerator UseInvisibility()
    {
        Debug.Log("[ItemSystem] Using Invisibility!");
        photonView.RPC("RPC_SetInvisibility", RpcTarget.All, true);
        yield return new WaitForSeconds(5f);
        photonView.RPC("RPC_SetInvisibility", RpcTarget.All, false);
    }

    [PunRPC]
    void RPC_FlashbangEffect(Vector3 pos)
    {
        float range = 10f;
        float dist = Vector3.Distance(Camera.main.transform.position, pos);
        
        // Only seeker gets blinded
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey) && 
            (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey] == SeekerRole)
        {
            if (dist < range)
            {
                StartCoroutine(BlindEffect());
            }
        }
    }

    IEnumerator BlindEffect()
    {
        GameObject blindPanel = GameObject.Find("BlindPanel");
        if (blindPanel != null)
        {
            blindPanel.SetActive(true);
            yield return new WaitForSeconds(3f);
            blindPanel.SetActive(false);
        }
    }

    [PunRPC]
    void RPC_SetInvisibility(bool invisible)
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        foreach (var r in rs)
        {
            // If it's another player, just hide it. If it's mine, maybe make it semi-transparent
            r.enabled = !invisible;
        }
        
        // Hide name tag if exists
        Canvas c = GetComponentInChildren<Canvas>();
        if (c != null) c.enabled = !invisible;
    }
}
