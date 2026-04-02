using UnityEngine;
using UnityEngine.AI;

public class RandomRoam : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;

    public float roamRadius = 10f;
    public float waitTime = 2f;

    // [추가★] 멍청함 방지용 변수들
    public float maxWalkTime = 5f; // 5초 이상 걷고 있으면 벽에 낀 걸로 간주!
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
        timer += Time.deltaTime;

        // [추가★] AI가 걷고 있는 시간 측정
        if (agent.velocity.magnitude > 0.1f)
        {
            currentWalkTime += Time.deltaTime;
        }

        // 1. 정상적으로 도착했거나 OR 2. 벽에 껴서 5초(maxWalkTime) 이상 헛걸음 쳤을 때!
        if ((agent.remainingDistance <= agent.stoppingDistance && timer >= waitTime) || currentWalkTime >= maxWalkTime)
        {
            Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1))
            {
                agent.SetDestination(hit.position);

                // 타이머 싹 다 초기화!
                timer = 0f;
                currentWalkTime = 0f;
            }
        }

        // 애니메이션 재생 로직 (기존과 동일)
        if (anim != null)
        {
            float currentSpeed = agent.velocity.magnitude;
            float animValue = currentSpeed > 0.1f ? 0.5f : 0f;
            anim.SetFloat("MoveSpeed", animValue);
        }
    }
}