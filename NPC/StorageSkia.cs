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
    [SerializeField] private Sprite _markerDeactive;
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
    private bool _isPlayerInZone;
    private bool _gaugeFullHapticPlayed;
    private Animator _anim;
    private Transform _lureTarget;
    private Vector3 _storageHomePosition;
    private Quaternion _storageStartRotation;
    private float _stuckTimer;
    private Vector3 _lastLureCheckPos;
    private Vector3 _lastNavDestination;
    private Coroutine _lookAtRoutine;
    private bool _timelineOpened;
    private bool _executeTriggered; // 같은 처형 시퀀스 내 LookAtPlayerThenExecute 중복 호출 방지
    private float _lastExecuteTime = -999f;
    private const float EXECUTE_COOLDOWN = 3f;
    private float _stillTimer;
    private const float STILL_IDLE_THRESHOLD = 0.4f;

    // ============================================
    // Lifecycle
    // ============================================

    protected override void Awake()
    {
        base.Awake();
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
        _storageStartRotation = transform.rotation;
        CachePlayerReferences();
        StartCoroutine(SubscribeDeathEventsWhenReady());

        // StorageSkiaManager의 triggerZones 중 자신이 assignedSkia인 zone을 찾아 캐싱
        if (StorageSkiaManager.Instance != null)
            _walkDurationTrigger = StorageSkiaManager.Instance.GetTriggerForSkia(this);

        // 초기 상태는 대기 — SetState를 거치지 않으므로 명시적으로 Idle 처리
        StopMovement();
    }

    // EventBroker.Instance가 늦게 초기화되는 경우(씬/매니저 로드 순서)에도 구독이 누락되지 않도록 대기
    private IEnumerator SubscribeDeathEventsWhenReady()
    {
        while (EventBroker.Instance == null)
            yield return null;

        EventBroker.Instance.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance.Unsubscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
        EventBroker.Instance.Subscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
        EventBroker.Instance.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        EventBroker.Instance.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    // 리스폰/맵 재진입 등으로 플레이어 레퍼런스가 갈렸을 때 다시 캐싱 (다른 Skia들과 동일 패턴)
    private void OnPlayerSpawned(object data)
    {
        CachePlayerReferences();
    }

    private void Update()
    {
        UpdateMarkerFill();
        UpdateStillIdle();
    }

    private void UpdateStillIdle()
    {
        if (_navAgent == null) return;
        // 회전 중엔 idle 전환 억제 (회전 완료 후 velocity 램프업은 0.4s threshold 안에 처리됨)
        if (IsTurning)
        {
            _stillTimer = 0f;
            return;
        }
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
        if (_walkDurationTrigger == null)
        {
            // 초기화 재시도 (빌드에서 Start 시점 순서 차이 대비)
            if (StorageSkiaManager.Instance != null)
                _walkDurationTrigger = StorageSkiaManager.Instance.GetTriggerForSkia(this);
            if (_walkDurationTrigger == null)
            {
                if (_markerImg != null) _markerImg.sprite = _markerDeactive;
                return;
            }
        }

        if (_timelineOpened)
        {
            if (_markerImg != null) _markerImg.sprite = _markerActive;
            if (_fillImg != null) _fillImg.fillAmount = 0f;
            return;
        }

        if (!_isPlayerInZone)
        {
            if (_markerImg != null) _markerImg.sprite = _markerDeactive;
            if (_fillImg != null) _fillImg.fillAmount = 0f;
            return;
        }

        float timer = _walkDurationTrigger.WalkTimer;
        float gaugeFull = _walkDurationTrigger.GaugeFullDuration;

        if (timer >= gaugeFull)
        {
            if (!_gaugeFullHapticPlayed)
            {
                _gaugeFullHapticPlayed = true;
                InputModeManager.Instance?.PlayActivate();
            }
            if (_fillImg != null) _fillImg.fillAmount = 0f;
            if (_markerImg != null) _markerImg.sprite = _markerActive;
        }
        else
        {
            _gaugeFullHapticPlayed = false;
            if (_fillImg != null)
            {
                float ratio = gaugeFull > 0f ? timer / gaugeFull : 0f;
                _fillImg.fillAmount = Mathf.Floor(ratio * 10f) / 10f;
            }
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
        if (_executeTriggered) return;
        if (Time.time - _lastExecuteTime < EXECUTE_COOLDOWN) return;
        _lastExecuteTime = Time.time;
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
        _executeTriggered = true;
        DebugLogger.Log($"[StorageSkia] 처형 애니메이션 실행 — skia: {gameObject.name}");
        InputModeManager.Instance?.PlayImpact();
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
        // 사망~리스폰 사이(아직 죽어있는 동안) + 리스폰 직후 면역 시간 — 같은 죽음 시퀀스 내 재발각 방지
        if (_playerCrouchDeath != null && (_playerCrouchDeath.IsDead || _playerCrouchDeath.IsRespawnImmune)) return;
        // WalkDurationTrigger가 이미 타임라인을 열었으면 중복 방지
        if (_walkDurationTrigger != null && _walkDurationTrigger.IsTriggered) return;
        _timelineOpened = true;
        // DebugLogger.Log($"[StorageSkia] 근접 캡슐 처형 트리거 — skia: {gameObject.name}, 플레이어 위치: {other.transform.position}");
        DeathEffectManager.Instance?.SetPendingTip("M2_Tip_02");
        GameUIManager.Instance.openTimeline(_storageTimelineName);
    }

    /// <summary>WalkDurationTrigger 등 외부에서 타임라인을 열었을 때 호출해 중복 방지</summary>
    public void MarkTimelineOpened()
    {
        _timelineOpened = true;
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
            SetRunTarget(_lureTarget.position); // 회전 후 이동
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
                _lastNavDestination = Vector3.positiveInfinity;
                if (_lureTarget != null)
                {
                    if (_navAgent != null) _navAgent.stoppingDistance = _lureArrivalDistance;
                    SetRunTarget(_lureTarget.position); // 회전 후 이동 (TurnThenMove 경유)
                    _lastNavDestination = _lureTarget.position;
                }
                break;

            case SkiaStateType.StorageTrigger:
                StopMovement();
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
        // IsTurning 중엔 isStopped=true → remainingDistance=0 오판 방지
        bool arrived = !IsTurning && (_navAgent != null && _navAgent.enabled
            ? !_navAgent.pathPending && _navAgent.remainingDistance <= _lureArrivalDistance
            : HorizontalDistance(lurePos) <= _lureArrivalDistance);

        if (arrived)
        {
            _lureTarget = null;
            StopMovement();
            SetState(SkiaStateType.IdlePatrol);
            return;
        }

        // 막힘 감지 — 회전 중이거나 목표 근처 접근 중(autoBraking)엔 스킵해서 오판 방지
        bool approaching = IsTurning || (_navAgent != null && !_navAgent.pathPending
                           && _navAgent.remainingDistance <= _lureArrivalDistance * 2f);
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
    
    public bool IsPlayerInZone => _isPlayerInZone;

    public void SetPlayerInZone(bool inZone)
    {
        _isPlayerInZone = inZone;
        // 스프라이트·게이지는 UpdateMarkerFill에서 매 프레임 처리
        if (!inZone)
        {
            if (_fillImg != null) _fillImg.fillAmount = 0f;
            _gaugeFullHapticPlayed = false;
        }
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
        _executeTriggered = false;
        _isPlayerInZone = false;
        _gaugeFullHapticPlayed = false;
        _walkDurationTrigger?.ResetTrigger();
        StopMovement();
        SetState(SkiaStateType.IdlePatrol);
    }

    private void OnScreenBlack()
    {
        if (_lookAtRoutine != null) { StopCoroutine(_lookAtRoutine); _lookAtRoutine = null; }
        _timelineOpened = false;
        _executeTriggered = false;
        _lastExecuteTime = Time.time; // 암전 직후 늦은 Signal 차단
        _isPlayerInZone = false;
        _gaugeFullHapticPlayed = false;
        if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.ResetPath();
            _navAgent.Warp(_storageHomePosition);
        }
        else
            transform.position = _storageHomePosition;
        transform.rotation = _storageStartRotation;

        SetState(SkiaStateType.IdlePatrol);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Unsubscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

}
