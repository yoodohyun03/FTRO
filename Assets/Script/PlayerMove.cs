using UnityEngine;
using Photon.Pun;
using Unity.Cinemachine; // ★ 네임스페이스가 이렇게 바뀌었습니다!

public class PlayerMove : MonoBehaviourPun
{
    public float speed = 5f;

    void Start()
    {
        if (photonView.IsMine)
        {
            // ★ 클래스 이름도 CinemachineCamera로 짧아졌습니다!
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

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(h, 0, v).normalized;
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
    }
}