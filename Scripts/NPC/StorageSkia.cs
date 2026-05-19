using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Storage 구역 전용 스키아 컨트롤러
/// SkiaController 상속 — Storage 전용 FSM 상태(StorageLure, StorageTrigger) 추가
/// </summary>
public class StorageSkia : SkiaController
{
    [Header("Storage — Marker")]
    [SerializeField] private Image _markerImg;
    [SerializeField] private Sprite _markerNormal;
    [SerializeField] private Sprite _markerActive;
    [SerializeField] private Image _fillImg;
    private WalkDurationTrigger _walkDurationTrigger;
    
    [Header("Storage — Timeline")]
    [SerializeField] private string _storageTimelineName;
    [SerializeField] private float _lookAtPlayerSpeed = 5f;
    [SerializeField] private float _lookAtCompleteAngle = 5f; // 이 각도 이하면 회전 완료로 판정

    [Header("Storage — Capsule Trigger")]
    [SerializeField] private CapsuleCollider _proximityCollider; // 즉시 트리거용 캡슐 콜라이더

    [Header("Storage — Lure")]
    [SerializeField] private float _lureArrivalDistance = 1.0f;
    [SerializeField] private float _lureRotationSpeed = 6f;
    [SerializeField] private float _stuckTimeout = 1.5f;    // 이 시간 이상 거의 안 움직이면 포기
    [SerializeField] private float _stuckThreshold = 0.02f; // 프레임당 이 거리 미만이면 막힌 것으로 판정

    [SerializeField] private float _navUpdateThreshold = 0.4f; // 목표가 이 거리 이상 움직여야 경로 재계산

    // 내부 상태
    private Animator _anim;
    private Transform _lureTarget;
    private Vector3 _storageHomePosition;
    private float _stuckTimer;
    private Vector3 _lastLureCheckPos;
    private Vector3 _lastNavDestination;
    private Coroutine _lookAtRoutine;
    private bool _timelineOpened;
    private float _stillTimer;
    private const float STILL_IDLE_THRESHOLD = 0.4f;

    // ============================================
    // Lifecycle
    // ============================================

    protected override void Awake()
    {
        base.Awake();

        _usePatrol = false;
        _useNavMesh = true;
        _anim = GetComponent<Animator>();

        // NavMeshAgent 설정 (base.Awake() 이후 추가되므로 부모 필드에 직접 주입)
        _navAgent = GetComponent<NavMeshAgent>();
        if (_navAgent == null) _navAgent = gameObject.AddComponent<NavMeshAgent>();
        _navAgent.updateRotation = false; // 회전은 SkiaController에서 처리
        _navAgent.angularSpeed = 0f;
        _navAgent.acceleration = 8f;
        _navAgent.autoBraking = true;
        _navAgent.stoppingDistance = 0f;

        // 근접 트리거용 캡슐 콜라이더 자동 생성
        if (_proximityCollider == null)
        {
            _proximityCollider = gameObject.AddComponent<CapsuleCollider>();
            _proximityCollider.isTrigger = true;
            _proximityCollider.radius = 2f;   // Inspector에서 조정
            _proximityCollider.height = 3f;
            _proximityCollider.center = Vector3.up * 1f;
        }
        else
        {
            _proximityCollider.isTrigger = true; // 혹시 Inspector 할당됐어도 강제 트리거
        }
    }
    
    protected override void Start()
    {
        _storageHomePosition = transform.position;
        CachePlayerReferences();
        EventBroker.Instance?.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);

        // StorageSkiaManager의 triggerZones 중 자신이 assignedSkia인 zone을 찾아 캐싱
        if (StorageSkiaManager.Instance != null)
            _walkDurationTrigger = StorageSkiaManager.Instance.GetTriggerForSkia(this);

