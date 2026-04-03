using UnityEngine;
using Photon.Pun;
using UnityEngine.AI; // 내비메시(땅)를 찾기 위해 필수!

public class AISpawner : MonoBehaviourPun
{
    [Header("소환 설정")]
    public string aiPrefabName = "AI_Dummy"; // Resources 폴더에 있는 파일 이름!
    public int spawnCount = 50;              // 몇 마리 뽑을까요?
    public float spawnRadius = 150f;         // 맵 크기 (소환 반경)

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
            // 1. 맵 안에서 랜덤한 위치를 하나 찌릅니다.
            Vector3 randomPos = Random.insideUnitSphere * spawnRadius;
            randomPos += Vector3.zero; // 맵의 중심을 (0,0,0)으로 가정

            NavMeshHit hit;
            // 2. 그 위치 근처에 'AI가 밟을 수 있는 진짜 땅(NavMesh)'이 있는지 검사합니다.
            if (NavMesh.SamplePosition(randomPos, out hit, spawnRadius, 1))
            {
                // 3. 땅을 찾았다면, 포톤 서버에 AI를 소환합니다!
                // InstantiateRoomObject를 쓰면 방장이 나가도 AI가 안 사라집니다!
                PhotonNetwork.InstantiateRoomObject(aiPrefabName, hit.position, Quaternion.identity);
            }
            else
            {
                // 혹시 땅을 못 찾았으면 한 번 더 기회를 줍니다.
                i--;
            }
        }
    }
}