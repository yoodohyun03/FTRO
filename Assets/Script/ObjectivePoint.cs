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

    [PunRPC]
    public void RPC_AddProgress(float amount)
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
