using UnityEngine;
using Photon.Pun;
using Unity.Cinemachine;

public class PlayerMove : MonoBehaviourPun
{
    public float speed = 5f;

    // ★ 애니메이터를 조종하기 위한 변수
    private Animator anim;

    void Start()
    {
        // ★ 내 캐릭터의 애니메이터 부품을 찾아서 변수에 쏙 넣습니다.
        anim = GetComponent<Animator>();

        if (photonView.IsMine)
        {
            CinemachineCamera vcam = FindObjectOfType<CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = this.transform;
                vcam.LookAt = this.transform;
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = new Vector3(h, 0, v).normalized;

        if (moveDir.magnitude > 0)
        {
            transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
            // ★ 요기! 이동 중일 때는 무조건 MoveSpeed를 1로 고정해서 덜덜거림 방지
            anim.SetFloat("MoveSpeed", 1.0f);
        }
        else
        {
            // ★ 요기! 완전히 멈췄을 때만 0으로 바꿈
            anim.SetFloat("MoveSpeed", 0f);
        }

        // 마우스 회전 코드는 그대로 두시고요!
    }
}