using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;

public class SurvivorItemHandler : MonoBehaviourPun
{
    static readonly string[] ItemNames = { "스프린트 부스트", "연막탄", "마커 교란", "EMP", "해킹 툴", "디코이" };
    static readonly string[] ItemDescriptions =
    {
        "이동속도 2배 (5초)",
        "주변 시야 차단 (3초)",
        "술래 미니맵 가짜 신호",
        "술래 감속 + 화면 노이즈",
        "다음 터미널 해킹 단축",
        "잔상 생성으로 위장"
    };
    static readonly bool[] IsRare = { false, false, false, true, true, true };

    private SurvivorItemType? heldItem = null;
    private PlayerMove playerMove;
    private GameObject uiContainer;
    private RoleSkillPanelUI.SkillSlot skillSlot;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    void Start()
    {
        if (photonView.IsMine) 
        {
            CreateUI();
            UpdateUI();
        }
    }

    void OnDestroy()
    {
        if (uiContainer != null) Destroy(uiContainer);
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        if (heldItem != null && Input.GetKeyDown(KeyCode.F))
        {
            UseItem();
        }
    }

    public void ReceiveItem(int itemTypeInt)
    {
        if (itemTypeInt < 0 || itemTypeInt >= ItemNames.Length) return;
        heldItem = (SurvivorItemType)itemTypeInt;
        if (photonView.IsMine) UpdateUI();
        Debug.Log($"[SurvivorItem] Received: {ItemNames[itemTypeInt]}");
    }

    void UseItem()
    {
        if (heldItem == null) return;
        SurvivorItemType item = heldItem.Value;
        heldItem = null;
        UpdateUI();

        Debug.Log($"[SurvivorItem] Using: {item}");

        switch (item)
        {
            case SurvivorItemType.Sprint:    StartCoroutine(SprintCoroutine());   break;
            case SurvivorItemType.Smoke:     UseSmoke();                          break;
            case SurvivorItemType.MarkerJam: UseMarkerJam();                      break;
            case SurvivorItemType.EMP:       UseEMP();                            break;
            case SurvivorItemType.Hack:      UseHack();                           break;
            case SurvivorItemType.Decoy:     UseDecoy();                          break;
        }
    }

    IEnumerator SprintCoroutine()
    {
        if (playerMove == null) yield break;
        float orig = playerMove.survivorRunSpeed;
        playerMove.survivorRunSpeed *= 2f;
        yield return new WaitForSeconds(5f);
        if (playerMove != null) playerMove.survivorRunSpeed = orig;
    }

    void UseSmoke()
    {
        photonView.RPC("RPC_SpawnSmoke", RpcTarget.All, transform.position);
    }

    [PunRPC]
    void RPC_SpawnSmoke(Vector3 pos)
    {
        GameObject smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        smoke.transform.position = pos + Vector3.up * 1.5f;
        smoke.transform.localScale = Vector3.one * 8f;
        
        Collider c = smoke.GetComponent<Collider>();
        if (c != null) Destroy(c);
        
        Renderer r = smoke.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.45f, 0.45f, 0.45f, 0.92f);
        
        Destroy(smoke, 3f);

