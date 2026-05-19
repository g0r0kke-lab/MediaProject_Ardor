using System;
using System.Collections;
using UnityEngine;
using StarterAssets;

public class PlayerCrouchDeath : MonoBehaviour
{
    public const string EVENT_DEATH   = "Player/Death";
    
    [Header("Crouch Settings")]
    public float CrouchHeight = 0.235f;
    public float StandHeight = 0.47f;
    public float CrouchSpeed = 1.2f;         // 쪼그린 상태 이동속도
    public float HeightTransitionSpeed = 8f; // 높이 전환 속도

    [Header("Camera Settings")]
    public Transform PlayerCameraRoot;
    public float StandCameraRootY = 0.5f;
    public float CrouchCameraRootY = 0.16f;
    
    // Stand/Crouch 콜라이더 설정
    private readonly Vector3 _standCenter = new Vector3(0f, 0.235f, 0f);
    private readonly float _standRadius = 0.08f;
    private readonly Vector3 _crouchCenter = new Vector3(0f, 0.1175f, 0f);
    private readonly float _crouchRadius = 0.08f;
    
    // Death Settings
    [Header("Death Settings")]
    [SerializeField] private int respawnTransformIdx = 0; // currentManager의 teleportTransforms 인덱스
    
    private StarterAssetsInputs _input;
    private CharacterController _controller;
    private ThirdPersonController _playerController;
    private Animator _animator;
    
    private float _originalMoveSpeed;
    private bool _isCrouching = false;
    public bool IsCrouching => _isCrouching;
    private int _animIDCrouched;
    // height가 StandHeight에 도달했는지 여부
    public bool IsStandingUp => !_isCrouching && Mathf.Abs(_controller.height - StandHeight) >= 0.1f;
    public event Action OnStandComplete;
    private bool _wasStandingUp = false;
    
    private bool _isDead = false;
    public bool IsDead => _isDead;
    private int _animIDIsDead;

    public bool IsHeavyAnchorImmune => _heavyAnchorImmuneTimer > 0f;
    private float _heavyAnchorImmuneTimer = 0f;
    
    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<CharacterController>();
        _playerController = GetComponent<ThirdPersonController>();
        _animator = GetComponent<Animator>();

