using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections.Generic;

public class MinimapFollow : MonoBehaviour
{
    public Transform target;
    public float height = 50f;
    public RenderTexture minimapTexture;
    [SerializeField] private float minimapSize = 310f;
    
    private GameObject minimapUI;
    private RectTransform playerArrow;
    private List<GameObject> offScreenIndicators = new List<GameObject>();
    private Sprite offscreenArrowSprite;

    void Start()
    {
        // Increase resolution if it's small
        if (minimapTexture != null && (minimapTexture.width < 512 || minimapTexture.height < 512))
        {
            // Note: In a real project we'd recreate it or adjust the asset, 
            // but for now we'll assume the user wants better quality.
            Debug.Log("Minimap texture might be low resolution.");
        }
        offscreenArrowSprite = Resources.Load<Sprite>("MinimapOffscreenArrow");
        SetupUI();
    }

    void SetupUI()
    {
        if (minimapUI != null) return;

        // Find the correct UI Canvas (Screen Space Overlay preferred)
        Canvas targetCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name.Contains("Canvas"))
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null) return;

        // Configure Camera Culling Mask to include MinimapIcon (Layer 7)
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            // Layer 7 is MinimapIcon. 1 << 7 = 128.
            cam.cullingMask |= (1 << 7);
        }

        minimapUI = new GameObject("MinimapUI_Runtime");
        minimapUI.transform.SetParent(targetCanvas.transform, false);
        minimapUI.transform.SetAsLastSibling();

        RawImage rawImage = minimapUI.AddComponent<RawImage>();
        rawImage.texture = minimapTexture;

        RectTransform rect = minimapUI.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(minimapSize, minimapSize);

        // Add Border
        GameObject border = new GameObject("Border");
        border.transform.SetParent(minimapUI.transform, false);
        border.transform.SetAsFirstSibling();
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 0.7f);
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-3, -3);
        borderRect.offsetMax = new Vector2(3, 3);

        // Add Player Arrow
        GameObject arrowObj = new GameObject("PlayerArrow");
        arrowObj.transform.SetParent(minimapUI.transform, false);
        Image arrowImg = arrowObj.AddComponent<Image>();
        arrowImg.sprite = Resources.Load<Sprite>("MinimapArrow_New");
        arrowImg.color = Color.white; 
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(22, 26);
        arrowRect.anchoredPosition = Vector2.zero; // Center of minimap
arrowRect.pivot = new Vector2(0.5f, 0.5f);
        playerArrow = arrowRect;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();
            return;
        }

        Vector3 newPosition = target.position;
        newPosition.y = height;
        transform.position = newPosition;
        
        // Minimap camera rotates with the main camera view
        float viewRotationY = 0f;
        if (Camera.main != null)
        {
            viewRotationY = Camera.main.transform.eulerAngles.y;
        }
        else
        {
            viewRotationY = target.eulerAngles.y;
        }
        
        transform.rotation = Quaternion.Euler(90f, viewRotationY, 0f);

        if (minimapUI == null) SetupUI();
        
        // Arrow represents the character's forward direction relative to the view
        if (playerArrow != null)
        {
            playerArrow.localRotation = Quaternion.Euler(0, 0, viewRotationY - target.eulerAngles.y);
        }

        UpdateOffScreenIndicators(viewRotationY);
    }

    void UpdateOffScreenIndicators(float viewRotationY)
    {
        // Clear old indicators (could be optimized with a pool)
        foreach (var obj in offScreenIndicators) Destroy(obj);
        offScreenIndicators.Clear();

        if (minimapUI == null || target == null) return;

        // Find targets: Terminals and Escapes
        ObjectivePoint[] terminals = Object.FindObjectsByType<ObjectivePoint>(FindObjectsSortMode.None);
        EscapePoint[] escapes = Object.FindObjectsByType<EscapePoint>(FindObjectsSortMode.None);

        foreach (var t in terminals) if (!t.isCompleted) CreateIndicator(t.transform.position, Color.green, viewRotationY);
        foreach (var e in escapes) if (e.isActive) CreateIndicator(e.transform.position, Color.cyan, viewRotationY);
    }

    void CreateIndicator(Vector3 targetPos, Color color, float viewRotationY)
    {
        Camera miniCam = GetComponent<Camera>();
        if (miniCam == null) return;

        // Convert world position to camera's viewport space
        Vector3 screenPos = miniCam.WorldToViewportPoint(targetPos);

        // Check if it's outside the viewport (0 to 1 range)
        bool isOffScreen = screenPos.z < 0 || screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1;

        if (isOffScreen)
        {
            // Calculate direction from player to target on XZ plane
            Vector3 diff = targetPos - target.position;
            diff.y = 0;
            
            // Rotate the direction vector by the minimap camera's rotation to get local UI direction
            Vector3 localDir = Quaternion.Euler(0, -viewRotationY, 0) * diff.normalized;
            Vector2 uiDir = new Vector2(localDir.x, localDir.z);

            // Create indicator UI
            GameObject indicator = new GameObject("OffScreenIndicator");
            indicator.transform.SetParent(minimapUI.transform, false);
            Image img = indicator.AddComponent<Image>();
            img.sprite = offscreenArrowSprite;
            img.color = color;
            
            RectTransform rect = indicator.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(32, 32);

            float radius = (minimapSize * 0.5f) - 12f;
            rect.anchoredPosition = uiDir * radius;
            
            // Rotate arrow to point towards target
            float angle = Mathf.Atan2(uiDir.y, uiDir.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0, 0, angle + 90);

            offScreenIndicators.Add(indicator);
        }
    }

    private void FindLocalPlayer()
{
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        foreach (PhotonView view in views)
        {
            if (view.IsMine && view.CompareTag("Player"))
            {
                target = view.transform;
                return;
            }
        }
        
        // Fallback for non-photon or if tag is missing
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) target = player.transform;
    }
}


