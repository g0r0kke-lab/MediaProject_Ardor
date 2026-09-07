using UnityEngine;
using DG.Tweening;

/// <summary>
/// 씬 전환 또는 이벤트 사이에 DOTween을 이용한 부드러운 페이드 인/아웃 전환을 제공하는 전체 화면 블랙 CanvasGroup 제어 싱글톤입니다.
/// </summary>
public class BlackScreenFader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    
    private CanvasGroup _canvasGroup;
    private Tween _currentTween;
    private bool _isFading = false;

    // 싱글톤 (어디서든 접근 가능)
    public static BlackScreenFader Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 즉시 까맣게 (페이드 없이)
    /// </summary>
    public void ShowImmediate()
    {
        _currentTween?.Kill();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// 즉시 투명하게 (페이드 없이)
    /// </summary>
    public void HideImmediate()
    {
        _currentTween?.Kill();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }
    
    /// <summary>
    /// 밝아짐 (검은 화면 사라짐)
    /// </summary>
    public void FadeIn()
    {
        _currentTween?.Kill();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        
        _currentTween = _canvasGroup.DOFade(0f, fadeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _canvasGroup.blocksRaycasts = false;
            });
    }

    /// <summary>
    /// 어두워짐 (검은 화면 나타남)
    /// </summary>
    public void FadeOut()
    {
        _currentTween?.Kill();
        if (_canvasGroup)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
        
            _currentTween = _canvasGroup.DOFade(1f, fadeDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }
    }

    void OnDestroy()
    {
        _currentTween?.Kill();
        if (Instance == this) Instance = null;
    }
}