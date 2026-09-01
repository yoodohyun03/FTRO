using UnityEngine;

/// <summary>폭탄 돌리기 모드에서 술래(폭탄 보유자) 머리 위 비주얼</summary>
public class BombPassBombVisual : MonoBehaviour
{
    GameObject visual;
    PlayerMove playerMove;
    float pulse;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    void LateUpdate()
    {
        if (!BombPassManager.IsModeActive || playerMove == null)
        {
            SetVisible(false);
            return;
        }

        bool show = BombPassManager.instance != null &&
                    playerMove.photonView != null &&
                    playerMove.photonView.Owner != null &&
                    playerMove.photonView.Owner.ActorNumber == BombPassManager.instance.BombHolderActor &&
                    !playerMove.isDead;
        SetVisible(show);
        if (!show || visual == null) return;

        pulse += Time.deltaTime * 6f;
        float scale = 0.42f + Mathf.Sin(pulse) * 0.06f;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(0f, 2.1f, 0f);
    }

    public void Refresh()
    {
        bool show = playerMove != null &&
                    !playerMove.isDead &&
                    BombPassManager.instance != null &&
                    playerMove.photonView != null &&
                    playerMove.photonView.Owner != null &&
                    playerMove.photonView.Owner.ActorNumber == BombPassManager.instance.BombHolderActor;
        SetVisible(show);
    }

    void SetVisible(bool visible)
    {
        if (visible)
        {
            EnsureVisual();
            if (visual != null) visual.SetActive(true);
        }
        else if (visual != null)
        {
            visual.SetActive(false);
        }
    }

    void EnsureVisual()
    {
        if (visual != null) return;

        visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "BombVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        visual.transform.localScale = Vector3.one * 0.42f;

        Collider col = visual.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(1f, 0.25f, 0.1f, 0.95f);
    }

    void OnDestroy()
    {
        if (visual != null) Destroy(visual);
    }
}
