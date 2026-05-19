using UnityEngine;

/// <summary>
/// UI 요소를 카메라 방향으로 회전시키는 빌보드 컴포넌트
/// </summary>
public class UIBillboard : MonoBehaviour
{
    [Header("Billboard Settings")]
    [Tooltip("빌보드가 바라볼 카메라 (null이면 메인 카메라 사용)")]
    [SerializeField] private Camera targetCamera;
    
    [Tooltip("빌보드 회전 모드")]
    [SerializeField] private BillboardMode mode = BillboardMode.LookAtCamera;
    
    [Tooltip("Y축 회전만 적용 (수평 회전만)")]
    [SerializeField] private bool lockYAxis = false;

    // 거리 컬링 설정 추가
    [Header("Distance Culling")]
    [Tooltip("거리 체크 활성화")]
    [SerializeField] private bool enableDistanceCulling = true;
    [Tooltip("UI가 보이는 최대 거리")]
    [SerializeField] private float maxVisibleDistance = 15f;

    private Transform cameraTransform;
    
    // static으로 카메라 공유 (한 번만 찾기)
    private static Camera sharedCamera;
    private static Transform sharedCameraTransform;
    
    [Header("Manager References")]
    [SerializeField, ReadOnly] private EventBroker eventBroker;

    // CanvasGroup 추가
    private CanvasGroup canvasGroup;
    private SpriteRenderer spriteRenderer;

    public enum BillboardMode
    {
        LookAtCamera,
        CameraForward,
        OppositeDirection
    }
    
    private void Start()
    {
        if (eventBroker == null)
        {
            eventBroker = EventBroker.Instance;
        }
        
        // CanvasGroup 또는 SpriteRenderer 준비
        if (enableDistanceCulling)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }
    }

    private void OnEnable()
    {
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
        
        // 이미 찾은 카메라가 있으면 재사용
        if (sharedCamera != null)
        {
            targetCamera = sharedCamera;
            cameraTransform = sharedCameraTransform;
        }
        // 없으면 즉시 찾기 시도
        else
        {
            TryFindCamera();
        }
    }

    private void OnDisable()
    {
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
    }

    // 플레이어 스폰 이벤트 핸들러
    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player != null)
        {
            // 기존 카메라가 파괴되었거나 null인지 확인
            if (sharedCamera == null || !sharedCamera.gameObject.activeInHierarchy)
            {
                sharedCamera = player.GetComponentInChildren<Camera>(true);
                if (sharedCamera != null)
                {
                    sharedCameraTransform = sharedCamera.transform;
                }
            }
        }
    
        targetCamera = sharedCamera;
        cameraTransform = sharedCameraTransform;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null || targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
        {
            TryFindCamera();
            if (cameraTransform == null) return;
        }

        Vector3 targetPosition;
        
        switch (mode)
        {
            case BillboardMode.LookAtCamera:
                targetPosition = cameraTransform.position;
                break;
                
            case BillboardMode.CameraForward:
                targetPosition = transform.position + cameraTransform.forward;
                break;
                
            case BillboardMode.OppositeDirection:
                targetPosition = transform.position - (cameraTransform.position - transform.position);
                break;
                
            default:
                targetPosition = cameraTransform.position;
                break;
        }

        if (lockYAxis)
        {
            Vector3 directionToCamera = targetPosition - transform.position;
            directionToCamera.y = 0;
            
            if (directionToCamera != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToCamera);
            }
        }
        else
        {
            Vector3 directionToCamera = targetPosition - transform.position;
            if (directionToCamera != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToCamera);
            }
        }

        // 거리 체크 및 Culling
        if (enableDistanceCulling)
        {
            float distance = Vector3.Distance(transform.position, cameraTransform.position);

            if (distance > maxVisibleDistance)
            {
                // Canvas UI인 경우
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                    canvasGroup.blocksRaycasts = false;
                }
                // Sprite인 경우
                else if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = 0;
                    spriteRenderer.color = color;
                }
            }
            else
            {
                // Canvas UI인 경우
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1;
                    canvasGroup.blocksRaycasts = true;
                }
                // Sprite인 경우
                else if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = 1;
                    spriteRenderer.color = color;
                }
            }
        }
    }

    private void TryFindCamera()
    {
        // targetCamera가 설정되어 있고 유효하면 사용
        if (targetCamera != null && targetCamera.gameObject.activeInHierarchy)
        {
            sharedCamera = targetCamera;
            sharedCameraTransform = targetCamera.transform;
            cameraTransform = sharedCameraTransform;
            return;
        }

        // 메인 카메라 찾기
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            sharedCamera = mainCam;
            sharedCameraTransform = mainCam.transform;
            targetCamera = sharedCamera;
            cameraTransform = sharedCameraTransform;
        }
    }
    
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        cameraTransform = camera != null ? camera.transform : null;
    }

    public void SetBillboardMode(BillboardMode newMode)
    {
        mode = newMode;
    }
}