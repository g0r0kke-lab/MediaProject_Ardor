using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 가벼운 물체(LightAnchor)의 자기력 상호작용 처리
/// - 픽업, 던지기, BackView 전환
/// - Hold 상태 추적으로 다른 interaction 차단
/// - BackView 전환 중 입력 차단
/// - ExplosiveChecker 컴포넌트가 있으면 던질 때 폭발 활성화
/// 
/// [최적화 버전]
/// - WaitForSeconds 캐싱 (GC 감소)
/// - sqrMagnitude 사용 (sqrt 연산 제거)
/// - Update early return
/// - 헬퍼 프로퍼티로 null 체크 통합
/// - VFX 초기화 실패 시 자동 비활성화
/// </summary>
public class LightAnchor : MagneticAnchor
{
    [Header("Rigidbody Explosion On Pickup")]
    [SerializeField] private bool _explodeOnPickup = false;
    [SerializeField] private RigidbodyGroupController _rigidbodyGroupController;
    [SerializeField] private float _explosionForce = 10f;
    [SerializeField] private float _explosionRadius = 3f; // 거리별 감쇠용
    private bool _hasExploded = false;
    
    #region Constants

    private const float PICKUP_DURATION = 0.3f;
    private const float ANCHOR_TRANSITION_DURATION = 0.1f;
    private const float SQR_POSITION_THRESHOLD = 0.0001f; // 0.01f * 0.01f
    private const float DISTANCE_CHANGE_THRESHOLD = 0.01f;

    #endregion

    [Header("Pickup Rotation")]
    [Tooltip("체크 시 픽업할 때 원래 회전값 유지")]
    [SerializeField] private bool _preserveRotationOnPickup = false;
    private Vector3 _cachedWorldScale;
    
    #region Animator Hashes (Static)

    private static readonly int HashIsPulling = Animator.StringToHash("IsPulling");
    private static readonly int HashIsPushing = Animator.StringToHash("IsPushing");
    private static readonly int HashPushTrigger = Animator.StringToHash("PushTrigger");

    #endregion

    #region Serialized Fields

    [Header("Light Anchor Settings")]
    public float ThrowForce = 15f;
    [SerializeField] private float _holdPadding = 0.3f;
    [Tooltip("던지기 방향 Y축 최솟값. 0 = 수평, 음수 허용 시 아래로 던지기 가능. 기본값 0")]
    [SerializeField] private float _minThrowDirY = 0f;

    [Header("Wall Clipping Prevention")]
    [Tooltip("홀딩 중 통과를 막을 레이어 (보통 Walls 레이어 선택)")]
    [SerializeField] private LayerMask _wallLayerMask;
    [Tooltip("SphereCast 반지름 오버라이드. 0이면 콜라이더 크기에서 자동 계산")]
    [SerializeField] private float _wallCheckRadiusOverride = 0f;
    [Tooltip("이 값 이하의 침투는 무시 (계단/모서리 떨림 방지). 기본 0.06")]
    [SerializeField] private float _wallPenetrationThreshold = 0.06f;

    [Header("BackView Transition")]
    [Tooltip("카메라 전환 완료 후 추가 대기 시간")]
    [SerializeField] private float _postTransitionDelay = 0.3f;

    [Header("Aim Transparency")]
    [Tooltip("조준 시 박스 투명도 (0=완전투명, 1=불투명)")]
    [SerializeField] [Range(0f, 1f)] private float _aimAlpha = 0.25f;

    private Renderer[] _targetRenderers;
    private Color[] _originalColors;
    private bool _isTransparent = false;

    [Header("Trajectory Preview")]
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private int _trajectorySegments = 35;
    [SerializeField] private float _trajectoryTimeStep = 0.1f;
    [SerializeField] private Color _trajectoryStartColor = new Color(0.4f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color _trajectoryEndColor = new Color(0.4f, 0.8f, 1f, 0f);
    private float _trajectoryWidth = 0.05f;
    [Tooltip("값이 클수록 점선이 촘촘해짐")]
    [SerializeField] private float _trajectoryDashTiling = 6f;
    [Tooltip("위로 조준할수록 포물선 시작점을 올리는 최대 높이 (수평=0, 수직=최대값)")]
    private float _trajectoryUpOffsetMax = 0.2f;
    private LayerMask _trajectoryCollisionMask;

    [Header("Trajectory End Marker")]
    [Tooltip("착탄 지점에 표시할 박스 프리팹. 비워두면 _target 메시 자동 복제")]
    [SerializeField] private GameObject _trajectoryEndMarkerPrefab;
    private GameObject _endMarker;
    private Renderer _endMarkerRenderer;

    [Header("VFX Settings - Particle System")]
    [Tooltip("던지기 VFX 지속 시간")]
    [SerializeField] private float _throwVFXDuration = 0.5f;

    [Tooltip("기본 길이 배율 (거리 1당)")]
    [SerializeField] private float _baseLengthScale = 1f;

    [Tooltip("VFX 최소 거리 (이하일 때 VFX 비활성화)")]
    [SerializeField] private float _minVFXDistance = 0.5f;

    [Tooltip("VFX 업데이트 간격 (초) - 프레임 드랍 방지")]
    [SerializeField] private float _vfxUpdateInterval = 0.02f;

    [Header("Optional References (Inspector 할당 시 자동 탐색 스킵)")]
    [SerializeField] private BackViewCameraController _backViewCameraControllerRef;

    #endregion

    #region VFX Components

    private ParticleSystem _lightVFX;
    private Transform _lightVFXTransform;
    private ParticleSystemRenderer _vfxRenderer;
    private ParticleSystem.MainModule _vfxMainModule;
    private ParticleSystem.EmissionModule _vfxEmissionModule;

    private bool _isVFXActive = false;
    private bool _vfxInitialized = false;
    private Coroutine _vfxStopCo;
    private Coroutine _vfxUpdateCo;

    // VFX 최적화 캐시
    private float _cachedDistance = 0f;
    private Vector3 _cachedStartPos = Vector3.zero;
    private Vector3 _cachedEndPos = Vector3.zero;
    private float _minVFXDistanceSqr; // 캐싱된 제곱 거리

    #endregion

    #region Cached WaitForSeconds (GC 최적화)

    private WaitForSeconds _waitVFXInterval;
    private WaitForSeconds _waitPostTransitionDelay;
    private WaitForSeconds _waitVFXStop;

    #endregion

    #region State Properties

    [Header("Debug Info - Read Only")]
    [SerializeField] private float _debugThrowDirY;
    public bool IsPickedUp { get; private set; }
    public bool IsMovingToAnchor { get; private set; }
    public bool IsBackView { get; private set; }
    public bool IsHolding { get; private set; }

    private bool _isBackViewTransitioning = false;

    #endregion

    #region Helper Properties (Null 체크 통합)

    private bool CanInteract => _target != null && HoldAnchor != null && !IsMovingToAnchor;
    private bool CanUseVFX => _lightVFX != null && _vfxRenderer != null;
    private bool HasValidRigidbody => _rb != null;
    private bool HasValidCollider => _physicsCollider != null;
    private bool HasValidAnimator => _animator != null;
    private bool IsHoldingOrMoving => IsHolding || IsMovingToAnchor;

    #endregion

    #region Input

    private InputActionAsset _inputActions;
    private InputAction _rcRepelTarget;
    private InputAction _lcRepelTarget;
    private bool _actionsHooked;

    #endregion

    #region Object/Physics

    private Rigidbody _rb;
    private Collider _physicsCollider;
    private Transform _originalParent;
    private Transform _target;
    private Transform _currentHoldAnchor;
    private Quaternion _cachedPickupRotation;
    private float _holdRadius; // 벽 클리핑 SphereCast 반지름
    private CharacterController _playerCC;
    private Vector3 _prevPlayerPos;
    private Vector3 _smoothedWallCorrection;

    #endregion

    #region Explosive

    private ExplosiveChecker _explosiveChecker;

    #endregion

    #region Coroutines

    private Coroutine _pickupCo;
    private Coroutine _anchorTransitionCo;

    #endregion

    #region Back view / UI

    private BackViewCameraController _backViewCameraController;
    private GameObject _aimReticle;
    private StarterAssets.ThirdPersonController _thirdPersonController;
    private StarterAssets.StarterAssetsInputs _starterAssetsInputs;

    #endregion

    #region Animator cache

    private int _upperBodyLayerIndex = -1;

    #endregion

    #region InputMode 감지

    private InputMode _lastInputMode = InputMode.Player;

    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        base.Start();

        CacheWaitForSeconds();
        SetupComponents();
        CacheAnimatorLayers();

        MagneticManager.Instance?.RegisterAnchor(this);
        _currentHoldAnchor = HoldAnchor;

        InitTrajectoryLine();
        FindLightVFX();
    }

