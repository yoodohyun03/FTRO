using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
/// <summary>
/// 폭탄 돌리기 모드: 5분 동안 폭탄을 넘기고, 시간 종료 시 폭탄 보유자가 패배합니다.
/// </summary>
public class BombPassManager : MonoBehaviourPunCallbacks
{
    public static BombPassManager instance;

    public int BombHolderActor { get; private set; } = -1;

    TextMeshProUGUI bombStatusText;

    const float TransientDisplayDuration = 1.25f;
    public const float FinalPhaseThresholdSeconds = 10f;

    public float finalPhaseSpeedMultiplier = 1.45f;

    public static bool FinalPhaseActive { get; private set; }

    Coroutine centerMessageRoutine;
    Coroutine holderAlertRoutine;

    public static bool IsModeActive => GameManager.CurrentGameMode == GameModeType.BombPass;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        FinalPhaseActive = false;
    }

    public void TryActivateFinalPhase()
    {
        if (!IsModeActive || FinalPhaseActive || !PhotonNetwork.IsMasterClient) return;

        FinalPhaseActive = true;
        photonView.RPC(nameof(RPC_SyncFinalPhase), RpcTarget.All, true);
    }

    public static bool ShouldBoostSeekerSpeed =>
        IsModeActive && FinalPhaseActive;

    [PunRPC]
    void RPC_SyncFinalPhase(bool active)
    {
        FinalPhaseActive = active;
    }

    public void StartBombMode()
    {
        if (!IsModeActive || !PhotonNetwork.IsMasterClient) return;

        FinalPhaseActive = false;
        photonView.RPC(nameof(RPC_SyncFinalPhase), RpcTarget.All, false);

        if (BombHolderActor >= 0) return;

        int seekerActor = RoleAssignmentHelper.ResolveBombHolderActorNumber(PhotonNetwork.CurrentRoom);
        if (seekerActor < 0) return;

        BombHolderActor = seekerActor;
        RoleAssignmentHelper.SetBombHolder(PhotonNetwork.CurrentRoom, BombHolderActor);
        photonView.RPC(nameof(RPC_SyncBombHolder), RpcTarget.AllBuffered, BombHolderActor);
        photonView.RPC(nameof(RPC_ShowTransientCenterMessage), RpcTarget.All,
            "5분 동안 폭탄을 넘기세요!\n시간 종료 시 폭탄 보유자 패배", BombHolderActor, TransientDisplayDuration);
        photonView.RPC(nameof(RPC_ShowBombHolderAlert), RpcTarget.All, BombHolderActor, TransientDisplayDuration);
    }

    public bool IsInitialized => BombHolderActor >= 0;

    public void RequestBombTransfer(int targetViewId, int attackerViewId)
    {
        if (!IsModeActive) return;
        photonView.RPC(nameof(RPC_RequestBombTransfer), RpcTarget.MasterClient, targetViewId, attackerViewId);
    }

    public void HandleGameTimeExpired()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (GameManager.instance != null && GameManager.instance.currentState != GameManager.GameState.Playing) return;

        if (BombHolderActor < 0)
        {
            photonView.RPC(nameof(RPC_EndBombPassGame), RpcTarget.All, -1);
            return;
        }

        PlayerMove holder = FindPlayerMoveByActor(BombHolderActor);
        if (holder != null)
            photonView.RPC(nameof(RPC_PlayExplosionFx), RpcTarget.All, holder.transform.position);

        photonView.RPC(nameof(RPC_EndBombPassGame), RpcTarget.All, BombHolderActor);
    }

    [PunRPC]
    void RPC_RequestBombTransfer(int targetViewId, int attackerViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonView targetView = PhotonView.Find(targetViewId);
        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (targetView == null || attackerView == null) return;

        PlayerMove targetPm = targetView.GetComponent<PlayerMove>();
        PlayerMove attackerPm = attackerView.GetComponent<PlayerMove>();
        if (targetPm == null || attackerPm == null) return;
        if (!RoleAssignmentHelper.IsAliveSurvivor(targetPm)) return;
        if (attackerPm.myRole != RoleAssignmentHelper.SeekerRole || attackerPm.isDead) return;
        if (attackerPm.photonView.Owner == null || attackerPm.photonView.Owner.ActorNumber != BombHolderActor) return;

        TransferBomb(attackerPm, targetPm);
    }

    void TransferBomb(PlayerMove fromSeeker, PlayerMove toSurvivor)
    {
        BombHolderActor = toSurvivor.photonView.Owner.ActorNumber;

        RoleAssignmentHelper.SetBombHolder(PhotonNetwork.CurrentRoom, BombHolderActor);

        fromSeeker.photonView.RPC(nameof(PlayerMove.RPC_ApplyRole), RpcTarget.All, RoleAssignmentHelper.SurvivorRole);
        toSurvivor.photonView.RPC(nameof(PlayerMove.RPC_ApplyRole), RpcTarget.All, RoleAssignmentHelper.SeekerRole);

        photonView.RPC(nameof(RPC_SyncBombHolder), RpcTarget.AllBuffered, BombHolderActor);
        photonView.RPC(nameof(RPC_ShowTransientCenterMessage), RpcTarget.All,
            "폭탄을 받았습니다!", BombHolderActor, TransientDisplayDuration);
        photonView.RPC(nameof(RPC_ShowBombHolderAlert), RpcTarget.All, BombHolderActor, TransientDisplayDuration);

        if (GameManager.instance != null)
            GameManager.instance.RefreshSurvivorCount();
    }

    [PunRPC]
    void RPC_EndBombPassGame(int loserActor)
    {
        FinalPhaseActive = false;

        string message;
        if (loserActor < 0)
            message = "폭탄 보유자 이탈!\n승리!";
        else
        {
            bool isLoser = PhotonNetwork.LocalPlayer.ActorNumber == loserActor;
            message = isLoser
                ? "시간 종료!\n폭탄 보유 — 패배!"
                : "시간 종료!\n폭탄 보유자 패배 — 승리!";
        }

        if (GameManager.instance != null)
            GameManager.instance.EndWithMessage(message);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!IsModeActive || !PhotonNetwork.IsMasterClient) return;
        if (GameManager.instance == null || GameManager.instance.currentState != GameManager.GameState.Playing) return;
        if (otherPlayer.ActorNumber != BombHolderActor) return;

        ReassignBombHolderAfterLeave();
    }

    void ReassignBombHolderAfterLeave()
    {
        var alivePlayers = new List<PlayerMove>();
        foreach (PlayerMove pm in Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if (pm == null || pm.isDead || pm.photonView == null || pm.photonView.Owner == null) continue;
            alivePlayers.Add(pm);
        }

        var survivors = new List<PlayerMove>();
        foreach (PlayerMove pm in alivePlayers)
        {
            if (RoleAssignmentHelper.IsAliveSurvivor(pm))
                survivors.Add(pm);
        }

        if (survivors.Count == 0 || alivePlayers.Count <= 1)
        {
            photonView.RPC(nameof(RPC_EndBombPassGame), RpcTarget.All, -1);
            return;
        }

        foreach (PlayerMove pm in alivePlayers)
        {
            if (pm.myRole == RoleAssignmentHelper.SeekerRole)
                pm.photonView.RPC(nameof(PlayerMove.RPC_ApplyRole), RpcTarget.All, RoleAssignmentHelper.SurvivorRole);
        }

        PlayerMove newHolder = survivors[Random.Range(0, survivors.Count)];
        newHolder.photonView.RPC(nameof(PlayerMove.RPC_ApplyRole), RpcTarget.All, RoleAssignmentHelper.SeekerRole);

        BombHolderActor = newHolder.photonView.Owner.ActorNumber;
        RoleAssignmentHelper.SetBombHolder(PhotonNetwork.CurrentRoom, BombHolderActor);
        photonView.RPC(nameof(RPC_SyncBombHolder), RpcTarget.AllBuffered, BombHolderActor);
    }

    [PunRPC]
    void RPC_SyncBombHolder(int actorNumber)
    {
        BombHolderActor = actorNumber;
        RefreshAllBombVisuals();
        HideBombStatusUi();
        if (GameManager.instance != null)
            GameManager.instance.photonView.RPC("SyncRoleIndicator", RpcTarget.All);
    }

    [PunRPC]
    void RPC_ShowTransientCenterMessage(string message, int targetActor, float duration)
    {
        if (targetActor >= 0 && PhotonNetwork.LocalPlayer.ActorNumber != targetActor)
            return;

        if (centerMessageRoutine != null)
            StopCoroutine(centerMessageRoutine);
        centerMessageRoutine = StartCoroutine(ShowCenterMessageRoutine(message, duration));
    }

    [PunRPC]
    void RPC_ShowBombHolderAlert(int holderActor, float duration)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != holderActor)
            return;

        if (holderAlertRoutine != null)
            StopCoroutine(holderAlertRoutine);
        holderAlertRoutine = StartCoroutine(ShowHolderAlertRoutine(duration));
    }

    IEnumerator ShowCenterMessageRoutine(string message, float duration)
    {
        if (GameManager.instance != null && GameManager.instance.centerText != null)
            GameManager.instance.centerText.text = message;

        yield return new WaitForSeconds(duration);

        if (GameManager.instance != null &&
            GameManager.instance.currentState != GameManager.GameState.End &&
            GameManager.instance.centerText != null)
            GameManager.instance.centerText.text = string.Empty;

        centerMessageRoutine = null;
    }

    IEnumerator ShowHolderAlertRoutine(float duration)
    {
        EnsureBombTimerUi();
        if (bombStatusText == null) yield break;

        bombStatusText.gameObject.SetActive(true);
        bombStatusText.text = "💣 당신이 폭탄을 가지고 있습니다!";
        bombStatusText.color = new Color(1f, 0.4f, 0.3f);

        yield return new WaitForSeconds(duration);

        HideBombStatusUi();
        holderAlertRoutine = null;
    }

    void HideBombStatusUi()
    {
        if (bombStatusText == null) return;
        bombStatusText.text = string.Empty;
        bombStatusText.gameObject.SetActive(false);
    }

    [PunRPC]
    void RPC_PlayExplosionFx(Vector3 position)
    {
        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = position + Vector3.up * 1f;
        fx.transform.localScale = Vector3.one * 3f;
        var renderer = fx.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(1f, 0.35f, 0.05f, 0.85f);
        var col = fx.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Destroy(fx, 1.2f);
    }

    public void EnsureBombTimerUi()
    {
        if (bombStatusText != null) return;

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        GameObject obj = new GameObject("BombStatusText");
        obj.transform.SetParent(canvas.transform, false);
        bombStatusText = obj.AddComponent<TextMeshProUGUI>();
        bombStatusText.fontSize = 26f;
        bombStatusText.fontStyle = FontStyles.Bold;
        bombStatusText.alignment = TextAlignmentOptions.Center;
        bombStatusText.raycastTarget = false;

        RectTransform rect = bombStatusText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -120f);
        rect.sizeDelta = new Vector2(520f, 40f);

        bombStatusText.gameObject.SetActive(false);
    }

    static PlayerMove FindPlayerMoveByActor(int actorNumber)
    {
        foreach (PlayerMove pm in Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if (pm.photonView != null && pm.photonView.Owner != null &&
                pm.photonView.Owner.ActorNumber == actorNumber)
                return pm;
        }
        return null;
    }

    static void RefreshAllBombVisuals()
    {
        foreach (BombPassBombVisual visual in Object.FindObjectsByType<BombPassBombVisual>(FindObjectsSortMode.None))
            visual.Refresh();
    }

    static Canvas FindOverlayCanvas()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;
        }
        return Object.FindFirstObjectByType<Canvas>();
    }
}
