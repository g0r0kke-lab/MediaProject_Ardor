using UnityEngine;
using Unity.Cinemachine;

/// <summary>후방 시점 카메라의 활성화, 줌 연출, 블렌드 전환을 관리하는 컨트롤러</summary>
public class BackViewCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public int OriginalPriority = 5;
    public int BoostPriority = 15;

    [Header("Position Settings (Transform mode fallback)")]
    public Vector3 FixedLocalPosition = new Vector3(0f, 0.1f, -2f);
    public Vector3 ZoomOutLocalPosition = new Vector3(0f, 2f, -3f);

    [Header("Rotation Settings")]
    public bool LockRotation = true;
    public Quaternion LockedRotation = Quaternion.identity;

    [Header("Zoom Settings")]
    [Range(0.05f, 0.5f)] public float ZoomOutDuration = 0.3f;
    [Range(0.00f, 0.2f)] public float HoldDuration = 0.0f;
    [Range(0.05f, 0.5f)] public float ZoomInDuration = 0.3f;

    [Header("Animation Curves")]
    public AnimationCurve ZoomOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve ZoomInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("ThirdPersonFollow-based zoom (if available)")]
    [Tooltip("평상시 카메라-타겟 거리 (ThirdPersonFollow.CameraDistance)")]
    public float FixedDistance = 2.0f;
    [Tooltip("줌아웃 시 거리 (값이 클수록 더 멀어짐)")]
    public float ZoomOutDistance = 3.0f;

    [Header("Behavior")]
    [Tooltip("활성화 시 블렌드 방식 - Cut(즉시) 또는 EaseInOut(부드럽게)")]
    [SerializeField] private bool UseInstantBlendOnActivation = false;

    [Tooltip("활성화 전환 시 블렌드 시간")]
    [Range(0f, 1f)] public float ActivationBlendTime = 0.3f;

    [Tooltip("비활성화 전환 시 블렌드 시간")]
    [Range(0f, 1f)] public float DeactivationBlendTime = 0.3f;

    [Header("Debug")]
    public bool IsActive = false;
    public bool ShowDebugInfo = false;

    // Cinemachine refs
    private CinemachineCamera _cmCamera;
    private CinemachineThirdPersonFollow _tpFollow; // optional
    private bool _hasThirdPersonFollow = false;

    // State
    private Coroutine _zoomCoroutine;
    private bool _isZoomInProgress = false;

    // Damping backup
    private Vector3 _tpFollowOriginalDamping;

    // Brain & Blend backup
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _hasSavedBlend = false;

    // ===== Lifecycle =====
    private void Awake()
    {
        if (ShowDebugInfo)
            Debug.unityLogger.logEnabled = true;

        _cmCamera = GetComponent<CinemachineCamera>();
        if (_cmCamera == null)
        {
            DebugLogger.LogError($"[BackViewCamera] CinemachineCamera not found on {name}!");
            enabled = false;
            return;
        }

        // ThirdPersonFollow를 같은 GO 또는 하위에서 탐색
        _tpFollow = GetComponentInChildren<CinemachineThirdPersonFollow>();
        _hasThirdPersonFollow = (_tpFollow != null);

        if (_hasThirdPersonFollow)
        {
            _tpFollowOriginalDamping = _tpFollow.Damping;
            _tpFollow.CameraDistance = FixedDistance;
            if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] Body=ThirdPersonFollow (CameraDistance active)");
        }
        else
        {
            transform.localPosition = FixedLocalPosition;
            if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] ThirdPersonFollow not found -> Transform fallback");
        }

        LockedRotation = Quaternion.identity;
        if (LockRotation) transform.localRotation = LockedRotation;

        OriginalPriority = _cmCamera.Priority;

        // Brain은 여기서 시도, 실패하더라도 이후 시점들에서 재시도
        TryGetBrain();

        if (ShowDebugInfo)
            DebugLogger.Log($"[BackViewCamera] Init - Total:{GetTotalZoomDuration():F2}s, HasTPF:{_hasThirdPersonFollow}, Brain:{(_brain != null)}");
    }

    private void OnEnable()
    {
        TryGetBrain();
    }

    private void LateUpdate()
    {
        if (!_isZoomInProgress)
        {
            if (_hasThirdPersonFollow)
            {
                if (!Mathf.Approximately(_tpFollow.CameraDistance, FixedDistance))
                    _tpFollow.CameraDistance = FixedDistance;
            }
            else
            {
                if (transform.localPosition != FixedLocalPosition)
                    transform.localPosition = FixedLocalPosition;
            }

            if (LockRotation)
                transform.localRotation = LockedRotation;
        }
        else if (LockRotation)
        {
            transform.localRotation = LockedRotation;
        }
    }

    private void OnDestroy()
    {
        if (_zoomCoroutine != null)
            StopCoroutine(_zoomCoroutine);

        // Brain Blend 복원 안전장치 (게임 종료 시에만)
        if (_hasSavedBlend && _brain != null)
        {
            _brain.DefaultBlend = _savedBlend;
            _hasSavedBlend = false;
        }
    }

    // ===== Public API =====
    public void SetActive(bool active)
    {
        if (_cmCamera == null) return;
        IsActive = active;

        if (active)
        {
            _cmCamera.Priority = BoostPriority;

            // 활성화 시: 원하는 블렌드 방식 적용
            if (UseInstantBlendOnActivation)
            {
                ApplyBrainCut();
            }
            else
            {
                ApplyBrainBlend(ActivationBlendTime);
            }

            ResetPosition();
            if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] Activated");
        }
        else
        {
            _cmCamera.Priority = OriginalPriority;

            if (_zoomCoroutine != null)
            {
                StopCoroutine(_zoomCoroutine);
                _zoomCoroutine = null;
                _isZoomInProgress = false;
            }

            // ��Ȱ��ȭ ��: �ε巯�� ������� ����
            ApplyBrainBlend(DeactivationBlendTime);

            ResetPosition();
            
            // 블렌드 복원 예약 (DeactivationBlendTime 이후 원복)
            StartCoroutine(RestoreBrainBlendDelayed(DeactivationBlendTime));

            if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] Deactivated");
        }
    }

    /// <summary> ������ �� ���� ���� ����� ���� �� ������ </summary>
    public void PlayThrowSequence()
    {
        if (_zoomCoroutine != null)
            StopCoroutine(_zoomCoroutine);

        // Brain 보장
        TryGetBrain();

        _zoomCoroutine = StartCoroutine(ThrowZoomSequence());
    }

    // ===== Core Sequence =====
    private System.Collections.IEnumerator ThrowZoomSequence()
    {
        _isZoomInProgress = true;

        if (ShowDebugInfo)
            DebugLogger.Log($"[BackViewCamera] === Zoom Start (Total:{GetTotalZoomDuration():F2}s, Mode:{(_hasThirdPersonFollow ? "TPF" : "Transform")}) ===");

        float startTime = Time.time;

        if (_hasThirdPersonFollow)
        {
            Vector3 prevDamp = _tpFollow.Damping;
            _tpFollow.Damping = Vector3.zero;

            // 1) 줌아웃
            yield return LerpCameraDistance(_tpFollow, FixedDistance, ZoomOutDistance, ZoomOutDuration, ZoomOutCurve);

            // 2) 홀드
            if (HoldDuration > 0f)
                yield return new WaitForSeconds(HoldDuration);

            // 3) 줌인
            yield return LerpCameraDistance(_tpFollow, ZoomOutDistance, FixedDistance, ZoomInDuration, ZoomInCurve);

            // 복원
            _tpFollow.Damping = prevDamp;
        }
        else
        {
            Vector3 startPos = FixedLocalPosition;
            Vector3 zoomOutPos = ZoomOutLocalPosition;

            float elapsed = 0f;
            while (elapsed < ZoomOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = ZoomOutCurve.Evaluate(Mathf.Clamp01(elapsed / ZoomOutDuration));
                transform.localPosition = Vector3.LerpUnclamped(startPos, zoomOutPos, t);
                if (LockRotation) transform.localRotation = LockedRotation;
                yield return null;
            }
            transform.localPosition = zoomOutPos;

            if (HoldDuration > 0f)
                yield return new WaitForSeconds(HoldDuration);

            elapsed = 0f;
            while (elapsed < ZoomInDuration)
            {
                elapsed += Time.deltaTime;
                float t = ZoomInCurve.Evaluate(Mathf.Clamp01(elapsed / ZoomInDuration));
                transform.localPosition = Vector3.LerpUnclamped(zoomOutPos, startPos, t);
                if (LockRotation) transform.localRotation = LockedRotation;
                yield return null;
            }
            transform.localPosition = startPos;
        }

        if (ShowDebugInfo)
            DebugLogger.Log($"[BackViewCamera] === Zoom Complete (Actual:{Time.time - startTime:F2}s) ===");

        _zoomCoroutine = null;
        _isZoomInProgress = false;
        ResetPosition();
    }

    // ===== Helpers =====
    private System.Collections.IEnumerator LerpCameraDistance(CinemachineThirdPersonFollow tp, float from, float to, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float nt = Mathf.Clamp01(elapsed / duration);
            float t = (curve != null) ? curve.Evaluate(nt) : nt;
            tp.CameraDistance = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }
        tp.CameraDistance = to;
    }

    public float GetTotalZoomDuration()
    {
        return ZoomOutDuration + HoldDuration + ZoomInDuration;
    }

    public void ResetPosition()
    {
        if (_hasThirdPersonFollow)
            _tpFollow.CameraDistance = FixedDistance;
        else
            transform.localPosition = FixedLocalPosition;

        if (LockRotation)
            transform.localRotation = LockedRotation;

        if (ShowDebugInfo)
            DebugLogger.Log("[BackViewCamera] Position reset");
    }

    [ContextMenu("Skip To Zoom End")]
    public void SkipToZoomEnd()
    {
        if (_zoomCoroutine != null)
        {
            StopCoroutine(_zoomCoroutine);
            _zoomCoroutine = null;
        }

        _isZoomInProgress = false;
        ResetPosition();
    }

    // ===== Brain & Blend control =====
    private bool TryGetBrain()
    {
        if (_brain != null) return true;

        var mainCam = Camera.main;
        if (mainCam != null)
            _brain = mainCam.GetComponent<CinemachineBrain>();

        if (_brain == null)
            _brain = FindFirstObjectByType<CinemachineBrain>();

        if (_brain == null && ShowDebugInfo)
            DebugLogger.LogWarning("[BackViewCamera] CinemachineBrain not found yet.");

        return _brain != null;
    }

    private void ApplyBrainCut()
    {
        if (!TryGetBrain()) return;

        if (!_hasSavedBlend)
        {
            _savedBlend = _brain.DefaultBlend;
            _hasSavedBlend = true;
        }

        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] Brain DefaultBlend -> Cut(0s)");
    }

    private void ApplyBrainBlend(float duration)
    {
        if (!TryGetBrain()) return;

        if (!_hasSavedBlend)
        {
            _savedBlend = _brain.DefaultBlend;
            _hasSavedBlend = true;
        }

        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, duration);
        if (ShowDebugInfo) DebugLogger.Log($"[BackViewCamera] Brain DefaultBlend -> EaseInOut({duration}s)");
    }

    private System.Collections.IEnumerator RestoreBrainBlendDelayed(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (_hasSavedBlend && _brain != null)
        {
            _brain.DefaultBlend = _savedBlend;
            _hasSavedBlend = false;
            if (ShowDebugInfo) DebugLogger.Log("[BackViewCamera] Brain DefaultBlend 원복됨");
        }
    }

    // ===== Gizmos =====
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = IsActive ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        if (LockRotation)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.1f);
        }
    }
}