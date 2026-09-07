using UnityEngine;

/// <summary>
/// 카메라가 최대 거리를 벗어나면 루프 3D AudioSource를 일시 정지하고, 전역 SFX 설정에 맞게 볼륨을 동기화합니다.
/// </summary>
public class LoopSound3D : MonoBehaviour
{
    [SerializeField, ReadOnly] private SoundManager soundManager;

    private AudioSource _audioSource;
    private float _baseVolume;
    private Camera _mainCamera;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (!soundManager) soundManager = SoundManager.Instance;
        if (_audioSource != null)
        {
            _baseVolume = _audioSource.volume;
        }
        
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    void Update()
    {
        if (_audioSource == null || soundManager == null) return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            float dist = Vector3.Distance(transform.position, _mainCamera.transform.position);
            bool inRange = dist <= _audioSource.maxDistance;

            if (!inRange)
            {
                if (_audioSource.isPlaying) _audioSource.Pause();
                return;
            }

            if (!_audioSource.isPlaying) _audioSource.Play();
        }

        _audioSource.volume = _baseVolume * soundManager.SfxVolume;
    }
    
    void OnDestroy()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    private void OnPlayerSpawned(object data)
    {
        if (_audioSource != null && !_audioSource.isPlaying)
            _audioSource.Play();
    }
}