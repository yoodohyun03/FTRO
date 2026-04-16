using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerNameTag : MonoBehaviourPun
{
    private const string CityMapSceneName = "CityMapScene";

    public TextMeshProUGUI nameText;
    private Camera mainCam;

    void Start()
    {
        bool isCityMap = SceneManager.GetActiveScene().name == CityMapSceneName;

        if (nameText != null && isCityMap)
        {
            nameText.gameObject.SetActive(false);
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
        // 게임 씬에서는 시작~진행 중 이름표 숨김, End 상태에서만 표시
        if (SceneManager.GetActiveScene().name == CityMapSceneName && nameText != null && GameManager.instance != null)
        {
            bool shouldShow = GameManager.instance.currentState == GameManager.GameState.End;
            if (nameText.gameObject.activeSelf != shouldShow)
            {
                nameText.gameObject.SetActive(shouldShow);
            }
        }

        // 카메라 방향 동기화
        if (SceneManager.GetActiveScene().name == CityMapSceneName && mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}