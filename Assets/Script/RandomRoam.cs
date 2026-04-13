using UnityEngine;
using UnityEngine.AI;
using Photon.Pun; // 🌟 [추가] 포톤(멀티) 기능을 쓰기 위해 필수!

// 🌟 [수정] 그냥 MonoBehaviour가 아니라 'MonoBehaviourPun'으로 변경!
public class RandomRoam : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Animator anim;
    private Rigidbody rb;              // 🌟 [추가] 점프를 위한 Rigidbody

    public float roamRadius = 30f;
    public float waitTime = 2f;
    public float maxWalkTime = 6f;

    // 🌟 [수정] AI 속도 다양화
    public float walkSpeed = 3.8f;       // 걷기 속도
    public float runSpeed = 6f;         // 달리기 속도
    public float runChance = 0.3f;      // 달리기 확률 (30%)

    // 🌟 [추가] AI 점프 설정
    public float jumpPower = 5f;        // 점프 힘
    public float jumpChance = 0.15f;    // 점프 확률 (15% - 가끔씩)
    public float rayLength = 0.3f;      // 땅 감지 거리

    // 🌟 [추가] NavMeshAgent 설정
    public float agentRadius = 0.35f;   // 에이전트 반지름 (좁은 길 통과용)
    public float agentHeight = 1.8f;    // 에이전트 높이

    private float currentWalkTime = 0f;
    private float timer;
    private bool isRunning = false;     // 🌟 [추가] 현재 달리는 중인지 추적
    private bool isGrounded = true;     // 🌟 [추가] 땅에 닿았는지 추적
    private const float moveThreshold = 0.05f;
    private Vector3 lastPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();  // 🌟 [추가] Rigidbody 초기화

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

        // 🌟 [추가] NavMeshAgent 회전 및 가속도 설정 (미끄러짐 방지)
        if (agent != null)
        {
            agent.angularSpeed = 180f;      // 회전 속도 (초당 각도) - 빠른 회전
            agent.acceleration = 8f;       // 가속도 - 자연스러운 속도 변화
            agent.speed = walkSpeed;       // 초기 속도 설정

            // 🌟 [추가] NavMeshAgent 크기 설정 (좁은 길 통과용)
            agent.radius = agentRadius;     // 반지름 줄이기
            agent.height = agentHeight;     // 높이 맞추기

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

        // 🌟 [추가] 땅에 닿았는지 확인
        CheckGrounded();

        // 🌟 [핵심] 마스터만 AI 목적지 설정 (네비게이션 이동)
        if (PhotonNetwork.IsMasterClient)
        {
            timer += Time.deltaTime;

            if (agent.velocity.magnitude > 0.1f)
            {
                currentWalkTime += Time.deltaTime;
            }

            if ((agent.remainingDistance <= agent.stoppingDistance && timer >= waitTime) || currentWalkTime >= maxWalkTime)
            {
                // 🌟 [수정] 내 위치를 기준으로 반경 30m 안에서 다음 목적지를 찾습니다!
                Vector3 randomDirection = Random.insideUnitSphere * roamRadius;

                // 🌟 [핵심] Vector3.zero(맵 중앙)가 아니라, transform.position(내 위치)을 더합니다!
                randomDirection += transform.position;

                NavMeshHit hit;
                // 여기서도 너무 멀리서 찾지 말고 근처에서만 찾도록 탐색 거리를 제한합니다.
                if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, NavMesh.AllAreas))
                {
                    // 🌟 [핵심] 목적지를 모든 클라이언트와 동기화! (애니메이션 끊김 방지)
                    photonView.RPC("RPC_SetDestination", RpcTarget.AllBuffered, hit.position);

                    // 🌟 [추가] 랜덤하게 달리기 또는 걷기 선택!
                    bool shouldRun = Random.value < runChance;
                    photonView.RPC("RPC_SetSpeed", RpcTarget.AllBuffered, shouldRun);

                    // 🌟 [추가] 아주 가끔씩 점프!
                    if (Random.value < jumpChance && isGrounded)
                    {
                        photonView.RPC("RPC_Jump", RpcTarget.AllBuffered);
                    }

                    timer = 0f;
                    currentWalkTime = 0f;
                }
            }
        }

        // 🌟 [핵심] 모든 클라이언트가 애니메이션 업데이트! (네트워크 동기화)
        if (anim != null)
        {
            float currentSpeed = Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
            bool hasMoveIntent = agent.hasPath && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.1f);
            float positionDelta = Vector3.Distance(transform.position, lastPosition);
            bool movedByTransform = positionDelta > 0.0015f;
            float animValue = 0f;

            if (currentSpeed < moveThreshold && !hasMoveIntent && !movedByTransform)
            {
                animValue = 0f;  // 멈춤
            }
            else if (isRunning)
            {
                animValue = 1.0f;  // 달리기 (빠른 애니메이션)
            }
            else
            {
                animValue = 0.5f;  // 걷기 (느린 애니메이션)
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

        // 🌟 [추가] Raycast로 땅에 닿았는지 확인
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
        // 🌟 [추가] 모든 클라이언트에서 동기화된 점프!
        if (rb != null && isGrounded)
        {
            // 점프 애니메이션 트리거
            if (anim != null)
            {
                anim.SetTrigger("Jump");
            }

            // Rigidbody에 위쪽 힘 적용
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);  // Y 속도 초기화
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

            isGrounded = false;
        }
    }
}