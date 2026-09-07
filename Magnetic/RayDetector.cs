using System.Collections.Generic;
using UnityEngine;

/// <summary>실린더 형태의 감지 범위로 MagneticAnchor 및 상호작용 오브젝트를 탐지하는 컴포넌트</summary>
public class RayDetector : MonoBehaviour
{
    [Header("Cylinder Detection Settings")]
    public Transform Origin;

    public Transform DirectionSource;
    public float CylinderLength = 5f;
    [Tooltip("���� �ݰ�")] public float CylinderRadius = 0.8f;

    [Tooltip("���� ������ ���� ������ (�ڱ� �ڽŰ��� �浹 ����)")]
    public float ForwardOffset = 0.2f;

    [Tooltip("���� ������ Y�� ������ (ī�޶� ��Ʈ���� ���߱�)")]
    public float VerticalOffset = -0.2f;

    [Tooltip("���� ������ ���� ������ (���� forward ����, �ڷ� ����)")]
    public float DepthOffset = -0.1f;

    [Tooltip("�ڱ� ������ �浹�� ���ϱ� ���� �ּ� �Ÿ�")]
    public float MinDetectionDistance = 0.3f;

    public LayerMask DetectionLayers;

    [Header("Occlusion Check")] [Tooltip("���� üũ�� Ȱ��ȭ���� ����")]
    public bool EnableOcclusionCheck = true;

    [Tooltip("���� üũ �� ����� �ִ� �Ÿ� ����")]
    public float OcclusionDistanceTolerance = 0.5f;

    [Header("Owner (Self-Ignore)")] [Tooltip("���� PlayerArmature. �� ��Ʈ ���� �ݶ��̴��� ���� ����")]
    public Transform OwnerRoot;

    [Tooltip("���� �� �ϸ� Awake���� OwnerRoot ������ �ڵ� ä��")]
    public Collider[] OwnerColliders;

    [Header("Optional Filter")] [Tooltip("�����ϸ� �ش� �±׸� ��ȿ�� ó��")]
    public string RequiredTag = "";

    [Header("Center Raycast Settings")]
    public bool EnableCenterRaycast = true;
    public float RaycastMaxDistance = 10f;

    [Header("Camera Analysis")] [Tooltip("������� ���� ����")] [Range(0f, 90f)]
    public float GroundAngleThreshold = 30f;

    [Header("Debug Settings")] public bool DrawGizmos = true;
    public bool ShowDetectionZone = true;
    public bool ShowDetectedAnchors = true;
    public bool ShowOcclusionRays = true;
    [Range(8, 32)] public int CylinderSegments = 16;

    [Header("Debug Info - Read Only")] public int DetectedAnchorsCount = 0;
    public int DetectedObjectsCount = 0;
    public string ClosestAnchorName = "None";
    public float ClosestAnchorDistance = 0f;
    public string ClosestObjectName = "None";
    public string ClosestObjectTag = "None";
    public float ClosestObjectDistance = 0f;
    public string CurrentHitTag;
    public string CurrentHitName;
    public string RaycastHitName = "None";
    public float RaycastHitDistance = 0f;

    [Header("Camera Direction Analysis")] public float PitchAngle = 0f;
    public string LookingDirection = "Forward";

    [HideInInspector] public GameObject CurrentHitObject;
    [HideInInspector] public MagneticAnchor CurrentHitAnchor;
    [HideInInspector] public GameObject CurrentClosestObject;
    [HideInInspector] public GameObject IgnoreParent = null;
    [HideInInspector] public MagneticAnchor CurrentRaycastHitAnchor;
    [HideInInspector] public GameObject CurrentRaycastClosestObject;

    private readonly List<MagneticAnchor> _detectedAnchors = new List<MagneticAnchor>(32);
    private readonly List<GameObject> _detectedObjects = new List<GameObject>(32);

    private readonly Dictionary<MagneticAnchor, float> _cachedAnchorDistances =
        new Dictionary<MagneticAnchor, float>(32);

    private readonly Dictionary<GameObject, float> _cachedObjectDistances = new Dictionary<GameObject, float>(32);

    private float _lastDistanceUpdateTime;
    private const float DISTANCE_UPDATE_INTERVAL = 0.1f;

