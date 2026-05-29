using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using TMPro;

public class SeekerItemHandler : MonoBehaviourPun
{
    public List<ItemData> items = new List<ItemData>();
    public float[] cooldownTimers;
    public float globalCooldown = 0f;

    private TextMeshProUGUI[] itemTexts;
    private UnityEngine.UI.Image[] cooldownImages;
    private GameObject uiContainer;

    void Start()
    {
        if (photonView.IsMine)
        {
            // Initialize items
            items.Clear();
            items.Add(Resources.Load<ItemData>("Items/FreezeItem"));
            items.Add(Resources.Load<ItemData>("Items/SwarmItem"));

            cooldownTimers = new float[items.Count];
            itemTexts = new TextMeshProUGUI[items.Count];
            cooldownImages = new UnityEngine.UI.Image[items.Count];

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

        // More robust Canvas finding
        Canvas canvas = null;
        
        // 1. Try to find a Screen Space Overlay canvas named "Canvas"
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj != null) canvas = canvasObj.GetComponent<Canvas>();

        // 2. If not found, look for any Overlay canvas
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }
        }

        // 3. Fallback to any canvas
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        
        if (canvas == null)
        {
            Debug.LogError("[SeekerItemHandler] No Canvas found in scene!");
            return;
        }

        uiContainer = new GameObject("SeekerItemsUI_Bottom");
        uiContainer.transform.SetParent(canvas.transform, false);
        uiContainer.transform.SetAsLastSibling(); // Ensure it's on top
        
        RectTransform rtContainer = uiContainer.AddComponent<RectTransform>();
        rtContainer.anchorMin = new Vector2(0.5f, 0);
        rtContainer.anchorMax = new Vector2(0.5f, 0);
        rtContainer.pivot = new Vector2(0.5f, 0);
        rtContainer.anchoredPosition = new Vector2(0, 30); // Lower position
        rtContainer.sizeDelta = new Vector2(400, 80);

        for (int i = 0; i < items.Count; i++)
        {
            float posX = (i == 0) ? -90 : 90;
            string key = (i == 0) ? "Q" : "R";

            GameObject itemSlot = new GameObject($"ItemSlot_{i}");
            itemSlot.transform.SetParent(uiContainer.transform, false);
            RectTransform rtSlot = itemSlot.AddComponent<RectTransform>();
            rtSlot.sizeDelta = new Vector2(160, 50); // Smaller
            rtSlot.anchoredPosition = new Vector2(posX, 0);

            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(itemSlot.transform, false);
            UnityEngine.UI.Image bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f); // Darker background
            RectTransform rtBg = bg.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero; rtBg.anchorMax = Vector2.one; rtBg.sizeDelta = Vector2.zero;

            // Text
            GameObject textObj = new GameObject("ItemText");
            textObj.transform.SetParent(itemSlot.transform, false);
            itemTexts[i] = textObj.AddComponent<TextMeshProUGUI>();
            
            // Apply custom Korean font
            TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Font_1-4Regular SDF");
            // If not in Resources, it will be null at runtime, but we can't use AssetDatabase in a runtime script.
            if (customFont != null) itemTexts[i].font = customFont;

            itemTexts[i].fontSize = 14; 
itemTexts[i].alignment = TextAlignmentOptions.Center;
            itemTexts[i].raycastTarget = false;
            RectTransform rtText = textObj.GetComponent<RectTransform>();
            rtText.anchorMin = Vector2.zero; rtText.anchorMax = Vector2.one; rtText.sizeDelta = Vector2.zero;

            // Cooldown Overlay
            GameObject cdObj = new GameObject("CooldownOverlay");
            cdObj.transform.SetParent(itemSlot.transform, false);
            cooldownImages[i] = cdObj.AddComponent<UnityEngine.UI.Image>();
            cooldownImages[i].color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Subtle grey overlay
            cooldownImages[i].type = UnityEngine.UI.Image.Type.Filled;
            cooldownImages[i].fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            cooldownImages[i].raycastTarget = false;
            RectTransform rtCd = cdObj.GetComponent<RectTransform>();
            rtCd.anchorMin = Vector2.zero; rtCd.anchorMax = Vector2.one; rtCd.sizeDelta = Vector2.zero;
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
            {
                UseItem(0);
            }
            else if (Input.GetKeyDown(KeyCode.R) && cooldownTimers.Length > 1 && cooldownTimers[1] <= 0)
            {
                UseItem(1);
            }
        }
    }

    void UpdateUI()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (itemTexts[i] == null || items[i] == null) continue;

            string key = (i == 0) ? "Q" : "R";
            float currentCD = Mathf.Max(cooldownTimers[i], globalCooldown);

            if (currentCD > 0)
            {
                itemTexts[i].text = $"{items[i].itemName}\n{currentCD:F1}s";
                cooldownImages[i].fillAmount = currentCD / 90f; 
            }
else
            {
                itemTexts[i].text = $"<b>{items[i].itemName}</b>\n[{key}] Ready";
                cooldownImages[i].fillAmount = 0;
            }
        }
    }

    void UseItem(int index)
    {
        if (index >= items.Count) return;
        ItemData item = items[index];

        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role")) return;
        if ((string)PhotonNetwork.LocalPlayer.CustomProperties["Role"] != "Seeker") return;

        // Set cooldowns (1 minute 30 seconds = 90s)
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
            Debug.Log($"Seeker Item Used: Swarm targeting {target.Owner.NickName} (ViewID: {target.ViewID}) for {allAIs.Length} AIs");
        }
        else
        {
            Debug.LogWarning("No alive survivors found to swarm!");
            cooldownTimers[1] = 5f; // Short refund
        }
    }
}
