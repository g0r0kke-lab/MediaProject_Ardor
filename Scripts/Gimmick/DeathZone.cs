using UnityEngine;

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
        }
    }
}
