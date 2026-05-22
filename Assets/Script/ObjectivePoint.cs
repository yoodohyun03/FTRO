using UnityEngine;
using Photon.Pun;

public class ObjectivePoint : MonoBehaviourPunCallbacks
{
    public float interactionTime = 10f; 
    public bool isCompleted = false;
    public float currentProgress = 0f;

    private SpriteRenderer minimapIcon;

    private Light hackingLight;

    void Start()
    {
        CreateMinimapIcon();
        SetupHackingLight();
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

    void SetupHackingLight()
    {
        GameObject lightObj = new GameObject("HackingLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1f;
        hackingLight = lightObj.AddComponent<Light>();
        hackingLight.type = LightType.Point;
        hackingLight.range = 5f;
        hackingLight.intensity = 0f;
        hackingLight.color = Color.yellow;
    }

    [PunRPC]
    public void RPC_AddProgress(float amount)
    {
        if (isCompleted) return;
        
        currentProgress += amount;
        
        if (hackingLight != null)
        {
            hackingLight.intensity = Mathf.PingPong(Time.time * 5f, 2f);
        }

        if (currentProgress >= interactionTime)
        {
            currentProgress = interactionTime;
            isCompleted = true;
            if (minimapIcon != null) minimapIcon.color = Color.gray;
            if (hackingLight != null) { hackingLight.color = Color.green; hackingLight.intensity = 5f; }
            Debug.Log("[Objective] Terminal Completed!");
            if (PhotonNetwork.IsMasterClient && GameManager.instance != null)
            {
                GameManager.instance.photonView.RPC("OnObjectiveCompleted", RpcTarget.All);
            }
        }
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
