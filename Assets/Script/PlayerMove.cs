using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;

public class PlayerMove : MonoBehaviourPun, IPunObservable
{
    private const string RoleKey = "Role";
    private const string SeekerRole = "Seeker";

    [Header("이동 속도 및 물리 설정")]
    public float walkSpeed = 3.8f;
    public float seekerRunSpeed = 7.5f;
    public float survivorRunSpeed = 6.5f;
    public float groundAcceleration = 25f;
    public float airAcceleration = 3f;
    public float groundDeceleration = 20f;
    public float airDeceleration = 1f;

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

    [Header("오인폭행 패널티 설정")]
    public int overkillThreshold = 5;       // 몇 번 때리면 패널티
    public float overkillPenaltyDuration = 10f;
    public float overkillSpeedMultiplier = 0.4f; // 이동속도 40%로 감소
    private int aiHitCount = 0;
    private bool isOverkillPenalty = false;
    private TextMeshProUGUI penaltyText;

    [Header("소음 시스템 설정")]
    public float sprintNoiseThreshold = 1.5f;
    private float sprintNoiseTimer = 0f;

    [HideInInspector] public bool hackSpeedBoost = false;

    [Header("점프 및 땅 감지 설정")]
    public float jumpPower = 6.5f;
    public float rayLength = 0.35f;
    public float groundCheckRadius = 0.28f;
    [Range(0f, 1f)] public float minGroundNormalY = 0.65f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float jumpGroundCheckDelay = 0.1f;
    public LayerMask groundMask = ~0;

    [Header("계단/턱 오르기 설정")]
    public float stepHeight = 0.3f;
    public float stepSmooth = 4.0f;

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundedIgnoreTimer;
    private Vector3 groundNormal = Vector3.up;

    private float currentH = 0f;
    private float currentV = 0f;
    private bool isRunningSync = false;
    private ObjectivePoint currentInteractionTarget;
    private UnityEngine.UI.Slider progressBar;
    private TextMeshProUGUI interactionText;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
        if (stream.IsWriting)
        {
            stream.SendNext(currentH);
            stream.SendNext(currentV);
            stream.SendNext(Input.GetKey(KeyCode.LeftShift));
            stream.SendNext(isGrounded);
            stream.SendNext(isDead);
        }
        else
        {
            currentH = (float)stream.ReceiveNext();
            currentV = (float)stream.ReceiveNext();
            isRunningSync = (bool)stream.ReceiveNext();
            isGrounded = (bool)stream.ReceiveNext();
            bool deadState = (bool)stream.ReceiveNext();
            
            if (isDead != deadState)
            {
                isDead = deadState;
                if (isDead) SyncDeadState();
            }
        }
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        if (anim == null) Debug.LogError($"[{gameObject.name}] Animator 컴포넌트를 찾을 수 없습니다!");
        if (rb == null) Debug.LogError($"[{gameObject.name}] Rigidbody 컴포넌트를 찾을 수 없습니다!");

        if (photonView.Owner.CustomProperties.ContainsKey(RoleKey))
        {
            myRole = (string)photonView.Owner.CustomProperties[RoleKey];
            
            // Add item handlers for all clients so RPCs can be found
            if (myRole == SeekerRole)
                gameObject.AddComponent<SeekerItemHandler>();
            else
                gameObject.AddComponent<SurvivorItemHandler>();
                
            // Refresh RPC cache since components were added at runtime
            photonView.RefreshRpcMonoBehaviourCache();
        }

