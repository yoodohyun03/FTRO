using UnityEngine;
using UnityEngine.SceneManagement;
#if USE_PHOTON_VOICE
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
#endif

#if USE_PHOTON_VOICE
/// <summary>
/// Photon Voice 2 + PUN2 전용. TitleManager·NetworkManager처럼 Pun 콜백으로 방 상태에 맞춥니다.
/// 씬의 전용 오브젝트에 VoiceManager와 PunVoiceClient를 같이 두면 됩니다(이 스크립트가 DDOL 처리).
/// 플레이어는 Instantiate 직후 Start() 이전이므로 Recorder 바인딩은 여러 프레임 재시도합니다.
/// </summary>
public class VoiceManager : MonoBehaviourPunCallbacks
#else
public class VoiceManager : MonoBehaviour
#endif
{
#if USE_PHOTON_VOICE
    static VoiceManager _instance;

    public static VoiceManager Instance => _instance;

    public bool IsInVoiceGameplay =>
        PhotonNetwork.InRoom && IsVoiceGameplayScene(SceneManager.GetActiveScene().name);

    public bool HasRecorder => _recorder != null;
    public bool IsPushToTalkEnabled => pushToTalk;
    public KeyCode PushToTalkKeyCode => pushToTalkKey;
    public bool IsTransmitAllowed => _recorder != null && _recorder.TransmitEnabled;
    public bool IsTransmitting => _recorder != null && _recorder.IsCurrentlyTransmitting;
    public bool IsRecordingEnabled => _recorder != null && _recorder.RecordingEnabled;

    public float MicPeakLevel
    {
        get
        {
            if (_recorder?.LevelMeter == null)
                return 0f;
            return _recorder.LevelMeter.CurrentPeakAmp;
        }
    }

    [Header("씬 전환")]
    [Tooltip("타이틀 씬이 내려가도 음성 클라이언트를 유지합니다.")]
    [SerializeField] bool persistAcrossScenes = true;

    [Header("PunVoiceClient")]
    [Tooltip("이 오브젝트에 PunVoiceClient가 없으면 붙입니다.")]
    [SerializeField] bool ensurePunVoiceClientOnThisObject = true;

    [Header("마이크")]
    [SerializeField] bool pushToTalk;
    [SerializeField] KeyCode pushToTalkKey = KeyCode.V;
    [SerializeField] Recorder recorderOverride;

    [SerializeField, Tooltip("PhotonVoiceView.Start·스폰 지연까지 대기(초)")]
    float recorderBindTimeoutSeconds = 10f;

    Recorder _recorder;
    Coroutine _bindRoutine;
    bool _micUsageLogged;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        if (ensurePunVoiceClientOnThisObject && GetComponent<PunVoiceClient>() == null)
            gameObject.AddComponent<PunVoiceClient>();

        ApplyPunVoiceClientSettings();

        if (GetComponent<VoiceStatusUI>() == null)
            gameObject.AddComponent<VoiceStatusUI>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PhotonNetwork.InRoom && IsVoiceGameplayScene(scene.name))
        {
            _recorder = null;
            _micUsageLogged = false;
            RequestBindRecorder();
            StartCoroutine(RefreshRemoteSpeakersDelayed());
        }
    }

    static bool IsVoiceGameplayScene(string sceneName)
    {
        return sceneName != "TitleScene";
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Start()
    {
        ApplyPunVoiceClientSettings();
        RequestBindRecorderIfNeeded();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        ApplyPunVoiceClientSettings();
        RequestBindRecorderIfNeeded();
        StartCoroutine(RefreshRemoteSpeakersDelayed());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        StartCoroutine(RefreshRemoteSpeakersDelayed());
    }

    IEnumerator RefreshRemoteSpeakersDelayed()
    {
        if (!PhotonNetwork.InRoom || !IsVoiceGameplayScene(SceneManager.GetActiveScene().name))
            yield break;

        for (int i = 0; i < 15; i++)
        {
            VoiceProximityAudio.RefreshAllRemoteSpeakers();
            yield return null;
        }
    }

    void RequestBindRecorderIfNeeded()
    {
        if (PhotonNetwork.InRoom && IsVoiceGameplayScene(SceneManager.GetActiveScene().name))
        {
            RequestBindRecorder();
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        _recorder = null;
        _micUsageLogged = false;
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }
    }

    void ApplyPunVoiceClientSettings()
    {
        var pvc = PunVoiceClient.Instance;
        if (pvc == null)
        {
            Debug.LogWarning("[VoiceManager] PunVoiceClient.Instance가 null입니다.");
            return;
        }

        pvc.UsePunAppSettings = true;
        pvc.AutoConnectAndJoin = true;
    }

    void RequestBindRecorder()
    {
        if (_bindRoutine != null)
            StopCoroutine(_bindRoutine);
        _bindRoutine = StartCoroutine(BindRecorderWhenReady());
    }

    IEnumerator BindRecorderWhenReady()
    {
        if (recorderOverride != null)
        {
            _recorder = recorderOverride;
            ApplyInitialTransmit();
            _bindRoutine = null;
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + recorderBindTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline && PhotonNetwork.InRoom)
        {
            ResolveRecorder();
            if (_recorder != null)
            {
                ApplyInitialTransmit();
                _bindRoutine = null;
                yield break;
            }

            yield return null;
        }

        ResolveRecorder();
        ApplyInitialTransmit();
        if (_recorder == null && IsVoiceGameplayScene(SceneManager.GetActiveScene().name))
            Debug.LogWarning(
                "[VoiceManager] Recorder를 찾지 못했습니다. " +
                "PhotonNetwork.Instantiate로 쓰는 프리팹(예: playerPrefab) 루트에 PhotonView + PhotonVoiceView + Recorder가 있는지, " +
                "Resources 폴더에 올바른 프리팹 이름이 있는지 확인하세요.");
        _bindRoutine = null;
    }

    void ResolveRecorder()
    {
        if (recorderOverride != null)
        {
            _recorder = recorderOverride;
            return;
        }

        foreach (var voiceView in FindObjectsByType<PhotonVoiceView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var pv = voiceView.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                _recorder = voiceView.RecorderInUse;
                if (_recorder == null)
                {
                    _recorder = voiceView.GetComponent<Recorder>();
                    if (_recorder == null)
                        _recorder = voiceView.GetComponentInChildren<Recorder>(true);
                }

                if (_recorder != null)
                    return;
            }
        }

        var pvc = PunVoiceClient.Instance;
        if (pvc != null && pvc.PrimaryRecorder != null)
            _recorder = pvc.PrimaryRecorder;
        if (_recorder == null)
        {
            foreach (var r in FindObjectsByType<Recorder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var pv = r.GetComponentInParent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    _recorder = r;
                    return;
                }
            }
        }
    }

    void ApplyInitialTransmit()
    {
        if (_recorder == null)
            return;
        _recorder.TransmitEnabled = !pushToTalk;
        TryLogMicInUse();
    }

    void TryLogMicInUse()
    {
        if (_micUsageLogged || _recorder == null || !PhotonNetwork.InRoom)
            return;

        if (!_recorder.RecordingEnabled || !_recorder.TransmitEnabled)
            return;

        _micUsageLogged = true;
        Debug.Log("[VoiceManager] 사용 중!");
    }

    void Update()
    {
        if (_recorder == null)
            return;

        if (!pushToTalk)
        {
            TryLogMicInUse();
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            _recorder.TransmitEnabled = false;
            return;
        }

        _recorder.TransmitEnabled = Input.GetKey(pushToTalkKey);
        TryLogMicInUse();
    }

    public void SetPushToTalk(bool enabled)
    {
        pushToTalk = enabled;
        if (_recorder != null && !pushToTalk)
        {
            _recorder.TransmitEnabled = true;
            TryLogMicInUse();
        }
    }

    public void SetMicTransmit(bool transmit)
    {
        if (_recorder != null)
        {
            _recorder.TransmitEnabled = transmit;
            TryLogMicInUse();
        }
    }
#else
    void Awake()
    {
        Debug.Log(
            "[VoiceManager] Photon Voice 2를 임포트하면 USE_PHOTON_VOICE 심볼이 생기고 이 컴포넌트가 활성화됩니다.");
    }
#endif
}