        // 초기 상태는 대기 — SetState를 거치지 않으므로 명시적으로 Idle 처리
        StopMovement();
    }
    
    private void Update()
    {
        UpdateMarkerFill();
        UpdateStillIdle();
    }

    private void UpdateStillIdle()
    {
        if (_navAgent == null) return;
        if (_navAgent.velocity.sqrMagnitude < 0.01f)
        {
            _stillTimer += Time.deltaTime;
            if (_stillTimer >= STILL_IDLE_THRESHOLD)
                SetAnimIdle();
        }
        else
        {
            _stillTimer = 0f;
        }
    }
    
    private void UpdateMarkerFill()
    {
        if (_fillImg == null || _walkDurationTrigger == null) return;

        float ratio = _walkDurationTrigger.RequiredDuration > 0f
            ? _walkDurationTrigger.WalkTimer / _walkDurationTrigger.RequiredDuration
            : 0f;

        if (ratio >= 0.9f)
        {
            _fillImg.fillAmount = 0f;
            if (_markerImg != null) _markerImg.sprite = _markerActive;
        }
        else
        {
            _fillImg.fillAmount = ratio;
            if (_markerImg != null) _markerImg.sprite = _markerNormal;
        }
    }

    // ============================================
    // 타임라인에서 호출 — 플레이어 바라보고 ExecuteTrigger
    // ============================================

    /// <summary>
    /// 타임라인 Signal 또는 외부에서 직접 호출.
    /// 플레이어 방향으로 회전 완료 후 Animator.SetTrigger("ExecuteTrigger").
    /// </summary>
    public void LookAtPlayerThenExecute()
    {
        if (_lookAtRoutine != null) StopCoroutine(_lookAtRoutine);
        _lookAtRoutine = StartCoroutine(LookAtPlayerThenExecuteRoutine());
    }

    private IEnumerator LookAtPlayerThenExecuteRoutine()
    {
        SetState(SkiaStateType.StorageTrigger);

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) { TriggerExecuteAnimation(); _lookAtRoutine = null; yield break; }

        while (true)
        {
            Vector3 dir = (player.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) break;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot,
                Time.deltaTime * _lookAtPlayerSpeed
            );

            float angle = Quaternion.Angle(transform.rotation, targetRot);
            if (angle <= _lookAtCompleteAngle) break;

            yield return null;
        }

        SetAnimIdle();
        TriggerExecuteAnimation();
        _lookAtRoutine = null;
    }

    private void TriggerExecuteAnimation()
    {
        if (_anim != null) _anim.SetTrigger("ExecuteTrigger");
        StartCoroutine(DelaySFX(0.4f));
    }
    
    private IEnumerator DelaySFX(float delay)
    {
        yield return new WaitForSeconds(delay);
        SoundManager.Instance?.PlaySFX(58);
    }

    // ============================================
    // 캡슐 콜라이더 — 플레이어가 범위에 닿으면 즉시 타임라인
    // ============================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_timelineOpened) return;
        _timelineOpened = true;
        GameUIManager.Instance.openTimeline(_storageTimelineName);
    }

    // ============================================
    // 던진 물체 유도 — StorageSkiaManager에서 호출
    // ============================================

    /// <summary>
    /// 가장 가까운 스키아가 배정받아 호출됨.
    /// </summary>
    public void AssignLureTarget(Transform lureTransform)
    {
        if (GetCurrentState() == SkiaStateType.StorageTrigger) return;

        _lureTarget = lureTransform;

        // 이미 StorageLure 상태면 SetState가 no-op이므로 직접 재설정
        if (GetCurrentState() == SkiaStateType.StorageLure)
        {
            _stuckTimer = 0f;
            _stillTimer = 0f;
            _lastNavDestination = Vector3.positiveInfinity;
            if (_navAgent != null && _navAgent.enabled)
            {
                _navAgent.isStopped = false;
                _navAgent.SetDestination(_lureTarget.position);
            }
            SetAnimRunning(true);
        }
        else
        {
            SetState(SkiaStateType.StorageLure);
        }
    }

    // ============================================
    // FSM 확장 — EnterState / UpdateCurrentState override
    // ============================================

    protected override void EnterStateExtended(SkiaStateType state)
    {
        switch (state)
        {
            case SkiaStateType.StorageLure:
                DebugLogger.Log($"[StorageSkia] StorageLure EnterState, target: {_lureTarget?.position}");
                _stuckTimer = 0f;
                _stillTimer = 0f;
                _lastLureCheckPos = transform.position;
                _lastNavDestination = Vector3.positiveInfinity; // 강제 최초 경로 계산
                if (_lureTarget != null)
                {
                    SetAnimRunning(true);
                    float runSpd = _patrolData != null ? _patrolData.RunSpeed : 5.5f;
                    if (_navAgent != null)
                    {
                        _navAgent.stoppingDistance = _lureArrivalDistance;
                        _navAgent.speed = runSpd;
                        _navAgent.isStopped = false;
                        _navAgent.SetDestination(_lureTarget.position);
                        _lastNavDestination = _lureTarget.position;
                    }
                }
                break;

            case SkiaStateType.StorageTrigger:
                StopMovement();
                break;

            // Return 시 부모 _homePosition 대신 Storage 전용 홈 위치로 덮어씀
            case SkiaStateType.Return:
                if (_navAgent != null) _navAgent.stoppingDistance = 0f;
                SetWalkTarget(_storageHomePosition);
                break;

            // 웨이포인트 없으므로 제자리 대기
            case SkiaStateType.IdlePatrol:
                if (_navAgent != null) _navAgent.stoppingDistance = 0f;
                StopMovement();
                break;
        }
    }

    protected override void UpdateCurrentStateExtended()
    {
        switch (GetCurrentState())
        {
            case SkiaStateType.StorageLure:
                UpdateStorageLure();
                break;
        }
    }

    private void UpdateStorageLure()
    {
        if (_lureTarget == null)
        {
            StopMovement();
            SetState(SkiaStateType.IdlePatrol);
            return;
        }

        // 목표가 _navUpdateThreshold 이상 움직였을 때만 경로 재계산 (매 프레임 SetDestination 방지)
        Vector3 lurePos = _lureTarget.position;
        if ((_lastNavDestination - lurePos).sqrMagnitude > _navUpdateThreshold * _navUpdateThreshold)
        {
            if (_navAgent != null && _navAgent.enabled)
            {
                _navAgent.SetDestination(lurePos);
                _lastNavDestination = lurePos;
            }
        }

        // 도착 판정 먼저 — stuck보다 우선해야 autoBraking 감속 중 오판 방지
        bool arrived = _navAgent != null && _navAgent.enabled
            ? !_navAgent.pathPending && _navAgent.remainingDistance <= _lureArrivalDistance
            : HorizontalDistance(lurePos) <= _lureArrivalDistance;

        if (arrived)
        {
            _lureTarget = null;
            StopMovement();
            SetState(SkiaStateType.IdlePatrol);
            return;
        }

        // 막힘 감지 — 목표 근처 접근 중(autoBraking)엔 스킵해서 오판 방지
        bool approaching = _navAgent != null && !_navAgent.pathPending
                           && _navAgent.remainingDistance <= _lureArrivalDistance * 2f;
        if (approaching)
        {
            _stuckTimer = 0f;
        }
        else
        {
            float movedDist = Vector3.Distance(transform.position, _lastLureCheckPos);
            if (movedDist < _stuckThreshold)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= _stuckTimeout)
                {
                    DebugLogger.Log("[StorageSkia] 벽에 막힘 — 유인 포기");
                    _lureTarget = null;
                    StopMovement();
                    SetState(SkiaStateType.IdlePatrol);
                    return;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }
        _lastLureCheckPos = transform.position;
    }
    
    public void SetPlayerInZone(bool inZone)
    {
        if (_markerImg == null) return;
        if (!inZone && _fillImg != null) _fillImg.fillAmount = 0f; // 퇴장 시 Fill 초기화
        _markerImg.sprite = inZone ? _markerActive : _markerNormal;
    }

    private void OnPlayerDeath(object data)
    {
        if (_lookAtRoutine != null)
        {
            StopCoroutine(_lookAtRoutine);
            _lookAtRoutine = null;
        }
        _lureTarget = null;
        _timelineOpened = false;
        SetState(SkiaStateType.Return);
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
    }

}
