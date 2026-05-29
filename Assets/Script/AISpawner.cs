using UnityEngine;
using Photon.Pun;
using UnityEngine.AI;

public class AISpawner : MonoBehaviourPun
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy";
    public int spawnCount = 50;
    public float spawnRadius = 80f;
    [Range(0.0f, 1.0f)]
    public float heightOffset = 0.5f; // 0.05에서 0.5로 상향 (안전성 확보)

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(SpawnWithDelay());
    }

    System.Collections.IEnumerator SpawnWithDelay()
    {
        yield return new WaitForSeconds(1f);
        SpawnAIs();
    }

    void SpawnAIs()
    {
        Debug.Log($"AI {spawnCount}마리 자동 소환 시작");

        int spawned = 0;
        int maxAttempts = spawnCount * 10;
        LayerMask groundMask = LayerMask.GetMask("Default", "Ground"); // 캐릭터 제외 바닥만 감지

        for (int attempts = 0; spawned < spawnCount && attempts < maxAttempts; attempts++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = new Vector3(randomCircle.x, 10f, randomCircle.y); // 높은 곳에서 샘플링

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(randomPos, out hit, 20f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 rayStart = hit.position + Vector3.up * 5f;
            float finalSpawnY = hit.position.y + heightOffset;

            // 바닥 레이캐스트 시 캐릭터(Player) 레이어 제외
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rayHit, 10f, groundMask))
            {
                finalSpawnY = rayHit.point.y + heightOffset;
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