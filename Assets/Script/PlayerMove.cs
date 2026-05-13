using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;

public class PlayerMove : MonoBehaviourPun
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";
    [Header("이동 속도 설정")]
    public float walkSpeed = 3.8f;
    public float seekerRunSpeed = 7.5f;
    public float survivorRunSpeed = 6.5f;

    [HideInInspector] public Animator anim;
    private Unity.Cinemachine.CinemachineCamera vcam;
    private Unity.Cinemachine.CinemachineOrbitalFollow orbitalRig;
    private Rigidbody rb;
    private Camera cachedMainCam;

    public string myRole = "";
    private bool isGrounded = true;
    private bool wasGrounded = true;

    [Header("관전 모드 설정")]
    public bool isDead = false;
    private List<Transform> aliveSurvivors = new List<Transform>();
    private int spectateIndex = 0;

    [Header("공격 및 페널티 설정")]
    public float hitStunTime = 0.5f;
    public float penaltyStunTime = 3.5f;
    public float attackRadius = 1.2f;
    private bool isAttacking = false;

    [Header("점프 및 땅 감지 설정")]
    public float jumpPower = 5f;
    public float rayLength = 0.45f;
    public float groundCheckRadius = 0.2f;
    [Range(0f, 1f)] public float minGroundNormalY = 0.55f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float jumpGroundCheckDelay = 0.08f;
    public LayerMask groundMask = ~0;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundedIgnoreTimer;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // 필수 컴포넌트 확인
        if (anim == null) Debug.LogError($"[{gameObject.name}] Animator 컴포넌트를 찾을 수 없습니다!");
        if (rb == null) Debug.LogError($"[{gameObject.name}] Rigidbody 컴포넌트를 찾을 수 없습니다!");

        if (photonView.IsMine)
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey))
            {
                myRole = (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey];
                StartCoroutine(ShowRoleSequence());
            }

            StartCoroutine(InitializeCameraWithDelay());
        }
    }

    IEnumerator InitializeCameraWithDelay()
    {
        if (!photonView.IsMine) yield break;

        // 씬 로드 안정화 대기
        yield return null;
        yield return new WaitForSeconds(0.3f);

        // vcam 탐색: 최대 3초간 반복 시도
        float timeout = 3f;
        float elapsed = 0f;

        while (vcam == null && elapsed < timeout)
        {
            // 1) CinemachineBrain이 붙은 카메라 → 같은 오브젝트에 CinemachineCamera가 있는지 확인
            Unity.Cinemachine.CinemachineBrain brain = FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null)
            {
                vcam = brain.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                // Brain과 vcam이 별도 오브젝트인 경우 씬 전체에서 vcam 검색
                if (vcam == null)
                    vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);

                // 이동 기준 카메라 = Brain이 붙은 Camera 컴포넌트
                if (cachedMainCam == null)
                    cachedMainCam = brain.GetComponent<Camera>();
            }

            // 2) Brain 없으면 MainCamera 태그 → CinemachineCamera 순으로 탐색
            if (vcam == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera");
                if (tagged != null)
                    vcam = tagged.GetComponent<Unity.Cinemachine.CinemachineCamera>()
                           ?? tagged.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
            }

            // 3) 최후 수단: 씬 전체 탐색
            if (vcam == null)
                vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);

            if (vcam == null)
            {
                elapsed += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
        }

        if (vcam == null)
        {
            Debug.LogError($"[{PhotonNetwork.NickName}] CinemachineCamera를 {timeout}초 내에 찾지 못했습니다. 씬에 Cinemachine Camera가 있는지 확인하세요.");
            yield break;
        }

        // vcam 활성화 보장
        if (!vcam.gameObject.activeSelf) vcam.gameObject.SetActive(true);
        if (!vcam.enabled) vcam.enabled = true;

        // 로컬 플레이어를 Follow/LookAt 대상으로 설정
        vcam.Follow = this.transform;
        vcam.LookAt = this.transform;
        orbitalRig = vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();

        // cachedMainCam이 아직 없으면 Camera.main → FindFirstObjectByType 순으로 탐색
        if (cachedMainCam == null) cachedMainCam = Camera.main;
        if (cachedMainCam == null) cachedMainCam = FindFirstObjectByType<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"[{PhotonNetwork.NickName}] 카메라 초기화 완료 — vcam: {vcam.name}, renderCam: {(cachedMainCam != null ? cachedMainCam.name : "null")}");
    }

    IEnumerator ShowRoleSequence()
    {
        GameObject cornerObj = GameObject.Find("CornerRoleText");
        GameObject blindObj = GameObject.Find("BlindPanel");

        if (cornerObj != null)
        {
            TextMeshProUGUI cornerText = cornerObj.GetComponent<TextMeshProUGUI>();
            cornerObj.SetActive(true);

            if (myRole == SeekerRole)
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

        // 펀치 애니메이션 재생 중이면 이동 차단
        if (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName("Cross Punch"))
        {
            float normalizedTime = anim.GetCurrentAnimatorStateInfo(0).normalizedTime;

            if (normalizedTime < 1.0f)
            {
                if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                anim.SetFloat("MoveSpeed", 0f);
                HandleCursorUpdate();
                return;
            }
            anim.SetFloat("MoveSpeed", 0f);
        }
        
        HandleCursorUpdate();
        CheckGrounded();

        if (isGrounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.deltaTime;

        jumpBufferCounter -= Time.deltaTime;

        if (!wasGrounded && isGrounded)
        {
            photonView.RPC("RPC_PlayLandAnimation", RpcTarget.All);
        }
        wasGrounded = isGrounded;

        // 점프
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            }

            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All);
            isGrounded = false;
            wasGrounded = false;
            groundedIgnoreTimer = jumpGroundCheckDelay;
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
        }

        // 공격 (마우스 좌클릭)
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (myRole == SeekerRole && !isAttacking)
            {
                isAttacking = true;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);
                CheckPunchHit();
            }
        }

        // 이동
        MoveUpdate();
    }

    void HandleCursorUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 마우스 클릭 시 커서 잠금
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void MoveUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = Vector3.zero;

        bool isAltLooking = Input.GetKey(KeyCode.LeftAlt);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetKeyUp(KeyCode.LeftAlt) && orbitalRig != null)
        {
            orbitalRig.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        // 카메라 참조 누락 시 재시도 — 활성 카메라만
        if (cachedMainCam == null)
        {
            cachedMainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        }

        if (h != 0 || v != 0)
        {
            if (cachedMainCam != null)
            {
                if (isAltLooking) moveDir = (transform.forward * v + transform.right * h).normalized;
                else
                {
                    Vector3 camForward = cachedMainCam.transform.forward;
                    camForward.y = 0;
                    Vector3 camRight = cachedMainCam.transform.right;
                    camRight.y = 0;
                    moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
                }
            }
            else moveDir = new Vector3(h, 0, v).normalized;

            if (!isAltLooking)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * 360f);
            }

            float currentSpeed = isRunning ? (myRole == SeekerRole ? seekerRunSpeed : survivorRunSpeed) : walkSpeed;
            Vector3 targetVelocity = moveDir * currentSpeed;
            if (rb != null)
            {
                targetVelocity.y = rb.linearVelocity.y;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 20f);
            }

            float animValue = isRunning ? 1.0f : 0.5f;
            if (anim != null) anim.SetFloat("MoveSpeed", animValue);
        }
        else
        {
            // 멈출 때도 부드럽게 감속
            Vector3 stoppedVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, stoppedVelocity, Time.deltaTime * 15f);

            if (anim != null) anim.SetFloat("MoveSpeed", 0f);
        }
    }

    void CheckPunchHit()
    {
        if (rb == null || anim == null) return;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position + Vector3.up * 1f, attackRadius, transform.forward, 1.5f);
        bool hitRealPlayer = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                PhotonView targetView = hit.collider.GetComponent<PhotonView>();
                PlayerMove targetPlayer = hit.collider.GetComponent<PlayerMove>();

                if (targetView != null && !targetView.IsMine && targetPlayer != null && !targetPlayer.isDead)
                {
                    targetView.RPC("GetCaught", RpcTarget.All);
                    hitRealPlayer = true;
                    break;
                }
            }
        }

        float finalDelay = hitRealPlayer ? hitStunTime : penaltyStunTime;
        StartCoroutine(AttackDelayRoutine(finalDelay));
    }

    IEnumerator AttackDelayRoutine(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        isAttacking = false;
    }

    [PunRPC]
    public void GetCaught()
    {
        if (isDead) return;
        isDead = true;
        if (anim != null) anim.SetTrigger("Die");

        // GameManager가 없는 경우 대비
        if (PhotonNetwork.IsMasterClient && GameManager.instance != null)
        {
            GameManager.instance.photonView.RPC("OnSurvivorCaught", RpcTarget.MasterClient);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) r.enabled = false;

        Collider coll = GetComponent<Collider>();
        if (coll != null) coll.enabled = false;

        UpdateSurvivorList();
    }

    void UpdateSurvivorList()
    {
        aliveSurvivors.Clear();
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject p in allPlayers)
        {
            Renderer pRenderer = p.GetComponentInChildren<Renderer>();
            if (p != this.gameObject && pRenderer != null && pRenderer.enabled)
                aliveSurvivors.Add(p.transform);
        }
    }

    void SpectateUpdate()
    {
        if (aliveSurvivors.Count == 0) return;
        if (Input.GetMouseButtonDown(0))
        {
            spectateIndex = (spectateIndex + 1) % aliveSurvivors.Count;
            UpdateSurvivorList();
        }

        if (spectateIndex < aliveSurvivors.Count)
        {
            Transform target = aliveSurvivors[spectateIndex];
            if (target != null) transform.position = target.position + new Vector3(0, 2f, 0);
            else UpdateSurvivorList();
        }
    }

    // Raycast 기반 바닥 감지
    void CheckGrounded()
    {
        if (groundedIgnoreTimer > 0f)
        {
            groundedIgnoreTimer -= Time.deltaTime;
            isGrounded = false;
            return;
        }

        Vector3 castStartPoint = transform.position + (Vector3.up * 0.2f);
        Debug.DrawRay(castStartPoint, Vector3.down * rayLength, Color.red);

        RaycastHit[] hits = Physics.SphereCastAll(
            castStartPoint,
            groundCheckRadius,
            Vector3.down,
            rayLength,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.normal.y < minGroundNormalY) continue;

            isGrounded = true;
            break;
        }
    }

    // 애니메이션 RPC
    [PunRPC] void RPC_PlayPunchAnimation() { if (anim != null) anim.SetTrigger("Punch"); } 
    [PunRPC]
    void RPC_PlayJumpAnimation()
    {
        if (anim == null) return;
        anim.SetTrigger("Jump");
        if (HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool))
        {
            anim.SetBool("IsGrounded", false);
        }
    }

    [PunRPC]
    void RPC_PlayLandAnimation()
    {
        if (anim == null) return;

        anim.ResetTrigger("Jump");

        if (HasAnimatorParameter("Land", AnimatorControllerParameterType.Trigger))
        {
            anim.SetTrigger("Land");
        }

        if (HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool))
        {
            anim.SetBool("IsGrounded", true);
        }
    }

    bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (anim == null) return false;

        foreach (var p in anim.parameters)
        {
            if (p.type == type && p.name == paramName)
            {
                return true;
            }
        }

        return false;
    }
}