        _originalMoveSpeed = _playerController.MoveSpeed;
        _animIDCrouched = Animator.StringToHash("Crouched");
        _animIDIsDead = Animator.StringToHash("IsDead");
    }

    private void Update()
    {
        if (_isDead) return; // 죽은 동안 입력 무시
        
        if (_heavyAnchorImmuneTimer > 0f)
            _heavyAnchorImmuneTimer -= Time.deltaTime;
        
        HandleCrouch();
    }
    
    public void ToggleCrouch()
    {
        // 죽은 상태 / 컴포넌트 비활성화 시 무시 (누적 방지)
        if (_isDead || !enabled) return;

        _isCrouching = !_isCrouching;
        if (_animator != null)
            _animator.SetBool(_animIDCrouched, _isCrouching);
    }

    private void HandleCrouch()
    {
        float targetHeight = _isCrouching ? CrouchHeight : StandHeight;
        Vector3 targetCenter = _isCrouching ? _crouchCenter : _standCenter;
        float targetRadius = _isCrouching ? _crouchRadius : _standRadius;

        _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * HeightTransitionSpeed);
        _controller.center = Vector3.Lerp(_controller.center, targetCenter, Time.deltaTime * HeightTransitionSpeed);
        _controller.radius = Mathf.Lerp(_controller.radius, targetRadius, Time.deltaTime * HeightTransitionSpeed);

        // 카메라 루트 Y 보간
        if (PlayerCameraRoot != null)
        {
            float targetCameraY = _isCrouching ? CrouchCameraRootY : StandCameraRootY;
            Vector3 camPos = PlayerCameraRoot.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * HeightTransitionSpeed);
            PlayerCameraRoot.localPosition = camPos;
        }
        
        // 쪼그릴 땐 즉시 감속, 일어설 땐 height가 다 올라왔을 때 복원
        if (_isCrouching)
            _playerController.MoveSpeed = CrouchSpeed;
        else if (Mathf.Abs(_controller.height - StandHeight) < 0.1f)
        {
            _playerController.MoveSpeed = _originalMoveSpeed;
            // 일어서기 완료 이벤트 발행
            if (_wasStandingUp)
            {
                OnStandComplete?.Invoke();
                _wasStandingUp = false;
            }
        }
        else
        {
            _wasStandingUp = true; // 일어서는 중 플래그
        }
    }

    // 외부에서 언락 제어 (맵2 진입 시 등)
    public void SetEnabled(bool value)
    {
        enabled = value;
        if (!value)
        {
            // 비활성화 시 강제 기립
            _isCrouching = false;
            _controller.height = StandHeight;
            _playerController.MoveSpeed = _originalMoveSpeed;
        }
    }
    
    public void ForceStand()
    {
        if (!_isCrouching) return;
    
        _isCrouching = false;
        if (_animator != null)
            _animator.SetBool(_animIDCrouched, false);
        _playerController.MoveSpeed = _originalMoveSpeed;
    }
    
    #region Death
    public void OnDeath(Vector3? killerPosition = null)
    {
        if (_isDead) return;
        _isDead = true;
        
        // 스키아 방향 바라보기
        if (killerPosition.HasValue)
        {
            Vector3 dir = killerPosition.Value - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        // HeavyAnchor 부착 중이면 강제 분리
        var heavyAnchor = MagneticManager.Instance?.CurrentHoldingAnchor as HeavyAnchor;
        heavyAnchor?.ForceDetachOnDeath();
        
        // 든 물체 강제 드랍
        var lightAnchor = MagneticManager.Instance?.CurrentHoldingAnchor as LightAnchor;
        lightAnchor?.ForceDropOnDeath();
        MagneticManager.Instance?.BlockDetectionTemporarily(9999f); // 리스폰 전까지 차단

        ForceStand();
        
        EventBroker.Instance?.Publish(EVENT_DEATH, null);

        if (_animator != null)
            _animator.SetBool(_animIDIsDead, true);

        _input.MoveInput(Vector2.zero); // 이동 입력 초기화
        _playerController.enabled = false; // 이동 컨트롤러 비활성화
        
        StartCoroutine(DeathSequence());
    }
    
    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(1.5f);
        DeathEffectManager.Instance.PlayDeathEffect(OnRespawn);
    }
    
    private void OnRespawn()
    {
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForEndOfFrame();

        // 리스폰 먼저, 상태 초기화는 나중에
        GameManagerRegistry.RespawnPlayer();

        yield return new WaitForEndOfFrame(); // 위치 이동 완료 대기

        // 스토리지 박스 초기화
        foreach (var box in FindObjectsByType<BoxCollisionSound>(FindObjectsSortMode.None))
            box.ResetToInitial();
        
        _isDead = false; // 위치 이동 후에 해제
        if (_animator != null)
            _animator.SetBool(_animIDIsDead, false);
        
        _playerController.enabled = true;

        // 리스폰 후 감지 차단 해제
        MagneticManager.Instance?.UnblockDetection();
        
        // 리스폰 시 HUD 정리
        GuideUIManager.Instance?.CloseButtonGuidePanel();
        
        DeathEffectManager.Instance.FadeIn();
    }
    #endregion
    
    // HeavyAnchor 분리 시 호출
    public void SetHeavyAnchorImmune(float duration = 1.5f)
    {
        _heavyAnchorImmuneTimer = duration;
    }
    
    // EventBroker 구독용 래퍼
    private void OnDeathEvent() => OnDeath();

    private void OnEnable()
    {
        EventBroker.Instance?.Subscribe(EVENT_DEATH, OnDeathEvent);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe(EVENT_DEATH, OnDeathEvent);
    }
}