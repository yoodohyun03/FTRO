using UnityEngine;
using TMPro;

public class NoisePing : MonoBehaviour
{
    public float duration = 3.0f; // Increased duration
    private SpriteRenderer worldSprite;
    private SpriteRenderer minimapSprite;
    private float timer = 0f;

    void Awake()
    {
        // World-space sprite
        GameObject worldObj = new GameObject("WorldSprite");
        worldObj.transform.SetParent(transform, false);
        worldSprite = worldObj.AddComponent<SpriteRenderer>();
        worldSprite.sprite = Resources.Load<Sprite>("NoiseExclamation");
        worldSprite.color = Color.red;
        worldObj.transform.localScale = Vector3.one * 1.2f; // Larger base scale
        worldObj.AddComponent<Billboard>();

        // Minimap-only sprite
        GameObject miniObj = new GameObject("MinimapSprite");
        miniObj.transform.SetParent(transform, false);
        minimapSprite = miniObj.AddComponent<SpriteRenderer>();
        minimapSprite.sprite = Resources.Load<Sprite>("NoiseExclamation");
        minimapSprite.color = Color.red;
        minimapSprite.sortingOrder = 100; // Ensure it's very top
        miniObj.layer = 7; // MinimapIcon layer
        miniObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        miniObj.transform.localScale = Vector3.one * 3.0f; // Significantly larger on minimap
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / duration;
        float alpha = 1.0f - progress;
        
        // Pulsing effect for both
        float pulse = 1.0f + Mathf.Sin(timer * 10f) * 0.15f;
        
        Color c = new Color(1, 0, 0, alpha);
        if (worldSprite != null) worldSprite.color = c;
        if (minimapSprite != null)
        {
            // Make it flash on minimap to be more annoying/noticeable
            float flashAlpha = alpha * (0.7f + Mathf.PingPong(timer * 5f, 0.3f));
            minimapSprite.color = new Color(1, 0, 0, flashAlpha);
        }
        
        // Float up
        transform.position += Vector3.up * Time.deltaTime * 1.0f;
        
        // Scaling logic
        if (worldSprite != null)
            worldSprite.transform.localScale = Vector3.one * (1.2f + progress * 0.8f) * pulse;

        if (minimapSprite != null)
            minimapSprite.transform.localScale = Vector3.one * 3.0f * pulse;

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