        if (photonView.IsMine)
        {
            StartCoroutine(ShowRoleSequence());
            StartCoroutine(InitializeCameraWithDelay());
            CreateInteractionUI();
        }
    }

    void CreateInteractionUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject textObj = new GameObject("InteractionText");
        textObj.transform.SetParent(canvas.transform, false);
        interactionText = textObj.AddComponent<TextMeshProUGUI>();
        interactionText.text = "Press [E] to Hack";
        interactionText.fontSize = 30;
        interactionText.alignment = TextAlignmentOptions.Center;
        interactionText.color = Color.yellow;
        RectTransform rtText = interactionText.GetComponent<RectTransform>();
        rtText.anchoredPosition = new Vector2(0, -100);
        textObj.SetActive(false);

        GameObject sliderObj = new GameObject("InteractionProgressBar");
        sliderObj.transform.SetParent(canvas.transform, false);
        progressBar = sliderObj.AddComponent<UnityEngine.UI.Slider>();
        RectTransform rtSlider = progressBar.GetComponent<RectTransform>();
        rtSlider.sizeDelta = new Vector2(300, 30);
        rtSlider.anchoredPosition = new Vector2(0, -150);

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        UnityEngine.UI.Image bgImg = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0, 0, 0, 0.7f);
        RectTransform rtBg = bgImg.GetComponent<RectTransform>();
        rtBg.anchorMin = Vector2.zero; rtBg.anchorMax = Vector2.one; rtBg.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform rtFillArea = fillArea.AddComponent<RectTransform>();
        rtFillArea.anchorMin = new Vector2(0, 0); rtFillArea.anchorMax = new Vector2(1, 1);
        rtFillArea.sizeDelta = new Vector2(-10, -10);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        UnityEngine.UI.Image fillImg = fill.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = Color.green;
        RectTransform rtFill = fillImg.GetComponent<RectTransform>();
        rtFill.anchorMin = Vector2.zero; rtFill.anchorMax = Vector2.one; rtFill.sizeDelta = Vector2.zero;

        progressBar.fillRect = rtFill;
        progressBar.minValue = 0;
        progressBar.maxValue = 1;
        sliderObj.SetActive(false);
    }

    IEnumerator InitializeCameraWithDelay()
    {
        if (!photonView.IsMine) yield break;
        yield return new WaitForSeconds(0.3f);
        float timeout = 3f; float elapsed = 0f;
        while (vcam == null && elapsed < timeout)
        {
            Unity.Cinemachine.CinemachineBrain brain = FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) { vcam = brain.GetComponent<Unity.Cinemachine.CinemachineCamera>() ?? FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include); if (cachedMainCam == null) cachedMainCam = brain.GetComponent<Camera>(); }
            if (vcam == null) { GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera"); if (tagged != null) vcam = tagged.GetComponent<Unity.Cinemachine.CinemachineCamera>() ?? tagged.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true); }
            if (vcam == null) vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);
            if (vcam == null) { elapsed += 0.2f; yield return new WaitForSeconds(0.2f); }
        }
        if (vcam != null) { vcam.Follow = this.transform; vcam.LookAt = this.transform; orbitalRig = vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>(); }
        if (cachedMainCam == null) cachedMainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    IEnumerator ShowRoleSequence()
    {
        GameObject cornerObj = GameObject.Find("CornerRoleText"); GameObject blindObj = GameObject.Find("BlindPanel");
        if (cornerObj != null) { TextMeshProUGUI cornerText = cornerObj.GetComponent<TextMeshProUGUI>(); cornerObj.SetActive(true); if (myRole == SeekerRole) { cornerText.text = "<color=red>Seeker</color>"; if (blindObj != null) blindObj.SetActive(true); } else { cornerText.text = "<color=#00BFFF>Surviver</color>"; if (blindObj != null) blindObj.SetActive(false); } }
        yield return new WaitForSeconds(5f); if (blindObj != null) blindObj.SetActive(false);
    }

    private float airTime = 0f;

    void Update()
    {
        if (!photonView.IsMine)
        {
            if (isDead) SyncDeadState();
            UpdateAnimation();
            return;
        }

        if (isDead) { SpectateUpdate(); return; }

        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null) return;
        
        HandleCursorUpdate();
        CheckGrounded();

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;

            // 공중 체류 0.15초 이상 또는 낙하 속도가 있을 때만 착지 처리 (낮은 턱에서 팔 흔들림 방지)
            if (!wasGrounded && (airTime > 0.15f || (rb != null && rb.linearVelocity.y < -2.0f)))
            {
                bool isRunAtLand = Input.GetKey(KeyCode.LeftShift);
                photonView.RPC("RPC_PlayLandAnimation", RpcTarget.All, isRunAtLand);

                if (rb != null)
                {
                    float reduction = isRunAtLand ? 0.95f : 0.8f;
                    Vector3 vel = rb.linearVelocity;
                    vel.x *= reduction;
                    vel.z *= reduction;
                    rb.linearVelocity = vel;
                }
            }
            airTime = 0f;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
            airTime += Time.deltaTime;
        }

        jumpBufferCounter -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            if (rb != null)
            {
                // 점프 시 수직 속도를 즉시 설정하여 물리 엔진의 씹힘 현상 방지
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
            }
            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All);

            if (myRole != SeekerRole) TriggerNoise();

            isGrounded = false;
            wasGrounded = false;
            groundedIgnoreTimer = 0.15f;
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
            airTime = 0f;
        }

        if (Input.GetMouseButtonDown(0)) { var _es = UnityEngine.EventSystems.EventSystem.current; if ((_es == null || !_es.IsPointerOverGameObject()) && myRole == SeekerRole && !isAttacking) StartCoroutine(PerformAttack()); }
        
        HandleNoiseDetection();
        HandleInteraction();
        MoveUpdate();
        UpdateAnimation();

        wasGrounded = isGrounded; // 마지막에 업데이트해야 착지 판정이 정확함
    }

        void HandleInteraction()
        {
            if (myRole == SeekerRole || isDead || !photonView.IsMine) return;

            ObjectivePoint nearest = null;
            float interactRange = 6.0f; // Increased range
            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
            foreach (var hit in hits)
            {
                // Search in self or parents
                ObjectivePoint op = hit.GetComponent<ObjectivePoint>() ?? hit.GetComponentInParent<ObjectivePoint>();
                if (op != null && !op.isCompleted)
                {
                    nearest = op;
                    break;
                }
            }

            if (interactionText != null) interactionText.gameObject.SetActive(nearest != null && currentInteractionTarget == null);

            if (Input.GetKey(KeyCode.E) && nearest != null)
            {
                currentInteractionTarget = nearest;
                float progressAmount = hackSpeedBoost ? Time.deltaTime * 2f : Time.deltaTime;
                currentInteractionTarget.photonView.RPC("RPC_AddProgress", RpcTarget.MasterClient, progressAmount);
                if (currentInteractionTarget.isCompleted) hackSpeedBoost = false;
            
                if (progressBar != null)
                {
                    progressBar.gameObject.SetActive(true);
                    progressBar.value = currentInteractionTarget.currentProgress / currentInteractionTarget.interactionTime;
                }

                currentH *= 0.1f;
                currentV *= 0.1f;
            }
            else
            {
                currentInteractionTarget = null;
                if (progressBar != null) progressBar.gameObject.SetActive(false);
            }
        }

    void HandleNoiseDetection()
    {
        if (myRole == SeekerRole || isDead) return;

        bool isRun = Input.GetKey(KeyCode.LeftShift);
        bool hasInput = (currentH != 0 || currentV != 0);

        if (isGrounded && isRun && hasInput)
        {
            sprintNoiseTimer += Time.deltaTime;
            if (sprintNoiseTimer >= sprintNoiseThreshold)
            {
                TriggerNoise();
                sprintNoiseTimer = 0f;
            }
        }
        else
        {
            sprintNoiseTimer = 0f;
        }
    }

    void TriggerNoise()
    {
        Debug.Log("[Noise] Triggering noise at " + transform.position);
        photonView.RPC("RPC_CreateNoisePing", RpcTarget.All, transform.position + Vector3.up * 2f);
    }

    [PunRPC]
    void RPC_CreateNoisePing(Vector3 position)
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoleKey) && 
            (string)PhotonNetwork.LocalPlayer.CustomProperties[RoleKey] == SeekerRole)
        {
            Debug.Log("[Noise] Seeker received noise ping at " + position);
            GameObject ping = new GameObject("NoisePing");
            ping.transform.position = position;
            ping.AddComponent<NoisePing>();
        }
    }

    IEnumerator PerformAttack()
    {
        if (isAttacking || isOverkillPenalty) yield break;
        isAttacking = true; if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);
        bool hit = CheckPunchHitOwner(); yield return new WaitForSeconds(hit ? hitStunTime : penaltyStunTime);
        isAttacking = false;
    }

    IEnumerator OverkillPenaltyRoutine()
    {
        isOverkillPenalty = true;

        float originalWalkSpeed = walkSpeed;
        float originalSeekerRunSpeed = seekerRunSpeed;
        walkSpeed *= overkillSpeedMultiplier;
        seekerRunSpeed *= overkillSpeedMultiplier;

        EnsurePenaltyText();

        float remaining = overkillPenaltyDuration;
        while (remaining > 0f)
        {
            if (penaltyText != null)
                penaltyText.text = $"⚠ 오인폭행 패널티\n공격 불가 · 이동 감소\n{Mathf.CeilToInt(remaining)}초";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        walkSpeed = originalWalkSpeed;
        seekerRunSpeed = originalSeekerRunSpeed;
        isOverkillPenalty = false;

        if (penaltyText != null)
        {
            penaltyText.text = "";
            penaltyText.gameObject.SetActive(false);
        }
    }

    void EnsurePenaltyText()
    {
        if (penaltyText != null) { penaltyText.gameObject.SetActive(true); return; }

        GameObject canvas = GameObject.Find("Canvas") ?? GameObject.Find("HUDCanvas") ?? GameObject.Find("GameCanvas");
        if (canvas == null) return;

        GameObject obj = new GameObject("PenaltyText");
        obj.transform.SetParent(canvas.transform, false);

        penaltyText = obj.AddComponent<TextMeshProUGUI>();
        penaltyText.fontSize = 22;
        penaltyText.color = new Color(1f, 0.3f, 0.3f, 1f);
        penaltyText.alignment = TextAlignmentOptions.Center;
        penaltyText.fontStyle = FontStyles.Bold;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.85f);
        rt.anchorMax = new Vector2(0.5f, 0.85f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(400f, 100f);
    }

    void MoveUpdate()
    {
        if (isAttacking) { currentH = 0; currentV = 0; return; }
        
        // Only read axes if we haven't set currentH/V elsewhere (like for interaction penalty)
        // Actually, it's better to always read but apply penalty at the end of MoveUpdate
        float h = Input.GetAxisRaw("Horizontal"); 
        float v = Input.GetAxisRaw("Vertical");
        
        if (Mathf.Abs(h) < 0.2f) h = 0f; 
        if (Mathf.Abs(v) < 0.2f) v = 0f;
        
        currentH = h; 
        currentV = v; 

        // Apply interaction penalty
        if (currentInteractionTarget != null)
        {
            currentH *= 0.1f;
            currentV *= 0.1f;
        }

        Vector3 moveDir = Vector3.zero; 
        bool isAlt = Input.GetKey(KeyCode.LeftAlt); 
        bool isRun = Input.GetKey(KeyCode.LeftShift);
        
        if (cachedMainCam == null) cachedMainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        
        if (currentH != 0 || currentV != 0)
        {
            if (cachedMainCam != null) 
            { 
                if (isAlt) moveDir = (transform.forward * currentV + transform.right * currentH).normalized; 
                else 
                { 
                    Vector3 f = cachedMainCam.transform.forward; f.y = 0; 
                    Vector3 r = cachedMainCam.transform.right; r.y = 0; 
                    moveDir = (f.normalized * currentV + r.normalized * currentH).normalized; 
                } 
            }
            else moveDir = new Vector3(currentH, 0, currentV).normalized;
            
            if (!isAlt && moveDir != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 540f);
            
            float speed = isRun ? (myRole == SeekerRole ? seekerRunSpeed : survivorRunSpeed) : walkSpeed; 
            Vector3 targetVel = moveDir * speed;
            
            if (rb != null)
            {
                targetVel.y = rb.linearVelocity.y;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * (isGrounded ? groundAcceleration : airAcceleration));

                // 계단/턱 오르기
                HandleStepOffset(moveDir);

                // Ground Snapping
                if (isGrounded && groundedIgnoreTimer <= 0f && rb.linearVelocity.y > -0.5f)
                    rb.AddForce(Vector3.down * 15f, ForceMode.Acceleration);
            }
        }
        else if (rb != null)
        {
            Vector3 targetVel = new Vector3(0, rb.linearVelocity.y, 0);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * (isGrounded ? groundDeceleration : airDeceleration));

            // 정지 중에도 지면 밀착 유지
            if (isGrounded && groundedIgnoreTimer <= 0f && rb.linearVelocity.y > -0.5f)
                rb.AddForce(Vector3.down * 10f, ForceMode.Acceleration);
        }
    }

    void UpdateAnimation()
    {
        if (anim == null) return;
        if (isAttacking) { ResetMovementAnimatorParameters(); return; }
        
        bool isRun = photonView.IsMine ? Input.GetKey(KeyCode.LeftShift) : isRunningSync;
        bool hasInput = (currentH != 0 || currentV != 0);
        
        Vector3 moveDir = Vector3.zero;
        if (hasInput)
        {
            if (cachedMainCam == null) cachedMainCam = Camera.main;
            if (cachedMainCam != null)
            {
                Vector3 f = cachedMainCam.transform.forward; f.y = 0;
                Vector3 r = cachedMainCam.transform.right; r.y = 0;
                moveDir = (f.normalized * currentV + r.normalized * currentH).normalized;
            }
            else moveDir = new Vector3(currentH, 0, currentV).normalized;
        }

        float targetH = 0f;
        float targetV = 0f;

        if (hasInput && moveDir != Vector3.zero)
        {
            // Use 1.1f to ensure it definitely crosses the 1.0 threshold in the blend tree
            float speedFactor = isRun ? 1.1f : 0.5f;
            Vector3 localDir = transform.InverseTransformDirection(moveDir);
            
            targetH = localDir.x * speedFactor;
            targetV = localDir.z * speedFactor;
        }

        // Use a faster and more consistent smoothing method
        float smoothTime = isGrounded ? 15f : 3f; // Snappier on ground
float curH = anim.GetFloat("Horizontal");
        float curV = anim.GetFloat("Vertical");
        float curSpeed = anim.GetFloat("MoveSpeed");

        anim.SetFloat("Horizontal", Mathf.MoveTowards(curH, targetH, Time.deltaTime * smoothTime));
        anim.SetFloat("Vertical", Mathf.MoveTowards(curV, targetV, Time.deltaTime * smoothTime));
        anim.SetFloat("MoveSpeed", Mathf.MoveTowards(curSpeed, Mathf.Max(Mathf.Abs(targetH), Mathf.Abs(targetV)), Time.deltaTime * smoothTime));
        
        // Sync ground/jump/fall states
        anim.SetBool("IsGrounded", isGrounded);
        
        float yVel = (rb != null) ? rb.linearVelocity.y : 0f;
        if (!isGrounded)
        {
            anim.SetBool("IsJump", yVel > 0.1f);
            anim.SetBool("IsFalling", yVel <= 0.1f);
        }
        else
        {
            anim.SetBool("IsJump", false);
            anim.SetBool("IsFalling", false);
        }
    }

    void ResetMovementAnimatorParameters() 
    { 
        if (anim == null) return; 
        anim.SetFloat("MoveSpeed", 0f); 
        anim.SetFloat("Horizontal", 0f);
        anim.SetFloat("Vertical", 0f);
    }

    bool CheckPunchHitOwner()
    {
        if (rb == null) return false;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position + Vector3.up * 1f, attackRadius, transform.forward, 1.5f);
        foreach (RaycastHit hit in hits)
        {
            // 생존자(플레이어) 적중
            if (hit.collider.CompareTag("Player"))
            {
                PhotonView tv = hit.collider.GetComponent<PhotonView>();
                PlayerMove tp = hit.collider.GetComponent<PlayerMove>();
                if (tv != null && !tv.IsMine && tp != null && !tp.isDead)
                {
                    tv.RPC("GetCaught", RpcTarget.All);
                    return true;
                }
            }

            // AI 시민 적중
            RandomRoam ai = hit.collider.GetComponent<RandomRoam>() ?? hit.collider.GetComponentInParent<RandomRoam>();
            if (ai != null)
            {
                PhotonView tv = ai.GetComponent<PhotonView>();
                if (tv != null)
                    tv.RPC("RPC_PlayHitAnimation", RpcTarget.All);

                aiHitCount++;
                if (aiHitCount >= overkillThreshold && !isOverkillPenalty)
                {
                    aiHitCount = 0;
                    StartCoroutine(OverkillPenaltyRoutine());
                }
                return true;
            }
        }
        return false;
    }

    [PunRPC] 
