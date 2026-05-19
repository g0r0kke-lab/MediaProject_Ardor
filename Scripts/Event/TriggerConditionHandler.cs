using UnityEngine;
using UnityEngine.Events;

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