    private Vector3 _cachedCylinderStart;
    private Vector3 _cachedCylinderDirection;
    private Vector3 _cachedCylinderEnd;

    private void Reset()
    {
        int interactionLayer = LayerMask.NameToLayer("Interaction");
        if (interactionLayer >= 0)
        {
            DetectionLayers = 1 << interactionLayer;
        }
        else
        {
            DetectionLayers = ~0;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) DetectionLayers &= ~(1 << playerLayer);
        }
    }

    private void Awake()
    {
        AutoConfigureComponents();
    }

    private void Start()
    {
        AttachToHoldAnchor();
    }

    private void OnEnable()
    {
        if (Origin == null || Origin.GetComponent<MagneticManager>() == null)
        {
            AttachToHoldAnchor();
        }
    }

    private void AutoConfigureComponents()
    {
        if (OwnerRoot == null)
        {
            OwnerRoot = transform;
        }

        if (OwnerRoot != null && (OwnerColliders == null || OwnerColliders.Length == 0))
        {
            OwnerColliders = OwnerRoot.GetComponentsInChildren<Collider>(true);
        }

        if (Origin == null)
        {
            if (MagneticManager.Instance != null)
            {
                Transform holdAnchor = MagneticManager.Instance.GetHoldAnchorTransform();
                if (holdAnchor != null)
                {
                    Origin = holdAnchor;
                }
            }
        }

        if (DirectionSource == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                DirectionSource = cam.transform;
            }
            else if (Origin != null)
            {
                DirectionSource = Origin;
            }
        }
    }

    private void AttachToHoldAnchor()
    {
        if (MagneticManager.Instance == null) return;

        Transform holdAnchor = MagneticManager.Instance.GetHoldAnchorTransform();
        if (holdAnchor != null && Origin != holdAnchor)
        {
            Origin = holdAnchor;
        }
    }

    private void Update()
    {
        if (Origin == null || DirectionSource == null)
        {
            ClearDetection();
            return;
        }

        UpdateCylinderCache();
        AnalyzeCameraDirection();
        PerformCenterRaycast();
        UpdateCachedData();
        DetectAnchorsInZone();
        DetectObjectsInZone();
        FindClosestAnchor();
        FindClosestObject();
    }

    private void UpdateCylinderCache()
    {
        Vector3 basePosition = Origin.position + DirectionSource.forward * ForwardOffset;

        _cachedCylinderStart = basePosition
                               + Vector3.up * VerticalOffset
                               + DirectionSource.forward * DepthOffset;

        _cachedCylinderDirection = DirectionSource.forward;
        _cachedCylinderEnd = _cachedCylinderStart + _cachedCylinderDirection * CylinderLength;
    }

    private void AnalyzeCameraDirection()
    {
        Vector3 forward = DirectionSource.forward;
        PitchAngle = Mathf.Asin(forward.y) * Mathf.Rad2Deg;

        if (PitchAngle > GroundAngleThreshold)
        {
            LookingDirection = "Up";
        }
        else if (PitchAngle < -GroundAngleThreshold)
        {
            LookingDirection = "Down";
        }
        else
        {
            LookingDirection = "Forward";
        }
    }

    private void PerformCenterRaycast()
    {
        CurrentRaycastHitAnchor = null;
        CurrentRaycastClosestObject = null;
        RaycastHitName = "None";
        RaycastHitDistance = 0f;

        if (!EnableCenterRaycast || DirectionSource == null) return;

        Vector3 rayOrigin = DirectionSource.position + DirectionSource.forward * ForwardOffset;

        // 최상단 부모 콜라이더가 어떤 레이어든 감지하기 위해 Player 레이어만 제외한 넓은 마스크 사용
        int playerLayer = LayerMask.NameToLayer("Player");
        LayerMask broadMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, DirectionSource.forward, RaycastMaxDistance, broadMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (IsSelf(hit.transform)) continue;
            if (hit.distance < MinDetectionDistance) continue;

            // 앵커 체크: 히트된 콜라이더의 자식 계층에서 MagneticAnchor 탐색
            MagneticAnchor anchor = hit.collider.GetComponentInChildren<MagneticAnchor>();
            if (anchor != null)
            {
                GameObject targetParent = anchor.GetTargetParent();
                if (targetParent != null && targetParent == IgnoreParent) continue;
                if (!string.IsNullOrEmpty(RequiredTag) &&
                    !anchor.CompareTag(RequiredTag) &&
                    !anchor.transform.root.CompareTag(RequiredTag)) continue;

                CurrentRaycastHitAnchor = anchor;
                CurrentRaycastClosestObject = null; // 앵커가 있으면 InteractionObject 무효
                RaycastHitName = anchor.name;
                RaycastHitDistance = hit.distance;
                break;
            }

            // 앵커가 아닌 경우: 솔리드면 레이 차단, 트리거면 통과 (뒤에 앵커가 있을 수 있음)
            if (!hit.collider.isTrigger) break;
        }
    }

    private bool IsLayerMatched(int layerMask, int objectLayer)
    {
        return (layerMask & (1 << objectLayer)) != 0;
    }

    private bool IsVisible(Vector3 targetPoint, float distance, GameObject ignoreObject = null)
    {
        if (!EnableOcclusionCheck) return true;

        Vector3 direction = (targetPoint - _cachedCylinderStart).normalized;
        RaycastHit[] allHits = Physics.RaycastAll(_cachedCylinderStart, direction, distance);

        foreach (var hit in allHits)
        {
            if (IsSelf(hit.collider.transform)) continue;
            if (ignoreObject != null && hit.collider.gameObject == ignoreObject) continue;
            if (ignoreObject != null &&
                hit.collider.transform.IsChildOf(ignoreObject.transform)) continue; // 자식 콜라이더도 무시

            return false;
        }

        return true;
    }

    private void UpdateCachedData()
    {
        if (Time.time - _lastDistanceUpdateTime < DISTANCE_UPDATE_INTERVAL) return;
        _lastDistanceUpdateTime = Time.time;

        _cachedAnchorDistances.Clear();
        _cachedObjectDistances.Clear();

        Collider[] anchorHits = Physics.OverlapCapsule(
            _cachedCylinderStart,
            _cachedCylinderEnd,
            CylinderRadius,
            DetectionLayers,
            QueryTriggerInteraction.Collide
        );

        foreach (var hit in anchorHits)
        {
            if (hit == null) continue;
            if (IsSelf(hit.transform)) continue;

            MagneticAnchor anchor = hit.GetComponentInChildren<MagneticAnchor>();
            if (anchor == null) continue;

            GameObject targetParent = anchor.GetTargetParent();
            if (targetParent != null && targetParent == IgnoreParent) continue;

            float distance = Vector3.Distance(_cachedCylinderStart, hit.transform.position);
            if (distance < MinDetectionDistance) continue;

            if (!IsVisible(anchor.GetPosition(), distance, targetParent)) continue;

            if (!_cachedAnchorDistances.ContainsKey(anchor))
                _cachedAnchorDistances[anchor] = distance;
        }

        Collider[] objectHits = Physics.OverlapSphere(
            _cachedCylinderStart + _cachedCylinderDirection * (CylinderLength * 0.5f),
            CylinderLength * 0.5f + CylinderRadius,
            DetectionLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in objectHits)
        {
            if (hit == null) continue;

            GameObject obj = hit.gameObject;

            if (IsSelf(hit.transform)) continue;
            if (obj.GetComponentInParent<MagneticAnchor>() != null) continue;

            Vector3 objPos = obj.transform.position;

            if (!IsPointInCylinder(objPos, _cachedCylinderStart, _cachedCylinderDirection, _cachedCylinderEnd))
                continue;

            float distance = Vector3.Distance(_cachedCylinderStart, objPos);
            if (distance < MinDetectionDistance) continue;

            if (!IsVisible(objPos, distance)) continue;

            _cachedObjectDistances[obj] = distance;
        }
    }

    private bool IsPointInCylinder(Vector3 point, Vector3 cylinderStart, Vector3 cylinderAxis, Vector3 cylinderEnd)
    {
        Vector3 toPoint = point - cylinderStart;
        float projectionLength = Vector3.Dot(toPoint, cylinderAxis);

        if (projectionLength < 0 || projectionLength > CylinderLength)
            return false;

        Vector3 closestPointOnAxis = cylinderStart + cylinderAxis * projectionLength;
        float radialDistance = Vector3.Distance(point, closestPointOnAxis);

        return radialDistance <= CylinderRadius;
    }

    private void DetectAnchorsInZone()
    {
        _detectedAnchors.Clear();

        foreach (var kvp in _cachedAnchorDistances)
        {
            MagneticAnchor anchor = kvp.Key;

            if (anchor == null) continue;

            if (!string.IsNullOrEmpty(RequiredTag))
            {
                if (!anchor.CompareTag(RequiredTag) && !anchor.transform.root.CompareTag(RequiredTag))
                    continue;
            }

            _detectedAnchors.Add(anchor);
        }

        DetectedAnchorsCount = _detectedAnchors.Count;
    }

    private void DetectObjectsInZone()
    {
        _detectedObjects.Clear();

        foreach (var kvp in _cachedObjectDistances)
        {
            GameObject obj = kvp.Key;

            if (obj == null) continue;

            if (!string.IsNullOrEmpty(RequiredTag))
            {
                if (!obj.CompareTag(RequiredTag) && !obj.transform.root.CompareTag(RequiredTag))
                    continue;
            }

            _detectedObjects.Add(obj);
        }

        DetectedObjectsCount = _detectedObjects.Count;
    }

    private void FindClosestAnchor()
    {
        ClearHit();

        if (_detectedAnchors.Count == 0)
        {
            ClosestAnchorName = "None";
            ClosestAnchorDistance = 0f;
            return;
        }

        MagneticAnchor closestAnchor = null;
        float closestDistance = float.MaxValue;

        foreach (var anchor in _detectedAnchors)
        {
            if (anchor == null) continue;

            if (_cachedAnchorDistances.TryGetValue(anchor, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestAnchor = anchor;
                }
            }
        }

        if (closestAnchor != null)
        {
            CurrentHitAnchor = closestAnchor;
            CurrentHitObject = closestAnchor.gameObject;
            CurrentHitName = closestAnchor.name;
            CurrentHitTag = closestAnchor.tag;
            ClosestAnchorName = closestAnchor.name;
            ClosestAnchorDistance = closestDistance;
        }
    }

    private void FindClosestObject()
    {
        CurrentClosestObject = null;
        ClosestObjectName = "None";
        ClosestObjectTag = "None";
        ClosestObjectDistance = 0f;

        if (_detectedObjects.Count == 0) return;

        GameObject closestObject = null;
        float closestDistance = float.MaxValue;

        foreach (var obj in _detectedObjects)
        {
            if (obj == null) continue;

            if (_cachedObjectDistances.TryGetValue(obj, out float distance))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestObject = obj;
                }
            }
        }

        if (closestObject != null)
        {
            CurrentClosestObject = closestObject;
            ClosestObjectName = closestObject.name;
            ClosestObjectTag = closestObject.tag;
            ClosestObjectDistance = closestDistance;
        }
    }

    private bool IsSelf(Transform t)
    {
        if (t == null) return false;

        if (OwnerRoot != null)
        {
            if (t == OwnerRoot || t.IsChildOf(OwnerRoot))
                return true;

            Transform current = t;
            while (current != null)
            {
                if (current == OwnerRoot)
                    return true;
                current = current.parent;
            }
        }

        if (OwnerColliders != null && OwnerColliders.Length > 0)
        {
            var c = t.GetComponent<Collider>();
            if (c != null)
            {
                foreach (var ownerCol in OwnerColliders)
                {
                    if (ownerCol == c) return true;
                }
            }

            var parentColliders = t.GetComponentsInParent<Collider>();
            foreach (var pc in parentColliders)
            {
                foreach (var ownerCol in OwnerColliders)
                {
                    if (ownerCol == pc) return true;
                }
            }
        }

        return false;
    }

    private void ClearDetection()
    {
        _detectedAnchors.Clear();
        _detectedObjects.Clear();
        _cachedAnchorDistances.Clear();
        _cachedObjectDistances.Clear();
        DetectedAnchorsCount = 0;
        DetectedObjectsCount = 0;
        ClearHit();
        CurrentRaycastHitAnchor = null;
        CurrentRaycastClosestObject = null;
        RaycastHitName = "None";
        RaycastHitDistance = 0f;
    }

    private void ClearHit()
    {
        CurrentHitObject = null;
        CurrentHitAnchor = null;
        CurrentClosestObject = null;
        CurrentHitTag = string.Empty;
        CurrentHitName = string.Empty;
        ClosestAnchorName = "None";
        ClosestAnchorDistance = 0f;
        ClosestObjectName = "None";
        ClosestObjectTag = "None";
        ClosestObjectDistance = 0f;
    }

    public List<MagneticAnchor> GetDetectedAnchors() => new List<MagneticAnchor>(_detectedAnchors);
    public List<GameObject> GetDetectedObjects() => new List<GameObject>(_detectedObjects);
    public (string tag, string name) GetCurrentHitInfo() => (CurrentHitTag, CurrentHitName);
    public bool IsLookingAt(MagneticAnchor anchor) => _detectedAnchors.Contains(anchor) && CurrentHitAnchor == anchor;

    private void OnDrawGizmos()
    {
        if (!DrawGizmos || Origin == null || DirectionSource == null) return;

        UpdateCylinderCache();

        if (ShowDetectionZone)
        {
            DrawCylinderWireframe(_cachedCylinderStart, _cachedCylinderDirection);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(_cachedCylinderStart, _cachedCylinderEnd);
            DrawArrow(_cachedCylinderStart, _cachedCylinderDirection * CylinderLength, Color.green);
        }

        if (ShowDetectedAnchors && _detectedAnchors.Count > 0)
        {
            foreach (var anchor in _detectedAnchors)
            {
                if (anchor == null) continue;

                Vector3 anchorPos = anchor.GetPosition();

                if (anchor == CurrentHitAnchor)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(anchorPos, 0.3f);

                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_cachedCylinderStart, anchorPos);

                    if (ShowOcclusionRays && EnableOcclusionCheck)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(_cachedCylinderStart, anchorPos);
                    }
                }
                else
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(anchorPos, 0.2f);

                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    Gizmos.DrawLine(_cachedCylinderStart, anchorPos);
                }
            }
        }

        if (ShowDetectedAnchors && _detectedObjects.Count > 0)
        {
            foreach (var obj in _detectedObjects)
            {
                if (obj == null) continue;

                Vector3 objPos = obj.transform.position;

                if (obj == CurrentClosestObject)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireCube(objPos, Vector3.one * 0.25f);
                    Gizmos.DrawLine(_cachedCylinderStart, objPos);
                }
                else
                {
                    Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                    Gizmos.DrawWireCube(objPos, Vector3.one * 0.15f);
                }
            }
        }

        if (EnableCenterRaycast && DirectionSource != null)
        {
            Vector3 rayOrigin = DirectionSource.position + DirectionSource.forward * ForwardOffset;
            Vector3 rayEnd = rayOrigin + DirectionSource.forward * RaycastMaxDistance;
            bool rayHit = CurrentRaycastHitAnchor != null || CurrentRaycastClosestObject != null;
            Gizmos.color = rayHit ? Color.red : new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawLine(rayOrigin, rayEnd);
            Gizmos.DrawWireSphere(rayOrigin, 0.05f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(_cachedCylinderStart, 0.1f);
    }

    private void DrawCylinderWireframe(Vector3 start, Vector3 direction)
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

        Vector3 end = start + direction * CylinderLength;

        DrawCircle(start, direction, CylinderRadius, CylinderSegments);
        DrawCircle(end, direction, CylinderRadius, CylinderSegments);

        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.Cross(direction, Vector3.forward).normalized;
        Vector3 up = Vector3.Cross(right, direction).normalized;

        Vector3[] offsets =
        {
            right * CylinderRadius,
            -right * CylinderRadius,
            up * CylinderRadius,
            -up * CylinderRadius
        };

        foreach (var offset in offsets)
        {
            Gizmos.DrawLine(start + offset, end + offset);
        }
    }

    private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.Cross(normal, Vector3.forward).normalized;
        Vector3 forward = Vector3.Cross(right, normal).normalized;

        float angleStep = 360f / segments;
        Vector3 prevPoint = center + right * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 point = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }

    private void DrawArrow(Vector3 start, Vector3 direction, Color color)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 end = start + direction;
        Gizmos.color = color;

        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

        float arrowHeadLength = direction.magnitude * 0.2f;
        Gizmos.DrawLine(end, end + right * arrowHeadLength);
        Gizmos.DrawLine(end, end + left * arrowHeadLength);
    }
}