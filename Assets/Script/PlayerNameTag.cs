using UnityEngine;
using Photon.Pun;
using TMPro;

public class PlayerNameTag : MonoBehaviourPun
{
    public TextMeshProUGUI nameText;
    private Camera mainCam;

    void Start()
    {
        if (nameText != null)
        {
            // 게임 시작 직후 기본은 비표시(End 상태에서만 표시)
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
        // 게임 상태 기준으로 이름표 표시 제어: End 상태에서만 표시
        if (nameText != null && GameManager.instance != null)
        {
            bool shouldShow = GameManager.instance.currentState == GameManager.GameState.End;
            if (nameText.gameObject.activeSelf != shouldShow)
            {
                nameText.gameObject.SetActive(shouldShow);
            }
        }

        // 카메라 방향 동기화
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}