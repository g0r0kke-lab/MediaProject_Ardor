
using UnityEngine;

public class InventoryCondition : TriggerConditionHandler
{
    public ItemData requiredItem; // 인스펙터에서 직접 드래그

    public override bool Evaluate()
    {
        var items = SaveDataManager.Instance.GetInventoryItems();
        return items.Contains(requiredItem);
    }
}