using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using FronkonGames.Glitches.VHS;
using UnityEngine.Localization;

/// <summary>
/// VHS 글리치, 블랙스크린 페이드, 현지화된 팁 표시, 리스폰 후 페이드 인을 포함한 전체 사망 연출 시퀀스를 조율하는 싱글톤입니다.
/// </summary>
public class DeathEffectManager : MonoBehaviour
{
    public static DeathEffectManager Instance { get; private set; }

    /// <summary>블랙스크린이 완전히 덮였을 때 발행되는 이벤트</summary>
    public const string EVENT_SCREEN_BLACK = "DeathEffect/ScreenBlack";

    private const float TIP_FADE_DURATION = 0.2f;

    [Header("References")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float blackScreenDuration = 3f;
    [SerializeField] private TextMeshProUGUI _tipText;

    [Header("Localization")]
    [SerializeField] private List<string> stringTableNames = new List<string>();

    private string _pendingTipKey = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var vhs = VHS.Instance;
        if (vhs != null) vhs.SetActive(false);

        if (_tipText != null)
            _tipText.gameObject.SetActive(false);
    }

    // _tipText가 씬 오버라이드 없이 null인 경우(빌드 첫 씬 등) 런타임에 찾아서 캐시
    private void EnsureTipText()
    {
        if (_tipText != null) return;
        foreach (var tmp in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (tmp.name == "Tmp_Tip")
            {
                _tipText = tmp;
                _tipText.gameObject.SetActive(false);
                break;
            }
        }
    }

    /// <summary>
    /// 죽음 트리거 시점에 미리 팁 키를 예약. PlayDeathEffect에서 한 번 소비 후 자동 클리어.
    /// </summary>
    public void SetPendingTip(string key)
    {
        _pendingTipKey = key;
    }

    /// <summary>
    /// 글리치 → 블랙스크린 후 onComplete 콜백 호출.
    /// </summary>
    public void PlayDeathEffect(Action onComplete)
    {
        StartCoroutine(DeathEffectCoroutine(onComplete));
    }

    private IEnumerator DeathEffectCoroutine(Action onComplete)
    {
        yield return StartCoroutine(PlayVhsEffect(glitchDuration));

        LoadingManager.Instance.StartFadeOut(-1, false);
        yield return new WaitUntil(() => !LoadingManager.Instance.IsFading);

        EventBroker.Instance?.Publish(EVENT_SCREEN_BLACK);

        // 예약된 팁 소비 — 즉시 클리어해서 다음 사망에 잔류하지 않도록
        string tipKey = _pendingTipKey;
        _pendingTipKey = null;

        yield return StartCoroutine(ShowTipCoroutine(tipKey));

        yield return new WaitForSeconds(blackScreenDuration);

        yield return StartCoroutine(HideTipCoroutine());

        onComplete?.Invoke();
    }

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
        ForceHideTip();
        SoundManager.Instance.ResumeBGM();
        LoadingManager.Instance.StartFadeIn();
    }

    private IEnumerator ShowTipCoroutine(string key)
    {
        EnsureTipText();
        if (_tipText == null) yield break;

        string text = null;
        yield return StartCoroutine(ResolveTipTextAsync(key, result => text = result));

        if (text == null)
        {
            _tipText.gameObject.SetActive(false);
            yield break;
        }

        _tipText.text = text;

        // alpha 0에서 시작해서 페이드인
        _tipText.DOKill();
        SetTipAlpha(0f);
        _tipText.gameObject.SetActive(true);
        _tipText.DOFade(1f, TIP_FADE_DURATION);

        yield return new WaitForSeconds(TIP_FADE_DURATION);
    }

    private IEnumerator HideTipCoroutine()
    {
        EnsureTipText();
        if (_tipText == null || !_tipText.gameObject.activeSelf) yield break;

        _tipText.DOKill();
        _tipText.DOFade(0f, TIP_FADE_DURATION);

        yield return new WaitForSeconds(TIP_FADE_DURATION);

        _tipText.gameObject.SetActive(false);
        SetTipAlpha(1f); // 다음 사용을 위해 알파 복원
    }

    // FadeIn() 등 즉시 숨겨야 할 때 사용
    private void ForceHideTip()
    {
        EnsureTipText();
        if (_tipText == null) return;
        _tipText.DOKill();
        _tipText.gameObject.SetActive(false);
        SetTipAlpha(1f);
    }

    private void SetTipAlpha(float alpha)
    {
        Color c = _tipText.color;
        c.a = alpha;
        _tipText.color = c;
    }

    private IEnumerator ResolveTipTextAsync(string key, Action<string> callback)
    {
        if (string.IsNullOrEmpty(key)) { callback(null); yield break; }

        foreach (string tableName in stringTableNames)
        {
            var localizedString = new LocalizedString(tableName, key);
            var handle = localizedString.GetLocalizedStringAsync();
            yield return handle;

            string translated = handle.Result;
            if (!string.IsNullOrEmpty(translated) && !translated.Contains("No translation"))
            {
                callback(translated);
                yield break;
            }
        }

        callback(key); // fallback
    }
}
