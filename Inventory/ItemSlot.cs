using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 아이템 썸네일을 표시하고, 마우스 오버 시 부모 InventoryUI에 아이템 상세 표시·숨김을 알리는 인벤토리 UI 슬롯입니다.
/// </summary>
public class ItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _thumbnailImage;
    private ItemData _currentItem;
    private InventoryUI _inventoryUI;
    
    [SerializeField] private Image _slotImage;           // 슬롯 자체 이미지
    [SerializeField] private Sprite _normalSprite;      // 기본 상태
    [SerializeField] private Sprite _hoverSprite;

    private void Awake()
    {
        if (_thumbnailImage == null)
        {
            // 자기 자신 제외하고 자식에서만 찾기
            foreach (Transform child in transform)
            {
                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    _thumbnailImage = img;
                    break;
                }
            }
        }
        
        if (_slotImage == null)
            _slotImage = GetComponent<Image>();
        
        if (_slotImage != null && _normalSprite != null)
            _slotImage.sprite = _normalSprite;
        
        // Raycast Target 확인
        if (_thumbnailImage != null)
        {
            _thumbnailImage.raycastTarget = true; // 이게 false면 호버 안됨!
            DebugLogger.Log($"Image Raycast Target: {_thumbnailImage.raycastTarget}");
        }
    }

    public void Setup(ItemData item, InventoryUI ui)
    {
        _currentItem = item;
        _inventoryUI = ui;
        
        if (_thumbnailImage != null && item != null && item.thumbnail != null)
        {
            _thumbnailImage.sprite = item.thumbnail;
            _thumbnailImage.enabled = true;
        }
        
        DebugLogger.Log($"ItemSlot Setup 완료 - Item: {item?.name}, UI: {ui != null}");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_slotImage != null && _hoverSprite != null)
            _slotImage.sprite = _hoverSprite;
        
        if (_currentItem != null && _inventoryUI != null)
        {
            _inventoryUI.ShowItemInfo(_currentItem);
        }
        else
        {
            DebugLogger.LogWarning($"Item: {_currentItem != null}, InventoryUI: {_inventoryUI != null}");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_slotImage != null && _normalSprite != null)
            _slotImage.sprite = _normalSprite;
        
        if (_inventoryUI != null)
        {
            _inventoryUI.ClearItemInfo();
        }
    }
}