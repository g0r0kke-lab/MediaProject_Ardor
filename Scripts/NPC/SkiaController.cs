using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using StarterAssets;

/// <summary>
/// 스키아 AI 공통 베이스. 서브클래스에서 NavMeshAgent 활성화 후 사용.
/// FSM 상태 머신 + Player.Instance / EventBroker 활용
/// </summary>
[RequireComponent(typeof(Animator))]
public class SkiaController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] protected SkiaPatrolData _patrolData;

    [Header("State (ReadOnly)")]
    [SerializeField] private SkiaStateType _currentState = SkiaStateType.IdlePatrol;
    [SerializeField] private int _currentWaypointIndex = 0;
    [SerializeField] private bool _isPatrolForward = true;
    protected bool _usePatrol = true;
    
    [Header("Movement")]
    [SerializeField] private float _arrivalDistance = 0.5f;
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _groundedGravity = -2f;

    [Header("Detection")]
    [SerializeField] private float _detectionGauge = 0f;
    [SerializeField] private bool _isPlayerInSight = false;
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private Transform _eyePoint;

    [Header("Chase")]
    [SerializeField] private float _chaseLostTimeout = 7f;
    [SerializeField] private float _stopCheckDuration = 5f;

    [Header("Roar")]
    [SerializeField] private float _roarDuration = 1f;

    [Header("Alert Propagation")]
    [SerializeField] private float _alertPropagationRadius = 20f;

    [Header("Attack")]
    [SerializeField] private float _attackRange = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLog = true;
    [SerializeField] private bool _drawGizmos = true;

    // 캐싱
    private Animator _animator;
    protected NavMeshAgent _navAgent;

    // NavMesh 모드 (하위 클래스에서 true로 설정 시 NavMeshAgent 사용)
    protected bool _useNavMesh = false;
    private Vector3 _homePosition;
    private Vector3 _lastKnownPosition;
    private Vector3 _noiseOrigin;
    private float _chaseLostTimer;
    private float _stopCheckTimer;
    private bool _hasNoiseTarget = false;
    private Coroutine _waitCoroutine;
    private bool _isInitialized = false;
    private bool _patrolStarted = false;

    // 현재 이동 목표
    private Vector3 _currentMoveTarget;
    private bool _hasMoveTarget = false;

    // 플레이어 참조
    private GameObject _player;
    private StarterAssetsInputs _playerInput;
    protected PlayerCrouchDeath _playerCrouchDeath;

    // 웨이포인트 (SkiaFactory에서 주입)
    private List<PatrolWaypoint> _waypoints;

    // Animator 파라미터 해시
    private static readonly int ANIM_IS_WALKING = Animator.StringToHash("IsWalking");
    private static readonly int ANIM_IS_RUNNING = Animator.StringToHash("IsRunning");
    private static readonly int ANIM_SPEED = Animator.StringToHash("Speed");
    private static readonly int ANIM_MOTION_SPEED = Animator.StringToHash("MotionSpeed");
    private static readonly int ANIM_ROAR = Animator.StringToHash("ScreamTrigger");

    // ============================================
    // Lifecycle
    // ============================================

    protected virtual void Awake()
    {
        _animator = GetComponent<Animator>();
        _navAgent = GetComponent<NavMeshAgent>();

        _animator.applyRootMotion = false;

        if (_navAgent != null)
            _navAgent.updateRotation = false; // 회전은 직접 처리
    }

    protected virtual void Start()
    {
        _homePosition = transform.position;
        CachePlayerReferences();

        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Subscribe("PlayerNoise", OnPlayerNoise);
        }

        if (_isInitialized)
        {
            BeginPatrol();
        }
    }

    private void Update()
    {
        if (_isInitialized && !_patrolStarted)
        {
            BeginPatrol();
        }

        if (_player == null)
        {
            CachePlayerReferences();
        }

        if (_useNavMesh)
        {
            // NavMeshAgent가 이동·중력 처리, 속도 방향으로 회전
            if (_navAgent != null && _navAgent.enabled && _navAgent.velocity.sqrMagnitude > 0.01f)
            {
                Vector3 vel = _navAgent.velocity;
                vel.y = 0f;
                if (vel.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(vel.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _rotationSpeed);
                }
            }
        }
        else
        {
            ApplyGravity();
        }

        if (_player != null)
        {
            UpdateSightDetection();
            UpdateNoiseDetection();
        }

        UpdateCurrentState();
    }

    protected virtual void OnDestroy()
    {
        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Unsubscribe("PlayerNoise", OnPlayerNoise);
        }
    }

    // ============================================
    // 초기화
    // ============================================

    public void Initialize(SkiaPatrolData patrolData, List<PatrolWaypoint> waypoints)
    {
        _patrolData = patrolData;
        _waypoints = waypoints;
        _isInitialized = true;
    }

    private void BeginPatrol()
    {
        if (!_usePatrol) return;
        if (_patrolStarted) return;

        if (_waypoints != null && _waypoints.Count > 0)
        {
            _patrolStarted = true;
            DebugLog($"순찰 시작 - 웨이포인트 {_waypoints.Count}개");
            // 기본 상태가 이미 IdlePatrol일 수 있어 SetState가 무시되는 케이스가 있음.
            // 이 경우에도 첫 웨이포인트로 이동 타겟을 반드시 세팅해야 한다.
            _detectionGauge = 0f;
            if (_currentState != SkiaStateType.IdlePatrol)
            {
                SetState(SkiaStateType.IdlePatrol);
            }
            else
            {
                MoveToCurrentWaypoint();
            }
        }
        else
        {
            DebugLog("웨이포인트가 없습니다!");
        }
    }

    protected void CachePlayerReferences()
    {
        _player = Player.Instance;
        if (_player == null) return;

        _playerInput = _player.GetComponent<StarterAssetsInputs>();
        _playerCrouchDeath = _player.GetComponent<PlayerCrouchDeath>();
    }

    // ============================================
    // 중력
    // ============================================

    private void ApplyGravity()
    {
    }

    // ============================================
    // 이동 헬퍼
    // ============================================

    protected void RotateTowards(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _rotationSpeed);
        }
    }

    protected float HorizontalDistance(Vector3 target)
    {
        Vector3 a = transform.position;
        Vector3 b = target;
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }

    private bool HasArrivedAtTarget()
    {
        if (!_hasMoveTarget) return true;
        if (_useNavMesh && _navAgent != null && _navAgent.enabled)
            return !_navAgent.pathPending && _navAgent.remainingDistance <= _arrivalDistance;
        return HorizontalDistance(_currentMoveTarget) <= _arrivalDistance;
    }

    protected void SetWalkTarget(Vector3 target)
    {
        _currentMoveTarget = target;
        _hasMoveTarget = true;
        if (_useNavMesh && _navAgent != null)
        {
            _navAgent.speed = _patrolData != null ? _patrolData.WalkSpeed : 2f;
            _navAgent.isStopped = false;
            _navAgent.SetDestination(target);
        }
        SetAnimWalking(true);
    }

    protected void SetRunTarget(Vector3 target)
    {
        _currentMoveTarget = target;
        _hasMoveTarget = true;
        if (_useNavMesh && _navAgent != null)
        {
            _navAgent.speed = _patrolData != null ? _patrolData.RunSpeed : 5.5f;
            _navAgent.isStopped = false;
            _navAgent.SetDestination(target);
        }
        SetAnimRunning(true);
    }

    protected void StopMovement()
    {
        _hasMoveTarget = false;
        if (_useNavMesh && _navAgent != null)
            _navAgent.isStopped = true;
        SetAnimIdle();
    }

    // ============================================
    // 애니메이터 헬퍼
    // ============================================

    protected void SetAnimWalking(bool walking)
    {
        if (_animator == null) return;
        if (HasParam(ANIM_IS_WALKING)) _animator.SetBool(ANIM_IS_WALKING, walking);
        if (HasParam(ANIM_IS_RUNNING)) _animator.SetBool(ANIM_IS_RUNNING, false);
        if (HasParam(ANIM_MOTION_SPEED))
        {
            float multiplier = _patrolData != null ? _patrolData.WalkSpeed / 2f : 1f;
            _animator.SetFloat(ANIM_MOTION_SPEED, multiplier);
        }
    }

    protected void SetAnimRunning(bool running)
    {
        if (_animator == null) return;
        if (HasParam(ANIM_IS_WALKING)) _animator.SetBool(ANIM_IS_WALKING, false);
        if (HasParam(ANIM_IS_RUNNING)) _animator.SetBool(ANIM_IS_RUNNING, running);
        if (HasParam(ANIM_MOTION_SPEED)) _animator.SetFloat(ANIM_MOTION_SPEED, 1f);
    }

    protected void SetAnimIdle()
    {
        if (_animator == null) return;
        if (HasParam(ANIM_IS_WALKING)) _animator.SetBool(ANIM_IS_WALKING, false);
        if (HasParam(ANIM_IS_RUNNING)) _animator.SetBool(ANIM_IS_RUNNING, false);
    }

    private bool HasParam(int hash)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return false;
        foreach (var p in _animator.parameters)
        {
            if (p.nameHash == hash) return true;
        }
        return false;
    }

    // ============================================
    // 시야 감지
    // ============================================

    private void UpdateSightDetection()
    {
        if (_patrolData == null || _player == null) return;

        Vector3 eyePos = _eyePoint != null ? _eyePoint.position : transform.position + Vector3.up;
        Vector3 playerPos = _player.transform.position + Vector3.up * 0.5f;
        Vector3 dirToPlayer = playerPos - eyePos;
        float dist = dirToPlayer.magnitude;

        bool wasInSight = _isPlayerInSight;
        _isPlayerInSight = false;

        float effectiveViewDist = _patrolData.ViewDistance;
        if (_playerCrouchDeath != null && _playerCrouchDeath.IsCrouching)
        {
            effectiveViewDist *= 0.5f;
        }

        if (dist <= effectiveViewDist)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            if (angle <= _patrolData.ViewAngle * 0.5f)
            {
                if (!Physics.Raycast(eyePos, dirToPlayer.normalized, dist, _obstacleMask))
                {
                    _isPlayerInSight = true;
                    _lastKnownPosition = _player.transform.position;
                }
            }
        }

        if (_isPlayerInSight && !wasInSight) OnPlayerEnterSight();
        else if (!_isPlayerInSight && wasInSight) OnPlayerExitSight();
    }

    // ============================================
    // 소음 감지
    // ============================================

    private void UpdateNoiseDetection()
    {
        if (_playerInput == null || _player == null || _patrolData == null) return;

        bool isRunning = _playerInput.sprint && _playerInput.move.sqrMagnitude > 0.01f;
        if (!isRunning) return;

        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (dist > _patrolData.NoiseDetectionRange) return;

        if (_currentState == SkiaStateType.IdlePatrol || _currentState == SkiaStateType.Return)
        {
            _noiseOrigin = _player.transform.position;
            _hasNoiseTarget = true;
            SetState(SkiaStateType.Investigate);
        }
    }

    private void OnPlayerNoise(object data)
    {
        if (data is Vector3 noisePos && _patrolData != null)
        {
            float dist = Vector3.Distance(transform.position, noisePos);
            if (dist > _patrolData.NoiseDetectionRange) return;

            _noiseOrigin = noisePos;
            _hasNoiseTarget = true;

            if (_currentState == SkiaStateType.IdlePatrol || _currentState == SkiaStateType.Return)
            {
                SetState(SkiaStateType.Investigate);
            }
        }
    }

    // ============================================
    // 시야 이벤트
    // ============================================

    private void OnPlayerEnterSight()
    {
        switch (_currentState)
        {
            case SkiaStateType.IdlePatrol:
            case SkiaStateType.Investigate:
            case SkiaStateType.StopCheck:
            case SkiaStateType.StopLost:
                SetState(SkiaStateType.AlertWarn);
                break;
        }
    }

    private void OnPlayerExitSight()
    {
        switch (_currentState)
        {
            case SkiaStateType.AlertWarn:
                if (_detectionGauge <= 0f) SetState(SkiaStateType.IdlePatrol);
                break;
            case SkiaStateType.ChaseActive:
                _chaseLostTimer = 0f;
                break;
        }
    }

    // ============================================
    // 동료 추격 신호
    // ============================================

    public void ReceiveChaseSignal(Vector3 targetPosition)
    {
        _lastKnownPosition = targetPosition;
        if (_currentState == SkiaStateType.IdlePatrol ||
            _currentState == SkiaStateType.Investigate ||
            _currentState == SkiaStateType.Return)
        {
            SetState(SkiaStateType.ChaseActive);
        }
    }

    // ============================================
    // FSM
    // ============================================

    public void SetState(SkiaStateType newState)
    {
        if (_currentState == newState) return;

        ExitState(_currentState);
        DebugLog($"{_currentState} → {newState}");
        _currentState = newState;
        EnterState(newState);
    }

    private void EnterState(SkiaStateType state)
    {
        switch (state)
        {
            case SkiaStateType.IdlePatrol:
                _detectionGauge = 0f;
                MoveToCurrentWaypoint();
                break;

            case SkiaStateType.AlertWarn:
                StopMovement();
                break;

            case SkiaStateType.RoarStart:
                StopMovement();
                if (HasParam(ANIM_ROAR)) _animator.SetTrigger(ANIM_ROAR);
                PropagateChaseSignal();
                StartCoroutine(RoarThenTransition(SkiaStateType.ChaseActive));
                break;

            case SkiaStateType.Investigate:
                if (_hasNoiseTarget) SetWalkTarget(_noiseOrigin);
                break;

            case SkiaStateType.ChaseActive:
                _chaseLostTimer = 0f;
                if (_player != null) SetRunTarget(_lastKnownPosition);
                break;

            case SkiaStateType.StopCheck:
                StopMovement();
                _stopCheckTimer = 0f;
                _homePosition = transform.position;
                break;

            case SkiaStateType.StopLost:
                StopMovement();
                _stopCheckTimer = 0f;
                break;

            case SkiaStateType.RoarFail:
                StopMovement();
                if (HasParam(ANIM_ROAR)) _animator.SetTrigger(ANIM_ROAR);
                StartCoroutine(RoarThenTransition(SkiaStateType.Return));
                break;

            case SkiaStateType.Return:
                SetWalkTarget(_homePosition);
                break;
        }
        EnterStateExtended(state);
    }

    private void ExitState(SkiaStateType state)
    {
        if (_waitCoroutine != null)
        {
            StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
        }
    }

    // ============================================
    // 상태별 Update
    // ============================================

    private void UpdateCurrentState()
    {
        switch (_currentState)
        {
            case SkiaStateType.IdlePatrol:   UpdateIdlePatrol();   break;
            case SkiaStateType.AlertWarn:     UpdateAlertWarn();    break;
            case SkiaStateType.Investigate:   UpdateInvestigate();  break;
            case SkiaStateType.ChaseActive:   UpdateChaseActive();  break;
            case SkiaStateType.StopCheck:     UpdateStopCheck();    break;
            case SkiaStateType.StopLost:      UpdateStopLost();     break;
            case SkiaStateType.Return:        UpdateReturn();       break;
        }
        UpdateCurrentStateExtended();
    }
    
    // 하위 클래스 확장용 훅 (빈 기본 구현)
    protected virtual void EnterStateExtended(SkiaStateType state) { }
    protected virtual void UpdateCurrentStateExtended() { }

    private void UpdateIdlePatrol()
    {
        if (!_hasMoveTarget) return;
        RotateTowards(_currentMoveTarget);

        if (HasArrivedAtTarget() && _waitCoroutine == null)
        {
            _waitCoroutine = StartCoroutine(WaitAtWaypoint());
        }
    }

    private IEnumerator WaitAtWaypoint()
    {
        StopMovement();

        if (_waypoints != null && _currentWaypointIndex < _waypoints.Count)
        {
            var wp = _waypoints[_currentWaypointIndex];

            if (wp.LookAtTarget != null)
            {
                Vector3 lookDir = (wp.LookAtTarget.position - transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }

            if (wp.WaitTime > 0f)
            {
                DebugLog($"웨이포인트 {_currentWaypointIndex} 대기 {wp.WaitTime}초");
                yield return new WaitForSeconds(wp.WaitTime);

                if (_currentState != SkiaStateType.IdlePatrol)
                {
                    _waitCoroutine = null;
                    yield break;
                }
            }
        }

        AdvanceWaypoint();
        MoveToCurrentWaypoint();
        _waitCoroutine = null;
    }

    private void AdvanceWaypoint()
    {
        if (_waypoints == null || _waypoints.Count == 0) return;

        var wp = _waypoints[_currentWaypointIndex];
        if (wp.IsReturnPoint) _isPatrolForward = !_isPatrolForward;

        if (_isPatrolForward)
        {
            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _waypoints.Count)
                _currentWaypointIndex = 0;
        }
        else
        {
            _currentWaypointIndex--;
            if (_currentWaypointIndex < 0)
                _currentWaypointIndex = _waypoints.Count - 1;
        }
    }

    private void MoveToCurrentWaypoint()
    {
        if (_waypoints == null || _waypoints.Count == 0) return;
        if (_currentWaypointIndex >= _waypoints.Count) return;

        var wp = _waypoints[_currentWaypointIndex];
        if (wp.Point != null)
        {
            SetWalkTarget(wp.Point.position);
            DebugLog($"→ 웨이포인트 {_currentWaypointIndex} ({wp.Point.name})");
        }
    }

    private void UpdateAlertWarn()
    {
        if (_player == null) return;
        RotateTowards(_player.transform.position);

        if (_isPlayerInSight)
        {
            float fillRate = _patrolData != null ? 100f / _patrolData.DetectionFillTime : 25f;
            _detectionGauge += fillRate * Time.deltaTime;

            if (_detectionGauge >= 100f)
            {
                _detectionGauge = 100f;
                SetState(SkiaStateType.RoarStart);
            }
        }
        else
        {
            _detectionGauge -= 30f * Time.deltaTime;
            if (_detectionGauge <= 0f)
            {
                _detectionGauge = 0f;
                SetState(SkiaStateType.IdlePatrol);
            }
        }
    }

    private void UpdateInvestigate()
    {
        if (!_hasMoveTarget) return;
        RotateTowards(_currentMoveTarget);

        if (HasArrivedAtTarget())
        {
            _hasNoiseTarget = false;
            SetState(SkiaStateType.StopCheck);
        }
    }

    private void UpdateChaseActive()
    {
        if (_player == null) return;

        if (_isPlayerInSight)
        {
            _lastKnownPosition = _player.transform.position;
            _chaseLostTimer = 0f;
            RotateTowards(_lastKnownPosition);

            float distToPlayer = Vector3.Distance(transform.position, _player.transform.position);
            if (distToPlayer <= _attackRange)
            {
                AttackPlayer();
            }
        }
        else
        {
            RotateTowards(_lastKnownPosition);
            _chaseLostTimer += Time.deltaTime;

            if (_chaseLostTimer >= _chaseLostTimeout)
            {
                SetState(SkiaStateType.StopLost);
            }
            else if (HorizontalDistance(_lastKnownPosition) <= _arrivalDistance)
            {
                SetState(SkiaStateType.RoarFail);
            }
        }
    }

    private void UpdateStopCheck()
    {
        _stopCheckTimer += Time.deltaTime;
        float angle = Mathf.Sin(_stopCheckTimer * 2f) * 45f;
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + angle * Time.deltaTime, 0);

        if (_stopCheckTimer >= _stopCheckDuration)
            SetState(SkiaStateType.IdlePatrol);
    }

    private void UpdateStopLost()
    {
        _stopCheckTimer += Time.deltaTime;
        if (_stopCheckTimer >= _chaseLostTimeout)
            SetState(SkiaStateType.Return);
    }

    private void UpdateReturn()
    {
        if (!_hasMoveTarget) return;
        RotateTowards(_currentMoveTarget);

        if (HasArrivedAtTarget())
            SetState(SkiaStateType.IdlePatrol);
    }

    // ============================================
    // 공격
    // ============================================

    protected virtual void AttackPlayer()
    {
        if (_playerCrouchDeath != null && !_playerCrouchDeath.IsDead)
        {
            // 헤비앵커 관련 상태면 공격 안 함 (부착 중 + 날아가는 중)
            var holding = MagneticManager.Instance?.CurrentHoldingAnchor;
            DebugLogger.Log($"[AttackPlayer] holding: {holding?.name ?? "null"}, IsHeavy: {holding is HeavyAnchor}");
            
            if (holding is HeavyAnchor) return;

            // 헤비앵커에서 막 분리됐을 때도 잠깐 무적
            if (_playerCrouchDeath.IsHeavyAnchorImmune) return;
            
            DebugLog("플레이어 공격!");
            // 스키아 위치 전달
            _playerCrouchDeath.OnDeath(transform.position);
            StopMovement();
            StartCoroutine(AfterAttackRoutine());
        }
    }

    private IEnumerator AfterAttackRoutine()
    {
        yield return new WaitForSeconds(2f);
        SetState(SkiaStateType.Return);
    }

    // ============================================
    // 코루틴
    // ============================================

    private IEnumerator RoarThenTransition(SkiaStateType nextState)
    {
        yield return new WaitForSeconds(_roarDuration);
        SetState(nextState);
    }

    // ============================================
    // 동료 전파
    // ============================================

    private void PropagateChaseSignal()
    {
        var allSkia = FindObjectsByType<SkiaController>(FindObjectsSortMode.None);
        foreach (var skia in allSkia)
        {
            if (skia == this) continue;
            float dist = Vector3.Distance(transform.position, skia.transform.position);
            if (dist <= _alertPropagationRadius)
            {
                skia.ReceiveChaseSignal(_lastKnownPosition);
            }
        }
    }

    // ============================================
    // 접근자
    // ============================================

    public SkiaStateType GetCurrentState() => _currentState;
    public float GetDetectionGauge() => _detectionGauge;

    // ============================================
    // 디버그
    // ============================================

    private void DebugLog(string msg)
    {
        if (_showDebugLog) Debug.Log($"<color=cyan>[{gameObject.name}]</color> {msg}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos) return;

        float viewDist = _patrolData != null ? _patrolData.ViewDistance : 12f;
        float viewAngle = _patrolData != null ? _patrolData.ViewAngle : 90f;

        Gizmos.color = _isPlayerInSight ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDist);

        Vector3 leftBound = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up, leftBound * viewDist);
        Gizmos.DrawRay(transform.position + Vector3.up, rightBound * viewDist);

        if (_hasMoveTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentMoveTarget);
            Gizmos.DrawWireSphere(_currentMoveTarget, _arrivalDistance);
        }

        if (Application.isPlaying && _lastKnownPosition != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_lastKnownPosition, 0.3f);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Application.isPlaying ? _homePosition : transform.position, Vector3.one * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.red, _detectionGauge / 100f);
            Gizmos.DrawCube(transform.position + Vector3.up * 2.5f,
                new Vector3(_detectionGauge / 100f * 2f, 0.2f, 0.2f));
        }

#if UNITY_EDITOR
        string stateText = Application.isPlaying
            ? $"{_currentState}\nGauge: {_detectionGauge:F0}%"
            : "Not Playing";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, stateText);
#endif
    }
}