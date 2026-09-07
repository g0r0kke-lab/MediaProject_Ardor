using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
/// <summary>자기력 앵커 시스템의 앵커와 상호작용 오브젝트를 등록, 활성화하고 UI 피드백을 관리하는 싱글턴 매니저</summary>
public class MagneticManager : MonoBehaviour
{
    #region Singleton

    public static MagneticManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (_playerArmature == null || _playerArmature.gameObject == null)
        {
            StartCoroutine(InitializeAfterFrame());
        }
    }

    #endregion

    [Header("Scene Wiring (auto if empty)")] [SerializeField]
    private Transform _playerArmature;

    [Tooltip("평상시 물체를 들고 있는 위치")] public Transform HoldAnchor;

    [Tooltip("BackView 모드에서 조준할 때 물체 위치")] public Transform HoldAnchorAim;

    [SerializeField] private Camera _playerCamera;
    [SerializeField] private PlayerInput _playerInput;

    [Header("External Detectors")] public Transform PlayerCameraRoot;
    [SerializeField] private RayDetector _rayDetector;

    [Header("Ray Detector Settings")] [Tooltip("HoldAnchor를 레이 원점으로 쓸지 여부 (기본: 카메라)")]
    public bool UsePlayerCameraRoot = true;

    [Tooltip("자기 몸을 피하기 위한 전방 오프셋(m)")] [Range(0f, 1f)]
    public float RaySelfOffset = 0.25f;

    [Tooltip("Raycast에서 Player 레이어를 제외할지")]
    public bool ExcludePlayerLayer = true;

    [Tooltip("이 태그만 유효 대상으로 인정(비우면 전체 허용)")]
    public string RayRequiredTag = "";

    [Header("Interaction Object Recognition")] [Tooltip("상호작용 오브젝트로 인정할 태그(선택). 비우면 레이어로만 판정")]
    public string InteractionObjectTagName = "InteractionObject";

    [Header("Materials")] [Tooltip("HeavyAnchor 감지 시 적용할 Material")]
    public Material HeavyAnchorMaterial;

    [Tooltip("LightAnchor 감지 시 적용할 Material")]
    public Material LightAnchorMaterial;

    [Tooltip("LightAnchor 조준 시 착탄 지점 고스트 마커 머티리얼")]
    public Material TrajectoryGhostMaterial;

    public Material GetTrajectoryGhostMaterial() => TrajectoryGhostMaterial;

    [Header("UI")] public GameObject EKeyUIPrefab;

    [Header("UI Text Objects")] [Tooltip("'Press E (Heavy)' 텍스트 오브젝트 이름")]
    public string PressEHeavyObjectName = "Press E Heavy";

    [Tooltip("'Press E (Light)' 텍스트 오브젝트 이름")]
    public string PressELightObjectName = "Press E Light";

    [Tooltip("'Press E (Interaction)' 텍스트 오브젝트 이름")]
    public string PressEInteractionObjectName = "Press E Interaction";

    [Header("Detect UI")]
    [Tooltip("크로스헤어 Image 컴포넌트")]
    [SerializeField] private Image _detectUIImage;
    [Tooltip("레이 감지 중일 때 스프라이트")]
    [SerializeField] private Sprite _detectActiveSprite;
    [Tooltip("레이 미감지 시 스프라이트")]
    [SerializeField] private Sprite _detectInactiveSprite;

    [Header("UI Socket")] [SerializeField] private GameObject UISocketPrefab;
    [SerializeField] private float uiSocketLeftOffset = 0.35f;
    [SerializeField] private float uiSocketHeightOffset = 0.95f;
    [SerializeField] private float uiSocketForwardOffset = -2.0f;
    [SerializeField] private float nearUILeftOffset = 0.4f;
    [SerializeField] private float nearUIHeightOffset = 0.85f;
    [SerializeField] private float nearUIForwardOffset = 2.5f;
    [Tooltip("PlayerCrouchDeath.StandCameraRootY 와 동일한 값으로 설정")]
    [SerializeField] private float _standCameraRootY = 0.5f;
    
    private GameObject _uiSocketInstance;
    private bool _wasTimelinePlaying = false;

    [Header("UI Position Settings")] [Tooltip("오브젝트 중심에서 UI가 뜨는 Y 오프셋")]
    public float UIHeightOffset = 0.2f;

    [Header("Execution Safety")] [Tooltip("매 프레임 RayDetector 참조를 재확인(씬 전환/재생성 대응)")]
    public bool ReacquireDetectorEveryFrame = true;

    [Header("Debug Settings")] public bool DebugMode = true;
    public bool ShowGizmos = true;
    public bool ShowAllAnchors = true;
    public bool ShowInteractionLines = true;
    public bool ShowAnchorStates = true;

    [Header("Detection Block Settings")] [Tooltip("R키 drop 후 다른 앵커 감지 차단 시간")] [SerializeField]
    private float _detectionBlockDuration = 0.5f;

    [Header("Debug - Read Only")] public MagneticAnchor CurrentActiveAnchor;
    public MagneticAnchor CurrentInteractingAnchor;
    public MagneticAnchor CurrentHoldingAnchor; // 현재 hold 중인 앵커
    public GameObject CurrentActiveInteractionObject;
    public int TotalAnchorsCount;
    public int TotalInteractionObjectsCount;
    public float PlayerWeight = 70f;
    public bool IsDetectionBlocked = false; // 감지 차단 상태 표시
    public float DetectionBlockTimeRemaining = 0f; // 남은 차단 시간
    public MagneticAnchor LastInputProcessedAnchor = null; // 마지막으로 입력 처리한 앵커
    public int LastInputProcessedFrame = -1; // 마지막 입력 처리 프레임

    private readonly Dictionary<GameObject, MagneticAnchor> _allAnchors = new();
    private readonly List<GameObject> _allInteractionObjects = new();
    private readonly Dictionary<GameObject, GameObject> _anchorUIs = new();
    private readonly Dictionary<GameObject, GameObject> _interactionObjectUIs = new();
    private readonly Dictionary<MeshRenderer, Material[]> _originalMaterials = new();

    private MagneticAnchor _lastActiveAnchor;
    private GameObject _lastActiveInteractionObject;
    private bool _initialized = false;
    public bool IsInitialized => _initialized;

    private bool _detectUIForceHidden = false;

    [Header("Collision-Based Interaction")]
    private HashSet<GameObject> _collisionActiveObjects = new HashSet<GameObject>(); // 충돌 중인 오브젝트들

    // R키 drop 직후 다른 앵커 활성화 방지
    private bool _detectionBlocked = false;
    private Coroutine _blockDetectionCo;

    private void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    private IEnumerator InitializeAfterFrame()
    {
        yield return new WaitForEndOfFrame();

        if (_playerArmature == null)
        {
            FindPlayerArmature();
            AutoWireSiblingsAndComponents();
        }

        FindAllAnchors();

        foreach (var (parent, anchor) in _allAnchors)
        {
            if (anchor != null)
                anchor.InitializeFromManager(_playerArmature.gameObject, _playerInput, HoldAnchor);
        }

        FindAllInteractionObjects();
        EnsureRayDetector();
        ConfigureRayDetector();

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        if (_playerArmature == null || _playerArmature.gameObject == null)
        {
            StartCoroutine(InitializeAfterFrame());
        }

        if (ReacquireDetectorEveryFrame && (_rayDetector == null || _rayDetector.gameObject == null))
        {
            EnsureRayDetector();
            ConfigureRayDetector();
        }
    }

    private void LateUpdate()
    {
        if (!_initialized) return;
        if (_playerArmature == null || _playerArmature.gameObject == null) return;

        // null 참조 정리 (파괴된 오브젝트 제거)
        _collisionActiveObjects.RemoveWhere(obj => obj == null);

        UpdateUISocketPosition();

        // 현재 상호작용 중인 앵커 찾기
        CurrentInteractingAnchor = null;
        foreach (var (parent, anchor) in _allAnchors)
        {
            if (anchor != null && anchor.IsCurrentlyInteracting())
            {
                // HeavyAnchor 부착 완료 상태면 interacting으로 취급 안 함
                if (anchor is HeavyAnchor heavy && heavy.IsAttachComplete)
                    continue;
            
                CurrentInteractingAnchor = anchor;
                break;
            }
        }

        _rayDetector.IgnoreParent = CurrentHoldingAnchor != null
            ? CurrentHoldingAnchor.GetTargetParent()
            : null;

        MagneticAnchor nextActiveAnchor = CurrentInteractingAnchor;
        GameObject nextActiveInteractionObject = null;

        // LightAnchor를 손에 들고 있는 중에만 감지 전체 차단 (HeavyAnchor 부착 중에는 감지 유지)
        if (CurrentHoldingAnchor == null || CurrentHoldingAnchor is HeavyAnchor)
        {
            // 1순위: 충돌 중인 오브젝트 (차단 중에는 건너뜀)
            if (_collisionActiveObjects.Count > 0 && !_detectionBlocked)
            {
                float closestDist = float.MaxValue;
                foreach (var obj in _collisionActiveObjects)
                {
                    if (obj == null) continue;
                    float dist = Vector3.Distance(_playerArmature.position, obj.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        nextActiveInteractionObject = obj;
                    }
                }

                // 예외: 2순위 레이캐스트가 앵커를 잡으면 ray 우선
                if (_rayDetector != null && _rayDetector.CurrentRaycastHitAnchor != null)
                {
                    nextActiveAnchor = _rayDetector.CurrentRaycastHitAnchor;
                    nextActiveInteractionObject = null;
                }
            }
            // 2순위 & 3순위: 감지 차단 체크 (차단 시 둘 다 건너뜀)
            else if (!_detectionBlocked && nextActiveAnchor == null && _rayDetector != null)
            {
                // 2순위: 카메라 정중앙 레이캐스트 (앵커만 감지)
                if (_rayDetector.CurrentRaycastHitAnchor != null)
                    nextActiveAnchor = _rayDetector.CurrentRaycastHitAnchor;

                // 3순위: 실린더 (2순위에서 아무것도 없을 때만, 더 가까운 것 하나만)
                if (nextActiveAnchor == null && nextActiveInteractionObject == null)
                {
                    var hitAnchor = _rayDetector.CurrentHitAnchor;
                    var closestObj = _rayDetector.CurrentClosestObject;
                    bool hasAnchor = hitAnchor != null;
                    bool hasObj = closestObj != null && IsInteractionObject(closestObj);

                    if (hasAnchor && hasObj)
                    {
                        if (_rayDetector.ClosestAnchorDistance <= _rayDetector.ClosestObjectDistance)
                            nextActiveAnchor = hitAnchor;
                        else
                            nextActiveInteractionObject = closestObj;
                    }
                    else if (hasAnchor)
                        nextActiveAnchor = hitAnchor;
                    else if (hasObj)
                        nextActiveInteractionObject = closestObj;
                }
            }
        }

        // 상태 업데이트
        if (CurrentActiveAnchor != nextActiveAnchor)
        {
            _lastActiveAnchor = CurrentActiveAnchor;
            CurrentActiveAnchor = nextActiveAnchor;
            EventBroker.Instance?.Publish("ActiveAnchorChanged");
        }

        if (CurrentActiveInteractionObject != nextActiveInteractionObject)
        {
            _lastActiveInteractionObject = CurrentActiveInteractionObject;
            CurrentActiveInteractionObject = nextActiveInteractionObject;
            
            if (CurrentActiveInteractionObject != null)
                EventBroker.Instance?.Publish("InteractionObjectActivated", CurrentActiveInteractionObject);
        }

        UpdateVisualFeedback();
        UpdateDetectUI();
    }

    private void UpdateUISocketPosition()
    {
        if (_uiSocketInstance == null && UISocketPrefab != null)
        {
            _uiSocketInstance = Instantiate(UISocketPrefab);
            _uiSocketInstance.SetActive(false);
        }

        if (_uiSocketInstance == null || _playerArmature == null || _playerCamera == null) return;

        Vector3 cameraRight = _playerCamera.transform.right;
        Vector3 cameraForward = _playerCamera.transform.forward;
        Vector3 screenLeft = -cameraRight;

        // 카메라-플레이어 거리에 따라 오프셋 보간
        float camDist = Vector3.Distance(_playerCamera.transform.position, _playerArmature.position);
        float normalDist = 3.2f;
        // 타임라인 종료 후 카메라가 정상 거리로 돌아올 때까지 차단
        bool isTimelineActive = TimeLineManager.Instance != null && 
                                 (TimeLineManager.Instance.IsTimelinePlaying || TimeLineManager.Instance.IsTimelineTransitioning);

        float t = isTimelineActive ? 0f : Mathf.Clamp01(1f - (camDist / normalDist));
        
        float currentLeftOffset = Mathf.Lerp(uiSocketLeftOffset, nearUILeftOffset, t);
        float currentHeightOffset = Mathf.Lerp(uiSocketHeightOffset, nearUIHeightOffset, t);
        float currentForwardOffset = Mathf.Lerp(uiSocketForwardOffset, nearUIForwardOffset, t);
        
        // 크라우치 시 PlayerCameraRoot가 내려간 만큼 UI도 같이 내림
        float crouchYAdjust = 0f;
        if (PlayerCameraRoot != null)
            crouchYAdjust = PlayerCameraRoot.localPosition.y - _standCameraRootY;

        Vector3 targetPosition = _playerArmature.position
                                 + screenLeft * currentLeftOffset
                                 + Vector3.up * (currentHeightOffset + crouchYAdjust)
                                 + cameraForward * currentForwardOffset;
        
        _uiSocketInstance.transform.position = targetPosition;
    }

    private void FindPlayerArmature()
    {
        _playerArmature = null;

        var thirdPersonController = FindFirstObjectByType<StarterAssets.ThirdPersonController>();
        if (thirdPersonController != null)
        {
            _playerArmature = thirdPersonController.transform;
            return;
        }

        GameObject playerArmatureObj = GameObject.Find("PlayerArmature");
        if (playerArmatureObj != null)
        {
            _playerArmature = playerArmatureObj.transform;
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            _playerArmature = taggedPlayer.transform;
            return;
        }

        CharacterController controller = FindFirstObjectByType<CharacterController>();
        if (controller != null && (controller.name.Contains("Player") || controller.name.Contains("Armature")))
        {
            _playerArmature = controller.transform;
        }
    }

    private void AutoWireSiblingsAndComponents()
    {
        if (_playerArmature == null)
        {
            _playerArmature = transform;
        }

        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
            if (_playerCamera == null && _playerArmature != null)
            {
                _playerCamera = _playerArmature.GetComponentInChildren<Camera>(true);
            }
        }

        if (_playerInput == null && _playerArmature != null)
        {
            _playerInput = _playerArmature.GetComponentInParent<PlayerInput>();
            if (_playerInput == null)
            {
                _playerInput = _playerArmature.GetComponent<PlayerInput>();
            }
        }
    }

    private void FindAllAnchors()
    {
        _allAnchors.Clear();

        foreach (var anchor in FindObjectsByType<MagneticAnchor>(FindObjectsSortMode.None))
        {
            if (anchor == null) continue;
            GameObject parent = anchor.GetTargetParent();
            if (parent == null)
            {
                Debug.LogWarning($"[MagneticManager] {anchor.name} has no parent.", anchor);
                continue;
            }

            if (!_allAnchors.ContainsKey(parent))
                _allAnchors[parent] = anchor;
        }

        TotalAnchorsCount = _allAnchors.Count;
        DebugLog($"FindAllAnchors: Found {TotalAnchorsCount} anchors");
    }

    private void FindAllInteractionObjects()
    {
        _allInteractionObjects.Clear();

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            if (obj != null && IsInteractionObject(obj))
            {
                _allInteractionObjects.Add(obj);
            }
        }

        TotalInteractionObjectsCount = _allInteractionObjects.Count;
    }

    private bool IsInteractionBlocked()
    {
        if (TimeLineManager.Instance != null &&
            (TimeLineManager.Instance.IsTimelinePlaying || TimeLineManager.Instance.IsTimelineTransitioning))
            return true;

        if (GuideUIManager.Instance != null && GuideUIManager.Instance.IsMemoryImageOpen)
            return true;

        if (GameUIManager.Instance != null)
        {
            if (GameUIManager.Instance.IsAnyUIOpen()) return true;
            if (GameUIManager.Instance.IsPanelOpen("SettingPanel")) return true;
            if (GameUIManager.Instance.dialogueRunner != null &&
                GameUIManager.Instance.dialogueRunner.IsDialogueRunning) return true;
        }

        return false;
    }

    private void UpdateVisualFeedback()
    {
        if (IsInteractionBlocked())
        {
            foreach (var (parent, anchor) in _allAnchors)
            {
                HideAnchorUI(parent);
                RestoreOriginalMaterial(parent);
            }
            if (_lastActiveInteractionObject != null)
                HideInteractionObjectUI(_lastActiveInteractionObject);
            if (CurrentActiveInteractionObject != null)
                HideInteractionObjectUI(CurrentActiveInteractionObject);
            if (_uiSocketInstance != null)
                _uiSocketInstance.SetActive(false);
            return;
        }

        bool needsUI = false;

        foreach (var (parent, anchor) in _allAnchors)
        {
            if (anchor == null) continue;

            if (anchor.IsCurrentlyInteracting())
            {
                HideAnchorUI(parent);
                RestoreOriginalMaterial(parent);
                continue;
            }

            bool isActive = (anchor == CurrentActiveAnchor);
            if (isActive)
            {
                if (CurrentHoldingAnchor != null && CurrentHoldingAnchor != anchor)
                {
                    // HeavyAnchor 부착 중일 때 다른 HeavyAnchor는 허용
                    if (!(CurrentHoldingAnchor is HeavyAnchor && anchor is HeavyAnchor))
                    {
                        HideAnchorUI(parent);
                        RestoreOriginalMaterial(parent);
                        continue;
                    }
                }

                bool materialChanged = SetParentMaterialByAnchorType(parent, anchor);
                if (materialChanged)
                {
                    ShowAnchorUI(parent, anchor);
                    needsUI = true;
                }
                else
                {
                    HideAnchorUI(parent);
                }
            }
            else
            {
                HideAnchorUI(parent);
                RestoreOriginalMaterial(parent);
            }
        }

        if (CurrentActiveInteractionObject != null)
        {
            if (CurrentHoldingAnchor != null)
                HideInteractionObjectUI(CurrentActiveInteractionObject);
            else
            {
                ShowInteractionObjectUI(CurrentActiveInteractionObject);
                needsUI = true;
            }
        }

        if (_lastActiveInteractionObject != null
            && _lastActiveInteractionObject != CurrentActiveInteractionObject)
            HideInteractionObjectUI(_lastActiveInteractionObject);

        if (_uiSocketInstance != null)
        {
            _uiSocketInstance.SetActive(needsUI);
        }
    }

    private void ShowAnchorUI(GameObject parent, MagneticAnchor anchor)
    {
        if (EKeyUIPrefab == null) return;
        GameObject ui;
        bool isNewUI = false;

        if (!_anchorUIs.TryGetValue(parent, out ui) || ui == null)
        {
            ui = Instantiate(EKeyUIPrefab, GetUISocketPosition(), Quaternion.identity);
            _anchorUIs[parent] = ui;
            isNewUI = true;
        }

        ui.transform.position = GetUISocketPosition();

        if (_playerCamera != null)
        {
            ui.transform.LookAt(_playerCamera.transform);
            ui.transform.Rotate(0, 180, 0);
        }

        if (isNewUI || ui != null)
        {
            SetUITextByTag(ui, "MagneticCube", anchor);
        }
    }

    private void ShowInteractionObjectUI(GameObject obj)
    {
        if (EKeyUIPrefab == null) return;

        GameObject ui;
        bool isNewUI = false;

        if (!_interactionObjectUIs.TryGetValue(obj, out ui) || ui == null)
        {
            Vector3 uiPosition = GetUISocketPosition();
            ui = Instantiate(EKeyUIPrefab, uiPosition, Quaternion.identity);
            _interactionObjectUIs[obj] = ui;
            isNewUI = true;
        }

        ui.transform.position = GetUISocketPosition();

        if (_playerCamera != null)
        {
            ui.transform.LookAt(_playerCamera.transform);
            ui.transform.Rotate(0, 180, 0);
        }

        if (isNewUI || ui != null)
        {
            string tagToUse = obj.CompareTag("InteractionObject") ? "InteractionObject" : obj.tag;
            SetUITextByTag(ui, tagToUse);
        }
    }

    private Vector3 GetUISocketPosition()
    {
        if (_uiSocketInstance == null && UISocketPrefab != null)
        {
            _uiSocketInstance = Instantiate(UISocketPrefab);
            _uiSocketInstance.SetActive(false);
        }

        if (_uiSocketInstance != null)
        {
            return _uiSocketInstance.transform.position;
        }

        Vector3 fallbackPos = _playerArmature != null
            ? _playerArmature.position + Vector3.up * UIHeightOffset
            : Vector3.zero;

        return fallbackPos;
    }

    private bool SetParentMaterialByAnchorType(GameObject parent, MagneticAnchor anchor)
    {
        if (parent == null) return false;
        MeshRenderer parentRenderer = parent.GetComponent<MeshRenderer>();
        if (parentRenderer == null)
            parentRenderer = parent.GetComponentInChildren<MeshRenderer>();
        if (parentRenderer == null) return false;

        Material targetMaterial = null;
        if (anchor is HeavyAnchor) targetMaterial = HeavyAnchorMaterial;
        else if (anchor is LightAnchor) targetMaterial = LightAnchorMaterial;
        if (targetMaterial == null) return false;

        if (!_originalMaterials.ContainsKey(parentRenderer))
            _originalMaterials[parentRenderer] = parentRenderer.sharedMaterials;

        parentRenderer.sharedMaterials = new Material[] { targetMaterial };
        return true;
    }

    private void RestoreOriginalMaterial(GameObject parent)
    {
        if (parent == null) return;
        MeshRenderer parentRenderer = parent.GetComponent<MeshRenderer>();
        if (parentRenderer == null)
            parentRenderer = parent.GetComponentInChildren<MeshRenderer>();
        if (parentRenderer == null) return;

        if (_originalMaterials.TryGetValue(parentRenderer, out Material[] originalMats))
        {
            parentRenderer.sharedMaterials = originalMats;
            _originalMaterials.Remove(parentRenderer);
        }
    }

    private void SetUITextByTag(GameObject uiObject, string targetIdentifier, MagneticAnchor anchor = null)
    {
        if (uiObject == null) return;

        Transform pressEHeavy = FindChildRecursive(uiObject.transform, PressEHeavyObjectName);
        Transform pressELight = FindChildRecursive(uiObject.transform, PressELightObjectName);
        Transform pressEInteraction = FindChildRecursive(uiObject.transform, PressEInteractionObjectName);

        bool isHeavy = targetIdentifier == "MagneticCube" && anchor is HeavyAnchor;
        bool isLight = targetIdentifier == "MagneticCube" && anchor is LightAnchor;
        bool isInteraction = targetIdentifier == "InteractionObject";

        if (pressEHeavy != null) pressEHeavy.gameObject.SetActive(isHeavy);
        if (pressELight != null) pressELight.gameObject.SetActive(isLight);
        if (pressEInteraction != null) pressEInteraction.gameObject.SetActive(isInteraction);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    private void HideAnchorUI(GameObject parent)
    {
        if (_anchorUIs.TryGetValue(parent, out var ui) && ui != null)
        {
            Destroy(ui);
            _anchorUIs.Remove(parent);
        }
    }

    public void HideInteractionObjectUI(GameObject obj)
    {
        if (_interactionObjectUIs.TryGetValue(obj, out var ui) && ui != null)
        {
            Destroy(ui);
            _interactionObjectUIs.Remove(obj);
        }
    }

    public bool IsAnchorActivated(MagneticAnchor anchor)
    {
        if (anchor == null) return false;
        if (anchor.IsCurrentlyInteracting()) return true;
        return anchor == CurrentActiveAnchor;
    }

    // ========================================
    // Hold 상태 관리 메서드
    // ========================================

    /// <summary>
    /// 현재 hold 중인 앵커가 있는지 확인
    /// </summary>
    public bool IsAnyAnchorHolding()
    {
        return CurrentHoldingAnchor != null;
    }

    /// <summary>
    /// 특정 앵커가 hold 상태임을 등록
    /// </summary>
    public void SetHoldingAnchor(MagneticAnchor anchor)
    {
        if (anchor == null) return;

        CurrentHoldingAnchor = anchor;
        DebugLog($"SetHoldingAnchor: {anchor.gameObject.name}");
    }

    /// <summary>
    /// 특정 앵커의 hold 상태를 해제
    /// </summary>
    public void ClearHoldingAnchor(MagneticAnchor anchor)
    {
        if (anchor == null) return;

        // 현재 hold 중인 앵커가 맞을 때만 해제
        if (CurrentHoldingAnchor == anchor)
        {
            DebugLog($"ClearHoldingAnchor: {anchor.gameObject.name}");
            CurrentHoldingAnchor = null;
        }
    }

    // ========================================
    // 입력 처리 중복 방지 메서드
    // ========================================

    /// <summary>
    /// 특정 앵커가 이번 프레임에 R키 입력을 처리할 수 있는지 확인
    /// 같은 프레임에서 한 앵커만 입력 처리 가능
    /// </summary>
    public bool CanAnchorProcessInput(MagneticAnchor anchor)
    {
        int currentFrame = Time.frameCount;

        // 이번 프레임에 아직 아무도 입력 처리 안 했으면 OK
        if (LastInputProcessedFrame != currentFrame)
        {
            return true;
        }

        // 이번 프레임에 이미 입력 처리했으면, 같은 앵커만 OK
        return LastInputProcessedAnchor == anchor;
    }

    /// <summary>
    /// 앵커가 R키 입력을 처리했음을 등록
    /// </summary>
    public void RegisterInputProcessed(MagneticAnchor anchor)
    {
        LastInputProcessedAnchor = anchor;
        LastInputProcessedFrame = Time.frameCount;

        if (DebugMode)
        {
            DebugLogger.Log($"[MagneticManager] 입력 처리 등록: {anchor.gameObject.name} (Frame: {LastInputProcessedFrame})");
        }
    }

    // ========================================
    // 감지 차단 메서드
    // ========================================

    /// <summary>
    /// 일정 시간 동안 모든 앵커 감지를 차단
    /// R키 drop 직후 다른 앵커가 즉시 활성화되는 것을 방지
    /// </summary>
    public void BlockDetectionTemporarily(float duration = -1f)
    {
        // duration이 지정되지 않으면 기본값 사용
        if (duration < 0f)
        {
            duration = _detectionBlockDuration;
        }

        // 코루틴 시작 전에 즉시 차단 (같은 프레임에서 바로 적용)
        _detectionBlocked = true;
        IsDetectionBlocked = true;
        DetectionBlockTimeRemaining = duration;
        DebugLog($"[감지 차단 즉시 적용] {duration}초 동안 모든 앵커 감지 차단");

        if (_blockDetectionCo != null)
        {
            StopCoroutine(_blockDetectionCo);
        }

        _blockDetectionCo = StartCoroutine(BlockDetectionCoroutine(duration));
    }

    private IEnumerator BlockDetectionCoroutine(float duration)
    {
        // 이미 즉시 적용되어 있으므로 여기서는 시간만 카운트
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            DetectionBlockTimeRemaining = Mathf.Max(0f, duration - elapsed);
            yield return null;
        }

        _detectionBlocked = false;
        IsDetectionBlocked = false;
        DetectionBlockTimeRemaining = 0f;
        _blockDetectionCo = null;

        DebugLog("[감지 차단 해제] 앵커 감지 재개");
    }

    // ========================================

    public void RegisterAnchor(MagneticAnchor anchor)
    {
        GameObject parent = anchor.GetTargetParent();
        if (parent == null) return;
        if (!_allAnchors.ContainsKey(parent))
            _allAnchors[parent] = anchor;
        TotalAnchorsCount = _allAnchors.Count;

        if (_initialized && _playerArmature != null)
        {
            anchor.InitializeFromManager(
                _playerArmature.gameObject,
                _playerInput,
                HoldAnchor
            );
        }
    }

    public void UnregisterAnchor(MagneticAnchor anchor)
    {
        GameObject parent = anchor.transform.parent?.gameObject;
        if (parent != null)
        {
            // 머티리얼 정리
            MeshRenderer parentRenderer = parent.GetComponent<MeshRenderer>();
            if (parentRenderer != null) _originalMaterials.Remove(parentRenderer);

            HideAnchorUI(parent); // 부모 기준으로
            _allAnchors.Remove(parent); // Dictionary 키는 parent
        }

        TotalAnchorsCount = _allAnchors.Count;
        if (CurrentActiveAnchor == anchor) CurrentActiveAnchor = null;
        if (CurrentInteractingAnchor == anchor) CurrentInteractingAnchor = null;
        if (CurrentHoldingAnchor == anchor) CurrentHoldingAnchor = null;
    }

    public void RegisterInteractionObject(GameObject obj)
    {
        if (!_allInteractionObjects.Contains(obj) && IsInteractionObject(obj))
        {
            _allInteractionObjects.Add(obj);
            TotalInteractionObjectsCount = _allInteractionObjects.Count;
        }
    }

    public void UnregisterInteractionObject(GameObject obj)
    {
        HideInteractionObjectUI(obj);
        _interactionObjectUIs.Remove(obj);
        _allInteractionObjects.Remove(obj);
        TotalInteractionObjectsCount = _allInteractionObjects.Count;
        if (CurrentActiveInteractionObject == obj) CurrentActiveInteractionObject = null;
    }

    public GameObject GetPlayerObject() => _playerArmature ? _playerArmature.gameObject : null;
    public Transform GetPlayerTransform() => _playerArmature;
    public PlayerInput GetPlayerInputFromManager() => _playerInput;
    public Transform GetPlayerArmatureTransform() => _playerArmature;
    public Transform GetHoldAnchorTransform() => HoldAnchor;

    /// <summary>
    /// HoldAnchorAim Transform 반환 (BackView 모드용)
    /// </summary>
    public Transform GetHoldAnchorAimTransform() => HoldAnchorAim;

    public Vector3 GetHoldAnchorPosition() => HoldAnchor ? HoldAnchor.position : Vector3.zero;
    public Camera GetPlayerCamera() => _playerCamera;
    public PlayerInput GetPlayerInput() => _playerInput;
    public RayDetector GetRayDetector() => _rayDetector;

    private bool IsInteractionObject(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.GetComponentInChildren<MagneticAnchor>() != null) return false;

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int interactionLayer = LayerMask.NameToLayer("Interaction");

        bool layerOk =
            (interactableLayer >= 0 && obj.layer == interactableLayer) ||
            (interactionLayer >= 0 && obj.layer == interactionLayer);

        bool tagOk = !string.IsNullOrEmpty(InteractionObjectTagName) && obj.CompareTag(InteractionObjectTagName);

        return layerOk || tagOk;
    }

    private void DebugLog(string msg)
    {
        if (DebugMode) DebugLogger.Log($"[MagneticManager] {msg}");
    }

    private void OnDrawGizmos()
    {
        if (!ShowGizmos) return;
        if (_playerArmature == null || _playerArmature.gameObject == null) return;

        if (ShowAllAnchors)
        {
            foreach (var (parent, anchor) in _allAnchors)
            {
                if (anchor == null) continue;

                Vector3 anchorPos = anchor.GetPosition();

                if (ShowAnchorStates)
                {
                    // Hold 중인 앵커는 더 밝게 표시
                    if (anchor == CurrentHoldingAnchor)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(anchorPos, 0.5f);
                        Gizmos.DrawSphere(anchorPos, 0.15f);
                    }
                    else if (anchor == CurrentInteractingAnchor)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(anchorPos, 0.4f);
                        Gizmos.DrawSphere(anchorPos, 0.1f);
                    }
                    else if (anchor == CurrentActiveAnchor)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(anchorPos, 0.35f);
                        Gizmos.DrawSphere(anchorPos, 0.08f);
                    }
                    else
                    {
                        Gizmos.color = Color.gray;
                        Gizmos.DrawWireSphere(anchorPos, 0.25f);
                        Gizmos.DrawSphere(anchorPos, 0.05f);
                    }
                }
            }
        }

        if (ShowInteractionLines)
        {
            if (CurrentInteractingAnchor != null)
            {
                Gizmos.color = Color.red;
                Vector3 interactingPos = CurrentInteractingAnchor.GetPosition();

                Gizmos.DrawLine(_playerArmature.position, interactingPos);
                Gizmos.DrawLine(_playerArmature.position + Vector3.up * 0.05f, interactingPos + Vector3.up * 0.05f);
                Gizmos.DrawLine(_playerArmature.position + Vector3.right * 0.05f,
                    interactingPos + Vector3.right * 0.05f);
                Gizmos.DrawLine(_playerArmature.position - Vector3.right * 0.05f,
                    interactingPos - Vector3.right * 0.05f);
            }
            else if (CurrentActiveAnchor != null)
            {
                Gizmos.color = Color.green;
                Vector3 activePos = CurrentActiveAnchor.GetPosition();

                Gizmos.DrawLine(_playerArmature.position, activePos);
                Gizmos.DrawLine(_playerArmature.position + Vector3.up * 0.03f, activePos + Vector3.up * 0.03f);
            }
        }

        if (HoldAnchor != null && HoldAnchor.gameObject != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(HoldAnchor.position, 0.12f);
            Gizmos.DrawSphere(HoldAnchor.position, 0.04f);
        }

        // HoldAnchorAim 기즈모 추가
        if (HoldAnchorAim != null && HoldAnchorAim.gameObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(HoldAnchorAim.position, 0.12f);
            Gizmos.DrawSphere(HoldAnchorAim.position, 0.04f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_playerArmature.position, 0.15f);

        if (_rayDetector != null && _rayDetector.Origin != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_rayDetector.Origin.position, Vector3.one * 0.1f);
        }

        if (CurrentActiveInteractionObject != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(CurrentActiveInteractionObject.transform.position, 0.5f);
        }

        // 감지 차단 중일 때 시각적 피드백
        if (_detectionBlocked)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_playerArmature.position, 0.3f);
        }
    }

    private void EnsureRayDetector()
    {
        RayDetector existing = FindFirstObjectByType<RayDetector>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _rayDetector = existing;
            return;
        }

        if (_playerCamera != null)
        {
            _rayDetector = _playerCamera.GetComponent<RayDetector>();
            if (_rayDetector != null) return;
        }

        Camera mainCam = Camera.main;
        if (_rayDetector == null && mainCam != null)
        {
            _rayDetector = mainCam.GetComponent<RayDetector>();
            if (_rayDetector != null) return;
        }

        if (_rayDetector == null)
        {
            GameObject attachTarget = _playerCamera != null
                ? _playerCamera.gameObject
                : (mainCam != null ? mainCam.gameObject : null);

            if (attachTarget != null)
            {
                _rayDetector = attachTarget.AddComponent<RayDetector>();
            }
        }
    }

    private void ConfigureRayDetector()
    {
        if (_rayDetector == null) return;

        // Origin 설정: PlayerCameraRoot 우선, 없으면 Camera
        if (UsePlayerCameraRoot && PlayerCameraRoot != null)
        {
            _rayDetector.Origin = PlayerCameraRoot;
            DebugLog($"RayDetector Origin: PlayerCameraRoot ({PlayerCameraRoot.name})");
        }
        else if (_playerCamera != null)
        {
            _rayDetector.Origin = _playerCamera.transform;
            DebugLog($"RayDetector Origin: PlayerCamera ({_playerCamera.name})");
        }

        // DirectionSource 설정: 항상 Camera
        if (_playerCamera != null)
        {
            _rayDetector.DirectionSource = _playerCamera.transform;
            DebugLog($"RayDetector DirectionSource: PlayerCamera ({_playerCamera.name})");
        }

        _rayDetector.OwnerRoot = _playerArmature;

        if (_rayDetector.OwnerColliders == null || _rayDetector.OwnerColliders.Length == 0)
        {
            if (_playerArmature != null)
            {
                _rayDetector.OwnerColliders = _playerArmature.GetComponentsInChildren<Collider>(true);
            }
        }

        _rayDetector.RequiredTag = string.IsNullOrWhiteSpace(RayRequiredTag) ? "" : RayRequiredTag;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Interactable 오브젝트와 충돌 시
        if (IsInteractionObject(other.gameObject))
        {
            AddCollisionActiveObject(other.gameObject);
            // DebugLog($"OnTriggerEnter: {other.gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Interactable 오브젝트와 충돌 종료 시
        if (IsInteractionObject(other.gameObject))
        {
            RemoveCollisionActiveObject(other.gameObject);
            // DebugLog($"OnTriggerExit: {other.gameObject.name}");
        }
    }

    /// <summary>
    /// 충돌로 활성화 추가
    /// </summary>
    public void AddCollisionActiveObject(GameObject obj)
    {
        if (obj != null && IsInteractionObject(obj))
        {
            _collisionActiveObjects.Add(obj);
            DebugLog($"Collision object added: {obj.name}");
        }
    }

    /// <summary>
    /// 충돌로 활성화 제거
    /// </summary>
    public void RemoveCollisionActiveObject(GameObject obj)
    {
        _collisionActiveObjects.Remove(obj);
        DebugLog($"Collision object removed: {obj.name}");
    }
    
    /// <summary>
    /// 현재 활성화된 인터랙션 대상/UI를 강제로 비움 (플레이어 사망 등 즉시 초기화가 필요한 경우)
    /// </summary>
    private void UpdateDetectUI()
    {
        if (_detectUIImage == null) return;

        bool shouldHide = _detectUIForceHidden ||
                          (CurrentHoldingAnchor != null && CurrentHoldingAnchor is LightAnchor) ||
                          _detectionBlocked ||
                          IsInteractionBlocked();

        if (shouldHide)
        {
            _detectUIImage.gameObject.SetActive(false);
            return;
        }

        _detectUIImage.gameObject.SetActive(true);

        // 레이캐스트가 앵커를 감지 중이면 active (트리거박스 안에 있어도 ray 앵커 감지 시 우선)
        bool isRayDetecting = _rayDetector != null &&
                              !_detectionBlocked &&
                              _rayDetector.CurrentRaycastHitAnchor != null;

        Sprite target = isRayDetecting ? _detectActiveSprite : _detectInactiveSprite;
        if (target != null) _detectUIImage.sprite = target;
    }

    /// <summary>
    /// 팝업, 컷씬 등 외부에서 Detect UI 표시 여부를 강제 제어
    /// </summary>
    public void SetDetectUIVisible(bool visible)
    {
        _detectUIForceHidden = !visible;
    }

    public void ClearInteractionState()
    {
        if (CurrentActiveInteractionObject != null)
        {
            HideInteractionObjectUI(CurrentActiveInteractionObject);
            _lastActiveInteractionObject = CurrentActiveInteractionObject;
            CurrentActiveInteractionObject = null;
        }

        _collisionActiveObjects.Clear();

        if (_uiSocketInstance != null)
            _uiSocketInstance.SetActive(false);

        if (_detectUIImage != null) _detectUIImage.gameObject.SetActive(false);
    }

    public void UnblockDetection()
    {
        if (_blockDetectionCo != null)
        {
            StopCoroutine(_blockDetectionCo);
            _blockDetectionCo = null;
        }
        _detectionBlocked = false;
        IsDetectionBlocked = false;
        DetectionBlockTimeRemaining = 0f;
    }

    private void OnDestroy()
    {
        // Coroutine 정리
        if (_blockDetectionCo != null)
        {
            StopCoroutine(_blockDetectionCo);
            _blockDetectionCo = null;
        }

        foreach (var kvp in _anchorUIs)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        _anchorUIs.Clear();

        foreach (var kvp in _interactionObjectUIs)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        _interactionObjectUIs.Clear();

        if (_uiSocketInstance != null)
        {
            Destroy(_uiSocketInstance);
        }
    }
}