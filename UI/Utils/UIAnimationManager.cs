using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>UI 패널의 페이드 인/아웃 애니메이션과 블랙스크린 전환 효과를 관리하는 매니저</summary>
public class UIAnimationManager : MonoBehaviour
{
    public static UIAnimationManager Instance;
    
    [Header("애니메이션 적용할 패널들")]
    [SerializeField] private List<GameObject> animatedPanels = new List<GameObject>();
    
    [Header("타임라인용 블랙스크린 패널")]
    [SerializeField] private GameObject blackScreenPanel;
    
    [Header("일반 패널 애니메이션 설정")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private Ease fadeInEase = Ease.OutQuad;
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;
    
    [Header("블랙스크린 페이드 설정")]
    [SerializeField] private float blackScreenFadeDuration = 1.5f;
    
    private Dictionary<GameObject, PanelData> panelDataMap = new Dictionary<GameObject, PanelData>();
    private Dictionary<string, GameObject> panelNameMap = new Dictionary<string, GameObject>();

    // 애니메이션 진행 중인 패널 추적
    private HashSet<string> animatingPanels = new HashSet<string>();

    private CanvasGroup blackScreenCanvasGroup;

    private class PanelData
    {
        public CanvasGroup canvasGroup;
        public Tween activeTween;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        
        InitializePanels();
        InitializeBlackScreen();
    }

    private void InitializePanels()
    {
        foreach (var panel in animatedPanels)
        {
            if (panel == null) continue;
            
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = panel.AddComponent<CanvasGroup>();
            
            cg.alpha = panel.activeSelf ? 1f : 0f;
            
            panelDataMap[panel] = new PanelData { canvasGroup = cg };
            panelNameMap[panel.name] = panel;
        }
    }

    private void InitializeBlackScreen()
    {
        if (blackScreenPanel != null)
        {
            blackScreenCanvasGroup = blackScreenPanel.GetComponent<CanvasGroup>();
            if (blackScreenCanvasGroup == null)
                blackScreenCanvasGroup = blackScreenPanel.AddComponent<CanvasGroup>();
            
            blackScreenCanvasGroup.alpha = 0f;
            blackScreenPanel.SetActive(false);
        }
    }

    public void ShowPanel(string panelName)
    {
        if (!panelNameMap.TryGetValue(panelName, out var panel)) return;
        if (!panelDataMap.TryGetValue(panel, out var data)) return;

        // 애니메이션 진행 중 표시
        animatingPanels.Add(panelName);

        data.activeTween?.Kill();
        panel.SetActive(true);
        data.canvasGroup.alpha = 0f;
        data.activeTween = data.canvasGroup.DOFade(1f, fadeDuration)
            .SetEase(fadeInEase)
            .SetUpdate(true)
            .OnComplete(() => animatingPanels.Remove(panelName)); // 완료 시 제거
    }

    public void HidePanel(string panelName, System.Action onComplete = null)
    {
        if (!panelNameMap.TryGetValue(panelName, out var panel))
        {
            onComplete?.Invoke();
            return;
        }
        if (!panelDataMap.TryGetValue(panel, out var data))
        {
            onComplete?.Invoke();
            return;
        }
        if (!panel.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }
        
        StartCoroutine(HidePanelCoroutine(panel, data, onComplete));
    }
    
    private System.Collections.IEnumerator HidePanelCoroutine(GameObject panel, PanelData data, System.Action onComplete)
    {
        // 애니메이션 진행 중 표시
        animatingPanels.Add(panel.name);

        data.activeTween?.Kill();
        data.activeTween = data.canvasGroup.DOFade(0f, fadeDuration)
            .SetEase(fadeOutEase)
            .SetUpdate(true);
    
        yield return data.activeTween.WaitForCompletion();  // Tween 완료 대기
    
        if (panel != null)
            panel.SetActive(false);

        // 완료 시 제거
        animatingPanels.Remove(panel.name);

        onComplete?.Invoke();
    }

    /// <summary>
    /// 검은 화면 → 밝게 (FadeIn: 블랙스크린 사라짐)
    /// </summary>
    public void FadeIn(System.Action onComplete = null)
    {
        if (blackScreenPanel == null || blackScreenCanvasGroup == null) return;
        
        blackScreenCanvasGroup.DOKill();
        blackScreenPanel.SetActive(true);
        blackScreenCanvasGroup.alpha = 1f;
        
        blackScreenCanvasGroup.DOFade(0f, blackScreenFadeDuration)
            .SetEase(fadeOutEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (blackScreenPanel != null)
                    blackScreenPanel.SetActive(false);
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// 밝은 화면 → 검게 (FadeOut: 블랙스크린 나타남)
    /// </summary>
    public void FadeOut(System.Action onComplete = null)
    {
        if (blackScreenPanel == null || blackScreenCanvasGroup == null) return;
        
        blackScreenCanvasGroup.DOKill();
        blackScreenPanel.SetActive(true);
        blackScreenCanvasGroup.alpha = 0f;
        
        blackScreenCanvasGroup.DOFade(1f, blackScreenFadeDuration)
            .SetEase(fadeInEase)
            .SetUpdate(true)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// 밝은 화면 → 검게 → 밝게 (FadeOut → FadeIn)
    /// </summary>
    public void FadeOutIn(System.Action onFadeOutComplete = null, System.Action onComplete = null)
    {
        FadeOut(() =>
        {
            onFadeOutComplete?.Invoke();
            FadeIn(onComplete);
        });
    }

    /// <summary>
    /// 블랙스크린 즉시 검게
    /// </summary>
    public void SetBlack()
    {
        if (blackScreenPanel == null || blackScreenCanvasGroup == null) return;
        
        blackScreenCanvasGroup.DOKill();
        blackScreenPanel.SetActive(true);
        blackScreenCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 블랙스크린 즉시 투명
    /// </summary>
    public void SetClear()
    {
        if (blackScreenPanel == null || blackScreenCanvasGroup == null) return;
        
        blackScreenCanvasGroup.DOKill();
        blackScreenCanvasGroup.alpha = 0f;
        blackScreenPanel.SetActive(false);
    }

    public bool IsAnimatedPanel(string panelName)
    {
        return panelNameMap.ContainsKey(panelName);
    }

    // 새 메서드 추가
    public bool IsAnimating(string panelName)
    {
        return animatingPanels.Contains(panelName);
    }

    private void OnDestroy()
    {
        foreach (var data in panelDataMap.Values)
            data.activeTween?.Kill();
        
        blackScreenCanvasGroup?.DOKill();
    }
}