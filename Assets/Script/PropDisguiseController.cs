using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// 사물 변신 모드(ChangeMode): 생존자가 G키로 주변 사물에 변신해 숨습니다.
/// </summary>
public class PropDisguiseController : MonoBehaviourPun
{
    struct PropVisual
    {
        public string label;
        public PrimitiveType primitive;
        public Vector3 scale;
        public Color color;
    }

    static readonly PropVisual[] PropCatalog =
    {
        new PropVisual { label = "상자", primitive = PrimitiveType.Cube, scale = new Vector3(0.9f, 0.9f, 0.9f), color = new Color(0.55f, 0.42f, 0.28f) },
        new PropVisual { label = "쓰레기통", primitive = PrimitiveType.Cylinder, scale = new Vector3(0.55f, 0.75f, 0.55f), color = new Color(0.35f, 0.38f, 0.42f) },
        new PropVisual { label = "화분", primitive = PrimitiveType.Cylinder, scale = new Vector3(0.45f, 0.55f, 0.45f), color = new Color(0.28f, 0.55f, 0.32f) },
        new PropVisual { label = "소화전", primitive = PrimitiveType.Cylinder, scale = new Vector3(0.35f, 0.95f, 0.35f), color = new Color(0.85f, 0.18f, 0.15f) },
        new PropVisual { label = "돌", primitive = PrimitiveType.Sphere, scale = new Vector3(0.7f, 0.55f, 0.7f), color = new Color(0.5f, 0.5f, 0.52f) },
    };

    const float RayDistance = 4.5f;
    const float DisguiseMoveMultiplier = 0.35f;

    public bool IsDisguised { get; private set; }
    public int PropIndex { get; private set; }
    public float PropYaw { get; private set; }

    PlayerMove playerMove;
    Transform propAnchor;
    GameObject propVisual;
    Renderer[] hiddenRenderers;
    TextMeshProUGUI hintText;
    float baseWalkSpeed;
    float baseSurvivorRunSpeed;
    bool speedsCached;
    int builtPropIndex = -1;

    public static bool IsModeActive =>
        GameManager.CurrentGameMode == GameModeType.ChangeMode;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    void Start()
    {
        if (!IsModeActive) return;

        if (photonView.IsMine)
            CreateHintUi();

        photonView.RPC(nameof(RPC_SyncDisguise), RpcTarget.AllBuffered, false, 0, 0f);
    }

    void Update()
    {
        if (!IsModeActive || !photonView.IsMine || playerMove == null) return;
        if (playerMove.isDead) return;
        if (GameManager.instance != null && GameManager.instance.currentState != GameManager.GameState.Playing)
            return;

        if (Input.GetKeyDown(KeyCode.G))
            ToggleDisguise();

        if (IsDisguised && Input.GetKeyDown(KeyCode.R))
        {
            PropYaw = Mathf.Repeat(PropYaw + 90f, 360f);
            photonView.RPC(nameof(RPC_SyncDisguise), RpcTarget.AllBuffered, true, PropIndex, PropYaw);
        }
    }

    void LateUpdate()
    {
        // 캐릭터 몸통 회전이 매 프레임 앵커를 덮어쓰므로, 변신 중에는 사물 회전을 계속 고정
        if (IsDisguised)
            ApplyVisualRotation();
    }

    void ToggleDisguise()
    {
        if (!IsDisguised)
        {
            int picked = DetectPropFromEnvironment();
            if (picked < 0)
            {
                ShowHint("변신할 사물이 없습니다. 사물을 바라보고 [G]");
                return;
            }

            PropIndex = picked;
            PropYaw = transform.eulerAngles.y;
            photonView.RPC(nameof(RPC_SyncDisguise), RpcTarget.AllBuffered, true, PropIndex, PropYaw);
        }
        else
        {
            photonView.RPC(nameof(RPC_SyncDisguise), RpcTarget.AllBuffered, false, PropIndex, PropYaw);
        }
    }

