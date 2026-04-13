using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerNameTag : MonoBehaviourPun
{
    public TextMeshProUGUI nameText;
    private Camera mainCam;
    private bool isGameStarted = false;

    void Start()
    {
        // 🌟 [수정] 씬 이름을 "CityMapScene"으로 변경
        if (SceneManager.GetActiveScene().name == "CityMapScene")
        {
            // 게임 씬이지만, 처음에는 이름표를 보여줌 (로비 상태)
            // GameManager에서 Playing 상태로 변경되면 숨길 것
        }

        if (photonView.IsMine)
        {
            nameText.text = PhotonNetwork.NickName;
            nameText.color = Color.green;
        }
        else
        {
            nameText.text = photonView.Owner.NickName;
        }

        mainCam = Camera.main;
    }

    void Update()
    {
        // 🌟 [추가] GameManager의 상태를 확인해서 게임 진행 중이면 이름표 숨기기
        if (GameManager.instance != null && GameManager.instance.currentState == GameManager.GameState.Playing)
        {
            if (nameText.gameObject.activeSelf)
            {
                nameText.gameObject.SetActive(false);
                isGameStarted = true;
            }
        }
        else if (isGameStarted && GameManager.instance != null && GameManager.instance.currentState != GameManager.GameState.Playing)
        {
            // 게임이 끝났으면 다시 이름표 표시
            if (!nameText.gameObject.activeSelf)
            {
                nameText.gameObject.SetActive(true);
                isGameStarted = false;
            }
        }

        // 카메라 방향 동기화
        if (SceneManager.GetActiveScene().name == "CityMapScene" && mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}