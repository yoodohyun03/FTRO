using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class RandomSkin : MonoBehaviourPun
{
    [System.Serializable]
    public class MapSkinSet
    {
        public string sceneName;
        public GameObject[] characterModels;
    }

    [Header("맵 이름별 스킨 목록")]
    public MapSkinSet[] mapSkinSets;

    [Header("위 목록에 없는 씬에서 쓸 기본 스킨")]
    public GameObject[] defaultModels;

    [Header("★ 애니메이션이 정상 작동하는 기준 캐릭터 (보통 배열 첫 번째)")]
    [Tooltip("이 캐릭터의 뼈대를 기준으로 다른 캐릭터 메시를 리타게팅합니다.")]
    public GameObject skeletonSource;

    // 기준 뼈대 맵 (이름 → Transform)
    private Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

    void Start()
    {
        HideAllModels();
        BuildBoneMap();

        if (!photonView.IsMine) return;

        GameObject[] models = GetModelsForCurrentScene();

        // 현재 씬 스킨도 없고 기본 스킨도 없으면 skeletonSource라도 표시
        if (models == null || models.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] RandomSkin: 현재 씬에 맞는 스킨이 없음 — skeletonSource로 대체합니다.");
            photonView.RPC("RPC_ShowSkeletonSource", RpcTarget.AllBuffered);
            return;
        }

        int randomIndex = Random.Range(0, models.Length);
        int globalIndex = GetGlobalIndex(models[randomIndex]);
        photonView.RPC("SyncCharacterSkin", RpcTarget.AllBuffered, globalIndex);
    }

    [PunRPC]
    public void RPC_ShowSkeletonSource()
    {
        HideAllModels();
        BuildBoneMap();
        if (skeletonSource == null) return;

        foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.enabled = true;

        Animator parentAnim = GetComponent<Animator>();
        if (parentAnim != null)
        {
            parentAnim.enabled = true;
            StartCoroutine(RestoreParamsNextFrame(parentAnim));
        }
    }

    void HideAllModels()
    {
        foreach (var set in mapSkinSets)
            foreach (var m in set.characterModels)
                if (m != null) m.SetActive(false);

        foreach (var m in defaultModels)
            if (m != null) m.SetActive(false);
    }

    // 기준 스켈레톤의 모든 뼈 이름 → Transform 매핑
    void BuildBoneMap()
    {
        boneMap.Clear();
        if (skeletonSource == null) return;

        // 기준 캐릭터는 메시만 숨기고 뼈대는 항상 활성 유지
        skeletonSource.SetActive(true);
        foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.enabled = false;

        foreach (Transform t in skeletonSource.GetComponentsInChildren<Transform>(true))
        {
            if (!boneMap.ContainsKey(t.name))
                boneMap[t.name] = t;
        }
    }

    // 선택된 캐릭터의 SkinnedMeshRenderer 뼈를 기준 스켈레톤에 리매핑
    void RemapBones(GameObject target)
    {
        if (boneMap.Count == 0) return;

        foreach (var smr in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] newBones = new Transform[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] != null && boneMap.TryGetValue(smr.bones[i].name, out Transform mapped))
                    newBones[i] = mapped;
                else
                    newBones[i] = smr.bones[i];
            }
            smr.bones = newBones;

            if (smr.rootBone != null && boneMap.TryGetValue(smr.rootBone.name, out Transform mappedRoot))
                smr.rootBone = mappedRoot;
        }
    }

    [PunRPC]
    public void SyncCharacterSkin(int globalIndex)
    {
        HideAllModels();
        BuildBoneMap();

        GameObject selected = GetModelByGlobalIndex(globalIndex);
        if (selected == null)
        {
            Debug.LogError($"[{gameObject.name}] 잘못된 스킨 인덱스: {globalIndex}");
            return;
        }

        // skeletonSource와 동일한 캐릭터면 그냥 메시만 보여줌
        if (selected == skeletonSource)
        {
            foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.enabled = true;
        }
        else
        {
            // 다른 캐릭터: 활성화 후 뼈를 기준 스켈레톤에 리매핑
            selected.SetActive(true);
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;
            selected.transform.localScale = Vector3.one;

            RemapBones(selected);

            // 선택된 캐릭터의 자식 Animator 비활성화 (부모 Animator가 제어)
            var childAnim = selected.GetComponent<Animator>();
            if (childAnim != null) childAnim.enabled = false;
        }

        Animator parentAnim = GetComponent<Animator>();
        if (parentAnim != null)
        {
            parentAnim.enabled = true;
            StartCoroutine(RestoreParamsNextFrame(parentAnim));
        }

        Debug.Log($"[{gameObject.name}] 스킨 적용: {selected.name} (index {globalIndex})");
    }

    IEnumerator RestoreParamsNextFrame(Animator a)
    {
        yield return null;
        if (a == null) yield break;
        a.SetBool("IsControl", true);
        a.SetBool("IsDead", false);
        a.SetBool("IsFalling", false);
        a.SetBool("IsJump", false);
    }

    // ── 유틸리티 ───────────────────────────────────────

    GameObject[] GetModelsForCurrentScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var set in mapSkinSets)
            if (set.sceneName == scene && set.characterModels.Length > 0)
                return set.characterModels;
        return defaultModels;
    }

    int GetGlobalIndex(GameObject target)
    {
        int offset = 0;
        foreach (var set in mapSkinSets)
        {
            for (int i = 0; i < set.characterModels.Length; i++)
                if (set.characterModels[i] == target) return offset + i;
            offset += set.characterModels.Length;
        }
        for (int i = 0; i < defaultModels.Length; i++)
            if (defaultModels[i] == target) return offset + i;
        return 0;
    }

    GameObject GetModelByGlobalIndex(int globalIndex)
    {
        int offset = 0;
        foreach (var set in mapSkinSets)
        {
            if (globalIndex < offset + set.characterModels.Length)
                return set.characterModels[globalIndex - offset];
            offset += set.characterModels.Length;
        }
        int di = globalIndex - offset;
        if (di >= 0 && di < defaultModels.Length) return defaultModels[di];
        return null;
    }
}
