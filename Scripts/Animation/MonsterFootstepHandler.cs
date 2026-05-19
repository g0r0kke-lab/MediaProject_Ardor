using UnityEngine;

public class MonsterFootstepHandler : FootstepHandlerBase
{
    [SerializeField] private int footstepLeftSFX = 59;
    [SerializeField] private int footstepRightSFX = 60;
    [SerializeField] private int footstepGroundedSFX = 2;

    public override void OnFootstepLeft()
    {
        SoundManager.Instance.PlaySFX(footstepLeftSFX);
    }

    public override void OnFootstepRight()
    {
        SoundManager.Instance.PlaySFX(footstepRightSFX);
    }

    public override void OnFootstepGrounded()
    {
        //
    }
}