        if (IsLocalSeeker() && Vector3.Distance(GetLocalCameraPos(), pos) < 15f)
            StartCoroutine(ScreenOverlay(new Color(0.32f, 0.32f, 0.32f, 0.97f), 2.5f));
    }

    void UseMarkerJam()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 fake = transform.position + new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-30f, 30f));
            photonView.RPC("RPC_FakeNoisePing", RpcTarget.All, fake);
        }
    }

    [PunRPC]
    void RPC_FakeNoisePing(Vector3 pos)
    {
        if (!IsLocalSeeker()) return;
        GameObject ping = new GameObject("FakeNoisePing");
        ping.transform.position = pos;
        ping.AddComponent<NoisePing>();
    }

    void UseEMP()
    {
        photonView.RPC("RPC_EMP", RpcTarget.All);
    }

    [PunRPC]
    void RPC_EMP()
    {
        if (!IsLocalSeeker()) return;
        PlayerMove seeker = FindLocalPlayerMove();
        if (seeker != null) StartCoroutine(EMPEffect(seeker));
    }

    IEnumerator EMPEffect(PlayerMove seeker)
    {
        float origWalk = seeker.walkSpeed;
        float origRun  = seeker.seekerRunSpeed;
        seeker.walkSpeed *= 0.4f;
        seeker.seekerRunSpeed *= 0.4f;
        
        StartCoroutine(ScreenOverlay(new Color(0f, 0.5f, 1f, 0.3f), 3f));
        yield return new WaitForSeconds(3f);
        
        if (seeker != null)
        {
            seeker.walkSpeed = origWalk;
            seeker.seekerRunSpeed = origRun;
        }
    }

    void UseHack()
    {
        if (playerMove != null) playerMove.hackSpeedBoost = true;
    }

    void UseDecoy()
    {
        photonView.RPC("RPC_SpawnDecoy", RpcTarget.All, transform.position, transform.rotation);
    }

    [PunRPC]
    void RPC_SpawnDecoy(Vector3 pos, Quaternion rot)
    {
        GameObject decoy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        decoy.transform.position = pos;
        decoy.transform.rotation = rot;
        decoy.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        
        Collider c = decoy.GetComponent<Collider>();
        if (c != null) Destroy(c);
        
        decoy.AddComponent<DecoyMover>();
        Destroy(decoy, 5f);
    }

    IEnumerator ScreenOverlay(Color color, float duration)
    {
        Canvas canvas = RoleSkillPanelUI.FindOverlayCanvas(transform);
        if (canvas == null) yield break;

        GameObject overlay = new GameObject("ItemOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();
        
        var img = overlay.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        img.raycastTarget = false;
        
        var rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

        yield return new WaitForSeconds(duration);
        if (overlay != null) Destroy(overlay);
    }

    void CreateUI()
    {
        if (uiContainer != null) Destroy(uiContainer);

        Canvas canvas = RoleSkillPanelUI.FindOverlayCanvas(transform);
        if (canvas == null) return;

        RectTransform panel = RoleSkillPanelUI.CreateBottomPanel(
            canvas.transform, "SurvivorSkillPanel", new Vector2(RoleSkillPanelUI.SlotWidth + 8f, RoleSkillPanelUI.SlotHeight + 8f));

        uiContainer = panel.gameObject;
        skillSlot = RoleSkillPanelUI.CreateSkillSlot(panel, Vector2.zero);
    }

    void UpdateUI()
    {
        if (skillSlot?.text == null) return;

        if (heldItem == null)
        {
            skillSlot.text.text = RoleSkillPanelUI.FormatEmptySlot(
                "아이템 없음", "터미널 해킹 시 획득");
            if (skillSlot.cooldownOverlay != null) skillSlot.cooldownOverlay.fillAmount = 0f;
            return;
        }

        int idx = (int)heldItem.Value;
        string rarity = IsRare[idx] ? "희귀" : "일반";
        string name = $"{ItemNames[idx]} ({rarity})";

        skillSlot.text.text = RoleSkillPanelUI.FormatSkillText(
            "F", name, ItemDescriptions[idx], true);
        if (skillSlot.cooldownOverlay != null) skillSlot.cooldownOverlay.fillAmount = 0f;
    }

    bool IsLocalSeeker()
    {
        if (PhotonNetwork.LocalPlayer == null) return false;
        return PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object r)
               && (string)r == "Seeker";
    }

    Vector3 GetLocalCameraPos()
    {
        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    PlayerMove FindLocalPlayerMove()
    {
        foreach (var p in Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if (p.photonView != null && p.photonView.IsMine) return p;
        }
        return null;
    }
}

/// <summary>
/// 디코이 오브젝트를 짧은 거리로 랜덤하게 이동시키는 간단한 AI
/// </summary>
public class DecoyMover : MonoBehaviour
{
    Vector3 target;
    float speed = 2.5f;

    void Start()  => PickNewTarget();

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) < 0.2f) PickNewTarget();
    }

    void PickNewTarget()
    {
        target = transform.position + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
    }
}
