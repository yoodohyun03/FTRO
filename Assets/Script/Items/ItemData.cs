using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    [TextArea(1, 3)] public string description;
    public Sprite icon;
    public float cooldown = 10f;
    public float duration = 5f;
    public ItemType itemType;
}

public enum ItemType
{
    SeekerFreeze,
    SeekerSwarm
}

public enum SurvivorItemType
{
    Sprint    = 0,  // 스프린트 부스터 [일반]
    Smoke     = 1,  // 연막탄         [일반]
    MarkerJam = 2,  // 마커 교란기    [일반]
    EMP       = 3,  // EMP            [희귀]
    Hack      = 4,  // 해킹 툴        [희귀]
    Decoy     = 5   // 디코이         [희귀]
}
