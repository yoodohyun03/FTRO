using UnityEngine;

public class SetupColliders : MonoBehaviour
{
    // 🌟 [설명] 이 스크립트를 씬의 아무 GameObject에 붙이고 플레이하면
    // "Car" 태그가 있는 모든 오브젝트에 Mesh Collider를 자동으로 추가합니다!

    void Start()
    {
        // "Car" 태그를 가진 모든 GameObject 찾기
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");

        Debug.Log($"🚗 {cars.Length}개의 차를 찾았습니다. Mesh Collider 추가 중...");

        foreach (GameObject car in cars)
        {
            // 이미 Mesh Collider가 있으면 스킵
            if (car.GetComponent<MeshCollider>() != null)
            {
                Debug.Log($"✅ {car.name}에는 이미 Mesh Collider가 있습니다.");
                continue;
            }

            // Mesh Collider 추가
            MeshCollider meshCollider = car.AddComponent<MeshCollider>();
            
            // 설정
            meshCollider.convex = false;  // 복잡한 형태 지원
            meshCollider.isTrigger = false; // 물리 충돌 활성화

            Debug.Log($"✅ {car.name}에 Mesh Collider를 추가했습니다!");
        }

        // 완료 후 이 스크립트 자신을 삭제 (원하면)
        // Destroy(this);
    }
}
