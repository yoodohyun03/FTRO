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

    [Header("소음 시스템 설정")]
    public float sprintNoiseThreshold = 1.5f;
    private float sprintNoiseTimer = 0f;

    [Header("점프 및 땅 감지 설정")]
    public float jumpPower = 6.5f;
    public float rayLength = 0.35f;
    public float groundCheckRadius = 0.28f;
    [Range(0f, 1f)] public float minGroundNormalY = 0.65f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float jumpGroundCheckDelay = 0.1f;
    public LayerMask groundMask = ~0;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundedIgnoreTimer;

    private float currentH = 0f;
    private float currentV = 0f;
    private bool isRunningSync = false;

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
        if (anim != null) anim.SetFloat("IsControl", 1f);

        if (photonView.Owner.CustomProperties.ContainsKey(RoleKey))
        {
            myRole = (string)photonView.Owner.CustomProperties[RoleKey];
        }

        if (photonView.IsMine)
        {
            StartCoroutine(ShowRoleSequence());
            StartCoroutine(InitializeCameraWithDelay());
        }
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

    void Update()
    {
        if (!photonView.IsMine)
        {
            if (isDead) SyncDeadState();
            UpdateAnimation();
            return;
        }

        if (isDead) { SpectateUpdate(); return; }

        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null) return;
        
        HandleCursorUpdate(); CheckGrounded();
        
        if (isGrounded) coyoteCounter = coyoteTime; else coyoteCounter -= Time.deltaTime;
        jumpBufferCounter -= Time.deltaTime;
        
        if (!wasGrounded && isGrounded) photonView.RPC("RPC_PlayLandAnimation", RpcTarget.All);
        wasGrounded = isGrounded;
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            if (rb != null) { rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse); }
            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All); 
            
            if (myRole != SeekerRole) TriggerNoise();

            isGrounded = false; wasGrounded = false; groundedIgnoreTimer = jumpGroundCheckDelay; coyoteCounter = 0f; jumpBufferCounter = 0f;
        }

        if (Input.GetMouseButtonDown(0)) { if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() && myRole == SeekerRole && !isAttacking) StartCoroutine(PerformAttack()); }
        
        HandleNoiseDetection();
        MoveUpdate(); 
        UpdateAnimation();
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
        if (isAttacking) yield break;
        isAttacking = true; if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);
        bool hit = CheckPunchHitOwner(); yield return new WaitForSeconds(hit ? hitStunTime : penaltyStunTime);
        isAttacking = false;
    }

    void MoveUpdate()
    {
        if (isAttacking) { currentH = 0; currentV = 0; return; }
        float h = Input.GetAxisRaw("Horizontal"); float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) < 0.2f) h = 0f; if (Mathf.Abs(v) < 0.2f) v = 0f;
        currentH = h; currentV = v; Vector3 moveDir = Vector3.zero; bool isAlt = Input.GetKey(KeyCode.LeftAlt); bool isRun = Input.GetKey(KeyCode.LeftShift);
        if (cachedMainCam == null) cachedMainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (h != 0 || v != 0)
        {
            if (cachedMainCam != null) { if (isAlt) moveDir = (transform.forward * v + transform.right * h).normalized; else { Vector3 f = cachedMainCam.transform.forward; f.y = 0; Vector3 r = cachedMainCam.transform.right; r.y = 0; moveDir = (f.normalized * v + r.normalized * h).normalized; } }
            else moveDir = new Vector3(h, 0, v).normalized;
            if (!isAlt && moveDir != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 540f);
            float speed = isRun ? (myRole == SeekerRole ? seekerRunSpeed : survivorRunSpeed) : walkSpeed; Vector3 targetVel = moveDir * speed;
            if (rb != null) { targetVel.y = rb.linearVelocity.y; rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * (isGrounded ? groundAcceleration : airAcceleration)); }
        }
        else if (rb != null) { Vector3 targetVel = new Vector3(0, rb.linearVelocity.y, 0); rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * (isGrounded ? groundDeceleration : airDeceleration)); }
    }

    void UpdateAnimation()
    {
        if (anim == null) return;
        if (isAttacking) { ResetMovementAnimatorParameters(); return; }
        
        bool isRun = photonView.IsMine ? Input.GetKey(KeyCode.LeftShift) : isRunningSync;
        bool hasInput = (currentH != 0 || currentV != 0);
        
        if (!hasInput)
        {
            ResetMovementAnimatorParameters();
            if (isGrounded && groundedIgnoreTimer <= 0f)
            {
                float velMag = (rb != null) ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude : 0f;
                if (velMag < 0.5f)
                {
                    AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);
                    if (s.IsName("No Weapon.Walking.Walking Blend Tree") || s.IsName("No Weapon.Walking.Start Walking Blend Tree") || s.IsName("Walking Blend Tree") || s.IsName("Start Walking Blend Tree"))
                        anim.CrossFadeInFixedTime("No Weapon.Standing.Idle", 0.1f);
                }
            }
        }
        else
        {
            float targetMag = isRun ? 1.0f : 0.5f; 
            float curMag = anim.GetFloat("InputMagnitude"); 
            if (curMag < 0.1f) curMag = 0.25f;
            
            anim.SetFloat("InputMagnitude", Mathf.MoveTowards(curMag, targetMag, Time.deltaTime * 30f)); 
            anim.SetFloat("InputAngle", 0f);
            anim.SetFloat("Vertical", targetMag); 
            anim.SetFloat("Horizontal", 0f); 
            anim.SetFloat("Z", targetMag); 
            anim.SetFloat("X", 0f);
            anim.SetBool("Running", isRun); 
            anim.SetFloat("SprintFactor", isRun ? 1f : 0f);
        }
        
        float yVel = (rb != null) ? rb.linearVelocity.y : 0f;
        anim.SetBool("IsJump", !isGrounded && yVel > 0.1f); 
        anim.SetBool("IsFalling", !isGrounded && yVel <= 0.1f);
    }

    void ResetMovementAnimatorParameters() { if (anim == null) return; anim.SetFloat("InputMagnitude", 0f); anim.SetFloat("InputAngle", 0f); anim.SetFloat("Vertical", 0f); anim.SetFloat("Horizontal", 0f); anim.SetFloat("Z", 0f); anim.SetFloat("X", 0f); anim.SetBool("Running", false); anim.SetFloat("SprintFactor", 0f); }
    bool CheckPunchHitOwner() { if (rb == null) return false; RaycastHit[] hits = Physics.SphereCastAll(transform.position + Vector3.up * 1f, attackRadius, transform.forward, 1.5f); foreach (RaycastHit hit in hits) { if (hit.collider.CompareTag("Player")) { PhotonView tv = hit.collider.GetComponent<PhotonView>(); PlayerMove tp = hit.collider.GetComponent<PlayerMove>(); if (tv != null && !tv.IsMine && tp != null && !tp.isDead) { tv.RPC("GetCaught", RpcTarget.All); return true; } } } return false; }
    [PunRPC] void RPC_PlayPunchAnimation() { if (anim != null) { anim.CrossFadeInFixedTime("No Weapon.Punching.Idle_Punch_Move_L", 0.02f); anim.SetTrigger("IsPunch"); anim.SetTrigger("IsPunchStart"); } }
    [PunRPC] void RPC_PlayJumpAnimation() { if (anim != null) { anim.SetBool("IsJump", true); anim.SetBool("IsFalling", false); bool hasInput = (currentH != 0 || currentV != 0); anim.CrossFadeInFixedTime(hasInput ? "No Weapon.Jumping.JumpRunStart_RU" : "No Weapon.Jumping.JumpIdleStart", 0.1f); } }
    [PunRPC] void RPC_PlayLandAnimation() { if (anim != null) { anim.SetBool("IsJump", false); anim.SetBool("IsFalling", false); } }
    
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
    void CheckGrounded() { if (groundedIgnoreTimer > 0f) { groundedIgnoreTimer -= Time.deltaTime; isGrounded = false; return; } Vector3 p = transform.position + (Vector3.up * 0.15f); RaycastHit[] hits = Physics.SphereCastAll(p, 0.25f, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore); isGrounded = false; foreach (RaycastHit h in hits) { if (h.collider == null || h.collider.isTrigger || h.collider.transform.IsChildOf(transform) || h.normal.y < minGroundNormalY) continue; isGrounded = true; break; } }
    void HandleCursorUpdate() { if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
}