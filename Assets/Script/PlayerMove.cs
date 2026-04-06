using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PlayerMove : MonoBehaviourPun
{
    [Header("이동 속도 설정")]
    public float walkSpeed = 3f;           // 🚶 걷는 속도 (AI와 동일)
    public float seekerRunSpeed = 7.5f;    // 🏃 술래 추격 속도
    public float survivorRunSpeed = 6.5f;  // 🏃 생존자 도주 속도
    public float jumpPower = 5f;

    private Animator anim;
    private Unity.Cinemachine.CinemachineCamera vcam;
    private Unity.Cinemachine.CinemachineOrbitalFollow orbitalRig;
    private Rigidbody rb;

    public string myRole = "";
    private bool isGrounded = true;

    [Header("관전 모드 설정")]
    public bool isDead = false;
    private List<Transform> aliveSurvivors = new List<Transform>();
    private int spectateIndex = 0;

    [Header("공격 및 페널티 설정")]
    public float hitStunTime = 0.5f;     // 🎯 적중 시 딜레이
    public float penaltyStunTime = 3.5f; // 😱 헛스윙/AI 타격 시 페널티
    public float attackRadius = 1.2f;    // 💥 펀치 판정 범위
    private bool isAttacking = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

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
        }
    }

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

        // 🛑 [중요] 공격 중이거나 'Cross Punch' 애니메이션 재생 중이면 이동 불가
        if (isAttacking || (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName("Cross Punch")))
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0); // 물리 속도 강제 제로
            if (anim != null) anim.SetFloat("MoveSpeed", 0f);

            // 공격 중에도 마우스 클릭으로 커서 잠금 해제는 가능하도록 배치
            HandleCursorUpdate();
            return;
        }

        HandleCursorUpdate();

        // 🚀 1. 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All);
            isGrounded = false;
        }

        // 🥊 2. 공격 (마우스 좌클릭)
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (myRole == "Seeker" && !isAttacking)
            {
                // 클릭 즉시 상태 전환 및 정지
                isAttacking = true;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

                photonView.RPC("RPC_PlayPunchAnimation", RpcTarget.All);
                CheckPunchHit();
            }
        }

        // 🏃 3. 이동 로직
        MoveUpdate();
    }

    void HandleCursorUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

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

            float currentSpeed = isRunning ? (myRole == "Seeker" ? seekerRunSpeed : survivorRunSpeed) : walkSpeed;

            Vector3 targetVelocity = moveDir * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
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

    void CheckPunchHit()
    {
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
        isAttacking = true;
        yield return new WaitForSeconds(delayTime);
        isAttacking = false;
    }

    [PunRPC]
    public void GetCaught()
    {
        if (isDead) return;
        isDead = true;
        if (anim != null) anim.SetTrigger("Die");

        if (PhotonNetwork.IsMasterClient)
            GameManager.instance.photonView.RPC("OnSurvivorCaught", RpcTarget.MasterClient);

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

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    [PunRPC] void RPC_PlayPunchAnimation() { if (anim != null) anim.SetTrigger("Punch"); }
    [PunRPC] void RPC_PlayJumpAnimation() { if (anim != null) anim.SetTrigger("Jump"); }
}