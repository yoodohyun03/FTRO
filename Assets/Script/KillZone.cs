using UnityEngine;
using Photon.Pun;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                PlayerMove pm = other.GetComponent<PlayerMove>();
                if (pm != null && !pm.isDead)
                {
                    Debug.Log("[KillZone] Player fell! Catching them.");
                    pv.RPC("GetCaught", RpcTarget.All);
                }
            }
        }
    }
}
