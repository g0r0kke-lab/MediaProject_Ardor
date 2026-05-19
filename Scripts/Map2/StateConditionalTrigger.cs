using UnityEngine;

/// <summary>
/// 플레이어가 진입했을 때 현재 게임 상태가 조건 상태와 일치하면
/// 목표 상태로 전환하는 트리거.
/// 상태 이름은 각 맵 GameManager의 State enum 값 이름(문자열)으로 입력.
/// 예) conditionStateName = "FindKey", targetStateName = "OpenDoor"
/// </summary>
public class StateConditionalTrigger : MonoBehaviour
{
    [Header("조건 — 이 상태일 때만 실행")]
    [SerializeField] private string _conditionStateName;

    [Header("목표 — 전환할 상태")]
    [SerializeField] private string _targetStateName;

    [Header("옵션")]
    [SerializeField] private bool _oneShot = true; // true: 1회만 실행

    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_oneShot && _triggered) return;

        object currentState = GameManagerRegistry.GetCurrentState();
        if (currentState == null)
        {
            DebugLogger.LogWarning("[StateConditionalTrigger] 현재 상태를 가져올 수 없습니다.");
            return;
        }

        // 현재 상태 이름 비교
        if (currentState.ToString() != _conditionStateName) return;

        // 같은 Enum 타입에서 목표 상태 파싱
        System.Type stateType = currentState.GetType();
        if (!System.Enum.IsDefined(stateType, _targetStateName))
        {
            DebugLogger.LogWarning($"[StateConditionalTrigger] '{_targetStateName}'은 {stateType.Name}에 존재하지 않는 상태입니다.");
            return;
        }

        object targetState = System.Enum.Parse(stateType, _targetStateName);
        GameManagerRegistry.SetState(targetState);

        DebugLogger.Log($"[StateConditionalTrigger] {_conditionStateName} → {_targetStateName} 전환 완료");

        if (_oneShot) _triggered = true;
    }
}
