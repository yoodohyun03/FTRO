using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

public class RandomRoam : MonoBehaviourPun
{
    public enum AIState { Normal, Frozen, Swarming }
    private AIState currentState = AIState.Normal;
    private float stateTimer = 0f;
    private Transform currentSwarmTarget;

    private NavMeshAgent agent;
    [HideInInspector] public Animator anim;
    private Rigidbody rb;
    private bool isHitStunned = false;

    public float roamRadius = 30f;
    public float waitTime = 2f;
    public float maxWalkTime = 6f;

    // AI 이동 속도
    public float walkSpeed = 3.8f;
    public float runSpeed = 6f;
    public float runChance = 0.3f;

    // AI 점프 설정
    public float jumpPower = 5f;
    public float jumpChance = 0.15f;
    public float rayLength = 0.3f;

    // NavMeshAgent 설정
    public float agentRadius = 0.35f;
    public float agentHeight = 1.8f;

    private float currentWalkTime = 0f;
    private float timer;
    private bool isRunning = false;
    private bool isGrounded = true;
    private const float moveThreshold = 0.05f;
    private Vector3 lastPosition;
    private ObjectivePoint[] cachedObjectives;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        cachedObjectives = Object.FindObjectsByType<ObjectivePoint>(FindObjectsSortMode.None);

        // 달리기/점프 비활성화
        runChance = 0f;
        jumpChance = 0f;
        runSpeed = walkSpeed;
        isRunning = false;

        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.SetFloat("IsControl", 1f);
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // NavMeshAgent 회전/가속도 설정
        if (agent != null)
        {
            agent.angularSpeed = 180f;
            agent.acceleration = 8f;
            agent.speed = walkSpeed;

            // NavMeshAgent 크기 설정
            agent.radius = agentRadius;
            agent.height = agentHeight;
            agent.baseOffset = 0f; // 발바닥 기준

            // 땅 파고들기 방지: 소환 시점에 NavMesh 위에 정확히 있는지 확인
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 5f, NavMesh.AllAreas))
            {
                // 현재 위치와 NavMesh 위치 차이가 너무 크면(땅 밑이면) 워프
                if (Mathf.Abs(transform.position.y - startHit.position.y) > 0.5f)
                {
                    agent.Warp(startHit.position);
                }
            }
        }

        timer = waitTime;
        lastPosition = transform.position;
    }

    void Update()
    {
        if (agent == null) return;
        if (!agent.isOnNavMesh) return;

        // 상태 타이머 관리
        if (currentState != AIState.Normal)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                currentState = AIState.Normal;
                if (agent.isOnNavMesh) agent.isStopped = false;
                currentSwarmTarget = null;
            }
        }

        // 상태별 로직 처리
        if (currentState == AIState.Frozen)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            UpdateAnimation(0f, false);
            return;
        }
        else if (currentState == AIState.Swarming && currentSwarmTarget != null)
        {
            // Only the Master Client should decide where to go to avoid jitter
            if (PhotonNetwork.IsMasterClient)
            {
                timer += Time.deltaTime;
                // Update destination every 0.5 seconds to track moving target
                if (timer >= 0.5f)
                {
                    if (agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                        agent.SetDestination(currentSwarmTarget.position);
                        agent.speed = runSpeed;
                    }
                    timer = 0f;
                }
            }
            
            UpdateAnimation(1.0f, true);
            return;
        }

        if (isHitStunned) return;
        if (agent.isOnNavMesh && agent.isStopped) agent.isStopped = false;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit recoverHit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(recoverHit.position);
            }

            return;
        }

        // 땅에 닿았는지 확인
