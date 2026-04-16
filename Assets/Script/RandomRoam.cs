using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

public class RandomRoam : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Animator anim;
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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // 달리기/점프 비활성화
        runChance = 0f;
        jumpChance = 0f;
        runSpeed = walkSpeed;
        isRunning = false;

        if (anim != null)
        {
            anim.applyRootMotion = false;
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
                Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
                randomDirection += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, NavMesh.AllAreas))
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
            float animValue = 0f;

            if (currentSpeed < moveThreshold && !hasMoveIntent && !movedByTransform)
            {
                animValue = 0f;
            }
            else if (isRunning)
            {
                animValue = 1.0f;
            }
            else
            {
                animValue = 0.5f;
            }

            anim.SetFloat("MoveSpeed", animValue);
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