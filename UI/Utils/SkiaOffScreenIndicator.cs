using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StorageSkia 여러 마리의 오프스크린 인디케이터를 한 컴포넌트에서 관리.
/// Screen Space - Overlay 캔버스의 자식에 배치.
///
/// 세팅 방법:
///   1. Canvas 자식 GameObject에 이 컴포넌트 추가.
///   2. 공통 스프라이트(spriteNormal, spriteActive) 할당.
///   3. Indicators 리스트에 스키아 수만큼 항목 추가 (skia, indicatorRoot, arrowImage, fillImage).
///   4. 각 항목의 IndicatorRoot RectTransform Anchor/Pivot을 Center(0.5, 0.5)로 설정.
/// </summary>
public class SkiaOffScreenIndicator : MonoBehaviour
{
    [System.Serializable]
    public class IndicatorEntry
    {
        public StorageSkia skia;
        public RectTransform indicatorRoot; // 위치·회전이 적용될 RectTransform
        public Image arrowImage;            // 화살표 (스프라이트 전환 + 회전)
        public Image fillImage;             // 게이지 Fill Image

        [HideInInspector] public WalkDurationTrigger trigger;
        [HideInInspector] public Canvas canvas;
    }

    [Header("공통 스프라이트")]
    [SerializeField] private Sprite spriteDeactive;
    [SerializeField] private Sprite spriteNormal;
    [SerializeField] private Sprite spriteActive;

    [Header("인디케이터 목록")]
    [SerializeField] private List<IndicatorEntry> indicators = new List<IndicatorEntry>();

    [Header("설정")]
    [SerializeField] private float borderOffset = 50f;
    [Tooltip("HUD 기준이 되는 캔버스 RectTransform — 직접 할당 권장")]
    [SerializeField] private RectTransform _canvasRect;

    [Header("왼쪽 가장자리 Y 제한")]
    [SerializeField] private float _leftClampYMin = 160f;
    [SerializeField] private float _leftClampYMax = 310f;

    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;

        if (_canvasRect == null)
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas)
                _canvasRect = parentCanvas.GetComponent<RectTransform>();
            else
                Debug.LogError("[SkiaOffScreenIndicator] 부모 Canvas를 찾을 수 없습니다. 인스펙터에서 직접 할당하세요.");
        }

        foreach (var entry in indicators)
        {
            if (entry.skia != null && StorageSkiaManager.Instance != null)
                entry.trigger = StorageSkiaManager.Instance.GetTriggerForSkia(entry.skia);

            if (entry.indicatorRoot != null)
            {
                entry.canvas = entry.indicatorRoot.GetComponent<Canvas>();
                entry.indicatorRoot.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!_mainCamera) _mainCamera = Camera.main;
        if (!_mainCamera || !_canvasRect) return;

        foreach (var entry in indicators)
            UpdateEntry(entry);
    }

    private void UpdateEntry(IndicatorEntry entry)
    {
        if (entry.skia == null || entry.indicatorRoot == null) return;

        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(entry.skia.transform.position);
        bool isBehind    = viewportPos.z < 0;
        bool isOffScreen = viewportPos.x < 0 || viewportPos.x > 1 ||
                           viewportPos.y < 0 || viewportPos.y > 1;

        bool shouldShow = isBehind || isOffScreen;
        entry.indicatorRoot.gameObject.SetActive(shouldShow);
        if (!shouldShow) return;

        Vector2 pos = CalcEdgePosition(viewportPos, isBehind);

        float edgeX = _canvasRect.sizeDelta.x * 0.5f - borderOffset;
        if (Mathf.Approximately(pos.x, -edgeX) && pos.y >= _leftClampYMin && pos.y <= _leftClampYMax)
        {
            float mid = (_leftClampYMin + _leftClampYMax) * 0.5f;
            pos.y = pos.y < mid ? _leftClampYMin : _leftClampYMax;
        }

        entry.indicatorRoot.anchoredPosition = pos;

        UpdateGauge(entry);
    }

    private void UpdateGauge(IndicatorEntry entry)
    {
        if (entry.skia == null) return;

        if (!entry.skia.IsPlayerInZone)
        {
            if (entry.arrowImage != null) entry.arrowImage.sprite = spriteDeactive;
            if (entry.fillImage != null)  entry.fillImage.fillAmount = 0f;
            if (entry.canvas != null)     entry.canvas.sortingOrder = 0;
            return;
        }

        if (entry.canvas != null) entry.canvas.sortingOrder = 1;

        if (entry.trigger == null) return;

        float timer      = entry.trigger.WalkTimer;
        float gaugeFull  = entry.trigger.GaugeFullDuration;
        bool isGaugeFull = timer >= gaugeFull;

        if (entry.arrowImage != null)
            entry.arrowImage.sprite = isGaugeFull ? spriteActive : spriteNormal;

        if (entry.fillImage != null)
            entry.fillImage.fillAmount = isGaugeFull ? 0f
                                                     : (gaugeFull > 0f ? timer / gaugeFull : 0f);
    }

    private Vector2 CalcEdgePosition(Vector3 viewportPos, bool isBehind)
    {
        Vector2 canvasSize = _canvasRect.sizeDelta;
        float edgeX = canvasSize.x * 0.5f - borderOffset;
        float edgeY = canvasSize.y * 0.5f - borderOffset;

        float vx = viewportPos.x - 0.5f;
        float vy = viewportPos.y - 0.5f;

        if (isBehind)
        {
            float xPos = Mathf.Clamp(vx * canvasSize.x, -edgeX, edgeX);
            return new Vector2(xPos, -edgeY);
        }

        Vector2 dir = new Vector2(vx, vy);
        if (dir.sqrMagnitude < 0.0001f) return Vector2.zero;
        dir.Normalize();

        if (Mathf.Abs(dir.x) * edgeY > Mathf.Abs(dir.y) * edgeX)
        {
            float sign = Mathf.Sign(dir.x);
            return new Vector2(sign * edgeX, dir.y / dir.x * sign * edgeX);
        }
        else
        {
            float sign = Mathf.Sign(dir.y);
            return new Vector2(dir.x / dir.y * sign * edgeY, sign * edgeY);
        }
    }
}
