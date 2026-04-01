using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class PlayerMove : MonoBehaviourPun
{
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    public float jumpPower = 5f; // [추가★] 점프력!

    private Animator anim;
    private Unity.Cinemachine.CinemachineCamera vcam;
    private Unity.Cinemachine.CinemachineOrbitalFollow orbitalRig;
    private Rigidbody rb; // [추가★] 물리 엔진

    public string myRole = "";
    private bool isGrounded = true; // [추가★] 땅에 닿아있는지 확인

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>(); // Rigidbody 연결

        if (photonView.IsMine)
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
            {
                myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties["Role"];
                StartCoroutine(ShowRoleSequence());
            }

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

    // (ShowRoleSequence 코루틴은 어제 형님이 수정한 그대로 냅뒀습니다! 생략 없이 그대로 쓰시면 됩니다.)
    IEnumerator ShowRoleSequence()
    {
        GameObject cornerObj = GameObject.Find("CornerRoleText");
        GameObject blindObj = GameObject.Find("BlindPanel");

        if (cornerObj != null)
        {
            TextMeshProUGUI cornerText = cornerObj.GetComponent<TextMeshProUGUI>();
            cornerObj.SetActive(true);

            if (myRole == "Seeker")
            {
                cornerText.text = "<color=red>Seeker</color>";
                if (blindObj != null) blindObj.SetActive(true);
            }
            else
            {
                cornerText.text = "<color=#00BFFF>Surviver</color>";
                if (blindObj != null) blindObj.SetActive(false);
            }
        }

        yield return new WaitForSeconds(5f);

        if (blindObj != null) blindObj.SetActive(false);
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // ==========================================
        // 🚀 1. 점프 (Space Bar)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse); // 위로 퓽! 쏴주기
            if (anim != null) anim.SetTrigger("Jump"); // 점프 애니메이션 실행
            isGrounded = false; // 공중에 뜸
        }

        // ==========================================
        // 👊 2. GTA 스타일 펀치! (마우스 좌클릭)
        // ==========================================
        if (Input.GetMouseButtonDown(0))
        {
            photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All); // 주먹 휘두르는 애니메이션!

            // 내가 '술래'일 때만 주먹에 데미지(타격 판정)가 들어갑니다!
            if (myRole == "Seeker")
            {
                CheckPunchHit();
            }
        }

        // ==========================================
        // 🏃 3. 이동 로직 (기존과 완벽 동일)
        // ==========================================
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = Vector3.zero;

        bool isAltLooking = Input.GetKey(KeyCode.LeftAlt);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetKeyUp(KeyCode.LeftAlt) && orbitalRig != null)
        {
            orbitalRig.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        if (h != 0 || v != 0)
        {
            if (Camera.main != null && SceneManager.GetActiveScene().name != "LobbyScene")
            {
                if (isAltLooking) moveDir = (transform.forward * v + transform.right * h).normalized;
                else
                {
                    Vector3 camForward = Camera.main.transform.forward;
                    camForward.y = 0;
                    Vector3 camRight = Camera.main.transform.right;
                    camRight.y = 0;
                    moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
                }
            }
            else moveDir = new Vector3(h, 0, v).normalized;

            if (!isAltLooking) transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * 10f);

            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            transform.Translate(moveDir * currentSpeed * Time.deltaTime, Space.World);

            float animValue = isRunning ? 1.0f : 0.5f;
            if (anim != null) anim.SetFloat("MoveSpeed", animValue);
        }
        else
        {
            if (anim != null) anim.SetFloat("MoveSpeed", 0f);
        }
    }

    // ==========================================
    // 🔍 바닥 착지 감지 센서
    // ==========================================
    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    // ==========================================
    // 💥 펀치 타격 판정 (투명 레이저 쏘기)
    // ==========================================
    void CheckPunchHit()
    {
        RaycastHit hit;
        // 내 명치(Vector3.up * 1f) 높이에서, 앞(forward)으로, 1.5미터짜리 투명 레이저를 쏩니다!
        if (Physics.Raycast(transform.position + Vector3.up * 1f, transform.forward, out hit, 1.5f))
        {
            // 맞은 놈이 플레이어 태그를 달고 있다면?!
            if (hit.collider.CompareTag("Player"))
            {
                PhotonView targetView = hit.collider.GetComponent<PhotonView>();

                // 그게 내가 아니고 다른 사람이라면!
                if (targetView != null && !targetView.IsMine)
                {
                    Debug.Log("🎯 펀치 적중! 맞은 사람: " + targetView.Owner.NickName);

                    // 맞은 사람의 컴퓨터로 "너 잡혔어!!" 하고 마법(RPC)을 날립니다.
                    targetView.RPC("GetCaught", RpcTarget.All);
                }
            }
        }
    }

    // ==========================================
    // 💀 잡혔을 때 실행되는 함수!
    // ==========================================
    [PunRPC]
    public void GetCaught()
    {
        Debug.Log("😱 " + photonView.Owner.NickName + "가 잡혔습니다!");

        // TODO: 나중에 여기에 뼈대 다 세우고 나면,
        // 애니메이션 픽 쓰러지는 걸로 바꾸고, 관전 모드로 전환하는 로직을 넣으면 됩니다!
        if (anim != null) anim.SetTrigger("Die"); // (나중에 Die 애니메이션 추가하십쇼!)

        // 임시: 일단 맞으면 캐릭터를 없애버림 (또는 안 보이게)
        gameObject.SetActive(false); 
    }
}