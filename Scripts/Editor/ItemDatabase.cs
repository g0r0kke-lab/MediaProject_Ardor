using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> allItems = new List<ItemData>();

    public ItemData GetItemByName(string name)
    {
        // 로컬라이제이션 사용 시, Table Key로 검색하는 게 더 안전함
        return allItems.Find(item => item.itemNameKey.TableReference == name);
    }
}