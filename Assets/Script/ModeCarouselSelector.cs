using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>방 만들기 패널 — 맵 캐러셀 아래 게임 모드 좌/우 선택 UI</summary>
[ExecuteAlways]
public class ModeCarouselSelector : MonoBehaviour
{
    [SerializeField] private Vector2 carouselSize = new Vector2(720f, 100f);
    [SerializeField] private float arrowButtonWidth = 56f;
    [SerializeField] private float topOffset = -252f;
    [SerializeField] private float layoutShiftDown = 100f;

    public event Action<int> OnIndexChanged;

    private int currentIndex;
    private TextMeshProUGUI modeLabel;
    private TextMeshProUGUI pageLabel;
    private bool isBuilding;
    private bool layoutAdjusted;

    void OnEnable()
    {
        if (!CacheUiReferences())
        {
            BuildCarouselUi();
            CacheUiReferences();
        }

        WireArrowButtons();
        SelectIndex(currentIndex, notify: false);
    }

    public int CurrentIndex => currentIndex;

    public void SelectIndex(int index, bool notify = true)
    {
        currentIndex = (index % GameModeTypeHelper.Count + GameModeTypeHelper.Count) % GameModeTypeHelper.Count;
        RefreshDisplay();

        if (notify && Application.isPlaying)
            OnIndexChanged?.Invoke(currentIndex);
    }

    public void SelectNext() => SelectIndex(currentIndex + 1);
    public void SelectPrevious() => SelectIndex(currentIndex - 1);

    bool CacheUiReferences()
    {
        Transform carousel = transform.Find("ModeCarousel");
        if (carousel == null) return false;

        Transform labelTransform = carousel.Find("ModeFrame/ModeNameLabel");
        modeLabel = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;

        Transform pageTransform = carousel.Find("PageLabel");
        pageLabel = pageTransform != null ? pageTransform.GetComponent<TextMeshProUGUI>() : null;

        return modeLabel != null && pageLabel != null;
    }

    void WireArrowButtons()
    {
        Transform carousel = transform.Find("ModeCarousel");
        if (carousel == null) return;

        WireButton(carousel.Find("PrevButton"), SelectPrevious);
        WireButton(carousel.Find("NextButton"), SelectNext);
    }

    static void WireButton(Transform buttonTransform, Action onClick)
    {
        if (buttonTransform == null) return;
        Button button = buttonTransform.GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }

    void BuildCarouselUi()
    {
        if (isBuilding || transform.Find("ModeCarousel") != null)
            return;

        isBuilding = true;
        TMP_FontAsset font = FindSceneFont();
        Sprite arrowSprite = LoadUiSprite();

        GameObject rootObject = CreateUiObject("ModeCarousel", transform);
        RectTransform carouselRoot = rootObject.GetComponent<RectTransform>();
        carouselRoot.anchorMin = new Vector2(0.5f, 1f);
        carouselRoot.anchorMax = new Vector2(0.5f, 1f);
        carouselRoot.pivot = new Vector2(0.5f, 1f);
        carouselRoot.anchoredPosition = new Vector2(0f, topOffset);
        carouselRoot.sizeDelta = carouselSize;

        Button prevButton = CreateArrowButton(rootObject.transform, "PrevButton", "<", font, arrowSprite);
        RectTransform prevRect = prevButton.GetComponent<RectTransform>();
        prevRect.anchorMin = new Vector2(0f, 0.5f);
        prevRect.anchorMax = new Vector2(0f, 0.5f);
        prevRect.pivot = new Vector2(0f, 0.5f);
        prevRect.anchoredPosition = new Vector2(0f, 0f);
        prevRect.sizeDelta = new Vector2(arrowButtonWidth, carouselSize.y - 20f);

        Button nextButton = CreateArrowButton(rootObject.transform, "NextButton", ">", font, arrowSprite);
        RectTransform nextRect = nextButton.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0.5f);
        nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot = new Vector2(1f, 0.5f);
        nextRect.anchoredPosition = new Vector2(0f, 0f);
        nextRect.sizeDelta = new Vector2(arrowButtonWidth, carouselSize.y - 20f);

