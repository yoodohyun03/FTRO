using UnityEngine;
using Photon.Pun;
using UnityEngine.AI;

public class AISpawner : MonoBehaviourPun
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy";
    public int spawnCount = 50;
    public float spawnRadius = 80f;
    [Range(-0.2f, 0.5f)]
    public float heightOffset = 0.05f;

    void Start()
    {
        // 방장만 AI 생성
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnAIs();
        }
    }

    void SpawnAIs()
    {
        Debug.Log($"AI {spawnCount}마리 자동 소환 시작");

        int spawned = 0;
        int maxAttempts = spawnCount * 10;

        for (int attempts = 0; spawned < spawnCount && attempts < maxAttempts; attempts++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(randomPos, out hit, 10f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 rayStart = hit.position + Vector3.up * 20f;
            float finalSpawnY = hit.position.y + heightOffset;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rayHit, 30f))
            {
                finalSpawnY = rayHit.point.y + heightOffset;
            }

            Vector3 headCheck = new Vector3(hit.position.x, finalSpawnY + 2f, hit.position.z);
            if (Physics.Raycast(headCheck, Vector3.up, 1.5f))
            {
                continue;
            }

            Vector3 spawnPos = new Vector3(hit.position.x, finalSpawnY, hit.position.z);
            PhotonNetwork.InstantiateRoomObject(aiPrefabName, spawnPos, Quaternion.identity);
            spawned++;
        }

        if (spawned < spawnCount)
        {
            Debug.LogWarning($"AI 스폰 미완료: {spawned}/{spawnCount}. 씬에 NavMesh가 베이크되어 있는지 확인하세요.");
        }
    }
}