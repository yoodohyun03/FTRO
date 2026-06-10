using System.Collections;
#if USE_PHOTON_VOICE
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
#endif
using UnityEngine;

/// <summary>
/// 역할(술래/생존자)과 무관하게, 가까이 있는 플레이어의 보이스를 3D로 들리게 합니다.
/// </summary>
[DisallowMultipleComponent]
public class VoiceProximityAudio : MonoBehaviour
{
    [SerializeField] float minDistance = 2f;
    [SerializeField] float maxDistance = 32f;
    [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;
    [SerializeField] float linkRetryInterval = 1f;

    float _nextLinkAttempt;

#if USE_PHOTON_VOICE
    PhotonView _photonView;
    PhotonVoiceView _voiceView;

    void Awake()
    {
        _photonView = GetComponent<PhotonView>();
        _voiceView = GetComponent<PhotonVoiceView>();
    }

    void OnEnable()
    {
        StartCoroutine(InitializeWhenReady());
    }

    IEnumerator InitializeWhenReady()
    {
        for (int i = 0; i < 30; i++)
        {
            if (!isActiveAndEnabled || _photonView == null)
                yield break;

            Speaker speaker = GetSpeaker();
            if (speaker != null)
            {
                ApplyProximitySettings(speaker);
                TryLinkSpeaker(speaker);
                yield break;
            }

            yield return null;
        }
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || _photonView == null || _photonView.IsMine)
            return;

        if (Time.unscaledTime < _nextLinkAttempt)
            return;

        _nextLinkAttempt = Time.unscaledTime + linkRetryInterval;

        Speaker speaker = GetSpeaker();
        if (speaker == null)
            return;

        ApplyProximitySettings(speaker);

        if (!speaker.IsLinked)
            TryLinkSpeaker(speaker);
    }

    Speaker GetSpeaker()
    {
        if (_voiceView != null && _voiceView.SpeakerInUse != null)
            return _voiceView.SpeakerInUse;

        return GetComponentInChildren<Speaker>(true);
    }

    void ApplyProximitySettings(Speaker speaker)
    {
        AudioSource source = speaker.GetComponent<AudioSource>();
        if (source == null)
            return;

        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;
        source.volume = 1f;
    }

    void TryLinkSpeaker(Speaker speaker)
    {
        PunVoiceClient pvc = PunVoiceClient.Instance;
        if (pvc == null || pvc.Client == null || !pvc.Client.InRoom)
            return;

        pvc.AddSpeaker(speaker, _photonView.ViewID);
    }

    public static void RefreshAllRemoteSpeakers()
    {
        if (!PhotonNetwork.InRoom)
            return;

        VoiceProximityAudio[] proximityAudios = FindObjectsByType<VoiceProximityAudio>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (VoiceProximityAudio proximityAudio in proximityAudios)
        {
            if (proximityAudio == null || proximityAudio._photonView == null || proximityAudio._photonView.IsMine)
                continue;

            Speaker speaker = proximityAudio.GetSpeaker();
            if (speaker == null)
                continue;

            proximityAudio.ApplyProximitySettings(speaker);
            proximityAudio.TryLinkSpeaker(speaker);
        }
    }
#endif
}
