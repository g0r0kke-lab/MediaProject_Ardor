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
    private Quaternion _startRotation;
    private Coroutine _respawnChaseCoroutine;
    private float _debugLogTimer = 0f;
    private PlayerCrouchDeath _cachedDeathOnRespawn;

    protected override void Awake()
    {
        base.Awake();
        _anim = GetComponent<Animator>();

        _navAgent = GetComponent<NavMeshAgent>();
        if (_navAgent == null) _navAgent = gameObject.AddComponent<NavMeshAgent>();
        _navAgent.updateRotation = false;
        _navAgent.angularSpeed = 0f;
        _navAgent.acceleration = 8f;
        _navAgent.autoBraking = true;
        _navAgent.stoppingDistance = _executionRange * 0.5f;
    }

    protected override void Start()
    {
        base.Start();
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        EventBroker.Instance?.Subscribe(_activateEvent, OnActivateEvent);
        EventBroker.Instance?.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Subscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }
    private void OnEnable()
    {
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawnedForChase);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawnedForChase);
    }
    
    private void OnPlayerSpawnedForChase(object obj)
    {
        if (obj is GameObject go)
        {
            _cachedDeathOnRespawn = go.GetComponentInChildren<PlayerCrouchDeath>(true);
            DebugLogger.Log($"[JailSkia] OnPlayerSpawnedForChase: death={_cachedDeathOnRespawn?.name ?? "null"}");
        }
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBroker.Instance?.Unsubscribe(_activateEvent, OnActivateEvent);
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Unsubscribe(DeathEffectManager.EVENT_SCREEN_BLACK, OnScreenBlack);
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    // ============================================
    // 이벤트 수신
    // ============================================

    private void OnPlayerSpawned(object data)
    {
        CachePlayerReferences();
    }
    
    private void OnActivateEvent()
    {
        DebugLogger.Log($"[JailSkia] OnActivateEvent 수신. _isChasing={_isChasing}");
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
        {
            _navAgent.isStopped = true;
            if (_navAgent.isOnNavMesh) _navAgent.ResetPath();
            _navAgent.Warp(_startPosition);
        }
        else
            transform.position = _startPosition;
        transform.rotation = _startRotation;

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

    private Vector3 GetPlayerPosition()
    {
        // 가장 신뢰도 높은 순서로 반환
        // PlayerCrouchDeath는 CharacterController와 같은 오브젝트(Soma)에 있으므로 정확한 위치
        if (_playerCrouchDeath != null) return _playerCrouchDeath.transform.position;

        // fallback: FindWithTag가 루트를 반환해도 CharacterController가 실제 이동 오브젝트
        var p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            var cc = p.GetComponentInChildren<CharacterController>();
            return cc != null ? cc.transform.position : p.transform.position;
        }
        return transform.position;
    }

    private IEnumerator ChaseAfterRespawn()
    {
        _cachedDeathOnRespawn = null;
        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        float timeout = 5f;

        while (elapsed < timeout)
        {
            if (_cachedDeathOnRespawn != null)
            {
                DebugLogger.Log($"[JailSkia] ChaseAfterRespawn: elapsed={elapsed:F1} IsDead={_cachedDeathOnRespawn.IsDead}");
                if (!_cachedDeathOnRespawn.IsDead) break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        DebugLogger.Log($"[JailSkia] ChaseAfterRespawn 완료: elapsed={elapsed:F1}");
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
        DebugLogger.Log("[JailSkia] StartChasing → ExecutionChase");
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
            _navAgent.speed = _chaseSpeed;
            _navAgent.isStopped = false;
        }
        if (_navAgent != null && _navAgent.isOnNavMesh)
            _navAgent.SetDestination(GetPlayerPosition());
    }

    protected override void UpdateCurrentStateExtended()
    {
        if (GetCurrentState() != SkiaStateType.ExecutionChase) return;

        // null이면 재시도하되 early return 하지 않음 — SetDestination 갱신은 항상 필요
        if (_playerCrouchDeath == null)
            CachePlayerReferences();

        Vector3 playerPos = GetPlayerPosition();
        float hDist = HorizontalDistance(playerPos);

        // 1초마다 거리 로그
        _debugLogTimer += Time.deltaTime;
        if (_debugLogTimer >= 1f)
        {
            _debugLogTimer = 0f;
            float navRemaining = (_navAgent != null && _navAgent.enabled) ? _navAgent.remainingDistance : -1f;
            bool onMesh = _navAgent != null && _navAgent.isOnNavMesh;
            DebugLogger.Log($"[JailSkia] 추격 중 | hDist={hDist:F2} executionRange={_executionRange} navRemaining={navRemaining:F2} isStopped={_navAgent?.isStopped} isOnNavMesh={onMesh} hasDeath={_playerCrouchDeath != null}");
        }

        // 처형은 _playerCrouchDeath가 확보됐을 때만
        if (_playerCrouchDeath != null && hDist <= _executionRange)
        {
            DebugLogger.Log($"[JailSkia] 공격 범위 진입 (hDist={hDist:F2}) → AttackPlayer");
            if (_navAgent != null && _navAgent.enabled)
                _navAgent.isStopped = true;
            AttackPlayer(_playerCrouchDeath.gameObject);
            return;
        }

        if (_navAgent != null && _navAgent.enabled)
        {
            if (!_navAgent.isOnNavMesh)
            {
                // NavMesh 벗어난 경우 현재 위치로 재워프 시도
                _navAgent.Warp(transform.position);
                DebugLogger.LogWarning("[JailSkia] NavMesh 이탈 감지, 재워프 시도");
            }
            if (_navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = false;
                _navAgent.SetDestination(playerPos);
            }
        }

        // 진짜로 길이 막힌 경우(경로를 못 찾거나 일부만 찾음)만 stuck으로 판정.
        // 플레이어가 장애물 뒤/위에 숨어서 목적지 근처에 멈춰선 경우는 정상 추격 상태이므로 제외.
        bool pathBlocked = _navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh
            && (_navAgent.pathStatus == NavMeshPathStatus.PathPartial
                || _navAgent.pathStatus == NavMeshPathStatus.PathInvalid);

        float moved = Vector3.Distance(transform.position, _lastCheckPos);
        if (pathBlocked && moved < _stuckThreshold)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckTimeout)
            {
                DebugLogger.Log("[JailSkia] Stuck 감지 → IdlePatrol");
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
    }

    // ============================================
    // 캡슐 콜라이더 — 플레이어가 닿으면 즉시 처형
    // ============================================

    private void OnTriggerEnter(Collider other) => TryAttackOnContact(other);
    private void OnTriggerStay(Collider other) => TryAttackOnContact(other);

    private void TryAttackOnContact(Collider other)
    {
        if (!_hasBeenActivated) return;
        var playerGo = FindPlayerInHierarchy(other.transform);
        // DebugLogger.Log($"[JailSkia] TryAttackOnContact: other={other.name} tag={other.tag} → playerGo={playerGo?.name ?? "null"}");
        if (playerGo == null) return;
        AttackPlayer(playerGo);
    }

    private static GameObject FindPlayerInHierarchy(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Player")) return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    // ============================================
    // 처형
    // ============================================

    private void AttackPlayer(GameObject playerObj = null)
    {
        var target = playerObj != null
            ? (playerObj.GetComponentInChildren<PlayerCrouchDeath>()
               ?? playerObj.GetComponentInParent<PlayerCrouchDeath>())
            : _playerCrouchDeath;

        if (target == null) CachePlayerReferences();
        target ??= _playerCrouchDeath;

        DebugLogger.Log($"[JailSkia] AttackPlayer: playerObj={playerObj?.name ?? "null"} target={target?.name ?? "null"} IsDead={target?.IsDead}");

        if (target == null || target.IsDead || target.IsRespawnImmune) return;

        DebugLogger.Log("[JailSkia] OnDeath 호출");
        InputModeManager.Instance?.PlayImpact();
        target.OnDeath(transform.position);
        _isChasing = false;
        StopMovement();
        if (_anim != null) _anim.SetTrigger("ExecuteTrigger");
        DeathEffectManager.Instance?.SetPendingTip("M2_Tip_03");
        StartCoroutine(DelaySFX(0.4f));
    }

    private IEnumerator DelaySFX(float delay)
    {
        yield return new WaitForSeconds(delay);
        SoundManager.Instance?.PlaySFX(58);
    }
}
