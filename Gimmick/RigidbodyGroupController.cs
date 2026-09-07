using UnityEngine;

/// <summary>
/// 자식 Rigidbody 그룹을 관리하여 필요 시 중력과 폭발력을 활성화하고, 사망 이벤트 시 초기 트랜스폼으로 초기화합니다.
/// </summary>
public class RigidbodyGroupController : MonoBehaviour
{
    private Rigidbody[] _rigidbodies;

    [Header("Event Keys")]
    [SerializeField] private string enableGravityEventKey = "EnableGravity";
    [SerializeField] private string resetEventKey = "DeathEffect/ScreenBlack";  // 비워두면 리셋 미구독 (예: EVENT_SCREEN_BLACK 상수값 입력)

    [Header("Manager References")]
    [SerializeField, ReadOnly] private EventBroker eventBroker;
    private bool _hasExploded = false;

    private struct RbState
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    private RbState[] _initialStates;

    private void Awake()
    {
        _rigidbodies = GetComponentsInChildren<Rigidbody>(true);

        _initialStates = new RbState[_rigidbodies.Length];
        for (int i = 0; i < _rigidbodies.Length; i++)
        {
            _initialStates[i] = new RbState
            {
                position = _rigidbodies[i].transform.position,
                rotation = _rigidbodies[i].transform.rotation
            };
            _rigidbodies[i].useGravity = false;
            _rigidbodies[i].isKinematic = true;
        }
    }

    private void Start()
    {
        if (eventBroker == null)
            eventBroker = EventBroker.Instance;

        OnEnable();
    }

    private void OnEnable()
    {
        if (eventBroker == null) return;

        if (!string.IsNullOrEmpty(enableGravityEventKey))
            eventBroker.Subscribe(enableGravityEventKey, OnEnableGravity);

        if (!string.IsNullOrEmpty(resetEventKey))
            eventBroker.Subscribe(resetEventKey, OnReset);
    }

    private void OnDisable()
    {
        if (eventBroker == null) return;

        if (!string.IsNullOrEmpty(enableGravityEventKey))
            eventBroker.Unsubscribe(enableGravityEventKey, OnEnableGravity);

        if (!string.IsNullOrEmpty(resetEventKey))
            eventBroker.Unsubscribe(resetEventKey, OnReset);
    }

    public void EnableGravity()
    {
        SetGravity(true);
    }

    private void OnEnableGravity()
    {
        SetGravity(true);
        DebugLogger.Log("[RigidbodyGroupController] 중력 활성화");
    }

    private void OnReset()
    {
        ResetToInitialState();
    }

    public void ResetToInitialState()
    {
        _hasExploded = false;

        for (int i = 0; i < _rigidbodies.Length; i++)
        {
            var rb = _rigidbodies[i];
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.transform.position = _initialStates[i].position;
            rb.transform.rotation = _initialStates[i].rotation;
        }

        DebugLogger.Log("[RigidbodyGroupController] 초기 상태로 리셋");
    }

    private void SetGravity(bool enable)
    {
        foreach (var rb in _rigidbodies)
            rb.useGravity = enable;
    }

    public void ExplodeFromPoint(Vector3 origin, float force, float radius)
    {
        if (_hasExploded) return;
        _hasExploded = true;

        foreach (var rb in _rigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddExplosionForce(force, origin, radius, 1f, ForceMode.Impulse);
        }
    }
}
