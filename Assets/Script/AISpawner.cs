using UnityEngine;
using Photon.Pun;
using UnityEngine.AI; // 내비메시(땅)를 찾기 위해 필수!

public class AISpawner : MonoBehaviourPun
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy"; // Resources 폴더에 있는 파일 이름!
    public int spawnCount = 50;              // 몇 마리 뽑을까요?
    public float spawnRadius = 80f;         // 맵 크기 (소환 반경)

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
                PhotonNetwork.InstantiateRoomObject(aiPrefabName, hit.position, Quaternion.identity);
            }
            else
            {
                // 땅을 못 찾았으면 이 턴은 무효! 다시 뽑습니다.
                i--;
            }
        }
    }
}