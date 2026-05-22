using UnityEngine;
using Photon.Pun;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HeartbeatSystem : MonoBehaviourPun
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";

    [Header("Detection Settings")]
    public float maxDistance = 25f; // Heartbeat starts at this distance
    public float intenseDistance = 5f; // Max intensity at this distance

    [Header("Audio Settings")]
    public AudioSource heartbeatAudio;
    public AudioClip heartbeatClip;
    public float minVolume = 0f;
    public float maxVolume = 1f;

    [Header("Vignette Settings")]
    public Volume postProcessVolume;
    private Vignette vignette;

    private Transform seekerTransform;
    private PlayerMove localPlayerMove;
    private float updateTimer = 0f;

    void Start()
    {
        if (!photonView.IsMine) return;

        localPlayerMove = GetComponent<PlayerMove>();
        
        // Find or create AudioSource
        if (heartbeatAudio == null)
        {
            heartbeatAudio = gameObject.AddComponent<AudioSource>();
            heartbeatAudio.loop = true;
            heartbeatAudio.playOnAwake = false;
            heartbeatAudio.spatialBlend = 0f; // UI-like sound
        }

        // Load the heartbeat sound
        if (heartbeatClip != null)
        {
            heartbeatAudio.clip = heartbeatClip;
        }
        else
        {
            Debug.LogWarning("[HeartbeatSystem] Heartbeat sound clip not assigned.");
        }

        // Initialize Volume and Vignette
        SetupVolume();
    }

    void SetupVolume()
    {
        if (postProcessVolume == null)
        {
            postProcessVolume = Object.FindFirstObjectByType<Volume>();
        }

        if (postProcessVolume != null)
        {
            // Use profile for runtime modification (instantiates if needed)
            VolumeProfile profile = postProcessVolume.profile;
            if (!profile.TryGet(out vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        
        // Ensure localPlayerMove is valid
        if (localPlayerMove == null) localPlayerMove = GetComponent<PlayerMove>();

        if (localPlayerMove.myRole == SeekerRole || localPlayerMove.isDead)
        {
            StopEffects();
            return;
        }

        // Periodic update for seeker transform
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            UpdateSeekerTransform();
            updateTimer = 1f; // Check every second
        }

        if (seekerTransform == null)
        {
            StopEffects();
            return;
        }

        float distance = Vector3.Distance(transform.position, seekerTransform.position);
        
        if (distance < maxDistance)
        {
            float t = 1f - Mathf.Clamp01((distance - intenseDistance) / (maxDistance - intenseDistance));
            ApplyEffects(t);
        }
        else
        {
            StopEffects();
        }
    }

    void UpdateSeekerTransform()
    {
        if (seekerTransform != null && seekerTransform.gameObject.activeInHierarchy)
        {
            // Verify it's still the seeker (roles could theoretically change, but unlikely here)
            return;
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            PhotonView pv = p.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null && pv.Owner.CustomProperties.ContainsKey(RoleKey))
            {
                if ((string)pv.Owner.CustomProperties[RoleKey] == SeekerRole)
                {
                    seekerTransform = p.transform;
                    break;
                }
            }
        }
    }

    void ApplyEffects(float intensity)
    {
        // Audio
        if (heartbeatAudio != null && heartbeatAudio.clip != null)
        {
            if (!heartbeatAudio.isPlaying) heartbeatAudio.Play();
            heartbeatAudio.volume = Mathf.Lerp(minVolume, maxVolume, intensity);
        }

        // Vignette
        if (vignette == null) SetupVolume(); // Retry setup if lost

        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = Mathf.Lerp(0f, 0.5f, intensity);
            vignette.color.overrideState = true;
            vignette.color.value = Color.red;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 1f;
        }
    }

    void StopEffects()
    {
        if (heartbeatAudio != null && heartbeatAudio.isPlaying)
        {
            heartbeatAudio.Stop();
        }

        if (vignette != null)
        {
            vignette.intensity.value = 0f;
        }
    }
}
