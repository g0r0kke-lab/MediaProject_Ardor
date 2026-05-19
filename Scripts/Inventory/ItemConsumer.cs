using UnityEngine;

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