    int DetectPropFromEnvironment()
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, RayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = hit.collider.name.ToLowerInvariant();
            if (n.Contains("trash") || n.Contains("bin")) return 1;
            if (n.Contains("plant") || n.Contains("pot") || n.Contains("planter")) return 2;
            if (n.Contains("hydrant") || n.Contains("fire")) return 3;
            if (n.Contains("rock") || n.Contains("stone") || n.Contains("manhole")) return 4;
            if (n.Contains("box") || n.Contains("crate") || n.Contains("bench")) return 0;
            return 0;
        }

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = groundHit.collider.name.ToLowerInvariant();
            if (n.Contains("plant") || n.Contains("pot")) return 2;
        }

        return -1;
    }

    public void ReleaseDisguiseOnDeath()
    {
        if (!IsDisguised) return;

        IsDisguised = false;
        ClearDisguiseVisual(restoreCharacterRenderers: false);
        ApplySpeedPenalty(false);
        speedsCached = false;
    }

    [PunRPC]
    public void RPC_SyncDisguise(bool disguised, int propIndex, float yaw)
    {
        IsDisguised = disguised;
        PropIndex = Mathf.Clamp(propIndex, 0, PropCatalog.Length - 1);
        PropYaw = yaw;

        if (IsDisguised)
        {
            CacheSpeeds();
            ApplyDisguiseVisual();
            ApplySpeedPenalty(true);
            if (photonView.IsMine)
                ShowHint($"변신: {PropCatalog[PropIndex].label}  |  [G] 해제  [R] 회전");
        }
        else
        {
            ClearDisguiseVisual();
            ApplySpeedPenalty(false);
            speedsCached = false;
            if (photonView.IsMine)
                ShowHint("[G] 사물 변신  |  [R] 회전 (변신 중)");
        }
    }

    void CacheSpeeds()
    {
        if (speedsCached || playerMove == null) return;
        baseWalkSpeed = playerMove.walkSpeed;
        baseSurvivorRunSpeed = playerMove.survivorRunSpeed;
        speedsCached = true;
    }

    void ApplySpeedPenalty(bool disguised)
    {
        if (playerMove == null || !photonView.IsMine || !speedsCached) return;

        if (disguised)
        {
            playerMove.walkSpeed = baseWalkSpeed * DisguiseMoveMultiplier;
            playerMove.survivorRunSpeed = baseSurvivorRunSpeed * DisguiseMoveMultiplier;
        }
        else
        {
            playerMove.walkSpeed = baseWalkSpeed;
            playerMove.survivorRunSpeed = baseSurvivorRunSpeed;
        }
    }

    void ApplyDisguiseVisual()
    {
        EnsurePropAnchor();
        EnsurePropVisual();

        if (hiddenRenderers == null)
        {
            var list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || propVisual != null && r.transform.IsChildOf(propVisual.transform)) continue;
                if (r.GetComponent<TextMeshProUGUI>() != null) continue;
                list.Add(r);
            }
            hiddenRenderers = list.ToArray();
        }

        foreach (Renderer r in hiddenRenderers)
            if (r != null) r.enabled = false;

        PropVisual def = PropCatalog[PropIndex];
        propVisual.transform.localScale = def.scale;
        var propRenderer = propVisual.GetComponent<Renderer>();
        if (propRenderer != null)
        {
            propRenderer.material.color = def.color;
            propRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        ApplyVisualRotation();
        if (propVisual != null) propVisual.SetActive(true);
    }

    void ClearDisguiseVisual(bool restoreCharacterRenderers = true)
    {
        if (restoreCharacterRenderers && hiddenRenderers != null)
        {
            foreach (Renderer r in hiddenRenderers)
                if (r != null) r.enabled = true;
        }

        if (propVisual != null) propVisual.SetActive(false);
    }

    void ApplyVisualRotation()
    {
        if (propAnchor != null)
            propAnchor.rotation = Quaternion.Euler(0f, PropYaw, 0f);
    }

    void EnsurePropAnchor()
    {
        if (propAnchor != null) return;
        GameObject anchor = new GameObject("PropDisguiseAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = Vector3.zero;
        propAnchor = anchor.transform;
    }

    void EnsurePropVisual()
    {
        PropVisual def = PropCatalog[PropIndex];
        if (propVisual != null && builtPropIndex != PropIndex)
        {
            Destroy(propVisual);
            propVisual = null;
        }

        if (propVisual != null) return;

        builtPropIndex = PropIndex;
        propVisual = GameObject.CreatePrimitive(def.primitive);
        propVisual.name = "PropDisguiseVisual";
        propVisual.transform.SetParent(propAnchor, false);
        propVisual.transform.localPosition = new Vector3(0f, def.scale.y * 0.5f, 0f);

        Collider col = propVisual.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void CreateHintUi()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        GameObject obj = new GameObject("PropDisguiseHint");
        obj.transform.SetParent(canvas.transform, false);
        hintText = obj.AddComponent<TextMeshProUGUI>();
        hintText.fontSize = 22f;
        hintText.alignment = TextAlignmentOptions.Bottom;
        hintText.color = new Color(0.85f, 0.95f, 1f, 0.95f);
        hintText.raycastTarget = false;

        RectTransform rect = hintText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(720f, 36f);

        ShowHint("[G] 사물 변신  |  [R] 회전 (변신 중)");
    }

    void ShowHint(string msg)
    {
        if (hintText != null) hintText.text = msg;
    }

    static Canvas FindOverlayCanvas()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;
        }
        return Object.FindFirstObjectByType<Canvas>();
    }

    void OnDestroy()
    {
        if (propVisual != null) Destroy(propVisual);
        if (hintText != null) Destroy(hintText.gameObject);
    }
}
