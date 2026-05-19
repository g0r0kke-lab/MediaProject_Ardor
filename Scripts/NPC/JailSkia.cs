using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 철창(Jail)이 열리면 플레이어를 추격·처형하는 스키아.
/// 타임라인 사용 예: Roar() → (대기) → StartChasing()
/// 또는 _activateEvent 수신 시 자동으로 StartChasing()
/// </summary>
public class JailSkia : SkiaController
{
    private static readonly int ANIM_SCAN = Animator.StringToHash("ScanTrigger");

    [Header("Jail Chase")]
    [SerializeField] private string _activateEvent = "DeactivateNPC1";
    [SerializeField] private float _chaseSpeed = 3.5f;
    [SerializeField] private float _executionRange = 1.2f;
    [SerializeField] private float _stuckTimeout = 1.5f;
    [SerializeField] private float _stuckThreshold = 0.02f;
    [SerializeField] private bool _rechaseAfterRespawn = true; // true: 부활 즉시 재추격 / false: 이벤트 재수신 대기

    private Animator _anim;
    private bool _isChasing = false;
    private bool _hasBeenActivated = false;
    private float _stuckTimer = 0f;
    private Vector3 _lastCheckPos;
    private Vector3 _startPosition;
    private Coroutine _respawnChaseCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _usePatrol = false;
        _useNavMesh = true;
        _anim = GetComponent<Animator>();

        _navAgent = GetComponent<NavMeshAgent>();
        if (_navAgent == null) _navAgent = gameObject.AddComponent<NavMeshAgent>();
        _navAgent.updateRotation = false;
        _navAgent.angularSpeed = 0f;
        _navAgent.acceleration = 8f;
        _navAgent.autoBraking = true;
        _navAgent.stoppingDistance = _executionRange;
    }

    protected override void Start()
    {
        base.Start();
        _startPosition = transform.position;
        EventBroker.Instance?.Subscribe(_activateEvent, OnActivateEvent);
        EventBroker.Instance?.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Subscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBroker.Instance?.Unsubscribe(_activateEvent, OnActivateEvent);
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Unsubscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
    }

    // ============================================
    // 이벤트 수신
    // ============================================

    private void OnActivateEvent()
    {
        _hasBeenActivated = true;
        if (_isChasing) return;
        StartChasing();
    }

    private void OnPlayerDeath(object data)
    {
        if (!_hasBeenActivated) return;
        _isChasing = false;
        StopMovement();
        SetState(SkiaStateType.IdlePatrol);
    }

    private void OnScreenBlack()
    {
        if (!_hasBeenActivated) return;

        if (_navAgent != null && _navAgent.enabled)
            _navAgent.Warp(_startPosition);
        else
            transform.position = _startPosition;

        if (_rechaseAfterRespawn)
        {
            if (_respawnChaseCoroutine != null) StopCoroutine(_respawnChaseCoroutine);
            _respawnChaseCoroutine = StartCoroutine(ChaseAfterRespawn());
        }
        else
        {
            // 이벤트 재수신 대기 — 활성화 이력 초기화해서 같은 이벤트가 다시 오면 반응
            _isChasing = false;
            _hasBeenActivated = false;
        }
    }

    private IEnumerator ChaseAfterRespawn()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var death = player.GetComponent<PlayerCrouchDeath>();
                if (death != null && !death.IsDead) break;
            }
            yield return null;
        }

        _respawnChaseCoroutine = null;
        StartChasing();
    }

    // ============================================
    // 타임라인 Signal 호출용 Public API
    // ============================================

    /// <summary>제자리 포효. 타임라인에서 StartChasing() 전에 호출.</summary>
    public void Scan()
    {
        StopMovement();
        if (_anim != null) _anim.SetTrigger(ANIM_SCAN);
    }

    /// <summary>플레이어 추격 시작. 타임라인 포효 이후 또는 이벤트 수신 시 호출.</summary>
    public void StartChasing()
    {
        if (_isChasing) return;
        _isChasing = true;
        SetState(SkiaStateType.ExecutionChase);
    }

    // ============================================
    // FSM 확장
    // ============================================

    protected override void EnterStateExtended(SkiaStateType state)
    {
        if (state != SkiaStateType.ExecutionChase) return;

        _stuckTimer = 0f;
        _lastCheckPos = transform.position;

        SetAnimRunning(true);
        if (_navAgent != null)
        {
            _navAgent.speed = _patrolData != null ? _patrolData.RunSpeed : _chaseSpeed;
            _navAgent.isStopped = false;
        }
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && _navAgent != null)
            _navAgent.SetDestination(player.transform.position);
    }

    protected override void UpdateCurrentStateExtended()
    {
        if (GetCurrentState() != SkiaStateType.ExecutionChase) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = false;
            _navAgent.SetDestination(player.transform.position);
        }

        float moved = Vector3.Distance(transform.position, _lastCheckPos);
        if (moved < _stuckThreshold)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckTimeout)
            {
                _isChasing = false;
                StopMovement();
                SetState(SkiaStateType.IdlePatrol);
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
        _lastCheckPos = transform.position;

        if (HorizontalDistance(player.transform.position) <= _executionRange)
            AttackPlayer();
    }

    // ============================================
    // 캡슐 콜라이더 — 플레이어가 닿으면 즉시 처형
    // ============================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        AttackPlayer();
    }

    // ============================================
    // 처형
    // ============================================

    protected override void AttackPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        var death = playerObj.GetComponent<PlayerCrouchDeath>();
        if (death == null || death.IsDead) return;

        death.OnDeath(transform.position);
        _isChasing = false;
        StopMovement();

        if (_anim != null) _anim.SetTrigger("ExecuteTrigger");
        StartCoroutine(DelaySFX(0.4f));
    }

    private IEnumerator DelaySFX(float delay)
    {
        yield return new WaitForSeconds(delay);
        SoundManager.Instance?.PlaySFX(58);
    }
}
