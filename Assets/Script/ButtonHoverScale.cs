using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float duration = 0.1f;
    [SerializeField] private AudioClip pop; // 1. Pop sound assignable via Inspector
    
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    private AudioSource audioSource;
    private Coroutine audioCoroutine;

    void Awake()
    {
        originalScale = transform.localScale;
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void OnDisable()
    {
        // Reset scale when disabled to avoid getting stuck in scaled state
        transform.localScale = originalScale;
        if (audioSource != null) audioSource.Stop();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopPreviousAndStart(originalScale * hoverScale);
        PlayPopSound(); // 2. Trigger audio on hover
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopPreviousAndStart(originalScale);
    }

    private void PlayPopSound()
    {
        if (pop == null) return;
        
        if (audioCoroutine != null) StopCoroutine(audioCoroutine);
        audioCoroutine = StartCoroutine(PlayAudioForDuration(0.2f)); // 3. Play for exactly 0.2 seconds
    }

    private IEnumerator PlayAudioForDuration(float playTime)
    {
        audioSource.clip = pop;
        audioSource.Play();
        
        // Use WaitForSecondsRealtime to ensure it works even if Time.timeScale is 0
        yield return new WaitForSecondsRealtime(playTime);
        
        audioSource.Stop();
    }

    private void StopPreviousAndStart(Vector3 targetScale)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(targetScale));
    }

    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaledDeltaTime for UI
            transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }
        transform.localScale = targetScale;
    }
}
