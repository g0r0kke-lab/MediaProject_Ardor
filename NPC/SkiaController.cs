using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
/// <summary>
/// NavMesh 기반 NPC 컨트롤러로 스키아 캐릭터의 이동, 부드러운 회전, 애니메이션 상태 전환을 처리합니다.
/// </summary>
public class SkiaController : MonoBehaviour
{
    [Header("State (ReadOnly)")]
    [SerializeField] private SkiaStateType _currentState = SkiaStateType.IdlePatrol;

    [Header("Movement")]
    [SerializeField] protected float _walkSpeed = 2f;
    [SerializeField] protected float _runSpeed = 5.5f;
    [SerializeField] private float _arrivalDistance = 0.5f;
    [SerializeField] private float _rotationSpeed = 5f;

    [Header("Turn Before Move")]
    [SerializeField] private float _turnCompleteAngle = 8f;
    [SerializeField] private float _turnSpeed = 360f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLog = true;
    [SerializeField] private bool _drawGizmos = true;

    private Animator _animator;
    protected NavMeshAgent _navAgent;

    private Coroutine _turnCoroutine;
    private bool _isTurning = false;
    protected bool IsTurning => _isTurning;
    private bool _pendingAnimIsRun = false;

    private Vector3 _currentMoveTarget;
    private bool _hasMoveTarget = false;

    protected PlayerCrouchDeath _playerCrouchDeath;

    private static readonly int ANIM_IS_WALKING  = Animator.StringToHash("IsWalking");
    private static readonly int ANIM_IS_RUNNING  = Animator.StringToHash("IsRunning");
    private static readonly int ANIM_MOTION_SPEED = Animator.StringToHash("MotionSpeed");

    // ============================================
    // Lifecycle
    // ============================================

    protected virtual void Awake()
    {
        _animator = GetComponent<Animator>();
        _navAgent = GetComponent<NavMeshAgent>();
        _animator.applyRootMotion = false;
        if (_navAgent != null)
            _navAgent.updateRotation = false;
    }

    protected virtual void Start()
    {
        CachePlayerReferences();
    }

    private void Update()
    {
        if (_playerCrouchDeath == null)
            CachePlayerReferences();

        UpdateCurrentState();

        if (!_isTurning && _navAgent != null && _navAgent.enabled && _navAgent.velocity.sqrMagnitude > 0.01f)
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

    protected virtual void OnDestroy() { }

    // ============================================
    // 초기화
    // ============================================

    protected void CachePlayerReferences()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;
        _playerCrouchDeath = playerObj.GetComponentInChildren<PlayerCrouchDeath>();
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
        if (_isTurning) return false;
        if (_navAgent != null && _navAgent.enabled)
            return !_navAgent.pathPending && _navAgent.remainingDistance <= _arrivalDistance;
        return HorizontalDistance(_currentMoveTarget) <= _arrivalDistance;
    }

    protected void SetWalkTarget(Vector3 target)
    {
        _currentMoveTarget = target;
        _hasMoveTarget = true;
        if (_navAgent != null)
        {
            _navAgent.speed = _walkSpeed;
            _navAgent.SetDestination(target);
            _pendingAnimIsRun = false;
            StartTurnThenMove();
        }
        SetAnimWalking(true);
    }

    protected void SetRunTarget(Vector3 target)
    {
        _currentMoveTarget = target;
        _hasMoveTarget = true;
        if (_navAgent != null)
        {
            _navAgent.speed = _runSpeed;
            _navAgent.SetDestination(target);
            _pendingAnimIsRun = true;
            StartTurnThenMove();
        }
        SetAnimRunning(true);
    }

    protected void StopMovement()
    {
        if (_turnCoroutine != null) { StopCoroutine(_turnCoroutine); _turnCoroutine = null; }
        _isTurning = false;
        _hasMoveTarget = false;
        if (_navAgent != null)
            _navAgent.isStopped = true;
        SetAnimIdle();
    }

    private void StartTurnThenMove()
    {
        if (_turnCoroutine != null) StopCoroutine(_turnCoroutine);
        _navAgent.isStopped = true;
        _turnCoroutine = StartCoroutine(TurnThenMoveCoroutine());
    }

    private IEnumerator TurnThenMoveCoroutine()
    {
        _isTurning = true;

        Vector3 dir = _currentMoveTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            while (_hasMoveTarget && Quaternion.Angle(transform.rotation, targetRot) > _turnCompleteAngle)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, _turnSpeed * Time.deltaTime);
                yield return null;
            }
            if (_hasMoveTarget)
                transform.rotation = targetRot;
        }

        _isTurning = false;
        if (_hasMoveTarget && _navAgent != null)
        {
            if (_pendingAnimIsRun) SetAnimRunning(true);
            else SetAnimWalking(true);
            _navAgent.isStopped = false;
        }
        _turnCoroutine = null;
    }

    // ============================================
    // 애니메이터 헬퍼
    // ============================================

    protected void SetAnimWalking(bool walking)
    {
        if (_animator == null) return;
        if (HasParam(ANIM_IS_WALKING))  _animator.SetBool(ANIM_IS_WALKING, walking);
        if (HasParam(ANIM_IS_RUNNING))  _animator.SetBool(ANIM_IS_RUNNING, false);
        if (HasParam(ANIM_MOTION_SPEED)) _animator.SetFloat(ANIM_MOTION_SPEED, _walkSpeed / 2f);
    }

    protected void SetAnimRunning(bool running)
    {
        if (_animator == null) return;
        if (HasParam(ANIM_IS_WALKING))  _animator.SetBool(ANIM_IS_WALKING, false);
        if (HasParam(ANIM_IS_RUNNING))  _animator.SetBool(ANIM_IS_RUNNING, running);
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
            if (p.nameHash == hash) return true;
        return false;
    }

    // ============================================
    // FSM
    // ============================================

    public void SetState(SkiaStateType newState)
    {
        if (_currentState == newState) return;
        DebugLog($"{_currentState} → {newState}");
        _currentState = newState;
        EnterState(newState);
    }

    private void EnterState(SkiaStateType state)
    {
        switch (state)
        {
            case SkiaStateType.IdlePatrol:
                break;
        }
        EnterStateExtended(state);
    }

    private void UpdateCurrentState()
    {
        switch (_currentState)
        {
            case SkiaStateType.IdlePatrol: break;
        }
        UpdateCurrentStateExtended();
    }

    protected virtual void EnterStateExtended(SkiaStateType state) { }
    protected virtual void UpdateCurrentStateExtended() { }

    // ============================================
    // 접근자
    // ============================================

    public SkiaStateType GetCurrentState() => _currentState;

    // ============================================
    // 디버그
    // ============================================

    private void DebugLog(string msg)
    {
        if (_showDebugLog) DebugLogger.Log($"<color=cyan>[{gameObject.name}]</color> {msg}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos) return;

        if (_hasMoveTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentMoveTarget);
            Gizmos.DrawWireSphere(_currentMoveTarget, _arrivalDistance);
        }

#if UNITY_EDITOR
        if (Application.isPlaying)
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, _currentState.ToString());
#endif
    }
}
