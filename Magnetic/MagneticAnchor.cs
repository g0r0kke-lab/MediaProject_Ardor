using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>자기력 앵커의 기반 클래스로 E키 입력 처리 및 상호작용 상태를 관리</summary>
public abstract class MagneticAnchor : MonoBehaviour
{
    [Header("Base Settings")] public string PlayerTag = "Player";
    public float ObjectWeight = 1f;
    public bool DebugMode = true;
    public Transform HoldAnchor;

    [Header("Debug Info - Read Only")] public bool IsInteracting = false; // 현재 상호작용 중인지

    // 기본 참조들
    protected GameObject _player;
    protected PlayerInput _playerInput;
    protected InputAction _interactAction;
    protected Animator _animator;
    protected Collider physicsCollider;
    private PlayerCrouchDeath _playerCrouchDeath;

    protected virtual void Start()
    {
    }

    /// <summary>
    /// MagneticManager가 플레이어 정보를 주입하는 메서드
    /// </summary>
    public virtual void InitializeFromManager(GameObject player, PlayerInput playerInput, Transform holdAnchor)
    {
        _player = player;
        _playerInput = playerInput;
        HoldAnchor = holdAnchor;

        // Animator 찾기
        if (player != null)
        {
            _animator = player.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = player.GetComponentInChildren<Animator>();
            }
            
            _playerCrouchDeath = player.GetComponent<PlayerCrouchDeath>();
        }

