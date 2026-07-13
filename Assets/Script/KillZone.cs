using UnityEngine;
using Photon.Pun;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PhotonView pv = other.GetComponent<PhotonView>() ?? other.GetComponentInParent<PhotonView>();
        if (pv == null || !pv.IsMine) return;

        PlayerMove pm = other.GetComponent<PlayerMove>() ?? other.GetComponentInParent<PlayerMove>();
        if (pm == null || pm.isDead) return;
        if (!RoleAssignmentHelper.IsAliveSurvivor(pm)) return;

        Debug.Log("[KillZone] Survivor fell! Catching them.");
        pv.RPC("GetCaught", RpcTarget.All);
    }
}
