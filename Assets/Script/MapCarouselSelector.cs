using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MapCarouselSelector : MonoBehaviour
{
    [Serializable]
    public class MapOption
    {
        public string displayName = "City";
        public Sprite previewSprite;
        public Color placeholderColor = new Color(0.25f, 0.45f, 0.7f, 1f);
    }

    [SerializeField] private MapOption[] mapOptions =
    {
        new MapOption { displayName = "City", placeholderColor = new Color(0.3f, 0.55f, 0.85f) },
        new MapOption { displayName = "West", placeholderColor = new Color(0.85f, 0.55f, 0.25f) },
        new MapOption { displayName = "CityMap", placeholderColor = new Color(0.35f, 0.75f, 0.45f) },
        new MapOption { displayName = "Random", placeholderColor = new Color(0.55f, 0.35f, 0.75f) },
    };

    [SerializeField] private Vector2 carouselSize = new Vector2(720f, 220f);
    [SerializeField] private float arrowButtonWidth = 56f;
    [SerializeField] private float previewHeight = 180f;

    public event Action<int> OnIndexChanged;

    private int currentIndex;
    private Image previewImage;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI pageLabel;
    private RectTransform carouselRoot;
    private Sprite[] placeholderSprites;
    private bool isBuilding;

    void OnEnable()
    {
        HideLegacyToggles();

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
        if (mapOptions == null || mapOptions.Length == 0)
        {
            return;
        }

        currentIndex = (index % mapOptions.Length + mapOptions.Length) % mapOptions.Length;
        RefreshDisplay();

        if (notify && Application.isPlaying)
        {
            OnIndexChanged?.Invoke(currentIndex);
        }
    }

    public void SelectNext()
    {
        SelectIndex(currentIndex + 1);
    }

    public void SelectPrevious()
    {
        SelectIndex(currentIndex - 1);
    }

    void HideLegacyToggles()
    {
        string[] legacyNames =
        {
            "CitySceneToggle",
            "WesternSceneToggle",
            "CityMapSceneToggle",
            "RandomMapToggle",
        };

        foreach (string legacyName in legacyNames)
        {
            Transform legacy = transform.Find(legacyName);
            if (legacy != null && legacy.gameObject.activeSelf)
            {
                legacy.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(legacy.gameObject);
                }
#endif
            }
        }
    }

    bool CacheUiReferences()
    {
        Transform carousel = transform.Find("MapCarousel");
        if (carousel == null)
        {
            return false;
        }

        carouselRoot = carousel as RectTransform;

        Transform previewTransform = carousel.Find("PreviewFrame/PreviewImage");
        previewImage = previewTransform != null ? previewTransform.GetComponent<Image>() : null;

        Transform nameTransform = carousel.Find("PreviewFrame/MapNameLabel");
        nameLabel = nameTransform != null ? nameTransform.GetComponent<TextMeshProUGUI>() : null;

        Transform pageTransform = carousel.Find("PageLabel");
        pageLabel = pageTransform != null ? pageTransform.GetComponent<TextMeshProUGUI>() : null;

        return previewImage != null && nameLabel != null && pageLabel != null;
    }

    void WireArrowButtons()
    {
        Transform carousel = transform.Find("MapCarousel");
        if (carousel == null)
        {
            return;
        }

        WireButton(carousel.Find("PrevButton"), SelectPrevious);
        WireButton(carousel.Find("NextButton"), SelectNext);
    }

    static void WireButton(Transform buttonTransform, Action onClick)
    {
        if (buttonTransform == null)
        {
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }

    void BuildCarouselUi()
    {
        if (isBuilding || transform.Find("MapCarousel") != null)
        {
            return;
        }

        isBuilding = true;

        TMP_FontAsset font = FindSceneFont();
        Sprite arrowSprite = LoadUiSprite();

        GameObject rootObject = CreateUiObject("MapCarousel", transform);
        carouselRoot = rootObject.GetComponent<RectTransform>();
        carouselRoot.anchorMin = new Vector2(0.5f, 1f);
        carouselRoot.anchorMax = new Vector2(0.5f, 1f);
        carouselRoot.pivot = new Vector2(0.5f, 1f);
        carouselRoot.anchoredPosition = new Vector2(0f, -24f);
        carouselRoot.sizeDelta = carouselSize;

        Button prevButton = CreateArrowButton(rootObject.transform, "PrevButton", "<", font, arrowSprite);
        RectTransform prevRect = prevButton.GetComponent<RectTransform>();
        prevRect.anchorMin = new Vector2(0f, 0.5f);
        prevRect.anchorMax = new Vector2(0f, 0.5f);
        prevRect.pivot = new Vector2(0f, 0.5f);
        prevRect.anchoredPosition = new Vector2(0f, -8f);
        prevRect.sizeDelta = new Vector2(arrowButtonWidth, previewHeight);

        Button nextButton = CreateArrowButton(rootObject.transform, "NextButton", ">", font, arrowSprite);
        RectTransform nextRect = nextButton.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0.5f);
        nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot = new Vector2(1f, 0.5f);
        nextRect.anchoredPosition = new Vector2(0f, -8f);
        nextRect.sizeDelta = new Vector2(arrowButtonWidth, previewHeight);

        float previewWidth = carouselSize.x - (arrowButtonWidth * 2f) - 24f;
        GameObject previewFrame = CreateUiObject("PreviewFrame", rootObject.transform);
        RectTransform frameRect = previewFrame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = new Vector2(0f, 0f);
        frameRect.sizeDelta = new Vector2(previewWidth, previewHeight);

        Image frameImage = previewFrame.AddComponent<Image>();
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;

        Outline frameOutline = previewFrame.AddComponent<Outline>();
        frameOutline.effectColor = Color.white;
        frameOutline.effectDistance = new Vector2(3f, -3f);

        GameObject previewImageObject = CreateUiObject("PreviewImage", previewFrame.transform);
        RectTransform previewRect = previewImageObject.GetComponent<RectTransform>();
        StretchRect(previewRect, 6f);

        previewImage = previewImageObject.AddComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.type = Image.Type.Simple;
        previewImage.raycastTarget = false;

        GameObject nameObject = CreateUiObject("MapNameLabel", previewFrame.transform);
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -10f);
        nameRect.sizeDelta = new Vector2(previewWidth - 20f, 42f);

        nameLabel = nameObject.AddComponent<TextMeshProUGUI>();
        nameLabel.font = font;
        nameLabel.fontSize = 30f;
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.color = Color.white;
        nameLabel.enableWordWrapping = false;
        nameLabel.raycastTarget = false;

        Shadow nameShadow = nameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        nameShadow.effectDistance = new Vector2(2f, -2f);

        GameObject pageObject = CreateUiObject("PageLabel", rootObject.transform);
        RectTransform pageRect = pageObject.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 0f);
        pageRect.anchorMax = new Vector2(0.5f, 0f);
        pageRect.pivot = new Vector2(0.5f, 0f);
        pageRect.anchoredPosition = new Vector2(0f, 2f);
        pageRect.sizeDelta = new Vector2(200f, 28f);

        pageLabel = pageObject.AddComponent<TextMeshProUGUI>();
        pageLabel.font = font;
        pageLabel.fontSize = 20f;
        pageLabel.alignment = TextAlignmentOptions.Center;
        pageLabel.color = new Color(1f, 1f, 1f, 0.9f);
        pageLabel.raycastTarget = false;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(gameObject);
        }
