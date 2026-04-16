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

        for (int i = 0; i < spawnCount; i++)
        {
            // 2D 원형 범위 랜덤 위치
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

            // X,Z만 랜덤, Y는 기본값
            Vector3 randomPos = new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit hit;
            // 랜덤 위치 근처의 NavMesh 지점만 탐색
            if (NavMesh.SamplePosition(randomPos, out hit, 10f, NavMesh.AllAreas))
            {
                // Raycast로 실제 표면 높이 계산
                Vector3 rayStart = hit.position + Vector3.up * 20f;
                float finalSpawnY = hit.position.y;

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rayHit, 30f))
                {
                    finalSpawnY = rayHit.point.y + heightOffset;
                    Debug.Log($"Raycast 성공: {rayHit.collider.name}, SpawnY={finalSpawnY:F2}");
                }
                else
                {
                    finalSpawnY = hit.position.y + heightOffset;
                    Debug.Log($"Raycast 실패: NavMesh 높이 사용 SpawnY={finalSpawnY:F2}");
                }

                // 머리 위 장애물 확인
                Vector3 headCheck = new Vector3(hit.position.x, finalSpawnY + 2f, hit.position.z);
                bool hasObstacleAbove = Physics.Raycast(headCheck, Vector3.up, 1.5f);

                if (hasObstacleAbove)
                {
                    Debug.Log("상단 장애물 감지: 해당 스폰 위치 건너뜀");
                    i--;
                    continue;
                }

                Vector3 spawnPos = new Vector3(hit.position.x, finalSpawnY, hit.position.z);
                PhotonNetwork.InstantiateRoomObject(aiPrefabName, spawnPos, Quaternion.identity);
            }
            else
            {
                i--;
            }
        }
    }
}