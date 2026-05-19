using UnityEngine;

/// <summary>
/// EventBroker를 통해 오브젝트의 활성화/비활성화를 제어하는 컴포넌트
/// </summary>
public enum ActivationType
{
    GameObject,
    Collider
}

public class EventActivator : MonoBehaviour
{
    [Header("Target Settings")] [Tooltip("제어할 대상 오브젝트 (null이면 자기 자신)")] [SerializeField]
    private GameObject targetObject;

    [SerializeField] private ActivationType activationType = ActivationType.GameObject;

    [SerializeField, ReadOnly] private Collider targetCollider;

    [Header("Event Keys")] [Tooltip("활성화 이벤트 키")] [SerializeField]
    private string activateEventKey = "";

    [Tooltip("비활성화 이벤트 키")] [SerializeField]
    private string deactivateEventKey = "";

    [Header("Initial State")]
    [SerializeField] private bool startEnabled = false;
    
    [Header("Manager References")] [SerializeField, ReadOnly]
    private EventBroker eventBroker;

    private void Awake()
    {
        // 타겟이 없으면 자기 자신
        if (targetObject == null)
        {
            targetObject = gameObject;
        }

        if (activationType == ActivationType.Collider)
            targetCollider = targetObject.GetComponent<Collider>();
    }

    private void Start()
    {
        if (eventBroker == null)
        {
            eventBroker = EventBroker.Instance;
        }

        if (activationType == ActivationType.GameObject && targetObject != null)
        {
            targetObject.SetActive(startEnabled);
        }
        else if (activationType == ActivationType.Collider && targetCollider != null)
        {
            targetCollider.enabled = startEnabled;
        }

        OnEnable();
    }

    private void OnEnable()
    {
        if (eventBroker == null) return;

        // 이벤트 키가 설정되어 있으면 구독
        if (!string.IsNullOrEmpty(activateEventKey))
        {
            eventBroker.Subscribe(activateEventKey, OnActivate);
        }

        if (!string.IsNullOrEmpty(deactivateEventKey))
        {
            eventBroker.Subscribe(deactivateEventKey, OnDeactivate);
        }
    }

    private void OnDisable()
    {
        if (eventBroker == null) return;

        // 구독 해제
        if (!string.IsNullOrEmpty(activateEventKey))
        {
            eventBroker.Unsubscribe(activateEventKey, OnActivate);
        }

        if (!string.IsNullOrEmpty(deactivateEventKey))
        {
            eventBroker.Unsubscribe(deactivateEventKey, OnDeactivate);
        }
    }

    // 활성화 이벤트 핸들러
    private void OnActivate()
    {
        if (activationType == ActivationType.GameObject)
        {
            targetObject.SetActive(true);
            DebugLogger.Log("[EventActivator] " + $"{targetObject.name} 활성화");
        }
        else if (targetCollider != null) targetCollider.enabled = true;
    }

    // 비활성화 이벤트 핸들러
    private void OnDeactivate()
    {
        if (activationType == ActivationType.GameObject)
        {
            if (targetObject != null)
            {
                targetObject.SetActive(false);
                DebugLogger.Log("[EventActivator] " + $"{targetObject.name} 비활성화");
            }
        }
        else if (activationType == ActivationType.Collider && targetCollider != null)
        {
            targetCollider.enabled = false;
        }
    }
}