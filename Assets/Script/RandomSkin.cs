using UnityEngine;
using Photon.Pun;

public class RandomSkin : MonoBehaviourPun
{
    [Header("스킨으로 사용할 캐릭터 모델 오브젝트들을 여기에 드래그해서 넣으세요")]
    public GameObject[] characterModels;

    void Start()
    {
        if (characterModels == null || characterModels.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] RandomSkin: characterModels가 비어 있습니다.");
            return;
        }

        foreach (GameObject model in characterModels)
        {
            if (model != null) model.SetActive(false);
        }

        if (photonView.IsMine)
        {
            int randomIndex = Random.Range(0, characterModels.Length);
            photonView.RPC("SyncCharacterSkin", RpcTarget.AllBuffered, randomIndex);
        }
    }

    [PunRPC]
    public void SyncCharacterSkin(int skinIndex)
    {
        if (characterModels == null || skinIndex < 0 || skinIndex >= characterModels.Length)
        {
            Debug.LogError($"[{gameObject.name}] 잘못된 스킨 인덱스: {skinIndex}");
            return;
        }

        foreach (GameObject model in characterModels)
        {
            if (model != null) model.SetActive(false);
        }

        GameObject selected = characterModels[skinIndex];
        selected.SetActive(true);

        // 공중 부양 방지: 로컬 위치/회전 초기화
        selected.transform.localPosition = Vector3.zero;
        selected.transform.localRotation = Quaternion.identity;
        selected.transform.localScale = Vector3.one;

        Animator parentAnim = GetComponent<Animator>();
        Animator childAnim = selected.GetComponent<Animator>();

        if (parentAnim == null || childAnim == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Animator를 찾을 수 없습니다.");
            return;
        }

        bool avatarSwapped = false;

        // Humanoid 리그인 경우 Avatar 스왑
        if (childAnim.avatar != null && childAnim.avatar.isHuman && parentAnim.avatar != null && parentAnim.avatar.isHuman)
        {
            parentAnim.avatar = childAnim.avatar;
            parentAnim.Rebind();
            parentAnim.enabled = true;
            childAnim.enabled = false;
            avatarSwapped = true;
            Debug.Log($"[{gameObject.name}] Humanoid Avatar 스왑 완료: {selected.name}");
        }

        // Generic 리그이거나 Avatar 스왑 실패 → 자식 Animator에 부모 Controller 적용
        if (!avatarSwapped)
        {
            RuntimeAnimatorController controller = parentAnim.runtimeAnimatorController;
            parentAnim.enabled = false;
            childAnim.runtimeAnimatorController = controller;
            childAnim.enabled = true;

            // PlayerMove / RandomRoam의 anim 참조를 자식 Animator로 교체
            PlayerMove pm = GetComponent<PlayerMove>();
            if (pm != null) pm.anim = childAnim;

            RandomRoam rr = GetComponent<RandomRoam>();
            if (rr != null) rr.UpdateAnimator(childAnim);

            Debug.Log($"[{gameObject.name}] Generic 리그 — 자식 Animator로 전환: {selected.name}");
        }

        Debug.Log($"[{gameObject.name}] 스킨 적용 완료: {selected.name} (index {skinIndex})");
    }
}
