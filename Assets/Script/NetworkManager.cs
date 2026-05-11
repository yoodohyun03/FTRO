using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using UnityEngine.SceneManagement; // 씬 이름을 확인하기 위해 추가

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";

    public Transform[] sharedSpawnPoints;
    private bool hasSpawned;

    // 인스펙터에서 캐릭터 이름을 직접 설정하고 싶을 때를 위해 변수 추가
    [Header("캐릭터 설정")]
    public string playerPrefabName = "male01_1";

    void Start()
    {
        // 현재 씬이 서부 맵이라면 자동으로 프리팹 이름을 변경
        if (SceneManager.GetActiveScene().name == "WesternMapScene")
        {
            playerPrefabName = "WesternPlayer";
        }

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

        // 고정된 문자열 대신 playerPrefabName 변수를 사용합니다.
        if (spawnPoint == null)
        {
            PhotonNetwork.Instantiate(playerPrefabName, new Vector3(0, 5, 0), Quaternion.identity);
        }
        else
        {
            PhotonNetwork.Instantiate(playerPrefabName, spawnPoint.position, spawnPoint.rotation);
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