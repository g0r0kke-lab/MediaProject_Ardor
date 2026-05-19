using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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