using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 씬 전환 시 화면 페이드 및 BGM 페이드아웃을 관리하는 싱글톤으로, 초기 프레임 안정화 후 VSync로 전환합니다.
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("References")] [SerializeField]
    protected GameObject fadePanel;

    [SerializeField] private DOTweenAnimation fadeAnimation;

    // [Header("Scene Transition UI")] [SerializeField]
    // private GameObject sceneTransitionObject; // 씬 전환 시 활성화할 오브젝트
    // [SerializeField] private UnityEngine.UI.Button titleButton;
    
    [Header("Audio Settings")] [SerializeField]
    private float bgmFadeDuration = 1f; // BGM 페이드아웃 시간

    // 페이드 완료 여부 플래그
    private bool isFading = false;
    public bool IsFading => isFading;
    private bool isGameFullyLoaded = false;
    private bool isBGMFading = false;
    public bool IsBGMFading => isBGMFading;

    // 페이드아웃이 완료되어 화면이 완전히 가려진 상태인지 (씬 전환 후 도착 씬에서 중복 페이드아웃 방지용)
    public bool IsScreenObscured => fadePanel != null && fadePanel.activeSelf && !isFading;


    // BGM 페이드아웃용 코루틴 추적
    private Coroutine _bgmFadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Canvas 전체를 유지

        // 게임 시작 시 60fps 강제 (초반 안정화)
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // 초기 상태: 비활성화
        fadePanel.SetActive(false);
        
        // if (titleButton != null)
        // {
        //     titleButton.onClick.AddListener(OnTitleButtonClicked);
        // }
    }
    
    private void OnTitleButtonClicked()
    {
        GameManagerRegistry.TransitionToTitle();
    }

    // Start에서 VSync로 전환
    private void Start()
    {
        StartCoroutine(StabilizeAndSwitchToVSync());
    }

    // 초반 안정화 후 VSync로 전환
    private IEnumerator StabilizeAndSwitchToVSync()
    {
        // 최소 3초는 대기
        yield return new WaitForSeconds(3f);

        // 최대 10초까지, 프레임이 안정되면 조기 종료
        float elapsedTime = 3f;
        while (elapsedTime < 10f && !IsFrameStable())
        {
            yield return new WaitForSeconds(0.5f);
            elapsedTime += 0.5f;
        }

        DebugLogger.Log($"✅ 프레임 안정화 완료 ({elapsedTime}초)");

        yield return new WaitUntil(() => isGameFullyLoaded);

#if !UNITY_EDITOR
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#endif
    }

    // 프레임이 안정적인지 체크 (연속 10프레임이 55fps 이상)
    private bool IsFrameStable()
    {
        // 간단한 구현 예시
        return Time.deltaTime < 0.018f; // 약 55fps 이상
    }

    // 외부에서 호출 (GameManager에서)
    public void NotifyGameFullyLoaded()
    {
        isGameFullyLoaded = true;
    }

    public void StartFadeOut()
    {
        StartFadeOut(-1); // 기본 페이드아웃 (씬 체크 안함)
    }

    /// <summary>
    /// 씬 인덱스를 받아서 페이드아웃 시작
    /// </summary>
    /// <param name="targetSceneIndex">이동할 씬 인덱스 (-1이면 체크 안함)</param>
    public void StartFadeOut(int targetSceneIndex, bool fadeBGM = true)
    {
        isFading = true;
        fadePanel.SetActive(true);
        fadeAnimation.enabled = true;
        fadeAnimation.DORestart();

        Cursor.lockState = CursorLockMode.Locked;

        // 씬 2로 이동할 때만 특별한 오브젝트 활성화
        // if (targetSceneIndex == 2 && sceneTransitionObject != null)
        // {
        //     sceneTransitionObject.SetActive(true);
        //     Cursor.lockState = CursorLockMode.None;
        //     Debug.Log("[LoadingManager] 씬 2 전환 UI 활성화");
        // }
        
        // BGM 페이드아웃 시작
        if (fadeBGM) FadeBGMOut();

        // 애니메이션 완료 콜백 등록
        fadeAnimation.tween.OnComplete(() =>
        {
            isFading = false;
            
            // 씬 전환 오브젝트 비활성화
            // if (sceneTransitionObject != null)
            // {
            //     sceneTransitionObject.SetActive(false);
            // }
            // DebugLogger.Log("✅ 페이드아웃 완료");
        });
    }

    public void StartFadeIn()
    {
        isFading = true;
        fadeAnimation.DOPlayBackwards(); // 역재생

        // 역재생 완료 콜백 등록
        fadeAnimation.tween.OnRewind(() =>
        {
            fadePanel.SetActive(false);
            isFading = false;
            // DebugLogger.Log("✅ 페이드인 완료");
        });
    }

    /// <summary>
    /// BGM 페이드아웃 처리
    /// </summary>
    private void FadeBGMOut()
    {
        if (SoundManager.Instance == null)
        {
            DebugLogger.LogWarning("[LoadingManager] SoundManager를 찾을 수 없습니다.");
            return;
        }

        // 기존 페이드 코루틴이 있으면 중지
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }

        _bgmFadeCoroutine = StartCoroutine(FadeBGMCoroutine());
    }

    /// <summary>
    /// BGM 볼륨을 점진적으로 줄이는 코루틴
    /// </summary>
    private IEnumerator FadeBGMCoroutine()
    {
        isBGMFading = true;
        AudioSource bgmSource = SoundManager.Instance.GetBGMSource();

        if (bgmSource == null)
        {
            yield break;
        }

        float startVolume = bgmSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < bgmFadeDuration)
        {
            if (bgmSource == null)
            {
                isBGMFading = false;
                yield break;
            }
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / bgmFadeDuration;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        // 완전히 0으로 설정하고 정지
        if (bgmSource != null) bgmSource.volume = 0f;
        SoundManager.Instance.PauseBGM();

        // 볼륨 복구 (다음 재생을 위해)
        if (bgmSource != null) bgmSource.volume = startVolume;

        isBGMFading = false;
        DebugLogger.Log("[LoadingManager] BGM 페이드아웃 완료");
    }
}