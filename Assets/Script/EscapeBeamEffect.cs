using System.Collections;
using UnityEngine;

public class EscapeBeamEffect : MonoBehaviour
{
    [SerializeField] private float beamHeight = 60f;
    [SerializeField] private float beamWidth = 0.9f;
    [SerializeField] private Color beamColor = new Color(0.4f, 0.95f, 1f, 0.45f);
    [SerializeField] private Color coreColor = new Color(0.9f, 1f, 1f, 0.75f);

    private GameObject beamRoot;
    private bool built;
    private bool buildStarted;
    private bool wantActive;

    public void SetBeamActive(bool active)
    {
        wantActive = active;
        if (!built && active && !buildStarted)
            StartCoroutine(BuildBeamRoutine());
        else if (built)
            ApplyActiveState();
    }

    IEnumerator BuildBeamRoutine()
    {
        buildStarted = true;
        yield return null;
        yield return null;

        try
        {
            CreateBeaconBeam();
            built = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EscapeBeam] 빛기둥 생성 실패: {e.Message}");
            enabled = false;
            yield break;
        }

        ApplyActiveState();
    }

    void ApplyActiveState()
    {
        if (beamRoot != null)
            beamRoot.SetActive(wantActive);
    }

    void CreateBeaconBeam()
    {
        beamRoot = new GameObject("BeaconBeam");
        beamRoot.transform.SetParent(transform, false);
        beamRoot.transform.localPosition = Vector3.zero;

        Material outerMat = CreateBeamMaterial(beamColor);
        Material coreMat = CreateBeamMaterial(coreColor);

        // 신호기처럼 십자 배치된 세로 판 4장 — 실린더보다 가볍고 멀리서도 기둥처럼 보임
        for (int i = 0; i < 4; i++)
        {
            float yaw = i * 45f;
            CreateBeamPanel("Outer_" + i, beamWidth, beamHeight, yaw, outerMat, beamRoot.transform);
            CreateBeamPanel("Core_" + i, beamWidth * 0.35f, beamHeight, yaw, coreMat, beamRoot.transform);
        }

        beamRoot.SetActive(false);
    }

    void CreateBeamPanel(string name, float width, float height, float yaw, Material material, Transform parent)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = name;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        panel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        panel.transform.localScale = new Vector3(width, height, 1f);

        Collider col = panel.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer renderer = panel.GetComponent<Renderer>();
        if (material != null)
            renderer.sharedMaterial = material;
    }

    Material CreateBeamMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        Material material = new Material(shader);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        return material;
    }
}
