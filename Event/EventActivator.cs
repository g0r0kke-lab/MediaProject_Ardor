using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EventBroker를 통해 오브젝트의 활성화/비활성화를 제어하는 컴포넌트
/// </summary>
public enum ActivationType
{
    GameObject,
    Collider
}

/// <summary>
/// EventBroker 이벤트를 구독하여 씬 리로드 지원과 함께 대상 GameObject 또는 Collider를 활성화하거나 비활성화합니다.
/// </summary>
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
        SubscribeEvents();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬 전환마다 EventBroker 인스턴스가 새로 교체되므로,
    /// 씬 전환에도 살아남는 오브젝트(예: DontDestroyOnLoad Player)는 재구독이 필요함
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsubscribeEvents();
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        var broker = EventBroker.Instance;
        if (broker == null) return;

        if (!string.IsNullOrEmpty(activateEventKey))
        {
            broker.Subscribe(activateEventKey, OnActivate);
        }

        if (!string.IsNullOrEmpty(deactivateEventKey))
        {
            broker.Subscribe(deactivateEventKey, OnDeactivate);
        }
    }

    private void UnsubscribeEvents()
    {
        var broker = EventBroker.Instance;
        if (broker == null) return;

        if (!string.IsNullOrEmpty(activateEventKey))
        {
            broker.Unsubscribe(activateEventKey, OnActivate);
        }

        if (!string.IsNullOrEmpty(deactivateEventKey))
        {
            broker.Unsubscribe(deactivateEventKey, OnDeactivate);
        }
    }

    // 활성화 이벤트 핸들러
    private void OnActivate()
    {
        if (activationType == ActivationType.GameObject)
        {
            if (targetObject != null)
            {
                targetObject.SetActive(true);
                DebugLogger.Log("[EventActivator] " + $"{targetObject.name} 활성화");
            }
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