        float frameWidth = carouselSize.x - (arrowButtonWidth * 2f) - 24f;
        GameObject modeFrame = CreateUiObject("ModeFrame", rootObject.transform);
        RectTransform frameRect = modeFrame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = new Vector2(frameWidth, carouselSize.y - 24f);

        Image frameImage = modeFrame.AddComponent<Image>();
        frameImage.color = new Color(0.12f, 0.22f, 0.38f, 0.55f);
        frameImage.raycastTarget = false;

        Outline outline = modeFrame.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject nameObject = CreateUiObject("ModeNameLabel", modeFrame.transform);
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        StretchRect(nameRect, 8f);

        modeLabel = nameObject.AddComponent<TextMeshProUGUI>();
        modeLabel.font = font;
        modeLabel.fontSize = 26f;
        modeLabel.fontStyle = FontStyles.Bold;
        modeLabel.alignment = TextAlignmentOptions.Center;
        modeLabel.color = Color.white;
        modeLabel.enableWordWrapping = false;
        modeLabel.raycastTarget = false;

        GameObject pageObject = CreateUiObject("PageLabel", rootObject.transform);
        RectTransform pageRect = pageObject.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0f);
        pageRect.anchorMax = new Vector2(0.5f, 0f);
        pageRect.pivot = new Vector2(0.5f, 0f);
        pageRect.anchoredPosition = new Vector2(0f, 2f);
        pageRect.sizeDelta = new Vector2(200f, 24f);

        pageLabel = pageObject.AddComponent<TextMeshProUGUI>();
        pageLabel.font = font;
        pageLabel.fontSize = 18f;
        pageLabel.alignment = TextAlignmentOptions.Center;
        pageLabel.color = new Color(1f, 1f, 1f, 0.9f);
        pageLabel.raycastTarget = false;

        AdjustRoomFormLayout();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(gameObject);
#endif

        isBuilding = false;
    }

    void AdjustRoomFormLayout()
    {
        if (layoutAdjusted) return;

        string[] siblingNames = { "NameInput", "PublicToggle", "PassWordInputField", "InButton", "CaneclButton" };
        foreach (string childName in siblingNames)
        {
            Transform child = transform.Find(childName);
            if (child == null) continue;

            RectTransform rect = child as RectTransform;
            if (rect == null) continue;

            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - layoutShiftDown);
        }

        layoutAdjusted = true;
    }

    void RefreshDisplay()
    {
        if (modeLabel != null)
            modeLabel.text = GameModeTypeHelper.GetDisplayName(currentIndex);

        if (pageLabel != null)
            pageLabel.text = $"{currentIndex + 1} / {GameModeTypeHelper.Count}";
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = parent.gameObject.layer;
        uiObject.transform.SetParent(parent, false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(uiObject, "Create Mode Carousel UI");
#endif
        return uiObject;
    }

    static void StretchRect(RectTransform rectTransform, float padding)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(padding, padding);
        rectTransform.offsetMax = new Vector2(-padding, -padding);
    }

    static Button CreateArrowButton(Transform parent, string objectName, string label, TMP_FontAsset font, Sprite sprite)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.layer = parent.gameObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);
        StretchRect(labelObject.GetComponent<RectTransform>(), 0f);

        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.font = font;
        labelText.text = label;
        labelText.fontSize = 42f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        return button;
    }

    static TMP_FontAsset FindSceneFont()
    {
        TextMeshProUGUI existingLabel = FindAnyObjectByType<TextMeshProUGUI>();
        if (existingLabel != null && existingLabel.font != null)
            return existingLabel.font;
        return TMP_Settings.defaultFontAsset;
    }

    static Sprite LoadUiSprite()
    {
        Sprite sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
        if (sprite != null) return sprite;
        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
