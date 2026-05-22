using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

public class RandomRoam : MonoBehaviourPun
{
    private NavMeshAgent agent;
    [HideInInspector] public Animator anim;
    private Rigidbody rb;

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
            anim.SetBool("IsControl", true);
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

            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(startHit.position);
            }
        }

        timer = waitTime;
        lastPosition = transform.position;
    }

    void Update()
    {
        if (agent == null) return;
        if (!agent.isOnNavMesh) return;

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

            if ((agent.remainingDistance <= agent.stoppingDistance && timer >= waitTime) || currentWalkTime >= maxWalkTime)
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
        if (anim != null)
        {
            float currentSpeed = Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
            bool hasMoveIntent = agent.hasPath && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.1f);
            float positionDelta = Vector3.Distance(transform.position, lastPosition);
            bool movedByTransform = positionDelta > 0.0015f;
            bool isMoving = currentSpeed >= moveThreshold || hasMoveIntent || movedByTransform;

            float magnitude = 0f;
            if (isMoving) magnitude = isRunning ? 1.0f : 0.5f;

            anim.SetFloat("InputMagnitude", magnitude);
            anim.SetBool("Running", isRunning && isMoving);

            if (isMoving)
            {
                Vector3 localVel = transform.InverseTransformDirection(agent.velocity.normalized);
                anim.SetFloat("Vertical", localVel.z);
                anim.SetFloat("Horizontal", localVel.x);
                anim.SetFloat("Z", localVel.z);
                anim.SetFloat("X", localVel.x);
            }
            else
            {
                anim.SetFloat("Vertical", 0f);
                anim.SetFloat("Horizontal", 0f);
                anim.SetFloat("Z", 0f);
                anim.SetFloat("X", 0f);
            }
            anim.SetFloat("SprintFactor", (isRunning && isMoving) ? 1f : 0f);
        }

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