using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement; // [핵심★] 현재 무슨 씬인지 알아내기 위해 마법의 단어를 추가합니다!

public class PlayerNameTag : MonoBehaviourPun
{
    public TextMeshProUGUI nameText; // 아까 만든 글자(Text) 연결할 곳
    private Camera mainCam;

    void Start()
    {
        // ==========================================
        // 🕵️‍♂️ [추가된 은신 마술] 
        // 현재 씬 이름이 "MainGameScene"이라면? 이름표를 아예 꺼버립니다!
        // ==========================================
        if (SceneManager.GetActiveScene().name == "MainGameScene")
        {
            nameText.gameObject.SetActive(false); // 글자 UI 전원 끄기
            return; // 아래에 있는 닉네임 세팅 코드는 실행할 필요도 없으니 여기서 종료!
        }

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
        // 게임 씬에서는 이름표가 꺼져 있으므로 카메라를 쳐다볼 필요도 없습니다!
        if (SceneManager.GetActiveScene().name == "MainGameScene") return;

        // 3. 해바라기 마술: 이름표가 항상 내 카메라를 정면으로 바라보게 싹 돌려줌!
        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }
    }
}