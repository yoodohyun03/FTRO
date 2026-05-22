using UnityEngine;
using Photon.Pun;

public class LootBox : MonoBehaviourPun
{
    public float interactionTime = 3f;
    public float currentProgress = 0f;
    public bool isOpened = false;

    private SpriteRenderer minimapIcon;

    void Start()
    {
        CreateMinimapIcon();
    }

    void CreateMinimapIcon()
    {
        GameObject iconObj = new GameObject("MinimapIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.transform.localPosition = Vector3.up * 5f;
        iconObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        iconObj.layer = 7; // Minimap layer

        minimapIcon = iconObj.AddComponent<SpriteRenderer>();
        // Use a default sprite or load one
        minimapIcon.sprite = Resources.Load<Sprite>("LootIcon"); 
        minimapIcon.color = Color.yellow;
        iconObj.transform.localScale = Vector3.one * 10f;
    }

    [PunRPC]
    public void RPC_AddProgress(float amount, int playerViewID)
    {
        if (isOpened) return;

        currentProgress += amount;
        if (currentProgress >= interactionTime)
        {
            currentProgress = interactionTime;
            isOpened = true;
            
            // Give item to the player
            PhotonView playerPV = PhotonView.Find(playerViewID);
            if (playerPV != null)
            {
                ItemSystem itemSys = playerPV.GetComponent<ItemSystem>();
                if (itemSys != null)
                {
                    ItemType randomItem = (ItemType)Random.Range(0, 3);
                    itemSys.photonView.RPC("RPC_GiveItem", playerPV.Owner, randomItem);
                }
            }

            // Sync opened state
            photonView.RPC("RPC_SetOpened", RpcTarget.All);
        }
    }

    [PunRPC]
    void RPC_SetOpened()
    {
        isOpened = true;
        if (minimapIcon != null) minimapIcon.enabled = false;
        // Optional: Play opening animation or hide mesh
        gameObject.SetActive(false);
    }
}
