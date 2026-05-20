using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";

    public Transform[] sharedSpawnPoints;
    private bool hasSpawned;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            // 방에 없는 채로 게임씬이 로드됐다면 타이틀로 복귀
            Debug.LogWarning("[NetworkManager] 방 밖에서 게임씬 진입 - 타이틀로 복귀합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
        }
    }

    void SpawnPlayer()
    {
        if (hasSpawned) return;

        Debug.Log($"[NetworkManager] 스폰 시작 — Region: {PhotonNetwork.CloudRegion}, Room: {PhotonNetwork.CurrentRoom?.Name}, Players: {PhotonNetwork.CurrentRoom?.PlayerCount}");

        // 자신의 역할 확인
        string myRole = "Survivor";  // 기본값
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
        {
            myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey];
        }

        Transform spawnPoint = null;
        Transform[] validPoints = sharedSpawnPoints?.Where(p => p != null).ToArray();

        if (validPoints != null && validPoints.Length > 0)
        {
            int spawnIndex = GetDeterministicSpawnIndex(myRole, validPoints.Length);
            if (spawnIndex >= 0)
            {
                spawnPoint = validPoints[spawnIndex];
                Debug.Log($"[{myRole}] 스폰: {spawnPoint.name} (index {spawnIndex})");
            }
        }

        // 스폰 포인트가 없으면 기본값 사용
        if (spawnPoint == null)
        {
            PhotonNetwork.Instantiate("playerPrefab", new Vector3(0, 5, 0), Quaternion.identity);
        }
        else
        {
            PhotonNetwork.Instantiate("playerPrefab", spawnPoint.position, spawnPoint.rotation);
        }

        hasSpawned = true;
    }

    int GetDeterministicSpawnIndex(string myRole, int pointCount)
    {
        if (pointCount <= 0) return -1;

        int seekerIndex = 0;

        if (myRole == SeekerRole)
        {
            return seekerIndex;
        }

        if (pointCount == 1)
        {
            return seekerIndex;
        }

        var survivorPlayers = PhotonNetwork.PlayerList
            .Where(p => !IsSeeker(p))
            .OrderBy(p => p.ActorNumber)
            .ToArray();

        int myOrder = 0;
        for (int i = 0; i < survivorPlayers.Length; i++)
        {
            if (survivorPlayers[i].ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                myOrder = i;
                break;
            }
        }

        int availableForSurvivor = pointCount - 1;
        int offset = myOrder % availableForSurvivor;
        return (seekerIndex + 1 + offset) % pointCount;
    }

    bool IsSeeker(Player player)
    {
        if (player.CustomProperties.ContainsKey(RoleKey))
        {
            return (string)player.CustomProperties[RoleKey] == SeekerRole;
        }

        return false;
    }
}