void RPC_PlayPunchAnimation() 
    { 
        if (anim != null) 
        { 
            anim.SetTrigger("Punch"); 
        } 
    }

    [PunRPC] 
    void RPC_PlayJumpAnimation() 
    { 
        if (anim != null) 
        { 
            anim.SetTrigger("Jump");
            anim.SetBool("IsGrounded", false);
        } 
    }
    [PunRPC]
    void RPC_PlayLandAnimation(bool wasRunningAtLand)
    {
        if (anim == null) return;
        anim.SetBool("IsJump", false);
        anim.SetBool("IsFalling", false);

        bool hasInput = (currentH != 0 || currentV != 0);
        if (hasInput)
        {
            if (wasRunningAtLand)
                anim.CrossFadeInFixedTime("No Weapon.Jumping.JumpRun_RU_Land2Run", 0.05f);
            else
                anim.CrossFadeInFixedTime("No Weapon.Falling.JumpIdleLand2Walk", 0.05f);
        }
        else
        {
            anim.CrossFadeInFixedTime("No Weapon.Falling.JumpIdleLandHard", 0.1f);
        }
    }
    
    [PunRPC] 
    public void GetCaught() 
    { 
        if (isDead) return; 
        isDead = true; 
        SyncDeadState();
        if (PhotonNetwork.IsMasterClient && GameManager.instance != null) GameManager.instance.photonView.RPC("OnSurvivorCaught", RpcTarget.MasterClient); 
        UpdateSurvivorList(); 
    }

    void SyncDeadState()
    {
        if (anim != null) anim.SetBool("IsDead", true); 
        Renderer[] rs = GetComponentsInChildren<Renderer>(); 
        foreach (Renderer r in rs) r.enabled = false; 
        Collider c = GetComponent<Collider>(); 
        if (c != null) c.enabled = false; 
    }
