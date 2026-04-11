#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class AnchorTools : MonoBehaviour
{
    // UI 인스펙터 창에서 우클릭하면 이 메뉴가 생깁니다!
    [MenuItem("CONTEXT/RectTransform/Anchors to Corners (자동 앵커 맞춤)")]
    static void AnchorsToCorners(MenuCommand command)
    {
        RectTransform t = command.context as RectTransform;
        RectTransform pt = t.parent as RectTransform;

        if (t == null || pt == null) return;

        Undo.RecordObject(t, "Set Anchors to Corners");

        Vector2 newAnchorsMin = new Vector2(t.anchorMin.x + t.offsetMin.x / pt.rect.width,
                                            t.anchorMin.y + t.offsetMin.y / pt.rect.height);
        Vector2 newAnchorsMax = new Vector2(t.anchorMax.x + t.offsetMax.x / pt.rect.width,
                                            t.anchorMax.y + t.offsetMax.y / pt.rect.height);

        t.anchorMin = newAnchorsMin;
        t.anchorMax = newAnchorsMax;
        t.offsetMin = t.offsetMax = new Vector2(0, 0);
    }
}
#endif