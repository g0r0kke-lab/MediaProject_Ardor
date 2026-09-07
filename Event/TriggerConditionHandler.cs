using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 오버라이드된 Evaluate 메서드의 반환값에 따라 true 또는 false UnityEvent를 호출하는 조건 기반 이벤트의 MonoBehaviour 기반 클래스입니다.
/// </summary>
public class TriggerConditionHandler : MonoBehaviour, ITriggerCondition
{
    public UnityEvent onConditionTrue;   // 조건 맞을 때
    public UnityEvent onConditionFalse;  // 조건 틀릴 때

    public virtual bool Evaluate() { return false; }

    public void Check()
    {
        if (Evaluate()) onConditionTrue?.Invoke();
        else onConditionFalse?.Invoke();
    }
}