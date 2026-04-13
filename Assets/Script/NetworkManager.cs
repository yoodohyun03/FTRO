using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public Transform[] seekerSpawnPoints;    // 🌟 [추가] 술래 스폰 포인트
    public Transform[] survivorSpawnPoints;  // 🌟 [추가] 생존자 스폰 포인트

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinOrCreateRoom("TestRoom", new RoomOptions { MaxPlayers = 4 }, null);
    }

    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        // 🌟 [핵심] 자신의 역할 확인
        string myRole = "Survivor";  // 기본값
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
        {
            myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties["Role"];
        }

        // 🌟 [핵심] 역할에 따라 다른 스폰 포인트에서 생성!
        Transform spawnPoint = null;

        if (myRole == "Seeker")
        {
            // 술래는 술래 스폰 포인트에서 생성
            if (seekerSpawnPoints != null && seekerSpawnPoints.Length > 0)
            {
                int randomIndex = Random.Range(0, seekerSpawnPoints.Length);
                spawnPoint = seekerSpawnPoints[randomIndex];
                Debug.Log($"🔴 술래 스폰: {spawnPoint.name}");
            }
        }
        else
        {
            // 생존자는 생존자 스폰 포인트에서 생성
            if (survivorSpawnPoints != null && survivorSpawnPoints.Length > 0)
            {
                int randomIndex = Random.Range(0, survivorSpawnPoints.Length);
                spawnPoint = survivorSpawnPoints[randomIndex];
                Debug.Log($"🔵 생존자 스폰: {spawnPoint.name}");
            }
        }

        // 스폰 포인트가 없으면 기본값 사용
        if (spawnPoint == null)
        {
            PhotonNetwork.Instantiate("male01_1", new Vector3(0, 5, 0), Quaternion.identity);
        }
        else
        {
            PhotonNetwork.Instantiate("male01_1", spawnPoint.position, spawnPoint.rotation);
        }
    }
}