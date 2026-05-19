using UnityEngine;

public class GameStateCondition : TriggerConditionHandler
{
    public string requiredStateName;

    public override bool Evaluate()
    {
        var state = GameManagerRegistry.GetCurrentState();
        return state != null && state.ToString() == requiredStateName;
    }
}