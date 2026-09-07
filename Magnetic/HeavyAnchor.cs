using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>플레이어를 자기력으로 당겨 부착시키고 방향 밀어내기를 수행하는 무거운 앵커 컴포넌트</summary>
public class HeavyAnchor : MagneticAnchor
{
    [Header("Heavy Anchor Settings")]
    public bool IsTop = false;
    public float MaxMoveTime = 1f;
    public float BaseDistance = 5f;
    public Transform Direction;

    [Header("Player Control")]
    public float RepelForce = 10f;
    public float JumpHeight = 5f;
    public float ReducedGravityTime = 2f;
    public float GravityReduction = 0.3f;
    private Animator _playerAnimator;

    [Header("VFX Settings - Particle System")]
    [Tooltip("VFX 최소 거리 (이하일 때 VFX 비활성화)")]
    [SerializeField] private float _minVFXDistance = 0.5f;

    [Tooltip("VFX 업데이트 간격 (초)")]
    [SerializeField] private float _vfxUpdateInterval = 0.02f;

    [Tooltip("기본 길이 배율 (거리 1당)")]
    [SerializeField] private float _baseLengthScale = 1f;

    // Player 하위의 공용 VFX (자동 탐색)
    private ParticleSystem _heavyVFX;
    private Transform _heavyVFXTransform;
    private ParticleSystemRenderer _vfxRenderer;
    private ParticleSystem.MainModule _vfxMainModule;
    private ParticleSystem.EmissionModule _vfxEmissionModule;
    private ParticleSystem.ShapeModule _vfxShapeModule;

    private bool _isVFXActive = false;
    private Coroutine _vfxUpdateCo;

    // VFX 최적화 캐시
    private float _cachedDistance = 0f;
    private Vector3 _cachedStartPos = Vector3.zero;
    private Vector3 _cachedEndPos = Vector3.zero;

    [Header("Debug Info - Read Only")]
    public bool IsPlayerAttached = false;
    public bool IsAttachComplete = false;
    public bool IsPlayerGrounded = false;
    public bool IsPlayerControlsDisabled = false;

    private CharacterController _playerController;
    private MonoBehaviour _thirdPersonController;
    private MonoBehaviour _starterAssetsInputs;
    private GameObject _aimReticle;

    private Transform _savedPlayerParent;
    private Vector3 _savedPlayerPosition;

    private InputAction _throwAction;
    private InputAction _lookAction;

    private Coroutine _currentMovementCoroutine;

    protected override void Start()
    {
        base.Start();
        if (MagneticManager.Instance != null)
            MagneticManager.Instance.RegisterAnchor(this);

        FindHeavyVFX();
    }

    public override void InitializeFromManager(GameObject player, PlayerInput playerInput, Transform holdAnchor)
    {
        base.InitializeFromManager(player, playerInput, holdAnchor);
        InitializeAdditionalInputActions();
        FindHeavyVFX();
    }

    private void AutoAssignDirection()
    {
        if (Direction != null) return;
        if (IsTop) return;

        Transform directionChild = transform.Find("Direction");
        if (directionChild != null)
        {
            Direction = directionChild;
            return;
        }

        if (transform.childCount > 0)
        {
            Direction = transform.GetChild(0);
            return;
        }

        DestroyImmediate(gameObject);
    }

    /// <summary>
    /// Player 하위 HoldAnchor에서 "HeavyVFX" 이름의 ParticleSystem 찾기
    /// </summary>
    private void FindHeavyVFX()
    {
        if (_heavyVFX != null) return;

        Transform holdAnchor = MagneticManager.Instance?.GetHoldAnchorTransform();

        if (holdAnchor != null)
        {
            Transform vfxTransform = holdAnchor.Find("HeavyVFX");
            if (vfxTransform != null)
            {
                _heavyVFX = vfxTransform.GetComponent<ParticleSystem>();
            }
        }

        if (_heavyVFX != null)
        {
            _heavyVFXTransform = _heavyVFX.transform;
            _vfxMainModule = _heavyVFX.main;
            _vfxEmissionModule = _heavyVFX.emission;
            _vfxShapeModule = _heavyVFX.shape;
            _vfxRenderer = _heavyVFX.GetComponent<ParticleSystemRenderer>();

            _heavyVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _vfxEmissionModule.enabled = false;
            _heavyVFX.gameObject.SetActive(true);

            DebugLog($"HeavyVFX 찾음: {_heavyVFX.name} (RenderMode: {_vfxRenderer.renderMode})");
        }
        else
        {
            DebugLog("WARNING: Player 하위에서 'HeavyVFX'를 찾을 수 없습니다!");
        }
    }

