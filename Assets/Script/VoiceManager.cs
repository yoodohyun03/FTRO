using UnityEngine;
#if USE_PHOTON_VOICE
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
#endif

/// <summary>
/// PUN2 + Photon Voice 2 연동 보조 스크립트.
/// Photon Voice 2를 Asset Store에서 임포트하면 USE_PHOTON_VOICE가 자동으로 켜집니다.
/// 씬에 PunVoiceClient가 있어야 하며, Primary Recorder에 Recorder를 지정하고 PunVoiceClient에서 Use Primary Recorder를 켜는 구성을 권장합니다.
/// 플레이어 프리팹에는 PhotonView와 함께 PhotonVoiceView를 추가하는 방식이 공식 권장입니다.
/// </summary>
public class VoiceManager : MonoBehaviour
{
#if USE_PHOTON_VOICE
    [Header("PunVoiceClient")]
    [Tooltip("씬에 PunVoiceClient가 없으면 DontDestroyOnLoad 오브젝트를 만들어 추가합니다.")]
    [SerializeField] bool createPunVoiceClientIfMissing = true;

    [Header("마이크 / 전송")]
    [SerializeField] bool pushToTalk;
    [SerializeField] KeyCode pushToTalkKey = KeyCode.V;
    [Tooltip("Inspector에서 지정한 Recorder가 우선합니다. 비어 있으면 PunVoiceClient.PrimaryRecorder 또는 자식에서 찾습니다.")]
    [SerializeField] Recorder recorderOverride;

    Recorder _recorder;

    void Start()
    {
        EnsurePunVoiceClient();
        var pvc = PunVoiceClient.Instance;
        if (pvc == null)
        {
            Debug.LogError("[VoiceManager] PunVoiceClient를 찾을 수 없습니다. 씬에 PunVoiceClient 컴포넌트를 추가하세요.");
            return;
        }

        pvc.UsePunAppSettings = true;
        pvc.AutoConnectAndJoin = true;
        pvc.AutoLeaveAndDisconnect = true;

        _recorder = recorderOverride != null ? recorderOverride : pvc.PrimaryRecorder;
        if (_recorder == null)
            _recorder = GetComponentInChildren<Recorder>(true);

        if (_recorder == null)
        {
            Debug.LogWarning("[VoiceManager] Recorder가 없습니다. PunVoiceClient의 Primary Recorder에 Recorder를 연결하거나, 이 오브젝트 자식에 Recorder를 두세요.");
            return;
        }

        if (!pushToTalk)
            _recorder.TransmitEnabled = true;
        else
            _recorder.TransmitEnabled = false;
    }

    void Update()
    {
        if (_recorder == null || !pushToTalk)
            return;

        if (!PhotonNetwork.InRoom)
        {
            _recorder.TransmitEnabled = false;
            return;
        }

        _recorder.TransmitEnabled = Input.GetKey(pushToTalkKey);
    }

    void EnsurePunVoiceClient()
    {
        if (PunVoiceClient.Instance != null)
            return;

        if (!createPunVoiceClientIfMissing)
            return;

        var host = new GameObject("PunVoiceClient");
        DontDestroyOnLoad(host);
        host.AddComponent<PunVoiceClient>();
    }

    public void SetPushToTalk(bool enabled)
    {
        pushToTalk = enabled;
        if (_recorder != null && !pushToTalk)
            _recorder.TransmitEnabled = true;
    }

    public void SetMicTransmit(bool transmit)
    {
        if (_recorder != null)
            _recorder.TransmitEnabled = transmit;
    }
#else
    void Awake()
    {
        Debug.Log(
            "[VoiceManager] Photon Voice 2를 Unity Asset Store에서 임포트하면 Voice 연동 코드가 활성화됩니다. " +
            "임포트 후 컴파일이 끝나면 USE_PHOTON_VOICE 심볼이 자동으로 추가됩니다.");
    }
#endif
}
