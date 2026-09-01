using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class EscapePoint : MonoBehaviourPunCallbacks
{
    public bool isActive = false;
    private List<PhotonView> survivorsInZone = new List<PhotonView>();
    private SpriteRenderer minimapIcon;
    private Transform minimapCamTransform;
    private EscapeBeamEffect beamEffect;
    private bool hasTriggeredWin;

    void Start()
    {
        MinimapFollow mf = Object.FindFirstObjectByType<MinimapFollow>();
        if (mf != null) minimapCamTransform = mf.transform;

        CreateMinimapIcon();

        if (isActive)
            ShowBeam();
    }

    void CreateMinimapIcon()
    {
        GameObject iconObj = new GameObject("MinimapIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.transform.localPosition = Vector3.up * 20f;
        iconObj.transform.rotation = Quaternion.Euler(90, 0, 0);
        iconObj.layer = 7;

        minimapIcon = iconObj.AddComponent<SpriteRenderer>();
        minimapIcon.sprite = Resources.Load<Sprite>("EscapeIcon");
        minimapIcon.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        iconObj.transform.localScale = Vector3.one * 35f;
    }

    void Update()
    {
        if (minimapCamTransform != null && minimapIcon != null)
        {
            float camY = minimapCamTransform.eulerAngles.y;
            minimapIcon.transform.rotation = Quaternion.Euler(90f, camY, 0f);
        }

        if (!isActive || !PhotonNetwork.IsMasterClient) return;
        if (!GameModeTypeHelper.UsesObjectives(GameManager.CurrentGameMode)) return;

        PruneSurvivorsInZone();

        int aliveSurvivors = CountAliveSurvivors();
        if (!hasTriggeredWin && aliveSurvivors > 0 && survivorsInZone.Count >= aliveSurvivors)
        {
            hasTriggeredWin = true;
            if (GameManager.instance != null)
                GameManager.instance.photonView.RPC("RPC_GameOver", RpcTarget.All, "Survivors Escaped!\nSurvivor Victory!");
        }
    }

    int CountAliveSurvivors()
    {
        int alive = 0;
        foreach (GameObject p in GameObject.FindGameObjectsWithTag("Player"))
        {
            PlayerMove pm = p.GetComponent<PlayerMove>();
            if (RoleAssignmentHelper.IsAliveSurvivor(pm))
                alive++;
        }
        return alive;
    }

    void PruneSurvivorsInZone()
    {
        for (int i = survivorsInZone.Count - 1; i >= 0; i--)
        {
            PhotonView pv = survivorsInZone[i];
            if (pv == null)
            {
                survivorsInZone.RemoveAt(i);
                continue;
            }

            PlayerMove pm = pv.GetComponent<PlayerMove>();
            if (!RoleAssignmentHelper.IsAliveSurvivor(pm))
                survivorsInZone.RemoveAt(i);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActive || !other.CompareTag("Player")) return;

        PlayerMove pm = other.GetComponent<PlayerMove>() ?? other.GetComponentInParent<PlayerMove>();
        if (!RoleAssignmentHelper.IsAliveSurvivor(pm)) return;

        PhotonView pv = other.GetComponent<PhotonView>() ?? other.GetComponentInParent<PhotonView>();
        if (pv != null && !survivorsInZone.Contains(pv))
            survivorsInZone.Add(pv);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PhotonView pv = other.GetComponent<PhotonView>() ?? other.GetComponentInParent<PhotonView>();
        if (pv != null && survivorsInZone.Contains(pv))
            survivorsInZone.Remove(pv);
    }

    [PunRPC]
    public void RPC_ActivateEscape()
    {
        isActive = true;
        if (minimapIcon != null) minimapIcon.color = Color.cyan;
        StartCoroutine(ShowBeamNextFrame());
        Debug.Log("[Escape] Escape Zone Activated!");
    }

    IEnumerator ShowBeamNextFrame()
    {
        yield return null;
        ShowBeam();
    }

    void ShowBeam()
    {
        if (beamEffect == null)
        {
            beamEffect = GetComponent<EscapeBeamEffect>();
            if (beamEffect == null)
                beamEffect = gameObject.AddComponent<EscapeBeamEffect>();
        }

        if (beamEffect != null)
            beamEffect.SetBeamActive(true);
    }
}
