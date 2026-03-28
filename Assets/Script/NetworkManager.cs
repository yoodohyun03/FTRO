using UnityEngine;
using Photon.Pun;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public Transform[] spawnPoints;

    void Start()
    {
        // 이제 헷갈리는 가짜 로그 지우고, 진짜 본 게임 진입 로그 띄웁니다!
        Debug.Log("본 게임 씬 진입! 스폰을 시작합니다.");

        // 내가 방 안에 잘 들어와 있는지 확인
        if (PhotonNetwork.InRoom)
        {
            // [에러 방어막] 형님이 유니티 화면에서 스폰 방석 연결을 안 했을 경우를 대비!
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int randomIndex = Random.Range(0, spawnPoints.Length);
                Transform randomPoint = spawnPoints[randomIndex];
                PhotonNetwork.Instantiate("male01_1", randomPoint.position, randomPoint.rotation);
            }
            else
            {
                // 방석 연결을 깜빡했다면? 에러 내지 말고 일단 맵 한가운데(0, 5, 0)에 냅다 꽂아버려!
                Debug.LogWarning("🚨 형님! 인스펙터에 spawnPoints(방석) 연결 안 하셨습니다! 일단 중앙에 소환합니다!");
                PhotonNetwork.Instantiate("male01_1", new Vector3(0, 5, 0), Quaternion.identity);
            }
        }
    }
}