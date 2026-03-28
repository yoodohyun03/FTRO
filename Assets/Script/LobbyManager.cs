using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI; // UI(버튼)를 끄고 켜려면 필수!
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("방장 전용 UI")]
    public GameObject startButton; // 게임 시작 버튼 연결할 곳

    void Start()
    {
        // [핵심★] 방장이 씬을 이동하면, 나머지 사람들도 강제로 똑같은 씬으로 끌려가게 만드는 마법의 스위치!
        PhotonNetwork.AutomaticallySyncScene = true;
        
        // 1. 대기실 바닥에 내 캐릭터 소환
        if (PhotonNetwork.InRoom)
        {
            Vector3 spawnPos = new Vector3(0, 5, 0);
            PhotonNetwork.Instantiate("male01_1", spawnPos, Quaternion.identity);
        }

        // 2. 방장 검사소: 내가 이 방을 만든 방장(Master Client)인가?
        if (PhotonNetwork.IsMasterClient)
        {
            startButton.SetActive(true); // 방장이면 시작 버튼 켜기!
        }
        else
        {
            startButton.SetActive(false); // 짭(?)이면 시작 버튼 숨기기!
        }
    }

    // 방장이 [게임 시작] 버튼을 누르면 실행될 함수
    public void ClickStartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("방장 명령 하달! 제비뽑기를 시작합니다!");

            // 1. 현재 방에 있는 모든 플레이어 명단을 가져옵니다.
            Player[] players = PhotonNetwork.PlayerList;

            // 2. 0번부터 마지막 사람 중 랜덤으로 1명을 '술래'로 뽑습니다!
            int seekerIndex = Random.Range(0, players.Length);

            // 3. 한 명씩 이마에 포스트잇(직업)을 붙여줍니다.
            for (int i = 0; i < players.Length; i++)
            {
                Hashtable props = new Hashtable(); // 새 포스트잇 한 장 꺼내기

                if (i == seekerIndex)
                {
                    props.Add("Role", "Seeker"); // 술래 당첨!
                    Debug.Log(players[i].NickName + " 님은 [술래] 입니다!");
                }
                else
                {
                    props.Add("Role", "Survivor"); // 생존자 당첨!
                    Debug.Log(players[i].NickName + " 님은 [생존자] 입니다!");
                }

                // 플레이어 이마에 포스트잇 찰싹! 붙이기
                players[i].SetCustomProperties(props);
            }

            // 4. 배정이 끝났으니 메인 게임 씬으로 다 같이 납치!
            PhotonNetwork.LoadLevel("MainGameScene");
        }
    }
}