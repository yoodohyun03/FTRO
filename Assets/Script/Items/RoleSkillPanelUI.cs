using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class RoleSkillPanelUI
{
    public const float SlotWidth = 248f;
    public const float SlotHeight = 96f;
    public const float PanelBottomOffset = 42f;

    public static Canvas FindOverlayCanvas(Transform excludeChildOf = null)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (excludeChildOf != null && canvas.transform.IsChildOf(excludeChildOf)) continue;
            return canvas;
        }

        return Object.FindFirstObjectByType<Canvas>();
    }

    public static TMP_FontAsset LoadFont()
    {
        return Resources.Load<TMP_FontAsset>("Font_1-4Regular SDF");
    }

    public static RectTransform CreateBottomPanel(Transform canvasTransform, string objectName, Vector2 panelSize)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(canvasTransform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, PanelBottomOffset);
        rt.sizeDelta = panelSize;
        return rt;
    }

    public class SkillSlot
    {
        public TextMeshProUGUI text;
        public Image cooldownOverlay;
    }

    public static SkillSlot CreateSkillSlot(Transform parent, Vector2 anchoredPosition)
    {
        GameObject slotObj = new GameObject("SkillSlot");
        slotObj.transform.SetParent(parent, false);

        RectTransform rtSlot = slotObj.AddComponent<RectTransform>();
        rtSlot.sizeDelta = new Vector2(SlotWidth, SlotHeight);
        rtSlot.anchoredPosition = anchoredPosition;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(slotObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform rtBg = bg.GetComponent<RectTransform>();
        rtBg.anchorMin = Vector2.zero;
        rtBg.anchorMax = Vector2.one;
        rtBg.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("SkillText");
        textObj.transform.SetParent(slotObj.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16f;
        text.lineSpacing = -4f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        TMP_FontAsset font = LoadFont();
        if (font != null) text.font = font;

        RectTransform rtText = text.GetComponent<RectTransform>();
        rtText.anchorMin = Vector2.zero;
        rtText.anchorMax = Vector2.one;
        rtText.offsetMin = new Vector2(8f, 6f);
        rtText.offsetMax = new Vector2(-8f, -6f);

        GameObject cdObj = new GameObject("CooldownOverlay");
        cdObj.transform.SetParent(slotObj.transform, false);
        Image cd = cdObj.AddComponent<Image>();
        cd.color = new Color(0.15f, 0.15f, 0.15f, 0.82f);
        cd.type = Image.Type.Filled;
        cd.fillMethod = Image.FillMethod.Vertical;
        cd.raycastTarget = false;
        RectTransform rtCd = cd.GetComponent<RectTransform>();
        rtCd.anchorMin = Vector2.zero;
        rtCd.anchorMax = Vector2.one;
        rtCd.sizeDelta = Vector2.zero;

        return new SkillSlot { text = text, cooldownOverlay = cd };
    }

    public static string FormatSkillText(string key, string name, string description, bool ready, float cooldownSeconds = 0f)
    {
        string desc = string.IsNullOrEmpty(description) ? "" : $"\n<size=12><color=#B0B0B0>{description}</color></size>";
        if (ready)
            return $"<b>[{key}] {name}</b>{desc}\n<size=11><color=#7DEE9A>사용 가능</color></size>";

        return $"<b>[{key}] {name}</b>{desc}\n<size=11><color=#FFAA66>{cooldownSeconds:F0}초</color></size>";
    }

    public static string FormatEmptySlot(string title, string description)
    {
        return $"<b>{title}</b>\n<size=12><color=#B0B0B0>{description}</color></size>";
    }
}
