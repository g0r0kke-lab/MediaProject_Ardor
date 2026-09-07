using UnityEngine;

/// <summary>
/// EventBroker 이벤트에 반응하거나 직접 호출 시 SoundManager의 전역 SFX 볼륨을 반영해 3D 공간화 오디오 클립을 재생합니다.
/// </summary>
public class Simple3DSound : MonoBehaviour
{
    [SerializeField, ReadOnly] private SoundManager soundManager;
    [SerializeField] private AudioClip soundClip;
    [SerializeField] private float volume = 1f;
    [SerializeField] private string eventKey; // 구독할 이벤트 키
    
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 3D 사운드 설정
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        
        if (!soundManager) soundManager = SoundManager.Instance;
    }

    // 이벤트 구독
    void OnEnable()
    {
        if (!string.IsNullOrEmpty(eventKey) && EventBroker.Instance)
        {
            EventBroker.Instance.Subscribe(eventKey, Play);
        }
    }

    // 이벤트 구독 해제
    void OnDisable()
    {
        if (!string.IsNullOrEmpty(eventKey) && EventBroker.Instance)
        {
            EventBroker.Instance.Unsubscribe(eventKey, Play);
        }
    }

    public void Play()
    {
        if (soundClip == null) return;
        
        float finalVolume = volume * soundManager.SfxVolume;
        audioSource.PlayOneShot(soundClip, finalVolume);
    }
}