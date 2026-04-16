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
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinOrCreateRoom("TestRoom", new RoomOptions { MaxPlayers = 4 }, null);
    }

    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (hasSpawned) return;

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
            PhotonNetwork.Instantiate("male01_1", new Vector3(0, 5, 0), Quaternion.identity);
        }
        else
        {
            PhotonNetwork.Instantiate("male01_1", spawnPoint.position, spawnPoint.rotation);
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