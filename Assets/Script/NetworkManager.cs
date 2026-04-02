using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // [추가★] 방 옵션 설정을 위해 필요합니다!

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public Transform[] spawnPoints;

    void Start()
    {
        // 1. [정상 루트] 로비 씬을 거쳐서 방에 이미 들어와 있는 경우! (빌드 후 실제 게임할 때)
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        // 2. [테스트 루트] 메인 씬에서 바로 재생(▶) 버튼을 누른 경우!
        else
        {
            Debug.Log("🚨 [테스트 모드 가동] 로비를 건너뛰었습니다! 포톤 서버에 강제 접속합니다...");
            PhotonNetwork.ConnectUsingSettings(); // 오프라인 상태면 일단 포톤 서버부터 연결!
        }
    }

    // ==========================================
    // 🛠️ 테스트 모드 전용 자동 접속기
    // ==========================================

    // 서버 접속이 성공하면 알아서 실행됨
    public override void OnConnectedToMaster()
    {
        Debug.Log("✅ [테스트 모드] 서버 접속 성공! 'TestRoom'을 파고 들어갑니다.");
        // "TestRoom"이라는 방이 있으면 들어가고, 없으면 하나 만들어서 들어감!
        PhotonNetwork.JoinOrCreateRoom("TestRoom", new RoomOptions { MaxPlayers = 4 }, null);
    }

    // 방에 성공적으로 입장하면 알아서 실행됨
    public override void OnJoinedRoom()
    {
        Debug.Log("✅ [테스트 모드] 임시 방 접속 완료! 캐릭터를 스폰합니다.");
        SpawnPlayer();
    }

    // ==========================================
    // 👤 스폰 로직 (기존과 동일하지만, 깔끔하게 함수로 뺐습니다!)
    // ==========================================
    void SpawnPlayer()
    {
        Debug.Log("본 게임 씬 진입! 스폰을 시작합니다.");

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform randomPoint = spawnPoints[randomIndex];
            PhotonNetwork.Instantiate("male01_1", randomPoint.position, randomPoint.rotation);
        }
        else
        {
            Debug.LogWarning("🚨 형님! 인스펙터에 spawnPoints(방석) 연결 안 하셨습니다! 일단 중앙에 소환합니다!");
            PhotonNetwork.Instantiate("male01_1", new Vector3(0, 5, 0), Quaternion.identity);
        }
    }
}