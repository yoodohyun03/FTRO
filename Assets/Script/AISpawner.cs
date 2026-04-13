using UnityEngine;
using Photon.Pun;
using UnityEngine.AI; // 내비메시(땅)를 찾기 위해 필수!

public class AISpawner : MonoBehaviourPun
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy"; // Resources 폴더에 있는 파일 이름!
    public int spawnCount = 50;              // 몇 마리 뽑을까요?
    public float spawnRadius = 80f;         // 맵 크기 (소환 반경)
    [Range(-0.2f, 0.5f)]
    public float heightOffset = 0.05f;      // 🌟 [수정] 미세 조정용 오프셋 (Raycast 후 추가 높이)

    void Start()
    {
        // 🌟 [핵심] 방장(서버장)만 소환 권한을 가집니다! 안 그러면 인원수만큼 중복 소환됨!
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnAIs();
        }
    }

    void SpawnAIs()
    {
        Debug.Log($"🤖 AI {spawnCount}마리 자동 소환 시작!");

        for (int i = 0; i < spawnCount; i++)
        {
            // 🌟 [수정 1] 구체(3D)가 아니라 원(2D)으로 랜덤을 돌립니다! (Y축 하늘에서 스폰되는 것 방지)
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

            // X, Z 축으로만 값을 넣고, 높이(Y)는 맵 기본 바닥 높이(보통 0)로 고정합니다.
            Vector3 randomPos = new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit hit;
            // 🌟 [수정 2] 150m나 뒤지지 말고, 그 랜덤 점 근처 '10m' 안에서만 땅을 찾습니다!
            // 이렇게 해야 맵 밖으로 찍힌 점들이 맵 가장자리로 억지로 끌려와서 뭉치는 현상이 사라집니다.
            if (NavMesh.SamplePosition(randomPos, out hit, 10f, NavMesh.AllAreas))
            {
                // 🌟 [핵심] 실제 지형 높이를 Raycast로 찾아서 발이 박히는 것을 방지!
                Vector3 rayStart = hit.position + Vector3.up * 20f; // 위에서 아래로 쏨
                float finalSpawnY = hit.position.y; // 기본값 (Raycast 실패 시)

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit rayHit, 30f))
                {
                    // Raycast가 맞춘 지점 (풀, 지형 등 실제 표면)의 높이
                    finalSpawnY = rayHit.point.y + heightOffset;
                    Debug.Log($"✅ Raycast 성공: {rayHit.collider.name} | Y높이: {rayHit.point.y:F2} + 오프셋{heightOffset:F2} = {finalSpawnY:F2}");
                }
                else
                {
                    // Raycast 실패 시 NavMesh 높이 + 오프셋 사용
                    finalSpawnY = hit.position.y + heightOffset;
                    Debug.Log($"⚠️ Raycast 실패 → NavMesh 높이 사용: {finalSpawnY:F2}");
                }

                // 🌟 [추가] 위에 지붕 같은 장애물이 있는지 확인!
                Vector3 headCheck = new Vector3(hit.position.x, finalSpawnY + 2f, hit.position.z);  // 머리 높이 확인
                bool hasObstacleAbove = Physics.Raycast(headCheck, Vector3.up, 1.5f);  // 1.5m 위에 뭔가 있으면 스킵

                if (hasObstacleAbove)
                {
                    Debug.Log($"🚫 위에 지붕 감지 → 이 스폰 위치 스킵");
                    i--;  // 이번 스폰 무효, 다시 뽑기
                    continue;
                }

                Vector3 spawnPos = new Vector3(hit.position.x, finalSpawnY, hit.position.z);
                PhotonNetwork.InstantiateRoomObject(aiPrefabName, spawnPos, Quaternion.identity);
            }
            else
            {
                // 땅을 못 찾았으면 이 턴은 무효! 다시 뽑습니다.
                i--;
            }
        }
    }
}