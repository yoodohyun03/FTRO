using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PlayerMove : MonoBehaviourPun
{
    // [수정 POINT★] 걷기와 뛰기 속도를 따로 나눴습니다! (입맛대로 조절하십쇼)
    public float walkSpeed = 3f;
    public float runSpeed = 7f;

    private Animator anim;
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

        // [추가 POINT★] 왼쪽 Shift 키를 누르고 있는지 감지!
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // Alt 뗐을 때 카메라 복귀
        if (Input.GetKeyUp(KeyCode.LeftAlt) && orbitalRig != null)
        {
            orbitalRig.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        // 키보드 입력이 있을 때 (이동 중일 때)
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

            // Alt 안 누를 때만 몸통 회전
            if (!isAltLooking)
            {
                transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);
            }

            // [핵심★] Shift 누르면 runSpeed(7), 안 누르면 walkSpeed(3)로 이동!
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            transform.Translate(moveDir * currentSpeed * Time.deltaTime, Space.World);

            // [핵심★] 애니메이터에 보내는 신호도 걷기(0.5)와 뛰기(1.0)로 나눠서 보냅니다!
            float animValue = isRunning ? 1.0f : 0.5f;
            if (anim != null) anim.SetFloat("MoveSpeed", animValue);
        }
        else
        {
            // 가만히 있을 때 (정지)
            if (anim != null) anim.SetFloat("MoveSpeed", 0f);
        }
    }
}