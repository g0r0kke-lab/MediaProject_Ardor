using UnityEngine;
using UnityEngine.InputSystem;

// 모든 맵에서 사용 가능한 제네릭 상태 디버거
/// <summary>
/// 키보드 단축키로 현재 게임 매니저의 열거형 상태를 순환하는 제네릭 에디터 유틸리티로, 모든 맵에서 빠른 상태 디버깅을 지원합니다.
/// </summary>
public class MapStateDebugger<TState> : MonoBehaviour where TState : System.Enum
{
    private InputAction debugNextStateAction;

    private void Awake()
    {
        debugNextStateAction = new InputAction(
            binding: "<Keyboard>/p",
            type: InputActionType.Button
        );
        debugNextStateAction.performed += OnDebugNextState;
    }

    private void OnEnable() => debugNextStateAction?.Enable();
    private void OnDisable() => debugNextStateAction?.Disable();

    private void OnDestroy()
    {
        debugNextStateAction.performed -= OnDebugNextState;
        debugNextStateAction?.Dispose();
    }

    // GameManagerRegistry를 통해 현재 매니저의 상태를 가져와서 다음으로 전환
    private void OnDebugNextState(InputAction.CallbackContext context)
    {
        object rawState = GameManagerRegistry.GetCurrentState();
        if (rawState == null || rawState is not TState currentState) return;

        TState nextState = GetNextState(currentState);
        DebugLogger.Log($"[Debug] State Change: {currentState} → {nextState}");
        GameManagerRegistry.SetState(nextState);
    }

    private TState GetNextState(TState current)
    {
        var values = System.Enum.GetValues(typeof(TState));
        int nextIndex = System.Convert.ToInt32(current) + 1;

        if (nextIndex >= values.Length)
        {
            DebugLogger.Log("[Debug] 마지막 상태입니다. 첫 상태로 돌아갑니다.");
            return (TState)values.GetValue(0);
        }

        return (TState)values.GetValue(nextIndex);
    }
}
