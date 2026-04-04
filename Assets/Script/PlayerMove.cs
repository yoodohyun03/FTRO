using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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


    [Header("관전 모드 설정")]
    public bool isDead = false; // 내가 죽었는지 체크
    private List<Transform> aliveSurvivors = new List<Transform>(); // 살아있는 사람들 명단
    private int spectateIndex = 0; // 지금 몇 번째 사람을 보고 있는지

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

        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null) return;
        if (!photonView.IsMine) return;

        if (isDead)
        {
            SpectateUpdate();
            return;
        }

        // ==========================================
        // 🚀 1. 점프 (Space Bar)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse); // 위로 퓽! 쏴주기

            // [수정] 내 화면 + 다른 사람 화면 모두 점프 애니메이션 재생
            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All);

            isGrounded = false; // 공중에 뜸
        }

        // ==========================================
        
        // ==========================================
        if (Input.GetMouseButtonDown(0))
        {
            // [수정] 내 화면 + 다른 사람 화면 모두 펀치 애니메이션 재생
            photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);

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


    void UpdateSurvivorList()
    {
        aliveSurvivors.Clear();
        // 아까 1단계에서 붙여준 "Player" 태그를 가진 모든 사람을 찾습니다.
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject p in allPlayers)
        {
            // 나 자신이 아니고 & 겉모습이 보이는(살아있는) 사람만 명단에 추가!
            Renderer pRenderer = p.GetComponentInChildren<Renderer>();
            if (p != this.gameObject && pRenderer != null && pRenderer.enabled)
            {
                aliveSurvivors.Add(p.transform);
            }
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
    void SpectateUpdate()
    {
        if (aliveSurvivors.Count == 0) return; // 다 죽었으면 스킵

        // 마우스 왼쪽 버튼 클릭 시 다음 사람으로 화면 넘기기!
        if (Input.GetMouseButtonDown(0))
        {
            spectateIndex++;
            if (spectateIndex >= aliveSurvivors.Count) spectateIndex = 0; // 끝까지 가면 다시 처음으로

            UpdateSurvivorList(); // 화면 넘길 때 혹시 누가 또 죽었나 명단 갱신
        }

        // 🌟 [진짜 핵심] 내 유령 몸뚱이를 타겟 위치로 강제 이동!
        // 내 카메라가 나를 찍고 있으니까, 내가 타겟한테 겹쳐지면 자연스럽게 관전이 됩니다.
        if (aliveSurvivors.Count > 0 && spectateIndex < aliveSurvivors.Count)
        {
            Transform target = aliveSurvivors[spectateIndex];

            // 타겟 머리 위나 어깨너머로 살짝 띄워주는 게 좋습니다. (y축으로 2미터 위)
            transform.position = target.position + new Vector3(0, 2f, 0);

            // 마우스로 화면 돌리는 건 그대로 먹히게 둬도 좋고, 타겟 시점과 완전히 맞추려면 아래 코드 주석 해제!
            // transform.rotation = target.rotation; 
        }
    }


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
        // 1. 이미 죽은 상태라면 중복 처리를 방지합니다.
        if (isDead) return;

        isDead = true;
        Debug.Log("😱 " + photonView.Owner.NickName + " 사망! 관전 모드 시작!");

        // 2. 애니메이션 실행
        if (anim != null) anim.SetTrigger("Die");

        // 3. [중요] 방장에게 딱 '한 번만' 보고합니다.
        if (PhotonNetwork.IsMasterClient)
        {
            GameManager.instance.photonView.RPC("OnSurvivorCaught", RpcTarget.MasterClient);
        }

        // 4. [중요] 캐릭터를 끄지 말고(SetActive X) 투명하게만 만듭니다.
        // 그래야 이 스크립트가 계속 돌면서 관전 모드(SpectateUpdate)를 실행합니다!
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) r.enabled = false;

        Collider coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = false;

        // 5. 관전 명단 뽑기
        UpdateSurvivorList();
    }

    // ==========================================
    // 📡 애니메이션 동기화용 RPC 함수들
    // ==========================================
    [PunRPC]
    void RPC_PlayPunchAnimation()
    {
        if (anim != null) anim.SetTrigger("Punch");
    }

    [PunRPC]
    void RPC_PlayJumpAnimation()
    {
        if (anim != null) anim.SetTrigger("Jump");
    }





}