    // -------------------------
    // VFX Control - Particle System (Emission Based + Optimized)
    // -------------------------

    /// <summary>
    /// VFX Transform 실시간 업데이트 코루틴 (최적화)
    /// </summary>
    private IEnumerator UpdateVFXTransformCoroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(_vfxUpdateInterval);

        while (_isVFXActive)
        {
            if (_heavyVFXTransform != null && _player != null)
            {
                UpdateVFXTransformImmediate();
            }

            yield return wait;
        }

        _vfxUpdateCo = null;
    }

    /// <summary>
    /// VFX Transform 즉시 업데이트
    /// Player가 HeavyAnchor의 하위에 있어도 월드 좌표로 정확히 계산
    /// </summary>
    private void UpdateVFXTransformImmediate()
    {
        if (_heavyVFXTransform == null || _player == null) return;

        // 시작점: HeavyAnchor의 월드 위치
        Vector3 startPos = transform.position;

        // 끝점: 플레이어의 월드 위치
        Vector3 endPos = _player.transform.position;

        // 거리 계산
        float distance = Vector3.Distance(startPos, endPos);

        // 최소 거리 체크
        if (distance <= _minVFXDistance)
        {
            if (_vfxEmissionModule.enabled)
            {
                _vfxEmissionModule.enabled = false;
                DebugLog($"VFX Emission 비활성화: 거리({distance:F2}) <= 최소({_minVFXDistance})");
            }
            return;
        }
        else
        {
            if (!_vfxEmissionModule.enabled)
            {
                _vfxEmissionModule.enabled = true;
                DebugLog($"VFX Emission 활성화: 거리({distance:F2}) > 최소({_minVFXDistance})");
            }
        }

        // 변화 감지 (최적화)
        bool needsUpdate = Mathf.Abs(distance - _cachedDistance) > 0.01f ||
                           Vector3.Distance(startPos, _cachedStartPos) > 0.01f ||
                           Vector3.Distance(endPos, _cachedEndPos) > 0.01f;

        if (!needsUpdate) return;

        // 캐시 업데이트
        _cachedDistance = distance;
        _cachedStartPos = startPos;
        _cachedEndPos = endPos;

        // VFX 위치 설정
        _heavyVFXTransform.position = startPos;

        // VFX 회전 설정 (끝점을 향하도록)
        Vector3 direction = endPos - startPos;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            _heavyVFXTransform.rotation = lookRotation * Quaternion.Euler(90f, 0f, 0f);
        }

        // VFX 길이 조정 - 조건별로 하나만 선택
        float targetScale = distance * _baseLengthScale;

        if (_vfxRenderer != null && _vfxRenderer.renderMode == ParticleSystemRenderMode.Stretch)
        {
            // Stretched Billboard 모드: lengthScale 사용
            _vfxRenderer.lengthScale = targetScale;
            DebugLog($"[VFX] Stretched Billboard - lengthScale: {targetScale:F2}");
        }
        else if (_vfxShapeModule.enabled && _vfxShapeModule.shapeType != ParticleSystemShapeType.Sphere)
        {
            // Shape 기반 (Cone/Box): Shape.scale 사용
            _vfxShapeModule.scale = new Vector3(1f, 1f, targetScale);
            DebugLog($"[VFX] Shape Scale - Z: {targetScale:F2}");
        }
        else
        {
            // 마지막 수단: Transform.localScale
            Vector3 currentScale = _heavyVFXTransform.localScale;
            _heavyVFXTransform.localScale = new Vector3(currentScale.x, currentScale.y, targetScale);
            DebugLog($"[VFX] Transform Scale - Z: {targetScale:F2}");
        }

        DebugLog($"[VFX UPDATE] 거리: {distance:F2}m → 스케일: {targetScale:F2}");
    }

    /// <summary>
    /// VFX 시작
    /// </summary>
    private void StartVFX()
    {
        if (_heavyVFX == null)
        {
            DebugLog("StartVFX 실패: _heavyVFX가 null");
            return;
        }

        // 기존 업데이트 코루틴 중지
        if (_vfxUpdateCo != null)
        {
            StopCoroutine(_vfxUpdateCo);
            _vfxUpdateCo = null;
        }

        // 캐시 초기화
        _cachedDistance = 0f;
        _cachedStartPos = Vector3.zero;
        _cachedEndPos = Vector3.zero;

        // VFX 재생
        _heavyVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _heavyVFX.Play();
        _isVFXActive = true;

        // 초기 업데이트
        UpdateVFXTransformImmediate();

        // 주기적 업데이트 시작
        _vfxUpdateCo = StartCoroutine(UpdateVFXTransformCoroutine());

        DebugLog("VFX 시작");
    }

    /// <summary>
    /// VFX 즉시 중지
    /// </summary>
    private void StopVFX()
    {
        if (_heavyVFX == null) return;

        // 업데이트 코루틴 중지
        if (_vfxUpdateCo != null)
        {
            StopCoroutine(_vfxUpdateCo);
            _vfxUpdateCo = null;
        }

        // Emission 비활성화 + 파티클 정리
        _vfxEmissionModule.enabled = false;
        _heavyVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _isVFXActive = false;

        // 캐시 초기화
        _cachedDistance = 0f;
        _cachedStartPos = Vector3.zero;
        _cachedEndPos = Vector3.zero;

        DebugLog("VFX 중지");
    }

    protected override bool IsActivatedByManager()
    {
        var mgr = MagneticManager.Instance;
        return mgr != null && mgr.IsAnchorActivated(this);
    }

    protected override void OnEKeyPressed()
    {
        if (!IsPlayerAttached)
        {
            base.OnEKeyPressed();
        }
        else if (IsAttachComplete)
        {
            // 다른 HeavyAnchor 감지 중이면 그쪽으로 이동
            var mgr = MagneticManager.Instance;
            if (mgr != null && mgr.CurrentActiveAnchor is HeavyAnchor nextHeavy && nextHeavy != this)
            {
                FullDetachAndCleanup();
                nextHeavy.AttachPlayerToAnchor();
            }
            else
            {
                HandleVerticalDrop();
            }
        }
    }

    protected override void StartMagneticInteraction()
    {
        if (!IsPlayerAttached)
            AttachPlayerToAnchor();
    }

    protected override void EndMagneticInteraction()
    {
        if (IsPlayerAttached)
            FullDetachAndCleanup();

        base.EndMagneticInteraction();
    }

    private void InitializeAdditionalInputActions()
    {
        if (_playerInput == null) return;
        if (_throwAction != null) return;

        _throwAction = _playerInput.actions.FindAction("RepelSource/LeftClick")
                       ?? _playerInput.actions.FindAction("RepelSource/Throw")
                       ?? _playerInput.actions.FindAction("Fire1");

        if (_throwAction != null)
        {
            _throwAction.started += OnThrowActionPressed;
            DebugLog($"✅ LeftClick 등록됨: {_throwAction.name}");
        }
        else
        {
            DebugLog("❌ LeftClick 액션을 찾을 수 없음!");
        }
        
        _lookAction = _playerInput.actions.FindAction("RepelSource/Look");
        if (_lookAction != null)
            _lookAction.performed += OnLookActionPerformed;
    }

    private void OnThrowActionPressed(InputAction.CallbackContext ctx)
    {
        if (InputModeManager.GetCurrentMode() != InputMode.RepelSource) return;
        if (!IsPlayerAttached || !IsAttachComplete) return;
        if (!IsPushEnabled()) return;

        InputModeManager.Instance?.PlayImpact();
        PerformDirectionPush();
    }
    
    private void OnLookActionPerformed(InputAction.CallbackContext ctx)
    {
        if (_starterAssetsInputs == null) return;
        var lookField = _starterAssetsInputs.GetType().GetField("look",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        lookField?.SetValue(_starterAssetsInputs, ctx.ReadValue<Vector2>());
    }

    private void HandleVerticalDrop()
    {
        DebugLog("[VERTICAL DROP]");
        DetachParentOnly();
        StartVerticalFall();
        CleanupInteractionState();
    }

    // ========== 부착/분리 ==========

    private void AttachPlayerToAnchor()
    {
        if (!EnsurePlayerReferences())
        {
            DebugLog("[ATTACH] 실패: EnsurePlayerReferences()==false (_player가 null)");
            return;
        }

        DebugLog($"[ATTACH] _player={_player.name}(id:{_player.GetInstanceID()}), " +
                 $"parent={(_player.transform.parent ? _player.transform.parent.name : "NULL")}, " +
                 $"_playerController={(_playerController != null ? _playerController.name : "NULL")}");

        SavePlayerState();
        DisablePlayerControls();

        IsPlayerAttached = true;
        IsAttachComplete = false;
        
        MagneticManager.Instance?.SetHoldingAnchor(this);
        
        EventBroker.Instance?.Subscribe("ActiveAnchorChanged", OnActiveAnchorChangedWhileAttached);
        
        _playerAnimator?.SetBool("IsHeavyAttached", true);
        SoundManager.Instance?.PlaySFX(48);
        
        // VFX 시작
        StartVFX();
        EventBroker.Instance?.Publish("HeavyAnchor_Attached");
        
        GuideUIManager.Instance?.OpenButtonGuidePanel(true);

        if (_currentMovementCoroutine != null)
            StopCoroutine(_currentMovementCoroutine);

        _currentMovementCoroutine = StartCoroutine(MovePlayerToAnchorCoroutine());
    }

    private void SavePlayerState()
    {
        _savedPlayerParent = _player.transform.parent;
        _savedPlayerPosition = _player.transform.position;
    }

    private void DetachParentOnly()
    {
        if (!IsPlayerAttached) return;
        
        // 분리 후 잠깐 무적
        var crouchDeath = _player?.GetComponent<PlayerCrouchDeath>();
        crouchDeath?.SetHeavyAnchorImmune(1.5f);
        
        EventBroker.Instance?.Publish("HeavyAnchor_Detached");

        if (_player != null)
        {
            if (_savedPlayerParent != null)
                _player.transform.SetParent(_savedPlayerParent, true);
            else
                _player.transform.SetParent(null, true);

            // SetParent로 HeavyAnchor의 씬으로 옮겨졌던 Player를 DontDestroyOnLoad 씬으로 복귀
            // (복귀시키지 않으면 씬 전환 시 Player가 함께 파괴됨)
            if (_savedPlayerParent == null)
                DontDestroyOnLoad(_player);

            DebugLog($"[DETACH] Parent 복원: {(_player.transform.parent ? _player.transform.parent.name : "NULL")}");
        }

        _playerAnimator?.SetBool("IsHeavyAttached", false);
        SoundManager.Instance?.PlaySFX(15);
        
        EventBroker.Instance?.Unsubscribe("ActiveAnchorChanged", OnActiveAnchorChangedWhileAttached);
        IsPlayerAttached = false;
        
        GuideUIManager.Instance?.CloseButtonGuidePanel();
        GuideUIManager.Instance?.SetHeavyDetectedGuide(false);
        
        // 분리 시 AimReticle 숨김
        if (_aimReticle != null)
            _aimReticle.SetActive(false);
        
        EnablePlayerControls();
        
        MagneticManager.Instance?.ClearHoldingAnchor(this);

        // InputMode를 Player로 복원
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchInputMode(InputMode.Player);
            DebugLog($"[INPUT MODE] RepelSource → Player (Current: {InputModeManager.GetCurrentMode()})");
        }
    }

    private void OnActiveAnchorChangedWhileAttached()
    {
        if (!IsPlayerAttached || !IsAttachComplete) return;

        var mgr = MagneticManager.Instance;
        bool otherHeavyDetected = mgr != null
                                  && mgr.CurrentActiveAnchor is HeavyAnchor otherHeavy
                                  && otherHeavy != this;

        GuideUIManager.Instance?.SetHeavyDetectedGuide(otherHeavyDetected);
    }
    
    private void FullDetachAndCleanup()
    {
        DebugLog("=== [FULL DETACH] ===");

        // VFX 중지
        StopVFX();
        EventBroker.Instance?.Publish("HeavyAnchor_Detached");
        
        DetachParentOnly();
        EnablePlayerControls();
        CleanupInteractionState();

        DebugLog("=== [FULL DETACH END] ===");
    }

    private void CleanupInteractionState()
    {
        IsInteracting = false;
        IsPlayerAttached = false;
        IsAttachComplete = false;

        if (IsTop && IsPlayerGrounded)
            RestorePlayerGroundState();

        _savedPlayerParent = null;
        _savedPlayerPosition = Vector3.zero;

        if (_currentMovementCoroutine != null)
        {
            StopCoroutine(_currentMovementCoroutine);
            _currentMovementCoroutine = null;
        }

        // InputMode도 Player로 복원 (안전장치)
        if (InputModeManager.Instance != null && InputModeManager.GetCurrentMode() == InputMode.RepelSource)
        {
            InputModeManager.Instance.SwitchInputMode(InputMode.Player);
            DebugLog($"[CLEANUP] InputMode → Player");
        }

        DebugLog("[CLEANUP] 상태 초기화 완료");
    }

    // ========== 이동/발사 ==========

    private void PerformJump()
    {
        DebugLog($"[JUMP] 높이: {JumpHeight}");

        DetachParentOnly();

        Vector3 jumpVector = Vector3.up * JumpHeight;
        MovePlayer(jumpVector);

        CleanupInteractionState();
    }

    private void PerformDirectionPush()
    {
        StartVFX();
        // if (Direction == null)
        // {
        //     DebugLog("[PUSH ERROR] Direction이 null입니다!");
        //     return;
        // }
        //
        // // 월드 좌표 기준 절대 방향 계산
        // Vector3 anchorWorldPos = transform.position;
        // Vector3 directionWorldPos = Direction.position;
        // Vector3 heading = directionWorldPos - anchorWorldPos;
        // float distance = heading.magnitude;
        // Vector3 absoluteDirection = heading.normalized;
        // float actualDistance = Mathf.Min(distance, RepelForce);
        //
        // DebugLog($"[PUSH] 시작 ==================");
        // DebugLog($"[PUSH] Anchor: {anchorWorldPos}");
        // DebugLog($"[PUSH] Direction: {directionWorldPos}");
        // DebugLog($"[PUSH] 방향: {absoluteDirection}");
        // DebugLog($"[PUSH] 거리: {actualDistance:F2}m");
        
        // 카메라 정중앙 방향으로 발사
        Camera cam = MagneticManager.Instance?.GetPlayerCamera() ?? Camera.main;
        if (cam == null) return;

        Vector3 absoluteDirection = cam.transform.forward;
        float actualDistance = RepelForce;

        // 코루틴으로 이동 (충돌 감지 포함)
        StartCoroutine(PushPlayerWithCollisionDetection(absoluteDirection, actualDistance));
    }

    private IEnumerator PushPlayerWithCollisionDetection(Vector3 worldDirection, float distance)
    {
        if (!EnsurePlayerReferences()) yield break;

        Vector3 startPos = _player.transform.position;
        Vector3 targetPos = startPos + (worldDirection * distance);

        float baseDuration = 0.5f;
        float normalizedDistance = Mathf.Clamp01(distance / RepelForce);
        float actualDuration = Mathf.Lerp(0.1f, baseDuration, normalizedDistance);

        float elapsed = 0f;
        int stuckFrameCount = 0;

        while (elapsed < actualDuration)
        {
            if (_player == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / actualDuration;
            float easeT = 1f - (1f - t) * (1f - t);

            Vector3 desiredPos = Vector3.Lerp(startPos, targetPos, easeT);
            Vector3 movement = desiredPos - _player.transform.position;

            // 이동 전 위치 저장
            Vector3 posBeforeMove = _player.transform.position;

            if (_playerController != null && _playerController.enabled)
            {
                _playerController.Move(movement);
            }
            else if (_player != null)
            {
                _player.transform.position = desiredPos;
            }

            // 이동 후 실제로 움직인 거리 측정
            Vector3 posAfterMove = _player.transform.position;
            float actualMoved = Vector3.Distance(posBeforeMove, posAfterMove);
            float expectedMove = movement.magnitude;

            // 예상 이동량의 10% 미만만 움직였다면 막힌 것
            if (expectedMove > 0.001f && actualMoved < expectedMove * 0.1f)
            {
                stuckFrameCount++;

                if (stuckFrameCount >= 1 && elapsed > 0.05f)
                {
                    DebugLog($"[PUSH] 충돌로 중단 (Frame: {stuckFrameCount})");
                    break;
                }
            }
            else
            {
                stuckFrameCount = 0;
            }

            yield return null;
        }

        DebugLog("[PUSH] 완료");
        StopVFX();
        DetachParentOnly();
        CleanupInteractionState();
    }
    
    public void ForceDetachOnDeath()
    {
        if (!IsPlayerAttached) return;
        StopVFX();
        FullDetachAndCleanup();
        // 안전장치
        GuideUIManager.Instance?.CloseButtonGuidePanel();
    }

    /// <summary>
    /// 씬 전환 직전 호출 - 부착/이동 중이던 상태를 강제로 정리하여
    /// Player가 HeavyAnchor에 매달린 채로 씬과 함께 파괴되는 것을 방지
    /// </summary>
    public void ForceDetachForSceneTransition()
    {
        if (!IsPlayerAttached) return;
        DebugLog("[SCENE TRANSITION] 부착 중 씬 전환 - 강제 분리");
        StopVFX();
        FullDetachAndCleanup();
        GuideUIManager.Instance?.CloseButtonGuidePanel();
    }

    private void StartVerticalFall()
    {
    }

    private void MovePlayer(Vector3 movement)
    {
        if (!EnsurePlayerReferences()) return;

        if (_playerController != null && _playerController.enabled)
        {
            _playerController.Move(movement);
        }
        else if (_player != null)
        {
            _player.transform.position += movement;
        }
    }

    // ========== 플레이어 컨트롤 ==========

    private void DisablePlayerControls()
    {
        if (_player == null) return;
        IsPlayerControlsDisabled = true;

        _starterAssetsInputs = _player.GetComponent("StarterAssetsInputs") as MonoBehaviour;
        if (_starterAssetsInputs != null)
        {
            var type = _starterAssetsInputs.GetType();
            type.GetField("move", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_starterAssetsInputs, Vector2.zero);
            type.GetField("jump", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_starterAssetsInputs, false);
            type.GetField("sprint", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_starterAssetsInputs, false);
        }
    }

    private void EnablePlayerControls()
    {
        IsPlayerControlsDisabled = false;
    }

    // ========== 코루틴 ==========

    private IEnumerator MovePlayerToAnchorCoroutine()
    {
        if (!EnsurePlayerReferences())
        {
            DebugLog("[MOVE TO ANCHOR] 실패: EnsurePlayerReferences()==false (_player가 null) → 정리");
            StopVFX();
            DetachParentOnly();
            CleanupInteractionState();
            yield break;
        }

        Vector3 startPos = _player.transform.position;
        Vector3 targetPos = transform.position + (IsTop ? Vector3.down * 0.1f : Vector3.zero);

        float distance = Vector3.Distance(startPos, targetPos);
        float moveTime = CalculateMoveTime(distance);

        DebugLog($"[MOVE TO ANCHOR] 거리: {distance:F2}m, 시간: {moveTime:F2}초, " +
                 $"_playerController: {(_playerController != null ? _playerController.name : "NULL")}, " +
                 $"enabled: {(_playerController != null ? _playerController.enabled.ToString() : "N/A")}");

        float elapsed = 0f;
        int stuckFrameCount = 0;

        while (elapsed < moveTime)
        {
            if (_player == null || (_playerController != null && !_playerController.enabled))
            {
                DebugLog($"[MOVE TO ANCHOR] 중단: _player==null:{_player == null}, " +
                         $"_playerController.enabled==false:{_playerController != null && !_playerController.enabled} → 정리");
                StopVFX();
                DetachParentOnly();
                CleanupInteractionState();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            t = t * t * (3f - 2f * t);

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            Vector3 posBeforeMove = _player.transform.position;

            if (_playerController != null && _playerController.enabled)
            {
                Vector3 delta = newPos - _player.transform.position;
                _playerController.Move(delta);
            }
            else if (_player != null)
            {
                _player.transform.position = newPos;
            }

            // 충돌 감지: 예상 이동의 10% 미만만 움직였으면 막힌 것
            float expectedMove = Vector3.Distance(posBeforeMove, newPos);
            float actualMoved  = Vector3.Distance(posBeforeMove, _player.transform.position);

            if (expectedMove > 0.001f && actualMoved < expectedMove * 0.1f)
            {
                stuckFrameCount++;
                if (stuckFrameCount >= 3 && elapsed > 0.1f)
                {
                    DebugLog("[MOVE TO ANCHOR] 충돌 감지 → Detach");
                    StopVFX();
                    DetachParentOnly();
                    CleanupInteractionState();
                    yield break;
                }
            }
            else
            {
                stuckFrameCount = 0;
            }

            yield return null;
        }

        if (_player != null)
        {
            _player.transform.position = targetPos;

            // Player를 HeavyAnchor의 하위로 설정
            _player.transform.SetParent(transform, true);
            DebugLog($"[PARENT] Player → {gameObject.name} 하위로 설정");

            yield return StartCoroutine(AdjustToLocalCenter());
        }

        _currentMovementCoroutine = null;
    }

    private IEnumerator AdjustToLocalCenter()
    {
        if (_player == null) yield break;

        Vector3 startLocal = _player.transform.localPosition;
        Vector3 targetLocal = IsTop ? Vector3.down * 0.1f : Vector3.zero;

        float elapsed = 0f;
        const float duration = 0.3f;

        while (elapsed < duration && _player != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _player.transform.localPosition = Vector3.Lerp(startLocal, targetLocal, t);
            yield return null;
        }

        if (_player != null)
        {
            _player.transform.localPosition = targetLocal;

            if (IsTop)
                ForcePlayerGroundState();

            IsAttachComplete = true;
            
            // 부착 완료 시점에 감지 상태 동기화 (이동 중 놓친 이벤트 보정)
            OnActiveAnchorChangedWhileAttached();
            
            // 부착 완료 시 AimReticle 표시
            if (_aimReticle != null)
                _aimReticle.SetActive(IsPushEnabled());

            // InputMode를 RepelSource로 전환
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.SwitchInputMode(InputMode.RepelSource);
                DebugLog($"[INPUT MODE] Player → RepelSource (Current: {InputModeManager.GetCurrentMode()})");
            }
            else
            {
                DebugLog("[INPUT MODE ERROR] InputModeManager.Instance가 null!");
            }

            // VFX 즉시 중지
            StopVFX();
            EventBroker.Instance?.Publish("HeavyAnchor_Attached");

            DebugLog("[ATTACH COMPLETE] 부착 완료 - VFX 즉시 중지");
        }
    }

    private float CalculateMoveTime(float distance)
    {
        float normalizedDistance = distance / BaseDistance;
        float timeMultiplier = Mathf.Sqrt(normalizedDistance);
        return Mathf.Clamp(0.5f * timeMultiplier, 0.1f, MaxMoveTime);
    }

    // ========== Ground 상태 ==========

    private void ForcePlayerGroundState()
    {
        IsPlayerGrounded = true;

        var fi = _starterAssetsInputs?.GetType().GetField("grounded",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (fi != null)
        {
            fi.SetValue(_starterAssetsInputs, true);
        }

        var pi = _thirdPersonController?.GetType().GetProperty("Grounded");
        if (pi != null && pi.CanWrite)
        {
            pi.SetValue(_thirdPersonController, true);
        }
    }

    private void RestorePlayerGroundState()
    {
        IsPlayerGrounded = false;
    }

    // ========== 유틸리티 ==========

    private bool EnsurePlayerReferences()
    {
        if (_player == null) return false;

        if (_playerController == null)
            _playerController = _player.GetComponent<CharacterController>();

        if (_playerAnimator == null)
            _playerAnimator = _player.GetComponent<Animator>();
        
        if (_aimReticle == null)
        {
            var canvas = _player.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                var reticle = canvas.transform.Find("AimReticle") ?? canvas.transform.Find("Reticle");
                if (reticle != null)
                    _aimReticle = reticle.gameObject;
            }
        }
        
        return true;
    }

    protected override void OnDestroy()
    {
        if (_throwAction != null)
        {
            _throwAction.started -= OnThrowActionPressed;
        }
        
        if (_lookAction != null)
            _lookAction.performed -= OnLookActionPerformed;

        if (_vfxUpdateCo != null)
        {
            StopCoroutine(_vfxUpdateCo);
            _vfxUpdateCo = null;
        }

        StopVFX();

        if (IsPlayerAttached)
        {
            FullDetachAndCleanup();
        }

        // 파괴 시에도 InputMode 복원 (안전장치)
        if (InputModeManager.Instance != null && InputModeManager.GetCurrentMode() == InputMode.RepelSource)
        {
            InputModeManager.Instance.SwitchInputMode(InputMode.Player);
            DebugLog($"[OnDestroy] InputMode → Player");
        }

        base.OnDestroy();
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = IsPlayerAttached ? Color.red : Color.magenta;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

        if (IsTop)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, Vector3.up * 1f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.down * 0.1f, 0.2f);

            if (IsPlayerAttached && _player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, Vector3.up * JumpHeight);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * JumpHeight, 0.3f);
            }
        }

        if (!IsTop && Direction != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Direction.position, Vector3.one * 0.2f);
            Gizmos.DrawLine(transform.position, Direction.position);

            // 절대 방향 (월드 좌표 기준)
            Vector3 heading = Direction.position - transform.position;
            float distance = heading.magnitude;
            Vector3 direction = heading.normalized;

            float actualDistance = Mathf.Min(distance, RepelForce);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, direction * actualDistance);

            if (IsPlayerAttached && _player != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, direction * actualDistance);

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position + direction * actualDistance, 0.25f);

                if (IsAttachComplete)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(transform.position, 0.3f);
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(transform.position, 0.3f);
                }
            }
        }

        // VFX 활성화 중일 때 시각적 피드백
        if (_isVFXActive && _player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.6f);

            // VFX 라인 시각화 (시작점 → 끝점)
            Gizmos.DrawLine(transform.position, _player.transform.position);

            // 현재 거리 표시
            Gizmos.color = Color.yellow;
            float currentDistance = Vector3.Distance(transform.position, _player.transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.4f + currentDistance * 0.05f);
        }
    }
    
    private bool IsPushEnabled()
    {
        return GameManagerRegistry.CurrentMapIndex >= 3;
    }
}