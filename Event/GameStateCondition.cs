using UnityEngine;

/// <summary>
/// 현재 게임 매니저 상태가 지정된 상태 이름 문자열과 일치할 때 true를 반환하는 트리거 조건입니다.
/// </summary>
public class GameStateCondition : TriggerConditionHandler
{
    public string requiredStateName;

    public override bool Evaluate()
    {
        var state = GameManagerRegistry.GetCurrentState();
        return state != null && state.ToString() == requiredStateName;
    }
}