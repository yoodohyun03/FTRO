using UnityEngine;
using TMPro;

public class NoisePing : MonoBehaviour
{
    public float duration = 3.0f; // Increased duration
    private SpriteRenderer worldSprite;
    private SpriteRenderer minimapSprite;
    private SpriteRenderer pingRing;
    private float timer = 0f;
    private Transform minimapCamTransform;

    void Awake()
    {
        // Find Minimap Camera to match rotation
        MinimapFollow mf = Object.FindFirstObjectByType<MinimapFollow>();
        if (mf != null) minimapCamTransform = mf.transform;

        // Minimap-only sprite
        GameObject miniObj = new GameObject("MinimapSprite");
        miniObj.transform.SetParent(transform, false);
        minimapSprite = miniObj.AddComponent<SpriteRenderer>();
        minimapSprite.sprite = Resources.Load<Sprite>("NoiseExclamation");
        minimapSprite.color = Color.red;
        minimapSprite.sortingOrder = 100;
        miniObj.layer = 7;
        
        // Initial setup
        miniObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Add Expanding Ring (Ping effect)
        GameObject ringObj = new GameObject("PingRing");
        ringObj.transform.SetParent(transform, false);
        pingRing = ringObj.AddComponent<SpriteRenderer>();
        pingRing.sprite = Resources.Load<Sprite>("NoiseExclamation");
        pingRing.color = new Color(1, 0, 0, 0.5f);
        pingRing.sortingOrder = 99;
        ringObj.layer = 7;
        ringObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;
        float alpha = 1.0f - progress;
        
        // Sync rotation with Minimap Camera so it's always "upright" on the map
        if (minimapCamTransform != null)
        {
            float camY = minimapCamTransform.eulerAngles.y;
            minimapSprite.transform.rotation = Quaternion.Euler(90f, camY, 0f);
            pingRing.transform.rotation = Quaternion.Euler(90f, camY, 0f);
        }

        // Pulsing effect for minimap
        float pulse = 1.0f + Mathf.Sin(timer * 15f) * 0.2f;
        
        // Wider aspect ratio as requested (1.3f width multiplier)
        Vector3 baseScale = new Vector3(1.3f, 1.0f, 1.0f) * 4.5f;

        if (minimapSprite != null)
        {
            float flashAlpha = alpha * (0.6f + Mathf.PingPong(timer * 8f, 0.4f));
            // Intense red (no green/blue mix)
            minimapSprite.color = new Color(1, 0, 0, flashAlpha);
            minimapSprite.transform.localScale = baseScale * pulse;
        }

        if (pingRing != null)
        {
            float ringProgress = (timer * 2.5f) % 1.0f;
            float ringAlpha = (1.0f - ringProgress) * alpha;
            // Intense red for ring
            pingRing.color = new Color(1, 0, 0, ringAlpha);
            pingRing.transform.localScale = baseScale * (1.0f + ringProgress * 3.5f);
        }

        // Float up (optional, but keep it for visual dynamic on minimap)
        transform.position += Vector3.up * Time.deltaTime * 1.0f;
        
        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}

public class Billboard : MonoBehaviour
{
    private Transform camTransform;

    void Start()
    {
        if (Camera.main != null) camTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (camTransform != null)
        {
            transform.LookAt(transform.position + camTransform.forward);
        }
        else
        {
            camTransform = Camera.main?.transform;
        }
    }
}
