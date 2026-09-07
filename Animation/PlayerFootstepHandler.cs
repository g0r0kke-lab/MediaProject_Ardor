using StarterAssets;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어용 FootstepHandlerBase 구현체로, 현재 지면 레이어에 따라 물 또는 지면 효과음을 선택하고 착지 사운드를 처리합니다.
/// </summary>
public class PlayerFootstepHandler : FootstepHandlerBase
{
    private ThirdPersonController _controller;

    [FormerlySerializedAs("waterLayer")] [SerializeField] private LayerMask waterLayer;

    private void Awake()
    {
        _controller = GetComponent<ThirdPersonController>();

        if (waterLayer == 0)
        {
            waterLayer = LayerMask.GetMask("Water");
        }
    }

    private void OnEnable()
    {
        EventBroker.Instance?.Subscribe("OnPlayerLanded", OnFootstepGrounded);
        EventBroker.Instance?.Subscribe("OnPratfall", OnPratfall);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerLanded", OnFootstepGrounded);
        EventBroker.Instance?.Unsubscribe("OnPratfall", OnPratfall);
    }

    public override void OnFootstepLeft()
    {
        if (IsOnWaterLayer()) SoundManager.Instance.PlaySFX(2);
        else SoundManager.Instance.PlaySFX(0);
    }

    public override void OnFootstepRight()
    {
        if (IsOnWaterLayer()) SoundManager.Instance.PlaySFX(3);
        else SoundManager.Instance.PlaySFX(1);
    }
    
    public void OnCrouchstepLeft()
    {
        if (IsOnWaterLayer()) SoundManager.Instance.PlaySFX(2, 0.5f);
        else SoundManager.Instance.PlaySFX(0, 0.5f);
    }

    public void OnCrouchstepRight()
    {
        if (IsOnWaterLayer()) SoundManager.Instance.PlaySFX(3, 0.5f);
        else SoundManager.Instance.PlaySFX(1, 0.5f);
    }

    public override void OnFootstepGrounded()
    {
        if (IsOnWaterLayer()) SoundManager.Instance.PlaySFX(5);
        else SoundManager.Instance.PlaySFX(4);
    }

    private bool IsOnWaterLayer()
    {
        if (_controller == null || !_controller.Grounded) return false;

        foreach (string colliderInfo in _controller.CurrentGroundColliders)
        {
            if (colliderInfo.Contains("Layer: Water")) return true;
        }

        return false;
    }

    private void OnPratfall()
    {
        SoundManager.Instance.PlaySFX(61);
    }
}