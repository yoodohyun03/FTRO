using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.AI;

public class AISpawner : MonoBehaviourPunCallbacks
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy";
    public int spawnCount = 50;
    public float spawnRadius = 80f;
    [Range(0.0f, 1.0f)]
    public float heightOffset = 0.5f;

    bool hasSpawned;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(SpawnWithDelay());
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient || hasSpawned) return;

        RandomRoam[] existing = Object.FindObjectsByType<RandomRoam>(FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0)
        {
            hasSpawned = true;
            return;
        }

        StartCoroutine(SpawnWithDelay());
    }

    System.Collections.IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(1f);
        if (hasSpawned) yield break;
        SpawnAIs();
    }

    void SpawnAIs()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        Debug.Log($"AI {spawnCount}마리 자동 소환 시작");

        int spawned = 0;
        int maxAttempts = spawnCount * 10;
        LayerMask groundMask = LayerMask.GetMask("Default", "Ground");

        for (int attempts = 0; spawned < spawnCount && attempts < maxAttempts; attempts++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = new Vector3(randomCircle.x, 10f, randomCircle.y);

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(randomPos, out hit, 20f, NavMesh.AllAreas))
                continue;

            Vector3 rayStart = hit.position + Vector3.up * 5f;
            float finalSpawnY = hit.position.y + heightOffset;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rayHit, 10f, groundMask))
                finalSpawnY = rayHit.point.y + heightOffset;

            Vector3 spawnPos = new Vector3(hit.position.x, finalSpawnY, hit.position.z);
            PhotonNetwork.InstantiateRoomObject(aiPrefabName, spawnPos, Quaternion.identity);
            spawned++;
        }

        if (spawned < spawnCount)
            Debug.LogWarning($"AI 스폰 미완료: {spawned}/{spawnCount}. 씬에 NavMesh가 베이크되어 있는지 확인하세요.");
    }
}
