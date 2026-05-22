using UnityEngine;
using Photon.Pun;

public class SprayArea : MonoBehaviourPun
{
    public float duration = 10f;
    public float slipFactor = 2.0f; // Multiplier or speed change

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DestroyAfterDelay());
        }
    }

    System.Collections.IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(duration);
        PhotonNetwork.Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerMove pm = other.GetComponent<PlayerMove>();
            if (pm != null)
            {
                // Simple slip: increase speed but maybe reduce control? 
                // Or just slow them down if it's "sticky" spray?
                // User said "미끄러지는" (slipping), usually means sliding.
                // For now, let's just apply a speed penalty or boost to simulate loss of control.
                StartCoroutine(SlipEffect(pm));
            }
        }
    }

    System.Collections.IEnumerator SlipEffect(PlayerMove pm)
    {
        float originalWalk = pm.walkSpeed;
        float originalRunS = pm.survivorRunSpeed;
        
        pm.walkSpeed *= 0.5f;
        pm.survivorRunSpeed *= 0.5f;
        
        yield return new WaitForSeconds(3f);
        
        pm.walkSpeed = originalWalk;
        pm.survivorRunSpeed = originalRunS;
    }
}
