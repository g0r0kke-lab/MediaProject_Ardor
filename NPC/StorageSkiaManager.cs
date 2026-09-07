using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storage 구역 스키아 4마리 관리
/// — 던진 물체 이벤트 수신 후 가장 가까운 1마리에게 배정
/// </summary>
public class StorageSkiaManager : MonoBehaviour
{
    public static StorageSkiaManager Instance { get; private set; }

    [SerializeField] private List<WalkDurationTrigger> _triggerZones;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    
    private void Start()
    {
        EventBroker.Instance?.Subscribe("ThrowableObjectLanded", OnThrowableLanded);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe("ThrowableObjectLanded", OnThrowableLanded);
    }

    // 이벤트 수신 — 가장 가까운 스키아에게 배정
    private void OnThrowableLanded(object data)
    {
        DebugLogger.Log($"[StorageSkiaManager] OnThrowableLanded 수신됨: {data}");
        if (data is not Transform lureTransform) return;

        // 착지 위치가 속한 트리거존 찾아서 담당 스키아에게 배정
        foreach (var zone in _triggerZones)
        {
            if (zone.IsInsideTrigger(lureTransform))
            {
                zone.OnLureObjectLanded(lureTransform);
                return;
            }
        }
    }
    
    public WalkDurationTrigger GetTriggerForSkia(StorageSkia skia)
    {
        return _triggerZones.Find(zone => zone.AssignedSkia == skia);
    }

    // 겹친 영역들 중 플레이어가 들어와 있는 zone들 중에서, 자신보다 인덱스가 낮은(우선순위가 높은) zone에
    // 플레이어가 있으면 false 반환 — 가장 우선순위 높은 zone만 true
    public bool IsHighestPriorityZone(WalkDurationTrigger zone)
    {
        int idx = _triggerZones.IndexOf(zone);
        for (int i = 0; i < idx; i++)
        {
            if (_triggerZones[i].IsPlayerInside) return false;
        }
        return true;
    }
}