void UpdateSurvivorList() { aliveSurvivors.Clear(); GameObject[] ps = GameObject.FindGameObjectsWithTag("Player"); foreach (GameObject p in ps) { Renderer r = p.GetComponentInChildren<Renderer>(); if (p != this.gameObject && r != null && r.enabled) aliveSurvivors.Add(p.transform); } }
    void SpectateUpdate() { if (aliveSurvivors.Count == 0) return; if (Input.GetMouseButtonDown(0)) { spectateIndex = (spectateIndex + 1) % aliveSurvivors.Count; UpdateSurvivorList(); } if (spectateIndex < aliveSurvivors.Count) { Transform t = aliveSurvivors[spectateIndex]; if (t != null) transform.position = t.position + new Vector3(0, 2f, 0); else UpdateSurvivorList(); } }
    void CheckGrounded()
    {
        if (groundedIgnoreTimer > 0f)
        {
            groundedIgnoreTimer -= Time.deltaTime;
            isGrounded = false;
            return;
        }

        // 구체 캐스팅 시작점을 groundCheckRadius 높이에서 시작해 정확도 향상
        Vector3 p = transform.position + (Vector3.up * groundCheckRadius);
        RaycastHit[] hits = Physics.SphereCastAll(p, groundCheckRadius, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore);

        isGrounded = false;
        groundNormal = Vector3.up;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform) || h.normal.y < minGroundNormalY)
                continue;
            isGrounded = true;
            groundNormal = h.normal;
            break;
        }
    }

    void HandleStepOffset(Vector3 moveDir)
    {
        if (!isGrounded || moveDir == Vector3.zero || rb == null) return;

        // 발 위치 근처에서 이동 방향으로 레이캐스트 (낮은 턱 감지)
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, moveDir, out RaycastHit hitLower, 0.45f, groundMask))
        {
            // 바닥(normal.y가 높음)은 제외, 수직에 가까운 벽/계단 면만 처리
            if (hitLower.normal.y > 0.4f) return;

            // stepHeight 높이에서는 안 걸리면 계단/턱으로 판단
            if (!Physics.Raycast(transform.position + Vector3.up * stepHeight, moveDir, out RaycastHit hitUpper, 0.55f, groundMask))
            {
                rb.position += Vector3.up * stepSmooth * Time.deltaTime;
            }
        }
    }

    void HandleCursorUpdate() { if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } var _ces = UnityEngine.EventSystems.EventSystem.current; if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && (_ces == null || !_ces.IsPointerOverGameObject())) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
}