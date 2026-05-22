using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class MinimapFollow : MonoBehaviour
{
    public Transform target;
    public float height = 50f;
    public RenderTexture minimapTexture;
    
    private GameObject minimapUI;

    void Start()
    {
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
        rect.sizeDelta = new Vector2(150, 150);

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
        arrowRect.sizeDelta = new Vector2(24, 24);
        arrowRect.anchoredPosition = Vector2.zero; // Center of minimap
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        playerArrow = arrowRect;
    }

    private RectTransform playerArrow;

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
    }

    void FindLocalPlayer()
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


