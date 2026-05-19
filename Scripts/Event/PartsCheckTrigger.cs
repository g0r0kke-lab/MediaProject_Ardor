using UnityEngine;

public class PartsCheckTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        // 상태 체크
        var state = GameManagerRegistry.GetCurrentState();
        if (state == null || !state.Equals(Map1State.PartsMissionStart)) return;

        // 파츠 수 체크
        if (Map1GameManager.Instance.GetCurrentPartsNum() >= 3)
        {
            hasTriggered = true;
            GameManagerRegistry.ProgressState();
            DebugLogger.Log("[PartsCheckTrigger] 파츠 수집 완료, 상태 진행");
        }
    }
}