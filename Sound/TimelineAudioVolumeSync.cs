using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 타임라인 AudioSource를 SoundManager 볼륨과 동기화
/// </summary>
public class TimelineAudioVolumeSync : MonoBehaviour
{
    [Header("Timeline Audio Sources")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    
    // PlayableDirector 참조 추가
    [Header("Timeline Reference")]
    [SerializeField, ReadOnly] private PlayableDirector director;
    
    private float _lastBgmVolume = -1f;
    private float _lastSfxVolume = -1f;
    
    private void Start()
    {
        // 자동으로 같은 GameObject의 PlayableDirector 찾기
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }
    
    private void Update()
    {
        // 타임라인이 재생 중일 때만 업데이트
        if (director == null || director.state != PlayState.Playing) return;
        
        if (SoundManager.Instance == null) return;
        
        // BGM 볼륨이 변경되었을 때만 적용
        float currentBgmVolume = SoundManager.Instance.BgmVolume;
        if (bgmAudioSource != null && !Mathf.Approximately(_lastBgmVolume, currentBgmVolume))
        {
            _lastBgmVolume = currentBgmVolume;
            bgmAudioSource.volume = currentBgmVolume;
        }
        
        // SFX 볼륨이 변경되었을 때만 적용
        float currentSfxVolume = SoundManager.Instance.SfxVolume;
        if (sfxAudioSource != null && !Mathf.Approximately(_lastSfxVolume, currentSfxVolume))
        {
            _lastSfxVolume = currentSfxVolume;
            sfxAudioSource.volume = currentSfxVolume;
        }
    }
}