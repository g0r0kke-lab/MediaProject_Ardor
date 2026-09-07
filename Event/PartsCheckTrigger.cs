using UnityEngine;

/// <summary>
/// 플레이어가 필요한 파츠를 모두 수집한 후 존에 진입하면 게임 상태를 진행시키는 트리거 존입니다.
/// </summary>
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