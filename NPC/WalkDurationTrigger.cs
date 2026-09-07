using UnityEngine;

/// <summary>
/// 플레이어가 존 내에서 이동하는 동안 걷기 지속 타이머를 누적하여, 임계값에 도달하면 발각 타임라인을 실행하는 트리거 존입니다.
/// </summary>
public class WalkDurationTrigger : MonoBehaviour
{
    [Header("설정")]
    public string timelineName;
    [Tooltip("게이지가 꽉 차고 markerActive로 전환되는 시간 (초)")]
    public float gaugeFullDuration = 0.9f;
    [Tooltip("실제 발각 타임라인이 열리는 시간 (초). gaugeFullDuration보다 커야 함")]
    public float requiredDuration = 1.3f;
    [Tooltip("플레이어가 멈췄을 때 타이머 감소 속도 배율 (fill 속도 대비). 높을수록 빠르게 감소.")]
    public float decayMultiplier = 2f;

    [SerializeField, ReadOnly] private bool _isPlayerInside = false;
    [SerializeField, ReadOnly] private float _walkTimer = 0f;

    [SerializeField, ReadOnly] private StarterAssets.ThirdPersonController _playerController;
    [SerializeField, ReadOnly] private CharacterController _characterController;
    [SerializeField, ReadOnly] private PlayerCrouchDeath _crouchDeath;

    [Header("Storage Lure")] // 이 트리거존 담당 스키아
    [SerializeField] private StorageSkia _assignedSkia;
    public StorageSkia AssignedSkia => _assignedSkia;
    private Collider[] _triggerColliders;
    private int _insideCount = 0;
    private bool _triggered = false;
    private bool _gaugeSoundPlaying = false;
    public bool IsTriggered => _triggered;
    public bool IsPlayerInside => _isPlayerInside;
    public float WalkTimer => _walkTimer;
    public float GaugeFullDuration => gaugeFullDuration;
    public float RequiredDuration => requiredDuration;
    
    private void Awake()
    {
        _triggerColliders = GetComponents<Collider>();
    }

    private void Start()
    {
        StartCoroutine(SubscribeDeathEventWhenReady());
    }

    // EventBroker.Instance가 늦게 초기화되는 경우(씬/매니저 로드 순서)에도 구독이 누락되지 않도록 대기
    private System.Collections.IEnumerator SubscribeDeathEventWhenReady()
    {
        while (EventBroker.Instance == null)
            yield return null;

        // 중복 방지를 위해 항상 해제 후 재구독
        EventBroker.Instance.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
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
        // _isPlayerInside가 잔류했는데 플레이어 컨트롤러가 비활성이면 강제 리셋
        if (_isPlayerInside && _playerController != null && !_playerController.enabled)
        {
            DebugLogger.Log($"[WalkDurationTrigger] 플레이어 컨트롤러 비활성 감지 — 강제 리셋 ({gameObject.name})");
            ResetTrigger();
        }
        if (!_isPlayerInside || _playerController == null) return;
        // 사망~리스폰 사이(아직 죽어있는 동안) + 리스폰 직후 면역 시간 — 같은 죽음 시퀀스 내 재발각 방지
        if (_crouchDeath != null && (_crouchDeath.IsDead || _crouchDeath.IsRespawnImmune))
        {
            _walkTimer = 0f;
            return;
        }

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
                    // 겹친 영역 중 더 높은 우선순위(인덱스 낮은) zone에 플레이어가 있으면 양보
                    if (StorageSkiaManager.Instance != null && !StorageSkiaManager.Instance.IsHighestPriorityZone(this))
                    {
                        return;
                    }

                    _triggered = true;
                    // DebugLogger.Log($"[WalkDurationTrigger] 처형 트리거 — zone: {gameObject.name}, timer: {_walkTimer:F2}s / 플레이어 위치: {_playerController?.transform.position}");
                    DeathEffectManager.Instance?.SetPendingTip("M2_Tip_01");
                    _assignedSkia?.MarkTimelineOpened(); // 근접 트리거 중복 방지
                    GameUIManager.Instance.openTimeline(timelineName);
                }
            }
        }
        else
        {
            // 멈추면 타이머 서서히 감소 (decayMultiplier배 속도로 drain)
            _walkTimer = Mathf.Max(0f, _walkTimer - Time.deltaTime * decayMultiplier);
        }

        // 게이지가 움직이는 동안만 루프 사운드 재생
        bool shouldLoop = !_triggered && _walkTimer > 0f;
        if (shouldLoop && !_gaugeSoundPlaying)
        {
            SoundManager.Instance?.PlaySFXLoop(151);
            _gaugeSoundPlaying = true;
        }
        else if (!shouldLoop && _gaugeSoundPlaying)
        {
            StopGaugeLoop();
        }
    }

    private void StopGaugeLoop()
    {
        if (!_gaugeSoundPlaying) return;
        SoundManager.Instance?.StopSFX();
        _gaugeSoundPlaying = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount++;
        if (_insideCount != 1) return; // 첫 진입 때만 초기화
        _isPlayerInside = true;
        _walkTimer = 0f;
        _triggered = false;
        _playerController = other.GetComponent<StarterAssets.ThirdPersonController>();
        _characterController = other.GetComponent<CharacterController>();
        _crouchDeath = other.GetComponent<PlayerCrouchDeath>();
        SoundManager.Instance?.PlaySFX(152);
        _assignedSkia?.SetPlayerInZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _insideCount = Mathf.Max(0, _insideCount - 1);
        if (_insideCount != 0) return; // 모든 콜라이더에서 완전히 나갔을 때만 리셋
        StopGaugeLoop();
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
        StopGaugeLoop();
        _insideCount = 0;
        _isPlayerInside = false;
        _walkTimer = 0f;
        _triggered = false;
        _playerController = null;
        _characterController = null;
        _crouchDeath = null;
        _assignedSkia?.SetPlayerInZone(false);
    }
    
    // 착지한 물체가 이 트리거 박스들 중 하나 안인지 확인
    public bool IsInsideTrigger(Transform target)
    {
        if (_triggerColliders == null) return false;
        foreach (var col in _triggerColliders)
        {
            if (col != null && col.bounds.Contains(target.position))
                return true;
        }
        return false;
    }
    
    public void OnLureObjectLanded(Transform lureTransform)
    {
        if (!IsInsideTrigger(lureTransform)) return;
        _assignedSkia?.AssignLureTarget(lureTransform);
    }
}