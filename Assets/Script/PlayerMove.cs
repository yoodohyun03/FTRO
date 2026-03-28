using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PlayerMove : MonoBehaviourPun
{
    public float speed = 5f;
    private Animator anim;

    // 카메라 부품들을 저장해둘 메모장!
    private Unity.Cinemachine.CinemachineCamera vcam;
    private Unity.Cinemachine.CinemachineOrbitalFollow orbitalRig;

    void Start()
    {
        anim = GetComponent<Animator>();

        if (photonView.IsMine)
        {
            vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();

            if (vcam != null)
            {
                vcam.Follow = this.transform;
                vcam.LookAt = this.transform;

                // [핵심★] 카메라의 '궤도(Orbital)' 조종 장치를 미리 찾아둡니다.
                orbitalRig = vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = Vector3.zero;

        bool isAltLooking = Input.GetKey(KeyCode.LeftAlt);

        // [1번 문제 해결★] Alt 키를 딱! 뗐을 때 실행되는 마법의 코드
        if (Input.GetKeyUp(KeyCode.LeftAlt) && orbitalRig != null)
        {
            // 카메라의 가로 회전축(HorizontalAxis)을 현재 내 캐릭터가 바라보는 각도(y)로 강제 고정시킵니다!
            orbitalRig.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        if (h != 0 || v != 0)
        {
            if (Camera.main != null && SceneManager.GetActiveScene().name != "LobbyScene")
            {
                if (isAltLooking)
                {
                    moveDir = (transform.forward * v + transform.right * h).normalized;
                }
                else
                {
                    Vector3 camForward = Camera.main.transform.forward;
                    camForward.y = 0;
                    Vector3 camRight = Camera.main.transform.right;
                    camRight.y = 0;
                    moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
                }
            }
            else
            {
                moveDir = new Vector3(h, 0, v).normalized;
            }

            // Alt 안 누를 때만 몸통 회전!
            if (!isAltLooking)
            {
                transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);
            }

            transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

            if (anim != null) anim.SetFloat("MoveSpeed", 1.0f);
        }
        else
        {
            if (anim != null) anim.SetFloat("MoveSpeed", 0f);
        }
    }
}