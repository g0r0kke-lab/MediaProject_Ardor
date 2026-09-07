using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>UI 요소의 클릭, 포인터 이벤트를 디버깅하기 위한 컴포넌트</summary>
public class UIClickDebugger : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private void Start()
    {
        DebugLogger.Log($"[UIClickDebugger] {gameObject.name} 초기화됨");
        
        // Button 컴포넌트 확인
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            DebugLogger.Log($"[UIClickDebugger] Button.interactable = {btn.interactable}");
            btn.onClick.AddListener(() => DebugLogger.Log($"[UIClickDebugger] {gameObject.name} 버튼 클릭됨!"));
        }
        
        // Image 확인
        Image img = GetComponent<Image>();
        if (img != null)
        {
            DebugLogger.Log($"[UIClickDebugger] Image.raycastTarget = {img.raycastTarget}");
        }
        
        // Canvas 확인
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            DebugLogger.Log($"[UIClickDebugger] Canvas 찾음: {canvas.name}");
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            DebugLogger.Log($"[UIClickDebugger] GraphicRaycaster 있음: {raycaster != null}");
        }
        
        // EventSystem 확인
        EventSystem eventSystem = EventSystem.current;
        DebugLogger.Log($"[UIClickDebugger] EventSystem 있음: {eventSystem != null}");
        if (eventSystem != null)
        {
            DebugLogger.Log($"[UIClickDebugger] EventSystem.enabled = {eventSystem.enabled}");
        }
    }
    
    private void Update()
    {
        // 마우스 클릭 시 레이캐스트 테스트
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Input.mousePosition;
            
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            
            DebugLogger.Log($"[UIClickDebugger] 마우스 클릭! 감지된 UI: {results.Count}개");
            foreach (var result in results)
            {
                DebugLogger.Log($"  - {result.gameObject.name}");
            }
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        DebugLogger.Log($"[UIClickDebugger] OnPointerClick 호출됨: {gameObject.name}");
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        DebugLogger.Log($"[UIClickDebugger] 마우스 올림: {gameObject.name}");
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        DebugLogger.Log($"[UIClickDebugger] 마우스 벗어남: {gameObject.name}");
    }
}