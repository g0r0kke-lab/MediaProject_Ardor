using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 포인터 진입 시 TextMeshPro 버튼 텍스트를 호버 색상으로 변경하고, 포인터 이탈 시 기본 색상으로 복원합니다.
/// </summary>
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI buttonText;
    public Color hoverColor = new Color32(0x24, 0x24, 0x24, 0xFF);
    public Color normalColor = Color.white;
    
    // Hover 시작
    public void OnPointerEnter(PointerEventData eventData)
    {
        buttonText.color = hoverColor;
    }
    
    // Hover 종료
    public void OnPointerExit(PointerEventData eventData)
    {
        buttonText.color = normalColor;
    }
}