    public override void InitializeFromManager(GameObject player, PlayerInput playerInput, Transform holdAnchor)
    {
        base.InitializeFromManager(player, playerInput, holdAnchor);
        FindBackViewComponents(player);
        _currentHoldAnchor = holdAnchor;

        // Manager 초기화 후 VFX 다시 탐색 (아직 안 했으면)
        if (!_vfxInitialized)
        {
            FindLightVFX();
        }

        // Start() 시점엔 MagneticManager.Instance가 null이어서 폴백 사용됨
        // Manager 준비 완료 후 실제 머티리얼로 교체
        RefreshEndMarkerMaterial();
    }

    private void RefreshEndMarkerMaterial()
    {
        if (_endMarker == null) return;
        Material mat = BuildGhostMaterial();
        if (mat == null) return;

        foreach (var r in _endMarker.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = mat;
    }

    private void OnEnable()
    {
        if (_inputActions != null)
            HookInputActions();

        EventBroker.Instance?.Subscribe("DeathEffect/ScreenBlack", OnReset);
    }

    protected override void SetInputActions(InputActionAsset actions)
    {
        _inputActions = actions;

        if (isActiveAndEnabled && _inputActions != null)
            HookInputActions();
    }

    private void Update()
    {
        // 아무것도 안 들고 있으면 체크할 필요 없음
        if (!IsHoldingOrMoving) return;

        if (IsBackView && IsHolding)
            UpdateTrajectoryPreview();

        if (InputModeManager.Instance == null) return;

        InputMode currentMode = InputModeManager.GetCurrentMode();

        // UI 모드 전환 시 자동 Drop
        if (_lastInputMode != InputMode.UI && currentMode == InputMode.UI)
        {
            if (IsHolding && !IsMovingToAnchor)
            {
                DropObject();
            }
        }

        _lastInputMode = currentMode;
    }

    private void LateUpdate()
    {
        if (IsHolding && !IsMovingToAnchor && !_isBackViewTransitioning && !IsBackView)
            ClampPositionToWall();

        if (_playerCC != null)
            _prevPlayerPos = _playerCC.transform.position;
    }

    private void OnDisable()
    {
        UnhookInputActions();
        StopAndNullCoroutine(ref _anchorTransitionCo);
        _isBackViewTransitioning = false;
        if (IsBackView) SetBackView(false);

        EventBroker.Instance?.Unsubscribe("DeathEffect/ScreenBlack", OnReset);
    }

    private void OnReset()
    {
        _hasExploded = false;
    }

    protected override void OnDestroy()
    {
        UnhookInputActions();

        StopAndNullCoroutine(ref _pickupCo);
        StopAndNullCoroutine(ref _anchorTransitionCo);
        StopAndNullCoroutine(ref _vfxStopCo);
        StopAndNullCoroutine(ref _vfxUpdateCo);

        _isVFXActive = false;

        if (IsHoldingOrMoving)
            DropObject();

        if (IsBackView)
            SetBackView(false);
        
        if (_starterAssetsInputs != null)
            _starterAssetsInputs.onMoveInput -= OnMoveInputDuringBackView;

        base.OnDestroy();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// WaitForSeconds 인스턴스 캐싱 (GC 최적화)
    /// </summary>
    private void CacheWaitForSeconds()
    {
        _waitVFXInterval = new WaitForSeconds(_vfxUpdateInterval);
        _waitPostTransitionDelay = new WaitForSeconds(_postTransitionDelay);
        _waitVFXStop = new WaitForSeconds(_throwVFXDuration);
        _minVFXDistanceSqr = _minVFXDistance * _minVFXDistance;
    }

    private void SetupComponents()
    {
        _target = FindRigidbodyParent(transform);

        if (_target == null)
            _target = transform.parent ?? transform;

        if (_target == null) return;

        _originalParent = _target.parent;
        _target.TryGetComponent(out _rb);

        // 물리 콜라이더 찾기
        var colliders = _target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!colliders[i].isTrigger)
            {
                _physicsCollider = colliders[i];
                break;
            }
        }

        // ExplosiveChecker 찾기 (선택사항)
        _target.TryGetComponent(out _explosiveChecker);

        if (_explosiveChecker != null && DebugMode)
        {
            DebugLogger.Log($"[LightAnchor] ExplosiveChecker found on {_target.name}");
        }

        // Rigidbody 최적화
        OptimizeRigidbodySettings();

        // 벽 클리핑 방지용 반지름 계산
        CacheHoldRadius();

        // Inspector 미설정 시 "Walls" 레이어 자동 할당
        if (_wallLayerMask == 0)
            _wallLayerMask = LayerMask.GetMask("Walls");

        CacheTargetRenderers();
    }

