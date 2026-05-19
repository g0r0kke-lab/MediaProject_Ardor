using System;
using System.Collections;
using UnityEngine;
using FronkonGames.Glitches.VHS;

public class DeathEffectManager : MonoBehaviour
{
    public static DeathEffectManager Instance { get; private set; }

    /// <summary>블랙스크린이 완전히 덮였을 때 발행되는 이벤트</summary>
    public const string EVENT_SCREEN_BLACK = "DeathEffect/ScreenBlack";

    // 글리치 에셋 컴포넌트 — 인스펙터에서 연결
    [Header("References")]
    [SerializeField] private float glitchDuration = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 글리치 → 블랙스크린 페이드인 후 onComplete 콜백 호출
    /// </summary>
    public void PlayDeathEffect(Action onComplete)
    {
        StartCoroutine(DeathEffectCoroutine(onComplete));
    }

    private IEnumerator DeathEffectCoroutine(Action onComplete)
    {
        // 글리치 연출
        yield return StartCoroutine(PlayVhsEffect(glitchDuration));

        // 블랙스크린 페이드인
        LoadingManager.Instance.StartFadeOut(-1, false);
        yield return new WaitUntil(() => !LoadingManager.Instance.IsFading);

        // 화면이 완전히 까매진 시점 — 텔레포트 등 처리용
        EventBroker.Instance?.Publish(EVENT_SCREEN_BLACK);

        onComplete?.Invoke();
    }

    // 글리치 에셋 코루틴
    private IEnumerator PlayVhsEffect(float duration)
    {
        var vhs = VHS.Instance;
        if (vhs != null) vhs.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (vhs != null) vhs.SetActive(false);
    }

    /// <summary>
    /// 리스폰 후 블랙스크린 해제 (PlayerCrouchAndDeath에서 호출)
    /// </summary>
    public void FadeIn()
    {
        SoundManager.Instance.ResumeBGM();
        LoadingManager.Instance.StartFadeIn();
    }
}