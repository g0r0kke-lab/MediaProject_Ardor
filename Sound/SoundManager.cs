using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class AudioClipData
{
    public AudioClip clip;
    [Range(0f, 1f)]
    public float volume = 1f;
}

/// <summary>
/// BGM 크로스페이드 재생, 인덱스 기반 SFX 재생(중복 방지), PlayerPrefs를 통한 볼륨 설정을 지원하는 중앙 집중형 오디오 매니저입니다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource sfxLoopSource;
    [SerializeField] private AudioSource bgmSourceSecondary;

    [Header("Audio Clips")]
    [SerializeField] private AudioClipData[] bgmClipsData;
    [SerializeField] private AudioClipData[] sfxClipsData;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)] private float _bgmVolume = 0.4f;
    [Range(0f, 1f)] private float _sfxVolume = 0.6f;

    [Header("UI References")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI bgmVolumeText;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    [Header("Manager References")]
    [SerializeField] private EventBroker eventBroker;

    [Header("Crossfade Settings")]
    [SerializeField] private float crossfadeDuration = 2f;
    [SerializeField] private bool loopWithCrossfade = true; // 루프 크로스페이드 활성화 여부
    private Coroutine _currentCrossfade;
    private Coroutine _loopMonitor; // 루프 모니터링 코루틴
    private AudioSource _activeBGMSource;
    
    [SerializeField, ReadOnly] private bool _isFirstSpawn = true;
    
    // 효과음 중복 재생 방지
    private AudioClip _lastPlayedSFX;
    private float _lastSFXPlayTime;
    private const float SFX_COOLDOWN = 0.1f;

    // PlayerPrefs 키
    private const string BGM_VOLUME_KEY = "BGMVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;
    
    // 현재 재생 중인 BGM 클립 데이터 캐싱
    private AudioClipData _currentBGMClipData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 보조 AudioSource 자동 생성
        if (bgmSourceSecondary == null)
        {
            GameObject secondarySourceObj = new GameObject("BGM Source Secondary");
            secondarySourceObj.transform.SetParent(transform);
            bgmSourceSecondary = secondarySourceObj.AddComponent<AudioSource>();
            bgmSourceSecondary.playOnAwake = false;
            bgmSourceSecondary.loop = true;
        }

        if (sfxLoopSource == null)
        {
            GameObject loopSourceObj = new GameObject("SFX Loop Source");
            loopSourceObj.transform.SetParent(transform);
            sfxLoopSource = loopSourceObj.AddComponent<AudioSource>();
            sfxLoopSource.playOnAwake = false;
            sfxLoopSource.loop = true;
        }

        _activeBGMSource = bgmSource; // 초기 활성 소스 설정
        
        LoadVolumeSettings();
    }

    private void Start()
    {
        if (eventBroker == null)
        {
            eventBroker = EventBroker.Instance;
        }

        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }

        InitializeSliders();
    }

    private void OnEnable()
    {
        // 활성화 시 현재 볼륨 값으로 UI 업데이트
        UpdateVolumeUI();
    }

    private void OnDisable()
    {
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// PlayerPrefs에서 볼륨 설정 로드
    /// </summary>
    private void LoadVolumeSettings()
    {
        _bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 0.4f);
        _sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.6f);

        ApplyVolume();
        
        // DebugLogger.Log($"[SoundManager] 볼륨 로드 완료 - BGM: {_bgmVolume:F2}, SFX: {_sfxVolume:F2}");
    }

    /// <summary>
    /// 볼륨 설정을 PlayerPrefs에 저장
    /// </summary>
    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, _bgmVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, _sfxVolume);
        PlayerPrefs.Save();
        
        // DebugLogger.Log($"[SoundManager] 볼륨 저장 완료 - BGM: {_bgmVolume:F2}, SFX: {_sfxVolume:F2}");
    }

    /// <summary>
    /// 슬라이더 초기화 및 이벤트 연결
    /// </summary>
    private void InitializeSliders()
    {
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            bgmSlider.value = _bgmVolume;
            bgmSlider.onValueChanged.AddListener(OnBGMSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = _sfxVolume;
            sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
        }

        UpdateVolumeUI();
    }

    /// <summary>
    /// BGM 슬라이더 값 변경 핸들러
    /// </summary>
    private void OnBGMSliderChanged(float value)
    {
        SetBGMVolume(value);
    }

    /// <summary>
    /// SFX 슬라이더 값 변경 핸들러
    /// </summary>
    private void OnSFXSliderChanged(float value)
    {
        SetSFXVolume(value);
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
    
        // clipData.volume 반영해서 실제 재생 볼륨 계산
        float targetVolume = _bgmVolume * (_currentBGMClipData != null ? _currentBGMClipData.volume : 1f);

        if (_activeBGMSource != null)
            _activeBGMSource.volume = targetVolume; // 활성 소스에만 적용

        // 비활성 소스는 마스터 볼륨만 기억시켜둠 (크로스페이드 시작 시 0에서 시작하므로)
        AudioSource inactiveSource = (_activeBGMSource == bgmSource) ? bgmSourceSecondary : bgmSource;
        if (inactiveSource != null)
            inactiveSource.volume = _bgmVolume; // 다음 크로스페이드 기준값

        UpdateBGMVolumeText();
        SaveVolumeSettings();
    }

    /// <summary>
    /// SFX 볼륨 설정
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        
        if (sfxSource != null)
        {
            sfxSource.volume = _sfxVolume;
        }

        UpdateSFXVolumeText();
        SaveVolumeSettings();
    }

    /// <summary>
    /// 현재 볼륨을 AudioSource에 적용
    /// </summary>
    private void ApplyVolume()
    {
        // 활성 BGM 소스는 clipData.volume 반영
        float targetBGMVolume = _bgmVolume * (_currentBGMClipData != null ? _currentBGMClipData.volume : 1f);
        if (_activeBGMSource != null)
            _activeBGMSource.volume = targetBGMVolume;

        // 비활성 소스
        AudioSource inactiveSource = (_activeBGMSource == bgmSource) ? bgmSourceSecondary : bgmSource;
        if (inactiveSource != null)
            inactiveSource.volume = _bgmVolume;

        if (sfxSource != null)
            sfxSource.volume = _sfxVolume;

        if (sfxLoopSource != null)
            sfxLoopSource.volume = _sfxVolume;
    }

    /// <summary>
    /// 볼륨 UI 업데이트 (슬라이더와 텍스트 모두)
    /// </summary>
    private void UpdateVolumeUI()
    {
        if (bgmSlider != null)
        {
            bgmSlider.value = _bgmVolume;
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = _sfxVolume;
        }

        UpdateBGMVolumeText();
        UpdateSFXVolumeText();
    }

    /// <summary>
    /// BGM 볼륨 텍스트 업데이트
    /// </summary>
    private void UpdateBGMVolumeText()
    {
        if (bgmVolumeText != null)
        {
            bgmVolumeText.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}";
        }
    }

    /// <summary>
    /// SFX 볼륨 텍스트 업데이트
    /// </summary>
    private void UpdateSFXVolumeText()
    {
        if (sfxVolumeText != null)
        {
            sfxVolumeText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}";
        }
    }

    /// <summary>
    /// 플레이어 스폰 이벤트 핸들러
    /// </summary>
    private void OnPlayerSpawned(object data)
    {
        if (_isFirstSpawn) // 씬 첫 스폰만 0번 재생
        {
            _isFirstSpawn = false;
            if (bgmClipsData != null && bgmClipsData.Length > 0 && bgmClipsData[0] != null)
                PlayBGM(0);
        }
    }

    /// <summary>
    /// 배경음악 재생 (인덱스)
    /// </summary>
    public void PlayBGM(int index)
    {
        if (this == null) return;

        if (index >= 0 && index < bgmClipsData.Length && bgmClipsData[index] != null && bgmClipsData[index].clip != null)
        {
            PlayBGMWithCrossfade(bgmClipsData[index]);
        }
    }

    /// <summary>
    /// 배경음악 재생 (클립 이름) - 크로스페이드 적용
    /// </summary>
    public void PlayBGM(string clipName)
    {
        if (this == null) return;

        AudioClipData clipData = FindClipData(bgmClipsData, clipName);
        if (clipData != null && clipData.clip != null)
        {
            PlayBGMWithCrossfade(clipData);
        }
        else
        {
            DebugLogger.LogWarning($"[SoundManager] BGM 클립을 찾을 수 없음: {clipName}");
        }
    }
    
    // 크로스페이드 재생 메서드 추가
    /// <summary>
    /// BGM을 크로스페이드로 전환
    /// </summary>
    private void PlayBGMWithCrossfade(AudioClipData clipData)
    {
        if (this == null) return;

        if (_currentCrossfade != null)
        {
            StopCoroutine(_currentCrossfade);
        }
        
        if (_loopMonitor != null)
        {
            StopCoroutine(_loopMonitor);
        }

        _currentCrossfade = StartCoroutine(CrossfadeBGM(clipData));
    }

    // 크로스페이드 코루틴 추가
    /// <summary>
    /// BGM 크로스페이드 코루틴
    /// </summary>
    private IEnumerator CrossfadeBGM(AudioClipData clipData)
    {
        AudioSource fadeOutSource = _activeBGMSource;
        AudioSource fadeInSource = (_activeBGMSource == bgmSource) ? bgmSourceSecondary : bgmSource;

        bool isFirstPlay = !fadeOutSource.isPlaying && fadeOutSource.clip == null;

        float targetVolume = _bgmVolume * clipData.volume;

        fadeInSource.clip = clipData.clip;
        fadeInSource.volume = isFirstPlay ? targetVolume : 0f;
        fadeInSource.loop = false;
        fadeInSource.Play();

        _currentBGMClipData = clipData;

        if (isFirstPlay)
        {
            _activeBGMSource = fadeInSource;
            _currentCrossfade = null;
            if (loopWithCrossfade)
                _loopMonitor = StartCoroutine(MonitorBGMLoop(clipData));
            yield break;
        }

        float elapsed = 0f;
        // 페이드 아웃 시작 볼륨 고정
        float fadeOutStartVolume = fadeOutSource.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / crossfadeDuration;

            if (fadeOutSource.isPlaying)
                // 고정된 시작 볼륨에서 선형으로 페이드 아웃
                fadeOutSource.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);

            fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);

            yield return null;
        }

        fadeOutSource.Stop();
        fadeOutSource.volume = _bgmVolume;
        fadeInSource.volume = targetVolume;

        _activeBGMSource = fadeInSource;
        _currentCrossfade = null;

        if (loopWithCrossfade)
            _loopMonitor = StartCoroutine(MonitorBGMLoop(clipData));
    }
    
    // 루프 모니터링 코루틴 추가
    /// <summary>
    /// BGM 루프를 모니터링하고 끝나기 전에 크로스페이드 시작
    /// </summary>
    private IEnumerator MonitorBGMLoop(AudioClipData clipData)
    {
        while (true)
        {
            if (_activeBGMSource != null && _activeBGMSource.isPlaying)
            {
                float timeRemaining = clipData.clip.length - _activeBGMSource.time;
            
                if (timeRemaining <= crossfadeDuration)
                {
                    PlayBGMWithCrossfade(clipData);
                    yield break;
                }
            }
        
            yield return null;
        }
    }
    
    /// <summary>
    /// 인덱스로 AudioClipData 가져오기
    /// </summary>
    public AudioClipData GetSFXClipData(int index)
    {
        if (index >= 0 && index < sfxClipsData.Length)
        {
            return sfxClipsData[index];
        }
        return null;
    }

    /// <summary>
    /// 효과음 루프 재생 (인덱스). 같은 클립이 이미 루프 중이면 무시.
    /// StopSFX()로 정지.
    /// </summary>
    public void PlaySFXLoop(int index)
    {
        if (index < 0 || index >= sfxClipsData.Length || sfxClipsData[index] == null || sfxClipsData[index].clip == null) return;
        AudioClipData clipData = sfxClipsData[index];
        if (sfxLoopSource.clip == clipData.clip && sfxLoopSource.isPlaying)
        {
            sfxLoopSource.volume = _sfxVolume * clipData.volume;
            return;
        }
        sfxLoopSource.Stop();
        sfxLoopSource.clip = clipData.clip;
        sfxLoopSource.loop = true;
        sfxLoopSource.volume = _sfxVolume * clipData.volume;
        sfxLoopSource.Play();
    }

    /// <summary>
    /// 효과음 재생 (인덱스) - 중복 재생 방지 포함
    /// </summary>
    public void PlaySFX(int index)
    {
        if (this == null) return;

        // AudioClipData 사용으로 변경
        if (index >= 0 && index < sfxClipsData.Length && sfxClipsData[index] != null && sfxClipsData[index].clip != null)
        {
            AudioClipData clipData = sfxClipsData[index];

            if (_lastPlayedSFX == clipData.clip && Time.time - _lastSFXPlayTime < SFX_COOLDOWN)
            {
                return;
            }

            // 개별 클립 볼륨 적용
            float finalVolume = _sfxVolume * clipData.volume;
            sfxSource.PlayOneShot(clipData.clip, finalVolume);

            _lastPlayedSFX = clipData.clip;
            _lastSFXPlayTime = Time.time;
        }
    }

    /// <summary>
    /// 효과음 재생 (인덱스 + 볼륨 배율) - 크라우치 발소리 등 볼륨 조정용
    /// </summary>
    public void PlaySFX(int index, float volumeScale)
    {
        if (this == null) return;

        if (index >= 0 && index < sfxClipsData.Length && sfxClipsData[index] != null && sfxClipsData[index].clip != null)
        {
            AudioClipData clipData = sfxClipsData[index];

            if (_lastPlayedSFX == clipData.clip && Time.time - _lastSFXPlayTime < SFX_COOLDOWN)
            {
                return;
            }

            float finalVolume = _sfxVolume * clipData.volume * volumeScale;
            sfxSource.PlayOneShot(clipData.clip, finalVolume);

            _lastPlayedSFX = clipData.clip;
            _lastSFXPlayTime = Time.time;
        }
    }

    /// <summary>
    /// 효과음 재생 (클립 이름) - 중복 재생 방지 포함
    /// </summary>
    public void PlaySFX(string clipName)
    {
        // AudioClipData 사용으로 변경
        AudioClipData clipData = FindClipData(sfxClipsData, clipName);
        if (clipData != null && clipData.clip != null)
        {
            if (_lastPlayedSFX == clipData.clip && Time.time - _lastSFXPlayTime < SFX_COOLDOWN)
            {
                return;
            }

            // 개별 클립 볼륨 적용
            float finalVolume = _sfxVolume * clipData.volume;
            sfxSource.PlayOneShot(clipData.clip, finalVolume);

            _lastPlayedSFX = clipData.clip;
            _lastSFXPlayTime = Time.time;
        }
        else
        {
            DebugLogger.LogWarning($"[SoundManager] SFX 클립을 찾을 수 없음: {clipName}");
        }
    }

    /// <summary>
    /// 효과음 단독 재생 (이전 SFX 중단)
    /// </summary>
    public void PlaySFXExclusive(string clipName)
    {
        AudioClipData clipData = FindClipData(sfxClipsData, clipName);
        if (clipData != null && clipData.clip != null)
        {
            sfxSource.Stop();
            sfxSource.clip = clipData.clip;
            sfxSource.volume = _sfxVolume * clipData.volume;
            sfxSource.Play();
            _lastPlayedSFX = clipData.clip;
            _lastSFXPlayTime = Time.time;
        }
        else
        {
            DebugLogger.LogWarning($"[SoundManager] SFX 클립을 찾을 수 없음: {clipName}");
        }
    }

    /// <summary>
    /// 효과음 정지 (루프 포함)
    /// </summary>
    public void StopSFX()
    {
        sfxLoopSource.Stop();
        sfxLoopSource.clip = null;
        sfxLoopSource.volume = _sfxVolume;
        _lastPlayedSFX = null;
    }

    /// <summary>
    /// 배경음악 정지
    /// </summary>
    public void StopBGM()
    {
        _isFirstSpawn = true;
        
        // 크로스페이드 중단
        if (_currentCrossfade != null)
        {
            StopCoroutine(_currentCrossfade);
            _currentCrossfade = null;
        }
        
        // 루프 모니터 중단
        if (_loopMonitor != null)
        {
            StopCoroutine(_loopMonitor);
            _loopMonitor = null;
        }
        
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }

        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.Stop();
        }
    }

    /// <summary>
    /// 배경음악 일시정지
    /// </summary>
    public void PauseBGM()
    {
        // 활성 소스만 일시정지
        if (_activeBGMSource != null)
        {
            _activeBGMSource.Pause();
        }
    }

    /// <summary>
    /// 배경음악 재개
    /// </summary>
    public void ResumeBGM()
    {
        // 활성 소스만 재개
        if (_activeBGMSource != null)
        {
            _activeBGMSource.UnPause();
        }
    }

    /// <summary>
    /// 이름으로 클립 데이터 찾기
    /// </summary>
    private AudioClipData FindClipData(AudioClipData[] clipsData, string clipName)
    {
        foreach (AudioClipData clipData in clipsData)
        {
            if (clipData != null && clipData.clip != null && clipData.clip.name == clipName)
            {
                return clipData;
            }
        }
        return null;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyVolume();
            UpdateVolumeUI();
        }

        // 조건 수정: volume == 0f → volume <= 0.01f (부동소수점 오차 고려)
        if (bgmClipsData != null)
        {
            for (int i = 0; i < bgmClipsData.Length; i++)
            {
                if (bgmClipsData[i] != null)
                {
                    // clip이 할당되어 있고 volume이 거의 0이면 1로 설정
                    if (bgmClipsData[i].clip != null && bgmClipsData[i].volume <= 0.01f)
                    {
                        bgmClipsData[i].volume = 1f;
                    }
                    // clip이 null이 아닌데 volume이 0이면 1로 설정
                    else if (bgmClipsData[i].clip == null && bgmClipsData[i].volume == 0f)
                    {
                        bgmClipsData[i].volume = 1f;
                    }
                }
            }
        }

        if (sfxClipsData != null)
        {
            for (int i = 0; i < sfxClipsData.Length; i++)
            {
                if (sfxClipsData[i] != null)
                {
                    if (sfxClipsData[i].clip != null && sfxClipsData[i].volume <= 0.01f)
                    {
                        sfxClipsData[i].volume = 1f;
                    }
                    else if (sfxClipsData[i].clip == null && sfxClipsData[i].volume == 0f)
                    {
                        sfxClipsData[i].volume = 1f;
                    }
                }
            }
        }
    }
    
    public AudioSource GetBGMSource()
    {
        return _activeBGMSource;
    }
}