    private void CacheTargetRenderers()
    {
        if (_target == null) return;

        _targetRenderers = _target.GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_targetRenderers.Length];

        for (int i = 0; i < _targetRenderers.Length; i++)
        {
            var mat = _targetRenderers[i].sharedMaterial;
            if (mat != null)
                _originalColors[i] = mat.HasProperty("_BaseColor")
                    ? mat.GetColor("_BaseColor")
                    : mat.color;
        }
    }

    /// <summary>
    /// Rigidbody 설정 최적화
    /// </summary>
    private void OptimizeRigidbodySettings()
    {
        if (!HasValidRigidbody) return;

        // 연속 충돌 감지 (빠른 물체에 필수)
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 드래그 (공기 저항)
        _rb.linearDamping = 0.05f;
        _rb.angularDamping = 0.05f;
    }

    private void CacheHoldRadius()
    {
        if (_wallCheckRadiusOverride > 0f)
        {
            _holdRadius = _wallCheckRadiusOverride;
            return;
        }

        if (_physicsCollider is SphereCollider sphere)
        {
            _holdRadius = sphere.radius * Mathf.Max(
                _target != null ? _target.lossyScale.x : 1f,
                _target != null ? _target.lossyScale.y : 1f,
                _target != null ? _target.lossyScale.z : 1f);
        }
        else if (_physicsCollider is BoxCollider box)
        {
            Vector3 worldSize = _target != null
                ? Vector3.Scale(box.size, _target.lossyScale)
                : box.size;
            _holdRadius = Mathf.Max(worldSize.x, worldSize.y, worldSize.z) * 0.5f;
        }
        else
        {
            _holdRadius = 0.3f;
        }
    }

    private void CacheAnimatorLayers()
    {
        if (!HasValidAnimator) return;
        _upperBodyLayerIndex = _animator.GetLayerIndex("UpperBody");
    }

    private Transform FindRigidbodyParent(Transform current)
    {
        const int MAX_DEPTH = 10;
        var t = current;

        for (int i = 0; i < MAX_DEPTH && t != null; i++)
        {
            if (t.TryGetComponent<Rigidbody>(out _))
                return t;
            t = t.parent;
        }

        return null;
    }

    private void FindBackViewComponents(GameObject player)
    {
        if (player == null) return;

        // Inspector 할당 우선
        if (_backViewCameraControllerRef != null)
        {
            _backViewCameraController = _backViewCameraControllerRef;
        }
        else
        {
            // fallback: 첫 번째 것 사용
            _backViewCameraController = player.GetComponentInChildren<BackViewCameraController>(true);
        }

        player.TryGetComponent(out _thirdPersonController);
        player.TryGetComponent(out _starterAssetsInputs);
        player.TryGetComponent(out _playerCC);

        var canvas = player.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            var reticle = canvas.transform.Find("AimReticle") ?? canvas.transform.Find("Reticle");
            if (reticle != null)
            {
                _aimReticle = reticle.gameObject;
                _aimReticle.SetActive(false);
            }
        }
    }

    private void InitTrajectoryLine()
    {
        if (_trajectoryLine == null)
        {
            var go = new GameObject("TrajectoryLine");
            go.transform.SetParent(transform);
            _trajectoryLine = go.AddComponent<LineRenderer>();
        }

        _trajectoryLine.useWorldSpace = true;
        _trajectoryLine.startWidth = _trajectoryWidth;
        _trajectoryLine.endWidth = _trajectoryWidth;
        _trajectoryLine.numCapVertices = 4;
        _trajectoryLine.startColor = _trajectoryStartColor;
        _trajectoryLine.endColor = _trajectoryEndColor;
        _trajectoryLine.textureMode = LineTextureMode.Tile;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = CreateDashTexture();
        mat.mainTextureScale = new Vector2(_trajectoryDashTiling, 1f);
        _trajectoryLine.material = mat;

        _trajectoryLine.positionCount = 0;
        _trajectoryLine.gameObject.SetActive(false);

        _trajectoryCollisionMask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Trigger"));

        InitEndMarker();
    }

    private void InitEndMarker()
    {
        if (_trajectoryEndMarkerPrefab != null)
        {
            _endMarker = Instantiate(_trajectoryEndMarkerPrefab, transform);
        }
        else if (_target != null)
        {
            // _target 메시를 그대로 복제해서 고스트 마커로 사용
            _endMarker = new GameObject("TrajectoryEndMarker");
            _endMarker.transform.SetParent(transform, false);

            MeshFilter[] srcFilters = _target.GetComponentsInChildren<MeshFilter>(true);
            Material ghostMat = BuildGhostMaterial();

            foreach (var srcMF in srcFilters)
            {
                if (srcMF.sharedMesh == null) continue;

                var child = new GameObject(srcMF.name);
                child.transform.SetParent(_endMarker.transform, false);

                // 원본 로컬 TRS 복사 (루트 기준 상대 위치 유지)
                Transform srcT = srcMF.transform;
                Transform rootT = _target;
                child.transform.localPosition = rootT.InverseTransformPoint(srcT.position);
                child.transform.localRotation = Quaternion.Inverse(rootT.rotation) * srcT.rotation;
                child.transform.localScale = new Vector3(
                    srcT.lossyScale.x / (rootT.lossyScale.x > 0.001f ? rootT.lossyScale.x : 1f),
                    srcT.lossyScale.y / (rootT.lossyScale.y > 0.001f ? rootT.lossyScale.y : 1f),
                    srcT.lossyScale.z / (rootT.lossyScale.z > 0.001f ? rootT.lossyScale.z : 1f));

                var mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = srcMF.sharedMesh;

                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterial = ghostMat;
            }

            // 스케일은 _target 실제 월드 스케일 그대로
            _endMarker.transform.localScale = _target.lossyScale;
        }

        if (_endMarker != null)
            _endMarker.SetActive(false);
    }

    private Material BuildGhostMaterial()
    {
        Material src = MagneticManager.Instance?.GetTrajectoryGhostMaterial();
        if (src != null)
            return new Material(src);

        // MagneticManager에 머티리얼 미할당 시 기본 반투명 폴백
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        return mat;
    }

    private Texture2D CreateDashTexture()
    {
        const int w = 64, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        const int dashEnd = (int)(w * 0.5f);   // 앞 50% = 대시
        const int fadeWidth = 3;               // 앞뒤 페이드 픽셀

        for (int x = 0; x < w; x++)
        {
            float alpha;
            if (x <= fadeWidth)
                alpha = x / (float)fadeWidth;
            else if (x < dashEnd - fadeWidth)
                alpha = 1f;
            else if (x < dashEnd)
                alpha = (dashEnd - x) / (float)fadeWidth;
            else
                alpha = 0f;

            var c = new Color(1f, 1f, 1f, alpha);
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private void HideTrajectory()
    {
        if (_trajectoryLine == null) return;
        _trajectoryLine.positionCount = 0;
        _trajectoryLine.gameObject.SetActive(false);
        if (_endMarker != null) _endMarker.SetActive(false);
    }

    private void UpdateTrajectoryPreview()
    {
        if (_trajectoryLine == null || _target == null) return;

        Vector3 throwDir = CalculateThrowDirection();

        // _minThrowDirY 클램핑 전 원시 Y값 — 위/아래를 제대로 구분
        Camera _trajCam = MagneticManager.Instance?.GetPlayerCamera() ?? Camera.main;
        float rawDirY = (_trajCam != null && Mouse.current != null)
            ? _trajCam.ScreenPointToRay(Mouse.current.position.ReadValue()).direction.y
            : throwDir.y;
        _debugThrowDirY = rawDirY;

        // 박스 상단 위에서 시작 → BackView에서 박스에 가려지지 않음
        float adjustedDirY = rawDirY - _minThrowDirY;
        float upBias = Mathf.Pow(Mathf.InverseLerp(0.08f, 0.2f, adjustedDirY), 0.5f);
        Vector3 startPos = _target.position
            + Vector3.up * (_holdRadius + upBias * _trajectoryUpOffsetMax);
        Vector3 velocity = throwDir * ThrowForce;

        _trajectoryLine.positionCount = _trajectorySegments;

        // 마커 위치는 시각 보정 없이 실제 던지기 원점(_target.position)에서 계산
        Vector3 physicsStartPos = _target.position;
        Vector3 endPoint = Vector3.zero;
        bool hasEndPoint = false;

        for (int i = 0; i < _trajectorySegments; i++)
        {
            float t = i * _trajectoryTimeStep;
            // LineRenderer는 시각 보정 startPos 사용
            Vector3 visualPos = startPos + velocity * t + 0.5f * Physics.gravity * t * t;
            _trajectoryLine.SetPosition(i, visualPos);

            // 착탄 지점은 물리 원점 기준
            Vector3 physicsPos = physicsStartPos + velocity * t + 0.5f * Physics.gravity * t * t;

            if (i > 0)
            {
                Vector3 prevPhysics = physicsStartPos + velocity * ((i - 1) * _trajectoryTimeStep) + 0.5f * Physics.gravity * ((i - 1) * _trajectoryTimeStep) * ((i - 1) * _trajectoryTimeStep);
                if (Physics.Linecast(prevPhysics, physicsPos, out RaycastHit hit, _trajectoryCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    _trajectoryLine.positionCount = i + 1;
                    _trajectoryLine.SetPosition(i, visualPos); // 선 끝은 시각 위치 유지
                    endPoint = hit.point + hit.normal * _holdRadius;
                    hasEndPoint = true;
                    break;
                }
            }

            if (i == _trajectorySegments - 1)
            {
                endPoint = physicsPos;
                hasEndPoint = true;
            }
        }

        // 끝 지점 마커 배치
        if (_endMarker != null)
        {
            if (hasEndPoint)
            {
                _endMarker.SetActive(true);
                _endMarker.transform.position = endPoint;
                _endMarker.transform.rotation = _target.rotation;
            }
            else
            {
                _endMarker.SetActive(false);
            }
        }

        // 위쪽으로 쏠 때 frustum culling으로 선이 사라지는 현상 방지
        _trajectoryLine.bounds = new Bounds(startPos, Vector3.one * 500f);
    }

    /// <summary>
    /// Player 하위 HoldAnchor에서 "LightVFX" 이름의 ParticleSystem 찾기
    /// MagneticManager가 준비 안됐으면 나중에 다시 시도
    /// </summary>
    private void FindLightVFX()
    {
        // 이미 찾았으면 스킵
        if (_lightVFX != null) return;

        // MagneticManager가 준비 안됐으면 나중에 다시 시도
        if (MagneticManager.Instance == null) return;

        Transform holdAnchor = MagneticManager.Instance.GetHoldAnchorTransform();
        Transform holdAnchorAim = MagneticManager.Instance.GetHoldAnchorAimTransform();

        // 둘 다 없으면 나중에 다시 시도
        if (holdAnchor == null && holdAnchorAim == null) return;

        // HoldAnchor 하위에서 "LightVFX" 검색
        if (holdAnchor != null)
        {
            Transform vfxTransform = holdAnchor.Find("LightVFX");
            if (vfxTransform != null)
            {
                vfxTransform.TryGetComponent(out _lightVFX);
            }
        }

        // HoldAnchorAim 하위에서도 검색 (없으면)
        if (holdAnchorAim != null && _lightVFX == null)
        {
            Transform vfxTransform = holdAnchorAim.Find("LightVFX");
            if (vfxTransform != null)
            {
                vfxTransform.TryGetComponent(out _lightVFX);
            }
        }

        // 실제로 찾았을 때만 초기화 완료 처리
        if (_lightVFX != null)
        {
            _vfxInitialized = true;
            CacheVFXComponents();
        }
        // 못 찾았어도 Anchor는 있었으니 더 이상 시도 안 함
        else if (holdAnchor != null || holdAnchorAim != null)
        {
            _vfxInitialized = true;
            DebugLogger.LogWarning($"[LightAnchor] {gameObject.name}: LightVFX를 찾을 수 없습니다. VFX 기능 비활성화.");
        }
        // holdAnchor도 없으면 _vfxInitialized = false 유지 → 나중에 재시도
    }

    /// <summary>
    /// VFX 컴포넌트 캐싱
    /// </summary>
    private void CacheVFXComponents()
    {
        _lightVFXTransform = _lightVFX.transform;
        _vfxMainModule = _lightVFX.main;
        _vfxEmissionModule = _lightVFX.emission;
        _lightVFX.TryGetComponent(out _vfxRenderer);

        // 초기 상태: GameObject는 활성화, Emission만 비활성화
        _lightVFX.gameObject.SetActive(true);
        _vfxEmissionModule.enabled = false;
    }

    #endregion

    #region VFX Control

    /// <summary>
    /// VFX Transform 실시간 업데이트 코루틴 (최적화)
    /// </summary>
    private IEnumerator UpdateVFXTransformCoroutine()
    {
        while (_isVFXActive)
        {
            if (_lightVFXTransform != null && _target != null && _vfxRenderer != null)
            {
                UpdateVFXTransformImmediate();
            }

            yield return _waitVFXInterval; // 캐싱된 WaitForSeconds 사용
        }

        _vfxUpdateCo = null;
    }

    /// <summary>
    /// VFX Transform 즉시 업데이트 (sqrMagnitude 최적화)
    /// </summary>
    private void UpdateVFXTransformImmediate()
    {
        if (!CanUseVFX || _target == null) return;

        // 시작점: 현재 HoldAnchor
        Vector3 startPos = _currentHoldAnchor != null
            ? _currentHoldAnchor.position
            : transform.position;

        // 끝점: 이 LightAnchor의 위치
        Vector3 endPos = transform.position;

        // sqrMagnitude로 거리 체크 (sqrt 연산 제거)
        Vector3 delta = endPos - startPos;
        float sqrDistance = delta.sqrMagnitude;

        // 최소 거리 체크
        if (sqrDistance <= _minVFXDistanceSqr)
        {
            if (_vfxEmissionModule.enabled)
                _vfxEmissionModule.enabled = false;
            return;
        }

        // 거리가 충분하면 emission 켜기
        if (!_vfxEmissionModule.enabled)
            _vfxEmissionModule.enabled = true;

        // 실제 거리 계산 (필요할 때만)
        float distance = Mathf.Sqrt(sqrDistance);

        // 변화 감지 (sqrMagnitude 사용)
        bool needsUpdate = false;

        if (Mathf.Abs(distance - _cachedDistance) > DISTANCE_CHANGE_THRESHOLD)
        {
            needsUpdate = true;
        }
        else
        {
            float sqrStartDelta = (_cachedStartPos - startPos).sqrMagnitude;
            float sqrEndDelta = (_cachedEndPos - endPos).sqrMagnitude;

            if (sqrStartDelta > SQR_POSITION_THRESHOLD || sqrEndDelta > SQR_POSITION_THRESHOLD)
            {
                needsUpdate = true;
            }
        }

        if (!needsUpdate) return;

        // 캐시 업데이트
        _cachedDistance = distance;
        _cachedStartPos = startPos;
        _cachedEndPos = endPos;

        // VFX를 시작점에 배치
        _lightVFXTransform.position = startPos;

        // VFX가 끝점을 향하도록 회전 + X축 90도 회전
        if (sqrDistance > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(delta);
            _lightVFXTransform.rotation = lookRotation * Quaternion.Euler(90f, 0f, 0f);
        }

        // VFX 길이를 거리에 맞춰 조정
        _vfxRenderer.lengthScale = distance * _baseLengthScale;
    }

    /// <summary>
    /// VFX 시작
    /// </summary>
    private void StartVFX()
    {
        if (!CanUseVFX) return;

        StopAndNullCoroutine(ref _vfxStopCo);
        StopAndNullCoroutine(ref _vfxUpdateCo);

        // 캐시 초기화
        ResetVFXCache();

        if (!_lightVFX.isPlaying)
        {
            _lightVFX.Play();
        }

        _isVFXActive = true;

        // 초기 위치/회전 설정 및 거리 체크
        UpdateVFXTransformImmediate();

        // 코루틴으로 주기적 업데이트 시작
        _vfxUpdateCo = StartCoroutine(UpdateVFXTransformCoroutine());
    }

    /// <summary>
    /// VFX 즉시 중지
    /// </summary>
    private void StopVFX()
    {
        if (!CanUseVFX) return;

        StopAndNullCoroutine(ref _vfxStopCo);
        StopAndNullCoroutine(ref _vfxUpdateCo);

        // Emission 비활성화 + 기존 파티클 정리
        _vfxEmissionModule.enabled = false;
        _lightVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _isVFXActive = false;
        ResetVFXCache();
    }

    /// <summary>
    /// VFX 지연 중지 (던지기용)
    /// </summary>
    private IEnumerator StopVFXDelayed()
    {
        yield return _waitVFXStop; // 캐싱된 WaitForSeconds 사용
        StopVFX();
        _vfxStopCo = null;
    }

    /// <summary>
    /// VFX 캐시 초기화
    /// </summary>
    private void ResetVFXCache()
    {
        _cachedDistance = 0f;
        _cachedStartPos = Vector3.zero;
        _cachedEndPos = Vector3.zero;
    }

    #endregion

    #region Input Action Hooks

    private void HookInputActions()
    {
        if (_actionsHooked || _inputActions == null) return;

        _rcRepelTarget = _inputActions.FindAction("RepelTarget/RightClick");
        _lcRepelTarget = _inputActions.FindAction("RepelTarget/LeftClick");

        if (_rcRepelTarget != null) _rcRepelTarget.started += OnRightClick;
        if (_lcRepelTarget != null) _lcRepelTarget.started += OnLeftClick;

        _actionsHooked = true;
    }

    private void UnhookInputActions()
    {
        if (!_actionsHooked) return;

        if (_rcRepelTarget != null) _rcRepelTarget.started -= OnRightClick;
        if (_lcRepelTarget != null) _lcRepelTarget.started -= OnLeftClick;

        _rcRepelTarget = null;
        _lcRepelTarget = null;
        _actionsHooked = false;
    }

    private void OnRightClick(InputAction.CallbackContext ctx)
    {
        if (InputModeManager.GetCurrentMode() != InputMode.RepelTarget) return;
        if (_isBackViewTransitioning) return;
        if (IsHolding) ToggleBackView();
    }

    private void OnLeftClick(InputAction.CallbackContext ctx)
    {
        if (InputModeManager.GetCurrentMode() != InputMode.RepelTarget) return;
        if (_isBackViewTransitioning) return;
        if (IsHolding && !IsMovingToAnchor)
        {
            InputModeManager.Instance?.PlayImpact();
            ThrowObject();
        }
    }

    #endregion

    #region Magnetic Interaction

    protected override bool IsActivatedByManager()
    {
        if (IsHoldingOrMoving) return true;
        return MagneticManager.Instance?.IsAnchorActivated(this) ?? false;
    }

    protected override void StartMagneticInteraction()
    {
        // 감지 차단 중이면 무조건 차단
        if (MagneticManager.Instance != null && MagneticManager.Instance.IsDetectionBlocked)
        {
            IsInteracting = false;
            return;
        }

        // 다른 앵커가 hold 중이면 interaction 차단
        if (MagneticManager.Instance != null && MagneticManager.Instance.IsAnyAnchorHolding())
        {
            if (!IsHolding)
            {
                IsInteracting = false;
                return;
            }
        }

        if (!CanInteract && !IsHolding)
        {
            IsInteracting = false;
            return;
        }

        if (HasValidAnimator && _animator.GetBool(HashIsPushing))
        {
            IsInteracting = false;
            return;
        }

        if (!IsHolding)
            PickupObject();
        else
            DropObject();
    }

    protected override void EndMagneticInteraction()
    {
        IsInteracting = false;

        if (IsHolding)
        {
            DropObject();

            MagneticManager.Instance?.BlockDetectionTemporarily();
        }

        base.EndMagneticInteraction();
    }

    #endregion

    #region Pickup

    private void PickupObject()
    {
        if (IsHolding || !CanInteract) return;

        // 픽업 시작 시점에 MagneticManager에 hold 상태 등록
        MagneticManager.Instance?.SetHoldingAnchor(this);

        IsInteracting = true;
        IsMovingToAnchor = true;

        SetUpperBodyLayerWeight(1f);

        if (HasValidAnimator)
            _animator.SetBool(HashIsPulling, true);

        if (HasValidRigidbody)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        StartVFX();

        SoundManager.Instance?.PlaySFX(14);

        // Collider 즉시 비활성화 (플레이어 밀림 방지)
        if (HasValidCollider)
        {
            _physicsCollider.enabled = false;
        }

        // 픽업 시 폭발력 적용
        if (_explodeOnPickup && !_hasExploded && _rigidbodyGroupController != null)
        {
            _hasExploded = true;
            _rigidbodyGroupController.ExplodeFromPoint(transform.position, _explosionForce, _explosionRadius);
            // EventBroker.Instance?.Publish("EnableGravity");
        }
        
        StopAndNullCoroutine(ref _pickupCo);
        _pickupCo = StartCoroutine(MoveToHoldAnchor());

        GuideUIManager.Instance?.OpenButtonGuidePanel(false);
    }

    private void SetUpperBodyLayerWeight(float weight)
    {
        if (!HasValidAnimator || _upperBodyLayerIndex < 0) return;
        _animator.SetLayerWeight(_upperBodyLayerIndex, weight);
    }

    private IEnumerator MoveToHoldAnchor()
    {
        if (_target == null || HoldAnchor == null)
        {
            OnPickupFailed();
            yield break;
        }

        Vector3 startPos = _target.position;
        Quaternion startRot = _target.rotation;
        Vector3 targetPos = HoldAnchor.position + HoldAnchor.forward * _holdPadding;

        // 픽업 시에만 회전 계산 & 캐싱
        Quaternion targetRot = CalculateOptimalRotation();
        _cachedPickupRotation = targetRot;

        float elapsed = 0f;

        while (elapsed < PICKUP_DURATION)
        {
            if (_target == null || HoldAnchor == null)
            {
                OnPickupFailed();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / PICKUP_DURATION);

            _target.position = Vector3.Lerp(startPos, targetPos, t);
            _target.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        if (_target == null || HoldAnchor == null)
        {
            OnPickupFailed();
            yield break;
        }

        _target.position = targetPos;
        _target.rotation = targetRot;

        CompletePickup();
    }

    /// <summary>
    /// 픽업 실패 시 상태 복구
    /// </summary>
    private void OnPickupFailed()
    {
        if (HasValidCollider)
            _physicsCollider.enabled = true;

        IsMovingToAnchor = false;
        IsInteracting = false;

        MagneticManager.Instance?.ClearHoldingAnchor(this);
    }

    /// <summary>
    /// BoxCollider 크기에 맞춰 최적 회전 계산
    /// </summary>
    private Quaternion CalculateOptimalRotation()
    {
        if (_preserveRotationOnPickup)
            return _target != null ? _target.rotation : Quaternion.identity;
        
        if (_physicsCollider is BoxCollider box && HoldAnchor != null && _target != null)
        {
            Vector3 worldSize = Vector3.Scale(box.size, _target.lossyScale);
            float y = (worldSize.x > worldSize.z) ? 0f : 90f;
            return Quaternion.Euler(0f, y, 0f);
        }
        return Quaternion.identity;
    }

    private void CompletePickup()
    {
        if (_target == null || HoldAnchor == null)
        {
            OnPickupFailed();
            return;
        }

        IsMovingToAnchor = false;
        IsPickedUp = true;
        IsHolding = true;
        _currentHoldAnchor = HoldAnchor;

        InputModeManager.Instance?.SwitchInputMode(InputMode.RepelTarget);

        Vector3 worldScale = _target.lossyScale;

        _cachedWorldScale = worldScale; // SetParent 호출 전 worldScale 캐싱
        _target.SetParent(HoldAnchor, true);
        _target.localPosition = Vector3.forward * _holdPadding;
        _target.localRotation = _cachedPickupRotation;

        Vector3 parentScale = HoldAnchor.lossyScale;
        _target.localScale = new Vector3(
            SafeDiv(worldScale.x, parentScale.x),
            SafeDiv(worldScale.y, parentScale.y),
            SafeDiv(worldScale.z, parentScale.z)
        );

        if (HasValidRigidbody)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        if (HasValidCollider)
        {
            _physicsCollider.enabled = false;
        }

        StopVFX();
    }

    private static float SafeDiv(float a, float b)
    {
        const float EPSILON = 0.001f;
        float absB = Mathf.Abs(b);
        return absB < EPSILON ? a / EPSILON : a / absB * Mathf.Sign(b);
    }

    #endregion

    #region Drop / Throw

    private void DropObject()
    {
        StopAndNullCoroutine(ref _pickupCo);
        StopAndNullCoroutine(ref _anchorTransitionCo);
        _isBackViewTransitioning = false;

        if (!IsHoldingOrMoving) return;

        // Hold 상태 해제
        IsInteracting = false;
        IsPickedUp = false;
        IsMovingToAnchor = false;
        IsHolding = false;

        MagneticManager.Instance?.ClearHoldingAnchor(this);
        ResetWallState();

        if (HasValidAnimator)
            _animator.SetBool(HashIsPulling, false);

        if (IsBackView)
            SetBackView(false);

        StopVFX();
        HideTrajectory();

        // UI 모드로 전환 추가
        if (InputModeManager.Instance != null)
        {
            InputMode currentMode = InputModeManager.GetCurrentMode();

            // UI 모드가 아닐 때만 Player 모드로 전환
            if (currentMode != InputMode.UI)
            {
                StartCoroutine(DelayedInputModeChange());
            }
        }
        else
        {
            StartCoroutine(DelayedInputModeChange());
        }

        if (_target == null) return;

        RestoreTargetParent();

        if (HasValidRigidbody)
        {
            // Continuous → non-kinematic 전환 시 이전 이동량이 속도로 상속되는 Unity 버그 방지
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (HasValidCollider)
        {
            _physicsCollider.enabled = true;
        }

        GuideUIManager.Instance?.CloseButtonGuidePanel();
    }
    
    // 죽을 때 외부에서 호출
    public void ForceDropOnDeath()
    {
        if (IsHoldingOrMoving)
            DropObject();
    }

    private void ThrowObject()
    {
        if (!IsHolding || _target == null)
        {
            IsInteracting = false;
            IsPickedUp = false;
            return;
        }

        // Hold 상태 해제
        IsInteracting = false;
        IsPickedUp = false;
        IsHolding = false;

        MagneticManager.Instance?.ClearHoldingAnchor(this);

        if (HasValidAnimator)
            _animator.SetTrigger(HashPushTrigger);

        HideTrajectory();
        StartVFX();

        SoundManager.Instance?.PlaySFX(15);

        _vfxStopCo = StartCoroutine(StopVFXDelayed());

        StartCoroutine(DelayedInputModeChange());

        RestoreTargetParent();

        float blockDuration = _backViewCameraController != null
            ? _backViewCameraController.GetTotalZoomDuration() + 0.5f
            : 1.0f;
        MagneticManager.Instance?.BlockDetectionTemporarily(blockDuration);
        
        if (HasValidRigidbody)
        {
            _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Vector3 throwDir = CalculateThrowDirection();
            _rb.AddForce(throwDir * ThrowForce, ForceMode.Impulse);
            
            // StorageObject면 착지 감지 재활성화
            var boxSound = _target.GetComponent<BoxCollisionSound>();
            if (boxSound != null) boxSound.ResetLure();

            // ExplosiveChecker가 있으면 폭발 활성화
            if (_explosiveChecker != null)
            {
                _explosiveChecker.ActivateExplosion();

                if (DebugMode)
                {
                    DebugLogger.Log($"[LightAnchor] Explosion activated for {_target.name}");
                }
            }
        }

        if (HasValidCollider)
        {
            _physicsCollider.enabled = true;
        }

        if (IsBackView && _backViewCameraController != null)
        {
            _backViewCameraController.PlayThrowSequence();
            StartCoroutine(DeactivateBackViewAfterThrow());
        }
        else if (IsBackView)
        {
            SetBackView(false);
        }

        GuideUIManager.Instance?.CloseButtonGuidePanel();
    }

    /// <summary>
    /// 타겟을 원래 부모로 복원
    /// </summary>
    private void RestoreTargetParent()
    {
        if (_target == null) return;

        Vector3 worldScale = _cachedWorldScale;
        _target.SetParent(_originalParent, true);

        if (_originalParent != null)
        {
            Vector3 parentScale = _originalParent.lossyScale;
            _target.localScale = new Vector3(
                SafeDiv(worldScale.x, parentScale.x),
                SafeDiv(worldScale.y, parentScale.y),
                SafeDiv(worldScale.z, parentScale.z)
            );
        }
        else
        {
            _target.localScale = worldScale;
        }
    }

    private IEnumerator DelayedInputModeChange()
    {
        yield return null;
        InputModeManager.Instance?.SwitchInputMode(InputMode.Player);
    }

    private Vector3 CalculateThrowDirection()
    {
        Camera cam = MagneticManager.Instance?.GetPlayerCamera() ?? Camera.main;
        Vector3 dir;

        if (cam != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            dir = cam.ScreenPointToRay(mousePos).direction;
        }
        else if (cam != null)
        {
            dir = cam.transform.forward;
        }
        else
        {
            dir = transform.forward;
        }

        dir.y = Mathf.Max(_minThrowDirY, dir.y);
        return dir.normalized;
    }

    private IEnumerator DeactivateBackViewAfterThrow()
    {
        float duration = _backViewCameraController != null
            ? _backViewCameraController.GetTotalZoomDuration()
            : 0.6f;

        yield return new WaitForSeconds(duration);
        SetBackView(false);
    }

    public override GameObject GetTargetParent()
    {
        return transform.parent?.parent?.gameObject; // box_card_5의 부모 = st_box_card_5
    }
    
    #endregion

    #region BackView Control & Anchor Transition

    private void ToggleBackView() => SetBackView(!IsBackView);

    private void SetBackView(bool on)
    {
        IsBackView = on;
        ResetWallState();
        SetTargetTransparent(on);
        
        if (on)
        {
            // 백뷰 진입 시 이동 입력 이벤트 구독
            if (_starterAssetsInputs != null)
                _starterAssetsInputs.onMoveInput += OnMoveInputDuringBackView;
        }
        else
        {
            // 백뷰 해제 시 구독 해제
            if (_starterAssetsInputs != null)
                _starterAssetsInputs.onMoveInput -= OnMoveInputDuringBackView;
        }

        if (_backViewCameraController != null)
        {
            _backViewCameraController.SetActive(on);
            _thirdPersonController?.SetRotateWithCamera(on);
        }

        // if (_aimReticle != null)
        //     _aimReticle.SetActive(on);

        if (_trajectoryLine != null)
        {
            _trajectoryLine.gameObject.SetActive(on);
            if (!on) _trajectoryLine.positionCount = 0;
        }

        if (_endMarker != null && !on)
            _endMarker.SetActive(false);

        // BackView 모드 전환에 따라 Anchor 전환
        if (IsHolding && _target != null)
        {
            Transform targetAnchor = on
                ? MagneticManager.Instance?.GetHoldAnchorAimTransform()
                : MagneticManager.Instance?.GetHoldAnchorTransform();

            if (targetAnchor != null && _currentHoldAnchor != targetAnchor)
            {
                TransitionToAnchor(targetAnchor);
            }
        }
    }
    
    private void OnMoveInputDuringBackView(Vector2 move)
    {
        if (move != Vector2.zero && IsBackView && !_isBackViewTransitioning)
            SetBackView(false);
    }

    private void TransitionToAnchor(Transform newAnchor)
    {
        ResetWallState();
        StopAndNullCoroutine(ref _anchorTransitionCo);
        _anchorTransitionCo = StartCoroutine(TransitionToAnchorCoroutine(newAnchor));
    }

    private IEnumerator TransitionToAnchorCoroutine(Transform newAnchor)
    {
        if (_target == null || newAnchor == null)
        {
            yield break;
        }

        _isBackViewTransitioning = true;

        Vector3 startPos = _target.position;
        Vector3 worldScale = _cachedWorldScale;

        float elapsed = 0f;

        while (elapsed < ANCHOR_TRANSITION_DURATION)
        {
            if (_target == null || newAnchor == null)
            {
                _isBackViewTransitioning = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / ANCHOR_TRANSITION_DURATION);

            Vector3 targetWorldPos = newAnchor.position + newAnchor.forward * _holdPadding;
            _target.position = Vector3.Lerp(startPos, targetWorldPos, t);

            yield return null;
        }

        _target.SetParent(newAnchor, true);
        _target.localPosition = Vector3.forward * _holdPadding;
        _target.localRotation = _cachedPickupRotation;

        Vector3 parentScale = newAnchor.lossyScale;
        _target.localScale = new Vector3(
            SafeDiv(worldScale.x, parentScale.x),
            SafeDiv(worldScale.y, parentScale.y),
            SafeDiv(worldScale.z, parentScale.z)
        );

        _currentHoldAnchor = newAnchor;

        yield return _waitPostTransitionDelay; // 캐싱된 WaitForSeconds 사용

        _isBackViewTransitioning = false;
        _anchorTransitionCo = null;
    }

    #endregion

    #region Wall Clipping Prevention

    private static readonly Collider[] _wallOverlapBuffer = new Collider[8];

    /// <summary>
    /// 박스 실제 콜라이더 형태로 ComputePenetration.
    /// LateUpdate 안에서만 enable/disable하므로 FixedUpdate에 영향 없음.
    /// </summary>
    private void ClampPositionToWall()
    {
        if (_currentHoldAnchor == null) return;
        if (_wallLayerMask == 0) return;
        if (_playerCC == null) return;
        if (_physicsCollider == null) return;

        Vector3 boxCenter = _currentHoldAnchor.position + _currentHoldAnchor.forward * _holdPadding;

        int count = Physics.OverlapSphereNonAlloc(
            boxCenter, _holdRadius + 0.1f,
            _wallOverlapBuffer, _wallLayerMask, QueryTriggerInteraction.Ignore);

        if (count == 0) return;

        // ComputePenetration은 enabled 상태여야 하므로 잠깐 켜기
        // LateUpdate 내 토글은 FixedUpdate(물리 시뮬레이션)에 영향 없음
        _physicsCollider.enabled = true;

        Vector3 totalCorrection = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            Collider wall = _wallOverlapBuffer[i];
            if (wall == null) continue;

            if (!Physics.ComputePenetration(
                _physicsCollider, _target.position, _target.rotation,
                wall, wall.transform.position, wall.transform.rotation,
                out Vector3 dir, out float dist))
                continue;

            if (dist <= _wallPenetrationThreshold) continue;

            totalCorrection += dir * dist;
        }

        _physicsCollider.enabled = false;

        totalCorrection.y = 0f;

        // 벽 폴리곤/엣지마다 ComputePenetration 방향이 미세하게 달라져 고주파 떨림 발생.
        // Lerp로 스무딩해 노이즈를 제거하되 응답성은 유지.
        _smoothedWallCorrection = Vector3.Lerp(_smoothedWallCorrection, totalCorrection, 0.15f);

        if (_smoothedWallCorrection == Vector3.zero) return;

        float frameMoved = (_playerCC.transform.position - _prevPlayerPos).magnitude;
        float maxCorrection = frameMoved + 0.01f;
        Vector3 correction = _smoothedWallCorrection.magnitude > maxCorrection
            ? _smoothedWallCorrection.normalized * maxCorrection
            : _smoothedWallCorrection;

        _playerCC.transform.position += correction;
    }

    private void ResetWallState()
    {
        _smoothedWallCorrection = Vector3.zero;
    }

    #endregion

    #region Aim Transparency

    private void SetTargetTransparent(bool transparent)
    {
        if (_targetRenderers == null || _isTransparent == transparent) return;
        _isTransparent = transparent;

        for (int i = 0; i < _targetRenderers.Length; i++)
        {
            var r = _targetRenderers[i];
            if (r == null) continue;

            // sharedMaterial 대신 instance material 사용 (런타임 변경)
            var mat = r.material;
            if (mat == null) continue;

            if (transparent)
            {
                ApplyTransparentMode(mat);
                Color c = _originalColors[i];
                c.a = _aimAlpha;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else mat.color = c;
            }
            else
            {
                ApplyOpaqueMode(mat);
                Color c = _originalColors[i];
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else mat.color = c;
            }
        }
    }

    // Standard Shader & URP Lit 공용 투명 모드 설정
    private static void ApplyTransparentMode(Material mat)
    {
        // URP Lit
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            // Standard Shader
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static void ApplyOpaqueMode(Material mat)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
        else
        {
            mat.SetFloat("_Mode", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 코루틴 중지 및 null 할당 헬퍼
    /// </summary>
    private void StopAndNullCoroutine(ref Coroutine coroutine)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }
    }

    #endregion
}