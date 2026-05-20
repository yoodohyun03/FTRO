using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RandomSkin : MonoBehaviourPunCallbacks
{
    private string SkinKey => "S_" + photonView.ViewID;

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

    [Header("★ 애니메이션 기준 캐릭터")]
    public GameObject skeletonSource;

    private Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
    private int chosenSkinIndex = -1;

    void Start()
    {
        BuildBoneMap();
        ShowSkeletonSourceLocal();

        if (photonView.IsMine)
        {
            // 내 스킨 선택
            GameObject[] models = GetModelsForCurrentScene();
            if (models != null && models.Length > 0)
                chosenSkinIndex = GetGlobalIndex(models[Random.Range(0, models.Length)]);
            else
                chosenSkinIndex = -1;

            // 즉시 로컬 적용
            ApplySkinByIndex(chosenSkinIndex);

            // CustomProperties로 저장 → 모든 클라이언트에 자동 동기화
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { SkinKey, chosenSkinIndex } });
        }
        else
        {
            // 상대방이 이미 프로퍼티를 설정했다면 즉시 적용
            TryApplySkinFromOwner();
        }
    }

    // 상대 플레이어의 CustomProperties에서 스킨 읽어서 적용
    void TryApplySkinFromOwner()
    {
        if (photonView.Owner == null) return;
        if (photonView.Owner.CustomProperties.TryGetValue(SkinKey, out object val))
            ApplySkinByIndex((int)val);
    }

    // CustomProperties가 바뀌면 해당 플레이어의 캐릭터에 스킨 적용
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(SkinKey)) return;
        if (photonView.Owner != targetPlayer) return;

        ApplySkinByIndex((int)changedProps[SkinKey]);
    }

    void ApplySkinByIndex(int index)
    {
        if (index < 0)
            RPC_ShowSkeletonSource();
        else
            SyncCharacterSkin(index);
    }

    // ── 스킨 적용 ───────────────────────────────────────

    void ShowSkeletonSourceLocal()
    {
        foreach (var set in mapSkinSets)
            foreach (var m in set.characterModels)
                if (m != null && m != skeletonSource) m.SetActive(false);
        foreach (var m in defaultModels)
            if (m != null && m != skeletonSource) m.SetActive(false);

        if (skeletonSource == null) return;
        skeletonSource.SetActive(true);
        foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.enabled = true;
    }

    [PunRPC]
    public void RPC_ShowSkeletonSource()
    {
        HideAllModels();
        BuildBoneMap();
        if (skeletonSource == null) return;
        foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.enabled = true;
        RefreshAnimator();
    }

    [PunRPC]
    public void SyncCharacterSkin(int globalIndex)
    {
        HideAllModels();
        BuildBoneMap();

        GameObject selected = GetModelByGlobalIndex(globalIndex);
        if (selected == null)
        {
            if (skeletonSource != null)
                foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
                    smr.enabled = true;
            return;
        }

        if (selected == skeletonSource)
        {
            foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.enabled = true;
        }
        else
        {
            selected.SetActive(true);
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;
            selected.transform.localScale = Vector3.one;
            RemapBones(selected);
            var childAnim = selected.GetComponent<Animator>();
            if (childAnim != null) childAnim.enabled = false;
        }

        RefreshAnimator();
    }

    void RefreshAnimator()
    {
        Animator a = GetComponent<Animator>();
        if (a == null) return;
        a.enabled = true;
        StartCoroutine(RestoreParamsNextFrame(a));
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

    // ── 뼈대 유틸 ───────────────────────────────────────

    void HideAllModels()
    {
        foreach (var set in mapSkinSets)
            foreach (var m in set.characterModels)
                if (m != null) m.SetActive(false);
        foreach (var m in defaultModels)
            if (m != null) m.SetActive(false);
    }

    void BuildBoneMap()
    {
        boneMap.Clear();
        if (skeletonSource == null) return;
        skeletonSource.SetActive(true);
        foreach (var smr in skeletonSource.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.enabled = false;
        foreach (Transform t in skeletonSource.GetComponentsInChildren<Transform>(true))
            if (!boneMap.ContainsKey(t.name)) boneMap[t.name] = t;
    }

    void RemapBones(GameObject target)
    {
        if (boneMap.Count == 0) return;
        foreach (var smr in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] nb = new Transform[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
                nb[i] = (smr.bones[i] != null && boneMap.TryGetValue(smr.bones[i].name, out var m)) ? m : smr.bones[i];
            smr.bones = nb;
            if (smr.rootBone != null && boneMap.TryGetValue(smr.rootBone.name, out var mr))
                smr.rootBone = mr;
        }
    }

    // ── 씬/인덱스 유틸 ───────────────────────────────────

    GameObject[] GetModelsForCurrentScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var set in mapSkinSets)
            if (set.sceneName == scene && set.characterModels != null && set.characterModels.Length > 0)
                return set.characterModels;
        if (defaultModels != null && defaultModels.Length > 0)
            return defaultModels;
        foreach (var set in mapSkinSets)
            if (set.characterModels != null && set.characterModels.Length > 0)
                return set.characterModels;
        return null;
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
