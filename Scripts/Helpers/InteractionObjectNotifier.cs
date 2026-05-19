using UnityEngine;
using UnityEngine.UI; 

public class InteractionObjectNotifier : MonoBehaviour
{
    [SerializeField] private ParticleSystem highlightParticle;
    
    // HUD 파티클 관련 필드
    [Header("HUD Particle")]
    [SerializeField] private GameObject hudParticleObject;   // 파티클이 붙은 GameObject
    [SerializeField] private RectTransform hudParticleRect;  // 마커 패널 RectTransform
    [SerializeField, ReadOnly] private Camera mainCamera;
    [SerializeField] private LayerMask occlusionMask;

    private ParticleSystem _hudPS;
    private Transform _playerTransform;
    
    [SerializeField] private Vector2 hudPosMin = new Vector2(-850f, -450f); 
    [SerializeField] private Vector2 hudPosMax = new Vector2(850f, 450f);   

    private bool _isHudTracking = false;
    private Canvas _hudCanvas;

    private void Start()
    {
        EventBroker.Instance?.Subscribe("InteractionObjectActivated", OnActivated);
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);

        if (hudParticleObject != null)
            _hudPS = hudParticleObject.GetComponent<ParticleSystem>();

        if (hudParticleRect != null)
            _hudCanvas = hudParticleRect.GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (_isHudTracking && hudParticleRect != null)
            UpdateHudPosition();
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe("InteractionObjectActivated", OnActivated);
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        _isHudTracking = false; 

        if (MagneticManager.Instance != null)
            MagneticManager.Instance.RemoveCollisionActiveObject(gameObject);
    }

    private void OnActivated(object data)
    {
        var target = data as GameObject;
        if (target != gameObject) return;
        if (mainCamera == null) return;

        // 카메라 뷰포트 기준으로 화면 안/밖 판별
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
        bool isVisible = viewportPos.z > 0f
                         && viewportPos.x is >= 0f and <= 1f
                         && viewportPos.y is >= 0f and <= 1f;
        // Debug.Log($"[Notifier] worldPos={transform.position}, viewportPos={viewportPos}, isVisible={isVisible}");

        if (IsParticleVisible())
        {
            // 기존 월드 파티클
            _isHudTracking = false; 
            if (highlightParticle != null)
            {
                highlightParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                highlightParticle.Play();
            }
        }
        else
        {
            // HUD 파티클
            if (hudParticleObject != null && hudParticleRect != null)
            {
                UpdateHudPosition();
                if (_hudPS != null)
                {
                    _hudPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _hudPS.Play();
                }
                _isHudTracking = true;
            }
        }
    }

    private bool IsParticleVisible()
    {
        if (mainCamera == null || highlightParticle == null) return false;

        Vector3 particleWorldPos = highlightParticle.transform.position; // ✨ 파티클 위치 기준
        Vector3 dirToParticle = particleWorldPos - mainCamera.transform.position;
        float distance = dirToParticle.magnitude;

        // ✨ 카메라 → 파티클 방향으로 레이캐스트
        if (Physics.Raycast(mainCamera.transform.position, dirToParticle.normalized, out RaycastHit hit, distance, occlusionMask))
        {
            // 뭔가 막혔으면 안 보임
            return false;
        }

        // ✨ Frustum 밖인지도 같이 체크
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(particleWorldPos);
        return viewportPos.z > 0f
               && viewportPos.x is >= 0f and <= 1f
               && viewportPos.y is >= 0f and <= 1f;
    }
    
    // 매 프레임 HUD 파티클 위치 갱신
    private void UpdateHudPosition()
    {
        if (mainCamera == null || hudParticleObject == null || hudParticleRect == null) return;

        if (_playerTransform == null) return;
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camPos     = mainCamera.transform.position;
        float playerDepth  = Vector3.Dot(camForward, _playerTransform.position - camPos);
        float objectDepth  = Vector3.Dot(camForward, transform.position        - camPos);
        
        // 오브젝트가 플레이어보다 카메라에 가까우면 뒤(아래)
        bool isBehind = objectDepth < playerDepth;
       
        // 뷰포트 x 좌표 추출 (0~1). 뒤에 있으면 x 반전
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
        float viewportX = isBehind ? 1f - viewportPos.x : viewportPos.x;

        // 뷰포트 x(0~1) → 마커 패널 로컬 x 범위(-850~850)로 선형 변환
        float x = Mathf.Lerp(hudPosMin.x, hudPosMax.x, Mathf.Clamp01(viewportX));

        // y: 카메라 앞이면 위, 뒤면 아래 고정
        float y = isBehind ? hudPosMin.y : hudPosMax.y;

        hudParticleObject.transform.localPosition = new Vector3(x, y, 0f);
    }
    
    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject
                            ?? GameObject.FindWithTag("Player");
        if (player == null) return;

        mainCamera       = player.GetComponentInChildren<Camera>();
        _playerTransform = player.transform;
    }
}