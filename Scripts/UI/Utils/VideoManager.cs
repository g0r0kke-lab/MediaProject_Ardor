using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;
using UnityEngine.InputSystem;

public class VideoManager : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private Action onVideoComplete;

    // ✅ 추가: 비디오 재생 중 플래그
    private bool isVideoPlaying = false;
    public bool IsVideoPlaying => isVideoPlaying; // ✅ 외부 접근용 프로퍼티

    [Header("UI Settings")] [SerializeField]
    private GameObject videoCanvas;

    [SerializeField] private bool isDebug = false;

    // 언어별 비디오 클립
    [Header("Language Videos")] [SerializeField]
    private VideoClip koreanVideo;

    [SerializeField] private VideoClip englishVideo;

    // 언어별 렌더 텍스처
    [Header("Language Render Textures")] [SerializeField]
    private RenderTexture koreanRenderTexture;

    [SerializeField] private RenderTexture englishRenderTexture;

    // Raw Image (렌더 텍스처를 표시할 UI)
    [Header("UI Display")] [SerializeField]
    private UnityEngine.UI.RawImage videoRawImage;

    // PlayerPrefs 키
    private const string LANGUAGE_KEY = "Language";
    private const string KOREAN = "Korean";

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            DebugLogger.LogError("[VideoManager] VideoPlayer 컴포넌트가 없습니다!");
            return;
        }

        videoPlayer.loopPointReached += OnVideoFinished;

        // VideoPlayer의 오디오 출력 모드 확인 (기본값: Direct)
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        if (videoCanvas != null)
        {
            videoCanvas.SetActive(false);
        }
    }

    public void PlayVideo(Action onComplete = null)
    {
        isVideoPlaying = true; // ✅ 추가
        onVideoComplete = onComplete;

        if (videoPlayer == null)
        {
            DebugLogger.LogError("[VideoManager] VideoPlayer가 null입니다!");
            isVideoPlaying = false; // ✅ 에러 시 플래그 초기화
            onComplete?.Invoke();
            return;
        }

        // 언어에 따라 비디오 클립과 렌더 텍스처 선택
        string currentLanguage = PlayerPrefs.GetString(LANGUAGE_KEY, KOREAN);
        bool isKorean = (currentLanguage == KOREAN);

        VideoClip selectedClip = isKorean ? koreanVideo : englishVideo;
        RenderTexture selectedRenderTexture = isKorean ? koreanRenderTexture : englishRenderTexture;

        // 유효성 검사
        if (selectedClip == null)
        {
            DebugLogger.LogError($"[VideoManager] {currentLanguage} 비디오 클립이 할당되지 않았습니다!");
            onComplete?.Invoke();
            return;
        }

        if (selectedRenderTexture == null)
        {
            DebugLogger.LogError($"[VideoManager] {currentLanguage} 렌더 텍스처가 할당되지 않았습니다!");
            onComplete?.Invoke();
            return;
        }

        // VideoPlayer에 클립과 렌더 텍스처 할당
        videoPlayer.clip = selectedClip;
        videoPlayer.targetTexture = selectedRenderTexture;

        // Raw Image에 렌더 텍스처 할당
        if (videoRawImage != null)
        {
            videoRawImage.texture = selectedRenderTexture;
        }
        else
        {
            DebugLogger.LogWarning("[VideoManager] Raw Image가 할당되지 않았습니다!");
        }

        if (isDebug) DebugLogger.Log($"[VideoManager] 동영상 재생 준비: {selectedClip.name} ({currentLanguage})");

        // Canvas를 먼저 활성화
        if (videoCanvas != null)
        {
            videoCanvas.SetActive(true);
            if (isDebug) DebugLogger.Log("[VideoManager] Video Canvas 활성화");
        }

        videoPlayer.time = 0;
        videoPlayer.frame = 0;

        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnVideoPrepared;
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (vp == null)
        {
            DebugLogger.LogError("[VideoManager] VideoPlayer가 null입니다 (OnVideoPrepared)");
            return;
        }

        vp.prepareCompleted -= OnVideoPrepared;

        // 비디오 재생 전 BGM 볼륨 적용
        ApplyVideoVolume(vp);

        if (isDebug) DebugLogger.Log($"[VideoManager] 준비 완료 - 길이: {vp.length}초, 프레임: {vp.frameCount}");

        if (isDebug && vp.targetTexture != null)
        {
            DebugLogger.Log(
                $"[VideoManager] Target Texture: {vp.targetTexture.name}, Size: {vp.targetTexture.width}x{vp.targetTexture.height}");
        }

        if (vp.length <= 0 || vp.frameCount <= 0)
        {
            DebugLogger.LogError("[VideoManager] 동영상 길이가 0입니다!");
            OnVideoFinished(vp);
            return;
        }

        StartCoroutine(PlayFromStart(vp));
    }

    // VideoPlayer에 BGM 볼륨 적용
    private void ApplyVideoVolume(VideoPlayer vp)
    {
        if (vp == null || SoundManager.Instance == null) return;

        float targetVolume = SoundManager.Instance.BgmVolume;

        // VideoPlayer의 모든 오디오 트랙에 볼륨 적용
        for (ushort i = 0; i < vp.audioTrackCount; i++)
        {
            vp.SetDirectAudioVolume(i, targetVolume);
        }

        if (isDebug) DebugLogger.Log($"[VideoManager] 비디오 볼륨 적용: {targetVolume:F2}");
    }

    // 첫 프레임부터 확실하게 재생
    private IEnumerator PlayFromStart(VideoPlayer vp)
    {
        if (vp == null)
        {
            DebugLogger.LogError("[VideoManager] VideoPlayer가 null입니다 (PlayFromStart)");
            yield break;
        }

        // 첫 프레임이 렌더링될 때까지 대기
        vp.Pause();
        vp.time = 0;
        vp.frame = 0;

        yield return null; // 한 프레임 대기

        vp.Play();
        if (isDebug) DebugLogger.Log("[VideoManager] 재생 시작 (첫 프레임부터)");

        // 재생 확인
        yield return new WaitForSeconds(0.5f);

        if (vp.isPlaying)
        {
            if (isDebug) DebugLogger.Log($"[VideoManager] ✅ 정상 재생 중 (현재: {vp.time:F2}초 / {vp.length:F2}초)");
        }
        else
        {
            DebugLogger.LogError("[VideoManager] ❌ 재생되지 않음!");
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (vp == null)
        {
            DebugLogger.LogError("[VideoManager] VideoPlayer가 null입니다 (OnVideoFinished)");
            return;
        }

        isVideoPlaying = false; // ✅ 추가

        if (isDebug) DebugLogger.Log("[VideoManager] 동영상 재생 완료");

        // Canvas 비활성화
        if (videoCanvas != null)
        {
            videoCanvas.SetActive(false);
            DebugLogger.Log("[VideoManager] Video Canvas 비활성화");
        }

        gameObject.SetActive(false);
        onVideoComplete?.Invoke();
    }

#if UNITY_EDITOR
    // 스킵 기능
    private void Update()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                DebugLogger.Log("[VideoManager] 동영상 스킵");
                videoPlayer.Stop();
                OnVideoFinished(videoPlayer);
            }
        }
    }
#endif

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
    }
}