        // Input Action 설정
        if (_playerInput != null)
        {
            SetInputActions(_playerInput.actions);

            // 각 ActionMap의 Repel 액션에 이벤트 등록
            RegisterRepelActions();
        }
    }

    // 여러 ActionMap의 Repel 액션에 이벤트 등록
    private void RegisterRepelActions()
    {
        if (_playerInput == null || _playerInput.actions == null)
            return;

        // Player/Interact
        var playerRepel = _playerInput.actions.FindAction("Player/Interact");
        if (playerRepel != null)
        {
            playerRepel.started += OnRepelPressed;
        }

        // RepelTarget/Repel (물체를 들고 있을 때)
        var repelTargetRepel = _playerInput.actions.FindAction("RepelTarget/Repel");
        if (repelTargetRepel != null)
        {
            repelTargetRepel.started += OnRepelPressed;
        }

        // RepelSource/Repel (HeavyAnchor에 부착되어 있을 때)
        var repelSourceRepel = _playerInput.actions.FindAction("RepelSource/Repel");
        if (repelSourceRepel != null)
        {
            repelSourceRepel.started += OnRepelPressed;
        }

        // if (DebugMode)
        // {
        //     DebugLogger.Log($"[{GetType().Name}] Repel actions registered for multiple ActionMaps");
        // }
    }

    /// <summary>
    /// 자식 클래스에서 추가 InputAction을 설정할 수 있도록 제공
    /// (LightAnchor: RepelTarget/RightClick, LeftClick 등)
    /// </summary>
    protected virtual void SetInputActions(InputActionAsset actions)
    {
        // 기본 구현은 비워둠 (자식 클래스에서 override)
    }

    /// <summary>
    /// 입력을 받을 수 있는 상태인지 확인
    /// </summary>
    protected virtual bool CanReceiveInput()
    {
        // [FIX] 이미 상호작용 중이면 무조건 허용 (drop 가능하도록)
        if (IsInteracting)
        {
            return true;
        }

        // MagneticManager의 감지 차단 중이면 입력 무시
        if (MagneticManager.Instance != null && MagneticManager.Instance.IsDetectionBlocked)
        {
            if (DebugMode)
            {
                DebugLogger.Log($"[{GetType().Name}] Input blocked - Detection is blocked");
                DebugLogger.Log($"  Remaining time: {MagneticManager.Instance.DetectionBlockTimeRemaining:F2}s");
            }
            return false;
        }

        // 다른 앵커가 hold 중이면 자신이 hold 중인 경우만 입력 가능
        if (MagneticManager.Instance != null && MagneticManager.Instance.IsAnyAnchorHolding())
        {
            bool isThisHolding = (MagneticManager.Instance.CurrentHoldingAnchor == this);
            if (!isThisHolding)
            {
                if (DebugMode)
                {
                    DebugLogger.Log($"[{GetType().Name}] Input blocked - Another anchor is holding");
                    DebugLogger.Log($"  Holding anchor: {MagneticManager.Instance.CurrentHoldingAnchor?.gameObject.name}");
                }
                return false;
            }
        }

        // MagneticManager가 활성화한 경우만 허용
        bool isActivated = IsActivatedByManager();
        if (DebugMode && !isActivated)
        {
            DebugLogger.Log($"[{GetType().Name}] Input blocked - Not activated by manager");
            if (MagneticManager.Instance != null)
            {
                DebugLogger.Log($"  CurrentActiveAnchor: {MagneticManager.Instance.CurrentActiveAnchor?.gameObject.name ?? "null"}");
                DebugLogger.Log($"  This anchor: {gameObject.name}");
            }
        }

        return isActivated;
    }

    private void OnRepelPressed(InputAction.CallbackContext ctx)
    {
        // 씬 재로드 후 초기화 대기 - 참조 유효성 먼저 확인
        if (_playerInput == null || _playerInput.actions == null)
            return;

        // MagneticManager 초기화 대기
        var manager = MagneticManager.Instance;
        if (manager == null) 
            return;

        // InputModeManager 초기화 대기 (static이므로 내부 상태 확인)
        if (!InputModeManager.IsInitialized())
            return;
        
        // 1. UI 모드면 즉시 리턴
        if (InputModeManager.GetCurrentMode() == InputMode.UI)
            return;

        // 3. 내가 Hold 중인 앵커면 무조건 통과 (drop 허용)
        if (manager.CurrentHoldingAnchor == (MagneticAnchor)this)
        {
            manager.RegisterInputProcessed(this);
            OnEKeyPressed();
            return;
        }

        // 4. 감지 차단 중이면 리턴
        if (manager.IsDetectionBlocked)
            return;

        // 5. 다른 앵커가 Hold 중이면 리턴
        if (manager.CurrentHoldingAnchor != null)
            return;

        // 6. 같은 프레임에서 다른 앵커가 이미 처리했으면 리턴
        if (!manager.CanAnchorProcessInput(this))
            return;

        // 7. 활성화된 앵커가 아니면 리턴
        if (manager.CurrentActiveAnchor != this)
            return;

        // 8. 입력 처리
        manager.RegisterInputProcessed(this);
        _playerCrouchDeath?.ForceStand();
        OnEKeyPressed();
    }

    /// <summary>
    /// E키 입력 처리 (자식 클래스에서 override 가능)
    /// </summary>
    protected virtual void OnEKeyPressed()
    {
        if (!IsInteracting)
        {
            IsInteracting = true;
            InputModeManager.Instance?.PlayActivate();
            StartMagneticInteraction();
        }
        else
        {
            InputModeManager.Instance?.PlayDeactivate();
            EndMagneticInteraction();
        }
    }

    /// <summary>
    /// 상호작용 종료 (자식 클래스에서 override 가능)
    /// </summary>
    protected virtual void EndMagneticInteraction()
    {
        IsInteracting = false;
        DebugLog("Interaction ended");
    }

    /// <summary>
    /// 플레이어 사망 시 호출 — Hold(부착/소지) 상태가 아니어도 IsInteracting만 켜진 채 남는 경우를 정리
    /// </summary>
    public void ForceEndInteractionOnDeath()
    {
        if (IsInteracting)
            EndMagneticInteraction();
    }


    public virtual GameObject GetTargetParent()
    {
        return transform.parent?.gameObject;
    }

    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    protected void DebugLog(string message)
    {
        if (DebugMode)
        {
            DebugLogger.Log($"[{GetType().Name}] {message}");
        }
    }

    // ========== Public API (MagneticManager용) ==========

    public float GetObjectWeight() => ObjectWeight;
    public bool IsCurrentlyInteracting() => IsInteracting;
    public Vector3 GetPosition() => transform.position;

    // ========== 자식 클래스에서 구현할 추상 메서드 ==========

    /// <summary>
    /// MagneticManager가 이 앵커를 활성화했는지 확인
    /// </summary>
    protected abstract bool IsActivatedByManager();

    /// <summary>
    /// 실제 상호작용 로직 (픽업, 부착 등)
    /// </summary>
    protected abstract void StartMagneticInteraction();

    // ========== 기즈모 ==========

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = IsInteracting ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }

    protected virtual void OnDestroy()
    {
        UnregisterRepelActions();
    }

    private void UnregisterRepelActions()
    {
        if (_playerInput == null || _playerInput.actions == null)
            return;

        var playerRepel = _playerInput.actions.FindAction("Player/Interact");
        if (playerRepel != null)
        {
            playerRepel.started -= OnRepelPressed;
        }

        var repelTargetRepel = _playerInput.actions.FindAction("RepelTarget/Repel");
        if (repelTargetRepel != null)
        {
            repelTargetRepel.started -= OnRepelPressed;
        }

        var repelSourceRepel = _playerInput.actions.FindAction("RepelSource/Repel");
        if (repelSourceRepel != null)
        {
            repelSourceRepel.started -= OnRepelPressed;
        }
    }
}