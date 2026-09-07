
using UnityEngine;

/// <summary>
/// 플레이어의 인벤토리에 지정된 ItemData 에셋이 포함되어 있을 때 true를 반환하는 트리거 조건입니다.
/// </summary>
public class InventoryCondition : TriggerConditionHandler
{
    public ItemData requiredItem; // 인스펙터에서 직접 드래그

    public override bool Evaluate()
    {
        var items = SaveDataManager.Instance.GetInventoryItems();
        return items.Contains(requiredItem);
    }
}