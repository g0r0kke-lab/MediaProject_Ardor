using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
/// <summary>
/// 아이템의 썸네일 스프라이트와 지역화된 이름·설명 문자열 참조를 담는 ScriptableObject입니다.
/// </summary>
public class ItemData : ScriptableObject
{
    public Sprite thumbnail;
    
    // 로컬라이제이션 키
    public LocalizedString itemNameKey;
    public LocalizedString itemDescriptionKey;
}