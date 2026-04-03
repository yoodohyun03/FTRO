using UnityEngine;
using UnityEngine.AI;
using Photon.Pun; // 🌟 [추가] 포톤(멀티) 기능을 쓰기 위해 필수!

// 🌟 [수정] 그냥 MonoBehaviour가 아니라 'MonoBehaviourPun'으로 변경!
public class RandomRoam : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Animator anim;

    // 🌟 [수정] 반경 기본값을 맵 크기만큼 엄청 키워둡니다! (인스펙터에서 수정 가능)
    public float roamRadius = 150f;
    public float waitTime = 2f;

    public float maxWalkTime = 6f;
    private float currentWalkTime = 0f;
    private float timer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        timer = waitTime;
    }

    void Update()
    {
        // 🌟 [핵심] 방장(마스터 클라이언트)이 아니면? 뇌를 꺼버립니다! (위치만 받아먹음)
        if (!PhotonNetwork.IsMasterClient) return;

        timer += Time.deltaTime;

        if (agent.velocity.magnitude > 0.1f)
        {
            currentWalkTime += Time.deltaTime;
        }

        if ((agent.remainingDistance <= agent.stoppingDistance && timer >= waitTime) || currentWalkTime >= maxWalkTime)
        {
            Vector3 randomDirection = Random.insideUnitSphere * roamRadius;

            // 🌟 [수정] 내 위치(transform.position)가 아니라, 맵의 정중앙(Vector3.zero)을 기준으로!
            randomDirection += Vector3.zero;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1))
            {
                agent.SetDestination(hit.position);

                timer = 0f;
                currentWalkTime = 0f;
            }
        }

        if (anim != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            float animValue = currentSpeed > 0.1f ? 0.5f : 0f;
            anim.SetFloat("MoveSpeed", animValue);
        }
    }
}