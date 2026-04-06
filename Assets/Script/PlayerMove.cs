using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PlayerMove : MonoBehaviourPun
{
    [Header("이동 속도 설정")]
    public float walkSpeed = 3f;           // 🚶 [핵심] 걷는 속도 (AI, 술래, 생존자 모두 완벽히 동일!)
    public float seekerRunSpeed = 7.5f;    // 🏃 술래가 정체를 드러내고 추격할 때의 속도
    public float survivorRunSpeed = 6.5f;  // 🏃 생존자가 정체를 들켜서 도망칠 때의 속도


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


    [Header("점프 및 땅 감지 설정")]
    public float jumpPower = 5f;
    public float rayLength = 0.2f;

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


        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        CheckGrounded();
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
        // 🥊 2. 공격 (마우스 좌클릭)
        // ==========================================
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return; // 여기서 코드를 멈춤!
            }


            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            // 생존자는 펀치 금지! '술래'일 때만 애니메이션과 판정 실행
            if (myRole == "Seeker")
            {
                photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);
                CheckPunchHit();
            }
        }

        // ==========================================
        // 🏃 3. 이동 로직 (걷기는 통일, 달리기는 차별화)
        // ==========================================

        if (isAttacking) return;
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

            // 🌟 [핵심 수정] 걷기 속도는 통일하고, 달릴 때만 직업 검사!
            float currentSpeed = walkSpeed; // 기본은 통일된 걷기 속도

            if (isRunning) // 쉬프트 키를 눌러 달린다면?
            {
                if (myRole == "Seeker") currentSpeed = seekerRunSpeed;
                else currentSpeed = survivorRunSpeed;
            }

            Vector3 targetVelocity = moveDir * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y; // 🌟 점프나 중력(떨어짐)은 그대로 유지되도록 y값은 살려둡니다!
            rb.linearVelocity = targetVelocity;

            float animValue = isRunning ? 1.0f : 0.5f;
            if (anim != null) anim.SetFloat("MoveSpeed", animValue);
        }
        else
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
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
    //void OnCollisionEnter(Collision col)
    //{
    //    if (col.gameObject.CompareTag("Ground")) isGrounded = true;
    //}

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

            if (target == null)
            {
                Debug.Log("관전 대상이 사라졌습니다! 명단을 갱신합니다.");
                UpdateSurvivorList(); // 명단 새로고침
                spectateIndex = 0;    // 0번 타자로 다시 초기화
                return;               // 에러 안 나게 이번 턴은 그냥 넘김!
            }

            // 타겟 머리 위나 어깨너머로 살짝 띄워주는 게 좋습니다. (y축으로 2미터 위)
            transform.position = target.position + new Vector3(0, 2f, 0);

            // 마우스로 화면 돌리는 건 그대로 먹히게 둬도 좋고, 타겟 시점과 완전히 맞추려면 아래 코드 주석 해제!
            // transform.rotation = target.rotation; 
        }
    }

    [Header("하이 리스크 하이 리턴 설정")]
    public float hitStunTime = 0.5f;     // 🎯 진짜를 맞췄을 때 딜레이 (짧음)
    public float penaltyStunTime = 3.5f; // 😱 허공/AI를 때렸을 때 페널티 (엄청 김!)
    public float attackRadius = 1.2f;    // 💥 펀치 판정 두께 (높을수록 맞추기 쉬움)
    private bool isAttacking = false;

    // ==========================================
    // 💥 펀치 타격 판정 (다중 관통 타격 지원!)
    // ==========================================
    void CheckPunchHit()
    {
        // 🌟 [수정] RaycastHit 단일 변수가 아니라, 배열([])을 써서 범위 내의 '모든' 타격 대상을 가져옵니다!
        RaycastHit[] hits = Physics.SphereCastAll(transform.position + Vector3.up * 1f, attackRadius, transform.forward, 1.5f);

        bool hitRealPlayer = false; // 진짜 플레이어를 때렸는지 체크

        // 범위 안에 들어온 모든 녀석들을 하나씩 멱살 잡고 검사합니다.
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                PhotonView targetView = hit.collider.GetComponent<PhotonView>();

                // 🌟 [핵심 완벽 구별법] 맞은 녀석한테 'PlayerMove(진짜 유저용)' 스크립트가 있는지 검사합니다!
                // AI 더미(RandomRoam만 있음)를 때렸을 때 오류가 나는 것을 완벽히 방지합니다.
                PlayerMove targetPlayer = hit.collider.GetComponent<PlayerMove>();

                // 1. PhotonView가 있고 2. 내 자신이 아니며 3. 진짜 유저(PlayerMove 존재)이고 4. 아직 안 죽었다면!
                if (targetView != null && !targetView.IsMine && targetPlayer != null && !targetPlayer.isDead)
                {
                    Debug.Log("🎯 펀치 관통 적중! 무리 속에서 진짜를 찾았습니다: " + targetView.Owner.NickName);

                    // 맞은 진짜 유저에게 잡혔다고 알림!
                    targetView.RPC("GetCaught", RpcTarget.All);
                    hitRealPlayer = true;

                    // 진짜를 찾았으니 더 이상 다른 녀석들을 검사할 필요 없이 반복문 탈출!
                    break;
                }
            }
        }

        // ==========================================
        // ⚖️ 판정 결과에 따른 페널티 발동
        // ==========================================
        if (hitRealPlayer)
        {
            // 진짜를 맞췄다! (AI랑 같이 맞았어도 진짜가 섞여있으니 무죄!) -> 짧은 공격 딜레이
            StartCoroutine(AttackDelayRoutine(hitStunTime));
        }
        else
        {
            // 아무도 안 맞았거나, 오직 AI 더미들만 잔뜩 때렸다! -> 엄청나게 긴 기절 페널티
            Debug.Log("😱 앗! 헛스윙이거나 AI 뭉치입니다! 기절 페널티 발동!");
            StartCoroutine(AttackDelayRoutine(penaltyStunTime));
        }
    }

    // ==========================================
    // 🛑 딜레이 및 페널티 코루틴(Coroutine)
    // ==========================================
    System.Collections.IEnumerator AttackDelayRoutine(float delayTime)
    {
        isAttacking = true; // 이동 불가 상태 켜기 (발 묶임!)

        yield return new WaitForSeconds(delayTime);

        isAttacking = false; // 이동 불가 상태 풀림 (다시 이동 가능!)
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




    void CheckGrounded()
    {
        Debug.Log("🔍 CheckGrounded 함수 팽팽 돌아가는 중!");
        Vector3 rayStartPoint = transform.position + (Vector3.up * 0.1f);

        // 💡 1. 씬 뷰에서 빨간 레이저가 "바닥까지 충분히 뚫고 들어가는지" 길이를 확인하세요!
        Debug.DrawRay(rayStartPoint, Vector3.down * rayLength, Color.red);

        if (Physics.Raycast(rayStartPoint, Vector3.down, out RaycastHit hit, rayLength))
        {
            // 💡 2. 콘솔창 확인: 레이저가 도대체 뭘 때리고 있는지 이름을 띄워봅니다!
            Debug.Log("🎯 레이저가 맞춘 물체: " + hit.collider.name + " / 태그: " + hit.collider.tag);

            if (hit.collider.CompareTag("Ground"))
            {
                isGrounded = true;
                return;
            }
        }

        isGrounded = false;
    }





}