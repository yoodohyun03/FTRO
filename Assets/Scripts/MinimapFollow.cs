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

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        minimapUI = new GameObject("MinimapUI_Runtime");
        minimapUI.transform.SetParent(canvas.transform, false);
        minimapUI.transform.SetAsLastSibling();

        RawImage rawImage = minimapUI.AddComponent<RawImage>();
        rawImage.texture = minimapTexture;

        RectTransform rect = minimapUI.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(200, 200);

        // Add Border
        GameObject border = new GameObject("Border");
        border.transform.SetParent(minimapUI.transform, false);
        border.transform.SetAsFirstSibling();
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 0.5f);
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-5, -5);
        borderRect.offsetMax = new Vector2(5, 5);
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
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (minimapUI == null) SetupUI();
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


