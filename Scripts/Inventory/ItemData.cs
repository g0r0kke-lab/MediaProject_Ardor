using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public Sprite thumbnail;
    
    // 로컬라이제이션 키
    public LocalizedString itemNameKey;
    public LocalizedString itemDescriptionKey;
}