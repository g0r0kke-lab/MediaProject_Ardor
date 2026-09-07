using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using TMPro;

/// <summary>
/// 플레이어 인벤토리를 아이템 슬롯 행으로 렌더링하고, 마우스 오버 시 지역화된 이름과 설명 텍스트를 표시합니다.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("인벤토리 슬롯")]
    public GameObject itemSlotPrefab;
    public Transform itemSlotParent; // Horizontal Layout Group이 있는 Transform

    [Header("아이템 정보 표시")]
    public LocalizeStringEvent itemNameText; // 아이템 이름 표시용 (로컬라이제이션)
    public LocalizeStringEvent itemDescriptionText; // 아이템 설명 표시용 (로컬라이제이션)
    
    private TMP_Text _nameTextComponent;
    private TMP_Text _descriptionTextComponent;
    private GameObject _inventoryPanel;

    void Start()
    {
        // 실제 텍스트 컴포넌트 참조 가져오기
        if (itemNameText != null)
        {
            _nameTextComponent = itemNameText.GetComponent<TMP_Text>();
        }
        
        if (itemDescriptionText != null)
        {
            _descriptionTextComponent = itemDescriptionText.GetComponent<TMP_Text>();
        }
    }

    public void UpdateUI()
    {
        // 기존 슬롯 제거
        foreach (Transform child in itemSlotParent)
        {
            Destroy(child.gameObject);
        }

        // SaveDataManager에서 인벤토리 아이템 가져오기
        List<ItemData> items = SaveDataManager.Instance.GetInventoryItems();
    
        // 각 아이템에 대해 슬롯 생성
        foreach (ItemData item in items)
        {
            GameObject slot = Instantiate(itemSlotPrefab, itemSlotParent);
            ItemSlot itemSlot = slot.GetComponent<ItemSlot>();
            
            if (itemSlot != null)
            {
                itemSlot.Setup(item, this);
            }
        }
        
        // 초기 상태: 정보 숨김
        ClearItemInfo();
    }

    // 아이템 정보 표시 (호버 시)
    public void ShowItemInfo(ItemData item)
    {
        if (item == null) return;
    
        // 원본 ItemData이므로 LocalizedString이 정상 작동
        if (itemNameText != null && !item.itemNameKey.IsEmpty)
        {
            itemNameText.StringReference = item.itemNameKey;
            itemNameText.RefreshString();
        }
    
        if (itemDescriptionText != null && !item.itemDescriptionKey.IsEmpty)
        {
            itemDescriptionText.StringReference = item.itemDescriptionKey;
            itemDescriptionText.RefreshString();
        }
    }

    // 아이템 정보 숨김 (호버 해제 시)
    public void ClearItemInfo()
    {
        if (_nameTextComponent != null)
        {
            _nameTextComponent.text = "";
        }
        
        if (_descriptionTextComponent != null)
        {
            _descriptionTextComponent.text = "";
        }
    }
}