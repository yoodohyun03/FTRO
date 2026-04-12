using UnityEngine;
using Photon.Pun; // 포톤 사용을 위해 필수

public class RandomSkin : MonoBehaviourPun
{
    [Header("여기에 자식으로 넣은 모델링들을 드래그해서 넣으세요")]
    public GameObject[] characterModels;

    void Start()
    {
        // 내가 생성한 내 캐릭터(혹은 마스터 클라이언트가 생성한 AI)일 때만 랜덤을 돌림
        if (photonView.IsMine)
        {
            int randomIndex;

            // 🌟 [핵심] 이름으로 플레이어/AI 구분!
            if (gameObject.name.Contains("male01_1"))
            {
                randomIndex = Random.Range(0, characterModels.Length);
                // 🌟 [핵심] 플레이어의 스킨 정보를 "글로벌"로 저장해서 AI들이 참고하게 함!
                PlayerPrefs.SetInt("PlayerSkinIndex", randomIndex);
                Debug.Log($"🎨 플레이어 스킨 저장됨: {randomIndex}");
            }
            else
            {
                // 🌟 [수정] AI라면? 플레이어의 스킨을 가져와서 사용!
                randomIndex = PlayerPrefs.GetInt("PlayerSkinIndex", 0); // 기본값은 0
                Debug.Log($"🤖 AI가 플레이어 스킨을 따라함: {randomIndex}");
            }

            // 나뿐만 아니라 이 방에 있는 모두에게(그리고 나중에 들어올 사람에게도) 내 스킨 번호를 쏴줌
            photonView.RPC("SyncCharacterSkin", RpcTarget.AllBuffered, randomIndex);
        }
    }

    [PunRPC]
    public void SyncCharacterSkin(int skinIndex)
    {
        // 1. 일단 모든 옷을 다 벗음
        foreach (GameObject model in characterModels)
        {
            model.SetActive(false);
        }

        // 2. 당첨된 옷만 입음
        if (skinIndex >= 0 && skinIndex < characterModels.Length)
        {
            GameObject selectedModel = characterModels[skinIndex];
            selectedModel.SetActive(true);

            // --- [애니메이션 고장 해결 핵심 파트] ---
            Animator parentAnim = GetComponent<Animator>(); // 부모(내 캐릭터 본체)의 애니메이터
            Animator childAnim = selectedModel.GetComponent<Animator>(); // 자식(새 옷)의 애니메이터

            if (parentAnim != null && childAnim != null)
            {
                // 1. 부모의 뼈대(Avatar)를 새 옷의 뼈대로 교체
                parentAnim.avatar = childAnim.avatar;

                // 2. 유니티한테 "야! 뼈대 바뀌었으니 새로고침 해!" 라고 강제 명령 (이게 없으면 안 움직임)
                parentAnim.Rebind();

                // 3. 자식한테 붙어있는 애니메이터 기능 끄기 (자식이 부모의 조종을 방해하지 못하게 함)
                childAnim.enabled = false;
            }
        }
        else
        {
            Debug.LogError($"❌ 잘못된 스킨 인덱스: {skinIndex}");
        }
    }
}