CheckGrounded();

        // 마스터만 AI 목적지 설정
        if (PhotonNetwork.IsMasterClient)
        {
            timer += Time.deltaTime;

            if (agent.velocity.magnitude > 0.1f)
            {
                currentWalkTime += Time.deltaTime;
            }

            if (!agent.pathPending &&
                ((agent.remainingDistance <= agent.stoppingDistance && timer >= waitTime) || currentWalkTime >= maxWalkTime))
            {
                Vector3 targetPos;

                // 25% chance to head towards an objective terminal to distract the seeker
                if (cachedObjectives != null && cachedObjectives.Length > 0 && Random.value < 0.25f)
                {
                    targetPos = cachedObjectives[Random.Range(0, cachedObjectives.Length)].transform.position;
                    // Add slight random offset so they don't all stack on one point
                    targetPos += Random.insideUnitSphere * 2f;
                    targetPos.y = transform.position.y;
                }
                else
                {
                    Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
                    randomDirection += transform.position;
                    targetPos = randomDirection;
                }

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, roamRadius, NavMesh.AllAreas))
                {
                    // 목적지 동기화
                    photonView.RPC("RPC_SetDestination", RpcTarget.AllBuffered, hit.position);

                    // 이동 속도 동기화
                    bool shouldRun = Random.value < runChance;
                    photonView.RPC("RPC_SetSpeed", RpcTarget.AllBuffered, shouldRun);

                    // 점프 동기화
                    if (Random.value < jumpChance && isGrounded)
                    {
                        photonView.RPC("RPC_Jump", RpcTarget.AllBuffered);
                    }

                    timer = 0f;
                    currentWalkTime = 0f;
                }
            }
        }

        // 모든 클라이언트에서 애니메이션 업데이트
        float currentSpeed = Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
        bool hasMoveIntent = !agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.1f);
        float positionDelta = Vector3.Distance(transform.position, lastPosition);
        bool movedByTransform = positionDelta > 0.0015f;
        bool isMoving = currentSpeed >= moveThreshold || hasMoveIntent || movedByTransform;

        float magnitude = 0f;
        if (isMoving) magnitude = isRunning ? 1.0f : 0.5f;

        UpdateAnimation(magnitude, isRunning);

        lastPosition = transform.position;
    }

    void CheckGrounded()
    {
        if (jumpChance <= 0f)
        {
            isGrounded = true;
            return;
        }

        if (rb == null) return;

        // Raycast로 바닥 감지
        isGrounded = Physics.Raycast(transform.position, Vector3.down, rayLength);
    }

    private void UpdateAnimation(float magnitude, bool running)
    {
        if (anim == null) return;

        // Use new PlayerAnim parameters
        anim.SetBool("IsGrounded", isGrounded);
        
        if (magnitude > 0.05f)
        {
            Vector3 localVel = transform.InverseTransformDirection(agent.velocity.normalized);
            float speedFactor = running ? 1.1f : 0.5f;
            
            float targetH = localVel.x * speedFactor;
            float targetV = localVel.z * speedFactor;

            float curH = anim.GetFloat("Horizontal");
            float curV = anim.GetFloat("Vertical");
            float lerpSpeed = Time.deltaTime * 8f;

            anim.SetFloat("Horizontal", Mathf.Lerp(curH, targetH, lerpSpeed));
            anim.SetFloat("Vertical", Mathf.Lerp(curV, targetV, lerpSpeed));
            anim.SetFloat("MoveSpeed", Mathf.Max(Mathf.Abs(targetH), Mathf.Abs(targetV)));
        }
        else
        {
            float curH = anim.GetFloat("Horizontal");
            float curV = anim.GetFloat("Vertical");
            float lerpSpeed = Time.deltaTime * 8f;

            anim.SetFloat("Horizontal", Mathf.Lerp(curH, 0f, lerpSpeed));
            anim.SetFloat("Vertical", Mathf.Lerp(curV, 0f, lerpSpeed));
            anim.SetFloat("MoveSpeed", 0f);
        }

        // Falling/Jump states for AI (simpler than player but keep consistent)
        if (!isGrounded)
        {
            float yVel = (agent != null) ? agent.velocity.y : 0f; 
            // Note: NavMeshAgent velocity.y is usually 0, but if we have a Rigidbody...
            anim.SetBool("IsJump", yVel > 0.1f);
            anim.SetBool("IsFalling", yVel <= 0.1f);
        }
        else
        {
            anim.SetBool("IsJump", false);
            anim.SetBool("IsFalling", false);
        }
    }

    [PunRPC]
    public void RPC_SetAIState(int state, int targetViewID, float duration)
    {
        currentState = (AIState)state;
        stateTimer = duration;

        if (currentState == AIState.Swarming)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null) currentSwarmTarget = targetView.transform;
        }
        else if (currentState == AIState.Frozen)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }
        else
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            currentSwarmTarget = null;
        }
    }

    public void UpdateAnimator(Animator newAnim)
{
        anim = newAnim;
    }

    [PunRPC]
    void RPC_SetDestination(Vector3 destination)
    {
        if (agent != null)
        {
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit recoverHit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(recoverHit.position);
            }

            agent.SetDestination(destination);
        }
    }

    [PunRPC]
    void RPC_SetSpeed(bool running)
    {
        isRunning = running;
        if (agent != null)
        {
            agent.speed = running ? runSpeed : walkSpeed;
        }
    }

    [PunRPC]
    void RPC_PlayHitAnimation()
    {
        if (anim != null)
            anim.CrossFadeInFixedTime("Idle_Hit_Strong_Left", 0.05f);
        StartCoroutine(HitStunRoutine());
    }

    IEnumerator HitStunRoutine()
    {
        isHitStunned = true;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        yield return new WaitForSeconds(1.2f); // Idle_Hit_Strong_Left 길이에 맞게 조절

        isHitStunned = false;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        if (anim != null)
            anim.CrossFadeInFixedTime("Grounded", 0.2f);
    }

    [PunRPC]
    void RPC_Jump()
    {
        // 모든 클라이언트에서 점프 동기화
        if (rb != null && isGrounded)
        {
            if (anim != null)
            {
                anim.SetTrigger("Jump");
            }

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

            isGrounded = false;
        }
    }
}