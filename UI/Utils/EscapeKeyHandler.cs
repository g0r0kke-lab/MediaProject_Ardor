using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Input System을 통해 Escape 키를 감지하고 설정 가능한 UnityEvent를 호출하여, 씬별 닫기 또는 취소 동작을 수행합니다.
/// </summary>
public class EscapeKeyHandler : MonoBehaviour
{
    [Header("ESC Key Event")]
    [SerializeField] private UnityEvent OnEscapePressed;

    // Input Action 생성
    private InputAction escapeAction;

    private void Awake()
    {
        // ESC 키에 대한 Input Action 생성
        escapeAction = new InputAction(binding: "<Keyboard>/escape");
        escapeAction.performed += OnEscapePerformed;
    }

    private void OnEnable()
    {
        // Input Action 활성화
        escapeAction?.Enable();
    }

    private void OnDisable()
    {
        // Input Action 비활성화
        escapeAction?.Disable();
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 및 리소스 정리
        escapeAction.performed -= OnEscapePerformed;
        escapeAction?.Dispose();
    }

    private void OnEscapePerformed(InputAction.CallbackContext context)
    {
        OnEscapePressed?.Invoke();
    }
}