#endif

        isBuilding = false;
    }

    void RefreshDisplay()
    {
        if (mapOptions == null || mapOptions.Length == 0)
        {
            return;
        }

        MapOption option = mapOptions[currentIndex];
        if (previewImage != null)
        {
            previewImage.sprite = option.previewSprite != null
                ? option.previewSprite
                : GetPlaceholderSprite(currentIndex, option.placeholderColor);
            previewImage.color = Color.white;
        }

        if (nameLabel != null)
        {
            nameLabel.text = option.displayName;
        }

        if (pageLabel != null)
        {
            pageLabel.text = $"{currentIndex + 1} / {mapOptions.Length}";
        }
    }

    Sprite GetPlaceholderSprite(int index, Color color)
    {
        if (placeholderSprites == null || placeholderSprites.Length != mapOptions.Length)
        {
            placeholderSprites = new Sprite[mapOptions.Length];
        }

        if (placeholderSprites[index] != null)
        {
            return placeholderSprites[index];
        }

        const int width = 640;
        const int height = 360;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color top = Color.Lerp(color, Color.white, 0.15f);
        Color bottom = Color.Lerp(color, Color.black, 0.2f);

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            Color rowColor = Color.Lerp(bottom, top, t);
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, rowColor);
            }
        }

        texture.Apply();
        placeholderSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return placeholderSprites[index];
    }

    GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = parent.gameObject.layer;
        uiObject.transform.SetParent(parent, false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(uiObject, "Create Map Carousel UI");
        }
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

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        StretchRect(labelRect, 0f);

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
        {
            return existingLabel.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    static Sprite LoadUiSprite()
    {
        Sprite sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
