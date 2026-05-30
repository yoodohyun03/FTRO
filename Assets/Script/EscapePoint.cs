using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class EscapePoint : MonoBehaviourPunCallbacks
{
    public bool isActive = false;
    private List<PhotonView> survivorsInZone = new List<PhotonView>();
    private SpriteRenderer minimapIcon;
    private Transform minimapCamTransform;

    void Start()
    {
        // Find Minimap Camera to match rotation
        MinimapFollow mf = Object.FindFirstObjectByType<MinimapFollow>();
        if (mf != null) minimapCamTransform = mf.transform;

        CreateMinimapIcon();
    }

    void CreateMinimapIcon()
    {
        GameObject iconObj = new GameObject("MinimapIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.transform.localPosition = Vector3.up * 20f; // Higher up
        iconObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        iconObj.layer = 7; 

        minimapIcon = iconObj.AddComponent<SpriteRenderer>();
        minimapIcon.sprite = Resources.Load<Sprite>("EscapeIcon");
        minimapIcon.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); 
        iconObj.transform.localScale = Vector3.one * 35f; 
    }

    void Update()
{
        // Sync rotation with Minimap Camera
        if (minimapCamTransform != null && minimapIcon != null)
        {
            float camY = minimapCamTransform.eulerAngles.y;
            minimapIcon.transform.rotation = Quaternion.Euler(90f, camY, 0f);
        }

        if (!isActive || !PhotonNetwork.IsMasterClient) return;

        int aliveSurvivors = 0;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            PlayerMove pm = p.GetComponent<PlayerMove>();
            if (pm != null && !pm.isDead && pm.myRole != "Seeker")
            {
                aliveSurvivors++;
            }
        }

        if (aliveSurvivors > 0 && survivorsInZone.Count >= aliveSurvivors)
        {
            // All alive survivors are in the zone
            GameManager.instance.photonView.RPC("RPC_GameOver", RpcTarget.All, "Survivors Escaped!");
        }
}

    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (other.CompareTag("Player"))
        {
            PlayerMove pm = other.GetComponent<PlayerMove>();
            if (pm != null && !pm.isDead && pm.myRole != "Seeker")
            {
                PhotonView pv = other.GetComponent<PhotonView>();
                if (!survivorsInZone.Contains(pv)) survivorsInZone.Add(pv);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (survivorsInZone.Contains(pv)) survivorsInZone.Remove(pv);
        }
    }

    [PunRPC]
    public void RPC_ActivateEscape()
    {
        isActive = true;
        if (minimapIcon != null) minimapIcon.color = Color.cyan; // Bright cyan when active
        Debug.Log("[Escape] Escape Zone Activated!");
    }
}
