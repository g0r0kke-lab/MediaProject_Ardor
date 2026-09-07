using UnityEngine;
using UnityEngine.UI; 

/// <summary>
/// 이 게임오브젝트가 활성 상호작용 대상이 될 때 월드 공간 하이라이트 파티클 또는 HUD 화면 외 인디케이터를 표시합니다.
/// </summary>
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

        Vector3 particleWorldPos = highlightParticle.transform.position;
        
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

        // 삭제: isBehind 판정 및 WorldToScreenPoint 기반 dir 계산 전체

        // 뷰포트 좌표 기반으로 방향 계산 (BossDirectionIndicator 방식)
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
        bool isBehind = viewportPos.z < 0f;

        float viewX = viewportPos.x - 0.5f;
        float viewY = viewportPos.y - 0.5f;

        Vector2 dir;
        if (isBehind)
            dir = new Vector2(viewX, -1f).normalized; // 후방이면 아래쪽으로 고정
        else
            dir = new Vector2(viewX, viewY).normalized;

        float halfW = (hudPosMax.x - hudPosMin.x) * 0.5f;
        float halfH = (hudPosMax.y - hudPosMin.y) * 0.5f;

        Vector2 indicatorPos;
        if (isBehind)
        {
            float xPos = Mathf.Clamp(viewX * halfW * 2f, -halfW, halfW);
            indicatorPos = new Vector2(xPos, -halfH); // 항상 하단
        }
        else if (Mathf.Abs(dir.x) * halfH > Mathf.Abs(dir.y) * halfW)
        {
            float sign = Mathf.Sign(dir.x);
            indicatorPos = new Vector2(sign * halfW, (dir.y / dir.x) * sign * halfW);
        }
        else
        {
            float sign = Mathf.Sign(dir.y);
            indicatorPos = new Vector2((dir.x / dir.y) * sign * halfH, sign * halfH);
        }

        hudParticleObject.transform.localPosition = new Vector3(indicatorPos.x, indicatorPos.y, 0f);
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