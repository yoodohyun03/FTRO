using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>각 클라이언트가 자신의 Role만 설정 (Photon 규칙 준수)</summary>
public static class RoleAssignmentHelper
{
    public const string RoleKey = "Role";
    public const string SeekerRole = "Seeker";
    public const string SurvivorRole = "Survivor";
    public const string AssignedSeekerKey = "AssignedSeeker";
    public const string BombHolderKey = "BombHolder";

    public static int ResolveSeekerActorNumber(Room room)
    {
        if (room == null) return -1;

        if (room.CustomProperties.TryGetValue(AssignedSeekerKey, out object assigned) && assigned is int assignedInt)
            return assignedInt;

        if (room.CustomProperties.TryGetValue(WaitingRoomController.SeekerSelectionKey, out object selected) && selected is int selectedInt && selectedInt >= 0)
            return selectedInt;

        return -1;
    }

    public static int ResolveBombHolderActorNumber(Room room)
    {
        if (room == null) return -1;

        if (room.CustomProperties.TryGetValue(BombHolderKey, out object holder) && holder is int holderInt && holderInt >= 0)
            return holderInt;

        return ResolveSeekerActorNumber(room);
    }

    public static void SetBombHolder(Room room, int actorNumber)
    {
        if (room == null) return;
        room.SetCustomProperties(new Hashtable { { BombHolderKey, actorNumber } });
    }

    public static bool TryApplyLocalRole(Room room)
    {
        if (room == null || !PhotonNetwork.InRoom) return false;

        int seekerActor = ResolveSeekerActorNumber(room);
        if (seekerActor < 0) return false;

        string role = PhotonNetwork.LocalPlayer.ActorNumber == seekerActor ? SeekerRole : SurvivorRole;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { RoleKey, role } });
        return true;
    }

    public static IEnumerator EnsureLocalRoleRoutine(float timeoutSeconds = 8f)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
                yield break;

            if (TryApplyLocalRole(PhotonNetwork.CurrentRoom))
                yield break;

            elapsed += 0.15f;
            yield return new WaitForSeconds(0.15f);
        }

        Debug.LogWarning("[RoleAssignment] 로컬 Role 할당 시간 초과");
    }

    public static bool IsSurvivor(Player player)
    {
        if (player == null) return false;
        return player.CustomProperties.TryGetValue(RoleKey, out object role) && (string)role == SurvivorRole;
    }

    public static bool IsSeeker(Player player)
    {
        if (player == null) return false;
        return player.CustomProperties.TryGetValue(RoleKey, out object role) && (string)role == SeekerRole;
    }

    public static bool IsAliveSurvivor(PlayerMove pm)
    {
        if (pm == null || pm.isDead) return false;

        if (pm.photonView != null && pm.photonView.Owner != null &&
            pm.photonView.Owner.CustomProperties.TryGetValue(RoleKey, out object role))
            return (string)role == SurvivorRole;

        return pm.myRole == SurvivorRole;
    }
}
