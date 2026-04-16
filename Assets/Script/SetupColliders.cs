using UnityEngine;

public class SetupColliders : MonoBehaviour
{
    // Car 태그 오브젝트에 MeshCollider를 일괄 추가

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
                Debug.Log($"{car.name}: MeshCollider가 이미 존재함");
                continue;
            }

            // Mesh Collider 추가
            MeshCollider meshCollider = car.AddComponent<MeshCollider>();
            
            // 설정
            meshCollider.convex = false;  // 복잡한 형태 지원
            meshCollider.isTrigger = false; // 물리 충돌 활성화

            Debug.Log($"{car.name}: MeshCollider 추가 완료");
        }

        // 필요 시 실행 후 스크립트 제거
        // Destroy(this);
    }
}
