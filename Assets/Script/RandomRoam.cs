using UnityEngine;
using UnityEngine.AI;
using Photon.Pun; // 🌟 [추가] 포톤(멀티) 기능을 쓰기 위해 필수!

// 🌟 [수정] 그냥 MonoBehaviour가 아니라 'MonoBehaviourPun'으로 변경!
public class RandomRoam : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Animator anim;

    // 🌟 [수정] 반경 기본값을 맵 크기만큼 엄청 키워둡니다! (인스펙터에서 수정 가능)
    public float roamRadius = 30f;
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
        if (!PhotonNetwork.IsMasterClient) return;

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