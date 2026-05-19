using UnityEngine;

public abstract class FootstepHandlerBase : MonoBehaviour
{
    public abstract void OnFootstepLeft();
    public abstract void OnFootstepRight();
    public abstract void OnFootstepGrounded();
}