using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using Steamworks;
using TMPro;

/// <summary>
/// 언어 설정 UI를 관리하는 매니저
/// 한국어/영어 버튼 활성화/비활성화 제어
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Language Buttons")]
    [SerializeField] private Button koreanButton;
    [SerializeField] private Button englishButton;

    [Header("Button Images")]
    [SerializeField] private Image koreanButtonImage;
    [SerializeField] private Image englishButtonImage;

    [Header("Button Sprites")]
    [SerializeField] private Sprite koreanActiveSprite;
    [SerializeField] private Sprite koreanInactiveSprite;
    [SerializeField] private Sprite englishActiveSprite;
    [SerializeField] private Sprite englishInactiveSprite;

    [Header("Mouse Sensitivity")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityText; 
    
    // PlayerPrefs 키
    private const string LANGUAGE_KEY = "Language";
    private const string KOREAN = "Korean";
    private const string ENGLISH = "English";
    private const string MOUSE_SENSITIVITY_KEY = "MouseSensitivity";

    // 감도 매핑 범위
    private const float SENSITIVITY_MIN = 0.1f;
    private const float SENSITIVITY_MAX = 2.0f;
    private const float SENSITIVITY_MID = 1.0f;
    private const float SLIDER_MID = 60f;
    
    // 현재 언어
    private string _currentLanguage;
    private float _currentSensitivity;
    private ThirdPersonController _tpc; 

    private void Start()
    {
        // 버튼 이벤트 연결
        if (koreanButton != null)
        {
            koreanButton.onClick.AddListener(OnKoreanButtonClicked);
        }

        if (englishButton != null)
        {
            englishButton.onClick.AddListener(OnEnglishButtonClicked);
        }
        
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        LoadSensitivity();
        InitializeSensitivitySlider();

        // 저장된 언어 로드 및 적용
        LoadLanguage();
        UpdateButtonStates();
    }

    private void OnEnable()
    {
        // UI 활성화 시 현재 상태로 버튼 업데이트
        UpdateButtonStates();
        UpdateSensitivityUI();
    }

    /// <summary>
    /// PlayerPrefs에서 언어 설정 로드
    /// </summary>
    private void LoadLanguage()
    {
        if (!PlayerPrefs.HasKey(LANGUAGE_KEY))
        {
            string systemLang = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            _currentLanguage = systemLang == "ko" ? KOREAN : ENGLISH;
            SaveLanguage(_currentLanguage);
        }
        else
        {
            _currentLanguage = PlayerPrefs.GetString(LANGUAGE_KEY);
        }

        ApplyLanguage();
        DebugLogger.Log($"[LanguageManager] 언어 로드 완료: {_currentLanguage}");
    }

    /// <summary>
    /// 언어를 실제로 적용 (Unity Localization 시스템에 반영)
    /// </summary>
    private void ApplyLanguage()
    {
        if (LocalizationSettings.AvailableLocales == null)
        {
            DebugLogger.LogWarning("[LanguageManager] Localization이 아직 초기화되지 않았습니다.");
            return;
        }

        string localeCode = _currentLanguage == KOREAN ? "ko-KR" : "en";
        
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            DebugLogger.Log($"[LanguageManager] 언어 적용: {localeCode}");
        }
        else
        {
            DebugLogger.LogWarning($"[LanguageManager] Locale을 찾을 수 없음: {localeCode}");
        }
    }

    /// <summary>
    /// 언어 설정 저장
    /// </summary>
    private void SaveLanguage(string language)
    {
        _currentLanguage = language;
        PlayerPrefs.SetString(LANGUAGE_KEY, _currentLanguage);
        PlayerPrefs.Save();
        
        DebugLogger.Log($"[LanguageManager] 언어 저장 완료: {_currentLanguage}");
    }

    /// <summary>
    /// 한국어 버튼 클릭 핸들러
    /// </summary>
    private void OnKoreanButtonClicked()
    {
        if (_currentLanguage == KOREAN)
        {
            DebugLogger.Log("[LanguageManager] 이미 한국어입니다.");
            return;
        }

        SaveLanguage(KOREAN);
        ApplyLanguage();
        UpdateButtonStates();
    }

    /// <summary>
    /// 영어 버튼 클릭 핸들러
    /// </summary>
    private void OnEnglishButtonClicked()
    {
        if (_currentLanguage == ENGLISH)
        {
            DebugLogger.Log("[LanguageManager] 이미 영어입니다.");
            return;
        }

        SaveLanguage(ENGLISH);
        ApplyLanguage();
        UpdateButtonStates();
    }

    /// <summary>
    /// 버튼 활성화/비활성화 상태 업데이트
    /// </summary>
    private void UpdateButtonStates()
    {
        bool isKorean = (_currentLanguage == KOREAN);

        // 한국어 버튼
        if (koreanButton != null)
        {
            koreanButton.interactable = !isKorean;
        
            if (koreanButtonImage != null)
            {
                koreanButtonImage.sprite = isKorean ? koreanActiveSprite : koreanInactiveSprite;
            }
        }

        // 영어 버튼
        if (englishButton != null)
        {
            englishButton.interactable = isKorean;
        
            if (englishButtonImage != null)
            {
                englishButtonImage.sprite = isKorean ? englishInactiveSprite : englishActiveSprite;
            }
        }

        DebugLogger.Log($"[LanguageManager] 버튼 상태 업데이트 - 현재: {_currentLanguage}");
    }

    /// <summary>
    /// 외부에서 현재 언어 가져오기
    /// </summary>
    public string GetCurrentLanguage()
    {
        return _currentLanguage;
    }

    /// <summary>
    /// 외부에서 언어 변경 (프로그래밍 방식)
    /// </summary>
    public void SetLanguage(string language)
    {
        if (language != KOREAN && language != ENGLISH)
        {
            DebugLogger.LogWarning($"[LanguageManager] 지원하지 않는 언어: {language}");
            return;
        }

        SaveLanguage(language);
        ApplyLanguage();
        UpdateButtonStates();
    }
    
    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player != null)
            _tpc = player.GetComponentInChildren<ThirdPersonController>(true);
    
        ApplySensitivity(); // 스폰 후 즉시 적용
    }
    
    // 감도 로드
    private void LoadSensitivity()
    {
        float saved = PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, 1.0f); // 기본값 1.0 (실제값)
        _currentSensitivity = Mathf.Clamp(saved, SENSITIVITY_MIN, SENSITIVITY_MAX);
        ApplySensitivity();
    }

    // 슬라이더 초기화
    private void InitializeSensitivitySlider()
    {
        if (sensitivitySlider == null) return;
        sensitivitySlider.minValue = 1f;
        sensitivitySlider.maxValue = 100f;
        sensitivitySlider.value = SensitivityToSlider(_currentSensitivity);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderChanged);
        UpdateSensitivityUI();
    }
    
    // 슬라이더(1~100) → 실제 감도 변환 (60=1.0 앵커)
    private float SliderToSensitivity(float sliderValue)
    {
        if (sliderValue <= SLIDER_MID)
            return SENSITIVITY_MIN + (sliderValue - 1f) / (SLIDER_MID - 1f) * (SENSITIVITY_MID - SENSITIVITY_MIN);
        else
            return SENSITIVITY_MID + (sliderValue - SLIDER_MID) / (100f - SLIDER_MID) * (SENSITIVITY_MAX - SENSITIVITY_MID);
    }

    // 실제 감도 → 슬라이더 변환 (60=1.0 앵커)
    private float SensitivityToSlider(float sensitivity)
    {
        if (sensitivity <= SENSITIVITY_MID)
            return 1f + (sensitivity - SENSITIVITY_MIN) / (SENSITIVITY_MID - SENSITIVITY_MIN) * (SLIDER_MID - 1f);
        else
            return SLIDER_MID + (sensitivity - SENSITIVITY_MID) / (SENSITIVITY_MAX - SENSITIVITY_MID) * (100f - SLIDER_MID);
    }
    
    // 슬라이더 변경 핸들러
    private void OnSensitivitySliderChanged(float sliderValue)
    {
        _currentSensitivity = SliderToSensitivity(sliderValue);
        ApplySensitivity();
        SaveSensitivity();
        UpdateSensitivityUI();
    }
    
    private void ApplySensitivity()
    {
        if (_tpc != null)
            _tpc.MouseSensitivity = _currentSensitivity;
    }
    
    // 저장
    private void SaveSensitivity()
    {
        PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, _currentSensitivity);
        PlayerPrefs.Save();
    }
    
    // UI 텍스트 업데이트
    private void UpdateSensitivityUI()
    {
        if (sensitivityText != null)
            sensitivityText.text = $"{Mathf.RoundToInt(SensitivityToSlider(_currentSensitivity))}";
    }
    
    private void OnDestroy()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
    }
}