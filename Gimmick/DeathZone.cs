using UnityEngine;

/// <summary>
/// 플레이어가 진입하면 PlayerCrouchDeath.OnDeath를 호출하여 즉시 사망 처리하는 트리거 존입니다.
/// </summary>
public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var death = other.GetComponentInChildren<PlayerCrouchDeath>();
        if (death != null)
        {
            if (death != null && !death.IsDead)
                SoundManager.Instance?.PlaySFX(52);
                death.OnDeath();
                DeathEffectManager.Instance?.SetPendingTip("M2_Tip_00");
        }
    }
}
