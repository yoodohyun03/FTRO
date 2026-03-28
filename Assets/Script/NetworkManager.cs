using UnityEngine;
using Photon.Pun;       // 포톤 네트워크 핵심 라이브러리
using Photon.Realtime;  // 룸, 매치메이킹 관련 라이브러리

// 주의: 서버의 응답(콜백)을 받기 위해 MonoBehaviour가 아니라 MonoBehaviourPunCallbacks를 상속받습니다.
public class NetworkManager : MonoBehaviourPunCallbacks
{

    public Transform[] spawnPoints;

    // Resources 폴더 안에 있는 내 캐릭터 프리팹의 이름을 똑같이 적어주세요!
    public string playerPrefabName = "MyLowPolyCharacter";

    void Start()
    {
        // 1. 게임 시작과 동시에 포톤 마스터 서버에 접속 시도
        Debug.Log("서버 접속 시도 중...");
        PhotonNetwork.ConnectUsingSettings();
        if (PhotonNetwork.InRoom)
        {
            // 어제 짠 랜덤 스폰 마법의 코드 그대로 가져오기
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform randomPoint = spawnPoints[randomIndex];

            // 랜덤 위치에 내 캐릭터 쾅! 소환
            PhotonNetwork.Instantiate("male01_1", randomPoint.position, randomPoint.rotation);
        }
    }

    // 2. 마스터 서버 접속 성공 시 자동으로 실행되는 콜백 함수
    public override void OnConnectedToMaster()
    {
        Debug.Log("서버 접속 성공! 로비로 진입합니다.");
        PhotonNetwork.JoinLobby(); // 로비 진입
    }

    // 3. 로비 진입 성공 시 실행되는 콜백 함수
    public override void OnJoinedLobby()
    {
        Debug.Log("로비 진입 성공! 'WhoWhoRoom' 방에 입장하거나 생성합니다.");
        // 'WhoWhoRoom'이라는 이름의 방이 있으면 들어가고, 없으면 최대 10명짜리 방을 새로 만듭니다.
        PhotonNetwork.JoinOrCreateRoom("WhoWhoRoom", new RoomOptions { MaxPlayers = 10 }, null);
    }

    // 4. 방 입장(또는 생성) 성공 시 실행되는 최종 콜백 함수
    //public override void OnJoinedRoom()
    //{
    //    Debug.Log("방 입장 완료! 내 캐릭터를 소환합니다.");

    //    int randomIndex = Random.Range(0, spawnPoints.Length);
    //    Transform randomPoint = spawnPoints[randomIndex];

    //    // 광장 중앙(0, 0, 0) 근처에 내 캐릭터를 무작위 위치로 스폰합니다.
    //    // 위치가 겹치지 않게 X, Z 좌표에 약간의 랜덤 값을 줍니다.
    //    Vector3 randomSpawnPos = new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));

    //    // ★핵심: Instantiate가 아니라 PhotonNetwork.Instantiate를 써야 모두의 화면에 보입니다!
    //    PhotonNetwork.Instantiate("male01_1", randomPoint.position, randomPoint.rotation);
    //}
}