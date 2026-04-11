using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public Transform[] spawnPoints;

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
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform randomPoint = spawnPoints[randomIndex];
            PhotonNetwork.Instantiate("male01_1", randomPoint.position, randomPoint.rotation);
        }
        else
        {
            PhotonNetwork.Instantiate("male01_1", new Vector3(0, 5, 0), Quaternion.identity);
        }
    }
}