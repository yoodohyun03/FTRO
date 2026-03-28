using UnityEngine;
using Photon.Pun;
using TMPro;

public class PlayerNameTag : MonoBehaviourPun
{
    public TextMeshProUGUI nameText; // 아까 만든 글자(Text) 연결할 곳
    private Camera mainCam;

    void Start()
    {
        // 1. 내 캐릭터면 내 이름, 남의 캐릭터면 남의 이름 가져와서 쾅!
        if (photonView.IsMine)
        {
            nameText.text = PhotonNetwork.NickName;
            nameText.color = Color.green; // 내 이름은 헷갈리지 않게 초록색으로!
        }
        else
        {
            nameText.text = photonView.Owner.NickName; // 남의 이름
        }

        // 2. 씬에 있는 메인 카메라(내 눈) 찾기
        mainCam = Camera.main;
    }

    void Update()
    {
        // 3. 해바라기 마술: 이름표가 항상 내 카메라를 정면으로 바라보게 싹 돌려줌!
        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}