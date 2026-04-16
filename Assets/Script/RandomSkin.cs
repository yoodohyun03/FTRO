using UnityEngine;
using Photon.Pun;

public class RandomSkin : MonoBehaviourPun
{
    [Header("여기에 자식으로 넣은 모델링들을 드래그해서 넣으세요")]
    public GameObject[] characterModels;

    void Start()
    {
        // 오브젝트 소유자만 스킨 인덱스 결정
        if (photonView.IsMine)
        {
            int randomIndex;

            // 이름으로 플레이어/AI 구분
            if (gameObject.name.Contains("male01_1"))
            {
                randomIndex = Random.Range(0, characterModels.Length);
                // 플레이어 스킨 정보를 저장하여 AI가 동일 스킨 사용
                PlayerPrefs.SetInt("PlayerSkinIndex", randomIndex);
                Debug.Log($"플레이어 스킨 저장: {randomIndex}");
            }
            else
            {
                // AI는 저장된 플레이어 스킨 인덱스 사용
                randomIndex = PlayerPrefs.GetInt("PlayerSkinIndex", 0);
                Debug.Log($"AI 스킨 동기화: {randomIndex}");
            }

            // 현재/후속 참가자까지 동일 스킨 동기화
            photonView.RPC("SyncCharacterSkin", RpcTarget.AllBuffered, randomIndex);
        }
    }

    [PunRPC]
    public void SyncCharacterSkin(int skinIndex)
    {
        // 모든 모델 비활성화
        foreach (GameObject model in characterModels)
        {
            model.SetActive(false);
        }

        // 선택된 모델만 활성화
        if (skinIndex >= 0 && skinIndex < characterModels.Length)
        {
            GameObject selectedModel = characterModels[skinIndex];
            selectedModel.SetActive(true);

            Animator parentAnim = GetComponent<Animator>();
            Animator childAnim = selectedModel.GetComponent<Animator>();

            if (parentAnim != null && childAnim != null)
            {
                // 부모 Animator에 선택 모델 Avatar 적용
                parentAnim.avatar = childAnim.avatar;

                // Avatar 변경 반영
                parentAnim.Rebind();

                // 자식 Animator 비활성화
                childAnim.enabled = false;
            }
        }
        else
        {
            Debug.LogError($"잘못된 스킨 인덱스: {skinIndex}");
        }
    }
}