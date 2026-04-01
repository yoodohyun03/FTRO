using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections; // [추가★] 시간 지연(코루틴)을 위한 마법의 단어
using Hashtable = ExitGames.Client.Photon.Hashtable;
//hihi hhhhiiiiii
public class LobbyManager : MonoBehaviourPunCallbacks
{
    public GameObject startButton;
    private bool hasSpawned = false;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient) startButton.SetActive(true);
        else startButton.SetActive(false);

        // [2번 문제 해결★] 씬에 들어오자마자 소환하지 말고, 0.5초 여유를 줍니다!
        StartCoroutine(DelaySpawn());
    }

    // 캐릭터 안전 소환 마법
    IEnumerator DelaySpawn()
    {
        yield return new WaitForSeconds(0.5f); // 0.5초 숨 고르기

        if (PhotonNetwork.InRoom && !hasSpawned)
        {
            float randomX = Random.Range(-2f, 2f);
            float randomZ = Random.Range(-2f, 2f);
            Vector3 spawnPos = new Vector3(randomX, 5, randomZ);

            PhotonNetwork.Instantiate("male01_1", spawnPos, Quaternion.identity);
            hasSpawned = true;
        }
    }

    // 방장이 [게임 시작] 버튼을 누르면 실행
    public void ClickStartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("제비뽑기를 시작합니다!");
            StartCoroutine(AssignRolesAndStart()); // [3번 문제 해결★] 시간차 씬 로딩 마법 발동!
        }
    }

    // 제비뽑기 후 안전하게 씬 넘어가기 마법
    IEnumerator AssignRolesAndStart()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int seekerIndex = Random.Range(0, players.Length);

        // 1. 포스트잇(직업) 배정
        for (int i = 0; i < players.Length; i++)
        {
            Hashtable props = new Hashtable();
            if (i == seekerIndex) props.Add("Role", "Seeker");
            else props.Add("Role", "Survivor");

            players[i].SetCustomProperties(props);
        }

        // 2. [핵심★] 포스트잇이 서버에 확실히 붙을 때까지 0.5초 기다려줍니다!
        yield return new WaitForSeconds(0.5f);

        // 3. 안전하게 다 같이 메인 게임으로 납치!
        PhotonNetwork.LoadLevel("MainGameScene");
    }
}