using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if USE_PHOTON_VOICE
using Photon.Pun;
#endif

/// <summary>
/// 인게임 보이스 상태를 기존 Overlay Canvas 좌하단에만 표시합니다.
/// 별도 Canvas를 만들지 않아 다른 UI 레이아웃에 영향을 주지 않습니다.
/// </summary>
public class VoiceStatusUI : MonoBehaviour
{
    const float PanelWidth = 220f;
    const float PanelHeight = 54f;
    const float BottomOffset = 42f;
    const float LeftOffset = 12f;
    const float CanvasWaitSeconds = 8f;

    RectTransform _panelRoot;
    Image _dot;
    TextMeshProUGUI _statusText;
    Image _levelFill;
    Coroutine _attachRoutine;

#if USE_PHOTON_VOICE
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttachForCurrentScene();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelAttach();
        ClearPanelReference();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearPanelReference();
        if (ShouldShowInScene(scene.name))
            _attachRoutine = StartCoroutine(AttachWhenCanvasReady());
    }

    void TryAttachForCurrentScene()
    {
        if (!ShouldShowInScene(SceneManager.GetActiveScene().name))
            return;

        _attachRoutine = StartCoroutine(AttachWhenCanvasReady());
    }

    static bool ShouldShowInScene(string sceneName)
    {
        return sceneName != "TitleScene" && PhotonNetwork.InRoom;
    }

    IEnumerator AttachWhenCanvasReady()
    {
        float deadline = Time.realtimeSinceStartup + CanvasWaitSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!ShouldShowInScene(SceneManager.GetActiveScene().name))
            {
                _attachRoutine = null;
                yield break;
            }

            Canvas canvas = RoleSkillPanelUI.FindOverlayCanvas();
            if (canvas != null)
            {
                BuildPanel(canvas.transform);
                _attachRoutine = null;
                yield break;
            }

            yield return null;
        }

        _attachRoutine = null;
    }

    void Update()
    {
        VoiceManager voice = VoiceManager.Instance;
        if (_panelRoot == null || voice == null || !voice.IsInVoiceGameplay)
            return;

        Refresh(voice);
    }

    void Refresh(VoiceManager voice)
    {
        if (!voice.HasRecorder)
        {
            SetDot(new Color(0.55f, 0.55f, 0.55f));
            _statusText.text = "보이스: 연결 대기...";
            SetLevel(0f);
            return;
        }

        if (!voice.IsRecordingEnabled)
        {
            SetDot(new Color(0.9f, 0.35f, 0.35f));
            _statusText.text = "보이스: 마이크 비활성";
            SetLevel(0f);
            return;
        }

        if (voice.IsPushToTalkEnabled && !voice.IsTransmitAllowed)
        {
            SetDot(new Color(0.95f, 0.75f, 0.2f));
            _statusText.text = $"보이스: 대기 ({voice.PushToTalkKeyCode}키)";
            SetLevel(0f);
            return;
        }

        float level = voice.MicPeakLevel;
        SetLevel(level);

        if (voice.IsTransmitting)
        {
            SetDot(new Color(0.35f, 0.95f, 0.45f));
            _statusText.text = "보이스: 송신 중";
            return;
        }

        if (voice.IsTransmitAllowed)
        {
            SetDot(new Color(0.45f, 0.8f, 0.55f));
            _statusText.text = voice.IsPushToTalkEnabled
                ? $"보이스: 켜짐 ({voice.PushToTalkKeyCode}키)"
                : "보이스: 켜짐";
            return;
        }

        SetDot(new Color(0.9f, 0.35f, 0.35f));
        _statusText.text = "보이스: 송신 꺼짐";
        SetLevel(0f);
    }

    void SetDot(Color color)
    {
        if (_dot != null)
            _dot.color = color;
    }

    void SetLevel(float normalized)
    {
        if (_levelFill == null)
            return;

        normalized = Mathf.Clamp01(normalized * 4f);
        _levelFill.fillAmount = normalized;
        _levelFill.color = normalized > 0.05f
            ? new Color(0.35f, 0.95f, 0.45f, 0.95f)
            : new Color(0.35f, 0.35f, 0.35f, 0.7f);
    }

    void CancelAttach()
    {
        if (_attachRoutine == null)
            return;

        StopCoroutine(_attachRoutine);
        _attachRoutine = null;
    }

    void ClearPanelReference()
    {
        CancelAttach();
        _panelRoot = null;
        _dot = null;
        _statusText = null;
        _levelFill = null;
    }

    void BuildPanel(Transform canvasTransform)
    {
        if (_panelRoot != null)
            return;

        var panelObj = new GameObject("VoiceStatusPanel");
        panelObj.transform.SetParent(canvasTransform, false);

        _panelRoot = panelObj.AddComponent<RectTransform>();
        _panelRoot.anchorMin = new Vector2(0f, 0f);
        _panelRoot.anchorMax = new Vector2(0f, 0f);
        _panelRoot.pivot = new Vector2(0f, 0f);
        _panelRoot.anchoredPosition = new Vector2(LeftOffset, BottomOffset);
        _panelRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        var panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.72f);
        panelBg.raycastTarget = false;

        var dotObj = new GameObject("StatusDot");
        dotObj.transform.SetParent(panelObj.transform, false);
        var dotRt = dotObj.AddComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0f, 0.5f);
        dotRt.anchorMax = new Vector2(0f, 0.5f);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = new Vector2(14f, 6f);
        dotRt.sizeDelta = new Vector2(10f, 10f);
        _dot = dotObj.AddComponent<Image>();
        _dot.color = new Color(0.55f, 0.55f, 0.55f);
        _dot.raycastTarget = false;

        var textObj = new GameObject("StatusText");
        textObj.transform.SetParent(panelObj.transform, false);
        _statusText = textObj.AddComponent<TextMeshProUGUI>();
        _statusText.fontSize = 14f;
        _statusText.alignment = TextAlignmentOptions.MidlineLeft;
        _statusText.raycastTarget = false;
        _statusText.text = "보이스: -";
        TMP_FontAsset font = RoleSkillPanelUI.LoadFont();
        if (font != null)
            _statusText.font = font;

        var textRt = _statusText.rectTransform;
        textRt.anchorMin = new Vector2(0f, 0.5f);
        textRt.anchorMax = new Vector2(1f, 0.5f);
        textRt.pivot = new Vector2(0f, 0.5f);
        textRt.anchoredPosition = new Vector2(26f, 6f);
        textRt.sizeDelta = new Vector2(-32f, 22f);

        var levelBgObj = new GameObject("LevelBg");
        levelBgObj.transform.SetParent(panelObj.transform, false);
        var levelBgRt = levelBgObj.AddComponent<RectTransform>();
        levelBgRt.anchorMin = new Vector2(0f, 0f);
        levelBgRt.anchorMax = new Vector2(1f, 0f);
        levelBgRt.pivot = new Vector2(0.5f, 0f);
        levelBgRt.anchoredPosition = new Vector2(0f, 8f);
        levelBgRt.sizeDelta = new Vector2(-16f, 6f);
        var levelBg = levelBgObj.AddComponent<Image>();
        levelBg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        levelBg.raycastTarget = false;

        var levelFillObj = new GameObject("LevelFill");
        levelFillObj.transform.SetParent(levelBgObj.transform, false);
        var levelFillRt = levelFillObj.AddComponent<RectTransform>();
        levelFillRt.anchorMin = Vector2.zero;
        levelFillRt.anchorMax = Vector2.one;
        levelFillRt.sizeDelta = Vector2.zero;
        _levelFill = levelFillObj.AddComponent<Image>();
        _levelFill.color = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        _levelFill.type = Image.Type.Filled;
        _levelFill.fillMethod = Image.FillMethod.Horizontal;
        _levelFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _levelFill.fillAmount = 0f;
        _levelFill.raycastTarget = false;
    }
#else
    void Awake()
    {
        enabled = false;
    }
#endif
}
