using UnityEngine;
using UnityEngine.AI;
using System.Collections;
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
        Application.runInBackground = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 15;

        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            // 씬 로드 직후 InRoom이 잠깐 false일 수 있으므로 재시도
            StartCoroutine(WaitAndSpawn());
        }
    }

    IEnumerator WaitAndSpawn()
    {
        float elapsed = 0f;
        while (!PhotonNetwork.InRoom && elapsed < 5f)
        {
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }

        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            Debug.LogWarning("[NetworkManager] 5초 대기 후에도 방에 없음 - 타이틀로 복귀합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
        }
    }

    public override void OnJoinedRoom()
    {
        // WaitAndSpawn 보다 OnJoinedRoom이 먼저 오는 경우 대비
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (hasSpawned) return;

        Debug.Log($"[NetworkManager] ★ 스폰 시작 — Region: {PhotonNetwork.CloudRegion} | Room: {PhotonNetwork.CurrentRoom?.Name} | Players: {PhotonNetwork.CurrentRoom?.PlayerCount} | IsMaster: {PhotonNetwork.IsMasterClient}");

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

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : FindSafeSpawnPosition();
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        PhotonNetwork.Instantiate("playerPrefab", spawnPos, spawnRot);

        hasSpawned = true;
    }

    Vector3 FindSafeSpawnPosition()
    {
        // NavMesh 위 무작위 위치 탐색
        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                return hit.position + Vector3.up * 1f;
            }
        }
        // NavMesh도 없으면 원점 위
        return new Vector3(0f, 5f, 0f);
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