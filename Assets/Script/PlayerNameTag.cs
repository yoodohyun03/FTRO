using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerNameTag : MonoBehaviourPun
{
    public TextMeshProUGUI nameText;
    private Camera mainCam;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "MainGameScene")
        {
            nameText.gameObject.SetActive(false);
            return;
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
        if (SceneManager.GetActiveScene().name == "MainGameScene") return;

        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}