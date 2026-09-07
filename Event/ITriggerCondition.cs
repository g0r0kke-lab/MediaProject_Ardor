using UnityEngine;

/// <summary>
/// 게임 상태에 기반하여 TriggerConditionHandler가 UnityEvent를 발행하는 데 사용하는 불리언 게임 조건 평가 인터페이스입니다.
/// </summary>
public interface ITriggerCondition
{
    bool Evaluate(); // 조건 만족 여부
}
