using UnityEngine;

/// <summary>
/// 몬스터 NPC용 FootstepHandlerBase 구현체로, 왼발·오른발·착지 이벤트에 대해 설정 가능한 SFX ID를 재생합니다.
/// </summary>
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