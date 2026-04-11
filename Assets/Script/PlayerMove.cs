using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using FindObjectsInactive = UnityEngine.FindObjectsInactive;

public class PlayerMove : MonoBehaviourPun
{
    [Header("이동 속도 설정")]
    public float walkSpeed = 3f;
    public float seekerRunSpeed = 7.5f;
    public float survivorRunSpeed = 6.5f;

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
    public float hitStunTime = 0.5f;
    public float penaltyStunTime = 3.5f;
    public float attackRadius = 1.2f;
    private bool isAttacking = false;

    [Header("점프 및 땅 감지 설정")]
    public float jumpPower = 5f;
    public float rayLength = 0.2f;

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

            StartCoroutine(InitializeCameraWithDelay());
        }
    }

    IEnumerator InitializeCameraWithDelay()
    {
        // 🚨 [중요] 자신의 플레이어가 아니면 카메라 초기화 중단!
        if (!photonView.IsMine)
        {
            Debug.Log($"[{PhotonNetwork.NickName}] 이 플레이어는 로컬 플레이어가 아니므로 카메라 설정을 건너뜁니다.");
            yield break;
        }

        Debug.Log($"[{PhotonNetwork.NickName}] ===== 카메라 찾기 시작 (로컬 플레이어 #{PhotonNetwork.LocalPlayer.ActorNumber}) =====");
        Debug.Log($"[{PhotonNetwork.NickName}] 현재 씬: {SceneManager.GetActiveScene().name}");

        // 한 프레임 대기 (씬이 완전히 로드될 때까지)
        yield return null;

        // 3프레임 더 대기 (Cinemachine이 초기화될 시간 확보)
        yield return new WaitForSeconds(0.1f);

        // 1. MainCamera 태그로 찾기
        GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
        Debug.Log($"[{PhotonNetwork.NickName}] 1️⃣ MainCamera 태그 GameObject: {(mainCameraObj != null ? mainCameraObj.name : "null")}");

        if (mainCameraObj != null)
        {
            Debug.Log($"[{PhotonNetwork.NickName}] MainCamera 상태: Active={mainCameraObj.activeSelf}");
            vcam = mainCameraObj.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                Debug.Log($"[{PhotonNetwork.NickName}] 2️⃣ CinemachineCamera 찾음 ✓");
                Debug.Log($"[{PhotonNetwork.NickName}] 카메라 Priority: {vcam.Priority}");
                Debug.Log($"[{PhotonNetwork.NickName}] 카메라 활성화: {vcam.enabled}");
            }
            else
            {
                Debug.Log($"[{PhotonNetwork.NickName}] 2️⃣ CinemachineCamera 없음 ✗");
            }
        }

        // 2. 태그 못 찾거나 컴포넌트 없으면 FindFirstObjectByType으로 찾기
        if (vcam == null)
        {
            vcam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);
            if (vcam != null)
            {
                Debug.Log($"[{PhotonNetwork.NickName}] 3️⃣ FindFirstObjectByType로 발견: {vcam.gameObject.name}");
            }
            else
            {
                Debug.Log($"[{PhotonNetwork.NickName}] 3️⃣ FindFirstObjectByType 실패");
            }
        }

        // 3. 씬의 모든 GameObject 순회하며 CinemachineCamera 찾기
        if (vcam == null)
        {
            Debug.Log($"[{PhotonNetwork.NickName}] 4️⃣ 씬의 모든 GameObject 검색 중...");
            GameObject[] allObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            Debug.Log($"[{PhotonNetwork.NickName}] 씬 루트 객체 수: {allObjects.Length}");

            foreach (GameObject rootObj in allObjects)
            {
                Unity.Cinemachine.CinemachineCamera[] cams = rootObj.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(includeInactive: true);
                if (cams.Length > 0)
                {
                    Debug.Log($"[{PhotonNetwork.NickName}] 📍 '{rootObj.name}'에서 {cams.Length}개의 Cinemachine 카메라 발견!");
                    for (int i = 0; i < cams.Length; i++)
                    {
                        Debug.Log($"[{PhotonNetwork.NickName}]   [{i}] {cams[i].gameObject.name} - Active={cams[i].enabled}, Priority={cams[i].Priority}");
                    }
                    vcam = cams[0];
                    break;
                }
            }
        }

        // 4. 카메라 설정
        if (vcam != null)
        {
            // 카메라가 비활성화되어 있으면 활성화
            if (!vcam.enabled)
            {
                Debug.Log($"[{PhotonNetwork.NickName}] ⚠️ 카메라가 비활성화되어 있음. 활성화 중...");
                vcam.enabled = true;
            }

            // 카메라 GameObject도 활성화
            if (!vcam.gameObject.activeSelf)
            {
                Debug.Log($"[{PhotonNetwork.NickName}] ⚠️ 카메라 GameObject가 비활성화되어 있음. 활성화 중...");
                vcam.gameObject.SetActive(true);
            }

            vcam.Follow = this.transform;
            vcam.LookAt = this.transform;
            orbitalRig = vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();

            if (orbitalRig != null)
            {
                Debug.Log($"[{PhotonNetwork.NickName}] CinemachineOrbitalFollow 찾음");
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[{PhotonNetwork.NickName}] ✅ 카메라 '{vcam.gameObject.name}' 설정 완료!");
            Debug.Log($"[{PhotonNetwork.NickName}] Follow: {(vcam.Follow != null ? vcam.Follow.name : "null")}, LookAt: {(vcam.LookAt != null ? vcam.LookAt.name : "null")}");
            Debug.Log($"[{PhotonNetwork.NickName}] ===== 카메라 찾기 완료 =====");
        }
        else
        {
            Debug.LogError($"[{PhotonNetwork.NickName}] ❌ 카메라를 찾을 수 없습니다!");
            Debug.LogError($"[{PhotonNetwork.NickName}] 확인할 사항:");
            Debug.LogError($"  1️⃣ MainCamera GameObject에 'MainCamera' 태그가 있는가?");
            Debug.LogError($"  2️⃣ 그 GameObject에 CinemachineCamera 컴포넌트가 있는가?");
            Debug.LogError($"  3️⃣ 카메라가 활성화(enabled=true)되어 있는가?");
            Debug.LogError($"  4️⃣ 카메라의 Priority가 음수가 아닌가? (현재 권장값: 10)");
            Debug.LogError($"[{PhotonNetwork.NickName}] ===== 카메라 찾기 실패 =====");
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

        // 펀치 애니메이션 재생 중이면 이동 차단
        if (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName("Cross Punch"))
        {
            float normalizedTime = anim.GetCurrentAnimatorStateInfo(0).normalizedTime;

            if (normalizedTime < 1.0f)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                anim.SetFloat("MoveSpeed", 0f);
                HandleCursorUpdate();
                return;
            }
            anim.SetFloat("MoveSpeed", 0f);
        }
        
        HandleCursorUpdate();
        CheckGrounded();

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            photonView.RPC("RPC_PlayJumpAnimation", RpcTarget.All);
            isGrounded = false;
        }

        // 공격 (마우스 좌클릭)
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (myRole == "Seeker" && !isAttacking)
            {
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

        // 🌟 [친구분 코드 병합] 마우스 클릭 시 커서 숨기기 기능
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
            // 🌟 [수정] 게임 씬(CityMapScene)에서 카메라 기반 이동 사용
            if (Camera.main != null && SceneManager.GetActiveScene().name == "CityMapScene")
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

            if (!isAltLooking)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * 360f);
            }

            float currentSpeed = isRunning ? (myRole == "Seeker" ? seekerRunSpeed : survivorRunSpeed) : walkSpeed;
            Vector3 targetVelocity = moveDir * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 12f);

            float animValue = isRunning ? 1.0f : 0.5f;
            if (anim != null) anim.SetFloat("MoveSpeed", animValue);
        }
        else
        {
            // 🔧 [수정] 멈출 때도 부드럽게 처리
            Vector3 stoppedVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, stoppedVelocity, Time.deltaTime * 8f);

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

    // 🌟 [우리가 수정한 부분] 옛날 구식 바닥 감지 OnCollisionEnter는 과감히 삭제하고 Raycast 버전만 남김!
    void CheckGrounded()
    {
        Vector3 rayStartPoint = transform.position + (Vector3.up * 0.1f);
        Debug.DrawRay(rayStartPoint, Vector3.down * rayLength, Color.red);

        if (Physics.Raycast(rayStartPoint, Vector3.down, out RaycastHit hit, rayLength))
        {
            isGrounded = hit.collider.CompareTag("Ground");
            return;
        }

        isGrounded = false;
    }

    // 🌟 [친구분 코드 병합] 애니메이션 관련 RPC
    [PunRPC] void RPC_PlayPunchAnimation() { if (anim != null) anim.SetTrigger("Punch"); } 
    [PunRPC] void RPC_PlayJumpAnimation() { if (anim != null) anim.SetTrigger("Jump"); }
}