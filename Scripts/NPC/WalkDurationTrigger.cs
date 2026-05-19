using UnityEngine;

public class WalkDurationTrigger : MonoBehaviour
{
    [Header("설정")]
    public string timelineName;
    public float requiredDuration = 2f; // 몇 초 이상 움직여야 하는지
    
    [SerializeField, ReadOnly] private bool _isPlayerInside = false;
    [SerializeField, ReadOnly] private float _walkTimer = 0f;

    [SerializeField, ReadOnly] private StarterAssets.ThirdPersonController _playerController;
    [SerializeField, ReadOnly] private CharacterController _characterController;
    [SerializeField, ReadOnly] private PlayerCrouchDeath _crouchDeath;
    
    [Header("Storage Lure")] // 이 트리거존 담당 스키아
    [SerializeField] private StorageSkia _assignedSkia;
    public StorageSkia AssignedSkia => _assignedSkia;
    private Collider _triggerCollider;
    private bool _triggered = false;
    public float WalkTimer => _walkTimer;
    public float RequiredDuration => requiredDuration;
    
    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        EventBroker.Instance?.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
    }

    private void OnPlayerDeath(object _) => ResetTrigger();
    
    private void Update()
    {
        if (!_isPlayerInside || _playerController == null) return;

        // 이동 중인지 확인 (speed > 0 이면 걷기/달리기 상태)
        var velocity = _characterController.velocity;
        bool isMoving = velocity.magnitude > 0.1f;
        bool isCrouching = _crouchDeath != null && _crouchDeath.IsCrouching;

        if (isMoving && !isCrouching)
        {
            // 헤비앵커 부착 중엔 타이머 안 참
            if (MagneticManager.Instance?.CurrentHoldingAnchor is HeavyAnchor) 
            {
                _walkTimer = 0f;
                return;
            }
            
            _walkTimer += Time.deltaTime;

            if (_walkTimer >= requiredDuration)
            {
                if (!_triggered)
                {
                    _triggered = true;
                    GameUIManager.Instance.openTimeline(timelineName);
                }
            }
        }
        else
        {
            // 멈추면 타이머 리셋할지 여부
            _walkTimer = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInside = true;
        _walkTimer = 0f;
        _triggered = false;
        _playerController = other.GetComponent<StarterAssets.ThirdPersonController>();
        _characterController = other.GetComponent<CharacterController>();
        _crouchDeath = other.GetComponent<PlayerCrouchDeath>();
        _assignedSkia?.SetPlayerInZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInside = false;
        _walkTimer = 0f;
        _triggered = false;
        _playerController = null;
        _characterController = null;
        _crouchDeath = null;
        _assignedSkia?.SetPlayerInZone(false);
    }
    
    public void ResetTrigger()
    {
        _isPlayerInside = false;
        _walkTimer = 0f;
        _triggered = false;
        _playerController = null;
        _characterController = null;
        _crouchDeath = null;
        _assignedSkia?.SetPlayerInZone(false);
    }
    
    // 착지한 물체가 이 트리거 박스 안인지 확인
    public bool IsInsideTrigger(Transform target)
    {
        if (_triggerCollider == null) return false;
        return _triggerCollider.bounds.Contains(target.position);
    }
    
    public void OnLureObjectLanded(Transform lureTransform)
    {
        if (!IsInsideTrigger(lureTransform)) return;
        _assignedSkia?.AssignLureTarget(lureTransform);
    }
}