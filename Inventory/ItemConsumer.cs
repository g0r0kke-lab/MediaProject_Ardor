using UnityEngine;

/// <summary>
/// ConsumeItem 호출 시 지정된 ItemData를 플레이어 인벤토리에서 제거하며, 주로 UnityEvent로 트리거됩니다.
/// </summary>
public class ItemConsumer : MonoBehaviour
{
    // 인스펙터에서 제거할 아이템 지정
    [SerializeField] private ItemData itemToConsume;

    // UnityEvent나 다른 트리거에서 호출
    public void ConsumeItem()
    {
        if (itemToConsume == null) return;

        if (SaveDataManager.Instance) SaveDataManager.Instance.RemoveInventoryItem(itemToConsume);
    }
}
