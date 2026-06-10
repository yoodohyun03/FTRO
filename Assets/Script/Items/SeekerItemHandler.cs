using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
public class SeekerItemHandler : MonoBehaviourPun
{
    public List<ItemData> items = new List<ItemData>();
    public float[] cooldownTimers;
    public float globalCooldown = 0f;

    private RoleSkillPanelUI.SkillSlot[] skillSlots;
    private GameObject uiContainer;

    void Start()
    {
        if (photonView.IsMine)
        {
            items.Clear();
            var freeze = Resources.Load<ItemData>("Items/FreezeItem");
            var swarm = Resources.Load<ItemData>("Items/SwarmItem");
            if (freeze != null) items.Add(freeze);
            else Debug.LogError("[SeekerItem] Items/FreezeItem 에셋을 찾을 수 없습니다.");
            if (swarm != null) items.Add(swarm);
            else Debug.LogError("[SeekerItem] Items/SwarmItem 에셋을 찾을 수 없습니다.");

            cooldownTimers = new float[items.Count];
            skillSlots = new RoleSkillPanelUI.SkillSlot[items.Count];

            CreateUI();
        }
    }

    void OnDestroy()
    {
        if (uiContainer != null) Destroy(uiContainer);
    }

    void CreateUI()
    {
        if (uiContainer != null) Destroy(uiContainer);

        Canvas canvas = RoleSkillPanelUI.FindOverlayCanvas(transform);
        if (canvas == null)
        {
            Debug.LogError("[SeekerItemHandler] Overlay Canvas를 찾을 수 없습니다.");
            return;
        }

        RectTransform panel = RoleSkillPanelUI.CreateBottomPanel(
            canvas.transform, "SeekerSkillPanel", new Vector2(540f, RoleSkillPanelUI.SlotHeight + 8f));

        uiContainer = panel.gameObject;

        for (int i = 0; i < items.Count; i++)
        {
            float posX = (i == 0) ? -130f : 130f;
            skillSlots[i] = RoleSkillPanelUI.CreateSkillSlot(panel, new Vector2(posX, 0f));
        }

        UpdateUI();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        bool changed = false;
        if (globalCooldown > 0)
        {
            globalCooldown -= Time.deltaTime;
            changed = true;
        }

        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0)
            {
                cooldownTimers[i] -= Time.deltaTime;
                changed = true;
            }
        }

        if (changed) UpdateUI();

        if (globalCooldown <= 0)
        {
            if (Input.GetKeyDown(KeyCode.Q) && cooldownTimers.Length > 0 && cooldownTimers[0] <= 0)
                UseItem(0);
            else if (Input.GetKeyDown(KeyCode.R) && cooldownTimers.Length > 1 && cooldownTimers[1] <= 0)
                UseItem(1);
        }
    }

    void UpdateUI()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (skillSlots[i]?.text == null || items[i] == null) continue;

            string key = (i == 0) ? "Q" : "R";
            float currentCD = Mathf.Max(cooldownTimers[i], globalCooldown);
            bool ready = currentCD <= 0f;
            string desc = string.IsNullOrEmpty(items[i].description) ? items[i].itemName : items[i].description;

            skillSlots[i].text.text = RoleSkillPanelUI.FormatSkillText(
                key, items[i].itemName, desc, ready, currentCD);

            if (skillSlots[i].cooldownOverlay != null)
            {
                skillSlots[i].cooldownOverlay.fillAmount = ready ? 0f : Mathf.Clamp01(currentCD / 90f);
            }
        }
    }

    void UseItem(int index)
    {
        if (index >= items.Count) return;
        ItemData item = items[index];

        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role")) return;
        if ((string)PhotonNetwork.LocalPlayer.CustomProperties["Role"] != "Seeker") return;

        cooldownTimers[index] = 90f;
        globalCooldown = 2f;

        if (item.itemType == ItemType.SeekerFreeze) TriggerFreeze(item);
        else if (item.itemType == ItemType.SeekerSwarm) TriggerSwarm(item);

        UpdateUI();
    }

    void TriggerFreeze(ItemData item)
    {
        RandomRoam[] allAIs = Object.FindObjectsByType<RandomRoam>(FindObjectsSortMode.None);
        foreach (var ai in allAIs)
        {
            if (ai.photonView != null)
                ai.photonView.RPC("RPC_SetAIState", RpcTarget.All, (int)RandomRoam.AIState.Frozen, -1, item.duration);
        }
        Debug.Log($"Seeker Item Used: Freeze on {allAIs.Length} AIs");
    }

    void TriggerSwarm(ItemData item)
    {
        List<PhotonView> aliveSurvivors = new List<PhotonView>();
        PlayerMove[] players = Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);

        foreach (var p in players)
        {
            if (!p.isDead && p.photonView.Owner != null && p.photonView.Owner.CustomProperties.ContainsKey("Role") &&
                (string)p.photonView.Owner.CustomProperties["Role"] == "Survivor")
            {
                aliveSurvivors.Add(p.photonView);
            }
        }

        if (aliveSurvivors.Count > 0)
        {
            PhotonView target = aliveSurvivors[Random.Range(0, aliveSurvivors.Count)];
            RandomRoam[] allAIs = Object.FindObjectsByType<RandomRoam>(FindObjectsSortMode.None);
            foreach (var ai in allAIs)
            {
                if (ai.photonView != null)
                    ai.photonView.RPC("RPC_SetAIState", RpcTarget.All, (int)RandomRoam.AIState.Swarming, target.ViewID, item.duration);
            }
            Debug.Log($"Seeker Item Used: Swarm targeting {target.Owner.NickName}");
        }
        else
        {
            Debug.LogWarning("No alive survivors found to swarm!");
            cooldownTimers[1] = 5f;
        }
    }
}
