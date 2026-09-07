using UnityEngine;

/// <summary>
/// 물리 오브젝트가 충돌할 때 3D 임팩트 사운드를 재생하고, 던진 물체가 착지하면 StorageLure 이벤트를 발행합니다.
/// </summary>
public class BoxCollisionSound : MonoBehaviour
{
    [SerializeField, ReadOnly] private SoundManager soundManager;
    [SerializeField] private AudioClip[] impactSounds; // 충돌 사운드 클립 배열
    [SerializeField] private float minVelocity = 1f; // 최소 속도
    [SerializeField] private float volumeMultiplier = 1f; // 볼륨 배율

    [Header("Storage Lure")] [SerializeField]
    private bool _isStorageObject = false; // 던지면 스키아가 따라오는 물체인지

    [SerializeField] private float _landedVelocityThreshold = 0.05f; // 착지 판정 속도 임계값
    private float _landedCheckDelay = 0f;
    private bool _landedPublished = false; // 착지 이벤트 중복 발행 방지
    private bool _hasBeenThrown = false;

    private AudioSource audioSource;
    private Rigidbody rb; // 부모의 Rigidbody 참조
    private float lastPlayTime;
    private float soundCooldown = 0.1f; // 연속 재생 방지
    private int lastPlayedIndex = -1; // 마지막 재생 인덱스 (연속 같은 소리 방지)

    // 초기 위치/회전 저장
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Awake()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;

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

        // Rigidbody 찾기
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            DebugLogger.LogWarning($"⚠️ {gameObject.name}: Rigidbody를 찾을 수 없습니다. Rigidbody가 있는지 확인하세요.");
        }

        if (!soundManager) soundManager = SoundManager.Instance;

        // 시작 직후 3초간 충돌 무시
        lastPlayTime = Time.time + 3f;
    }

    private void Start()
    {
        if (!soundManager) soundManager = SoundManager.Instance;
    }

    private void Update()
    {
        if (!_isStorageObject || _landedPublished || rb == null) return;
        if (!_hasBeenThrown) return; // 던지기 전엔 감지 안 함
        if (Time.time < _landedCheckDelay) return;

        if (rb.linearVelocity.magnitude < _landedVelocityThreshold)
        {
            _landedPublished = true;
            DebugLogger.Log($"[BoxCollisionSound] ThrowableObjectLanded 발행: {transform.position}");
            // 착지 이벤트 발행 — StorageSkiaManager가 수신
            EventBroker.Instance?.Publish("ThrowableObjectLanded", transform);
        }
    }

    // 던질 때 LightAnchor에서 호출 — 착지 감지 재활성화
    public void ResetLure()
    {
        _hasBeenThrown = true; // 던졌다는 플래그
        _landedPublished = false;
        // 던진 직후엔 착지 감지 무시 (물체가 날아갈 시간 확보)
        _landedCheckDelay = Time.time + 0.3f;
    }


    void OnCollisionEnter(Collision collision)
    {
        // Rigidbody 없거나 사운드 클립이 없으면 종료
        if (rb == null || impactSounds == null || impactSounds.Length == 0) return;

        float velocity = collision.relativeVelocity.magnitude;

        // 쿨다운 체크 (시작 3초 포함)
        if (Time.time < lastPlayTime) return;
        if (Time.time - lastPlayTime < soundCooldown) return;

        if (velocity >= minVelocity)
        {
            // 랜덤 사운드 선택
            AudioClip selectedClip = GetRandomSound();

            if (selectedClip != null && soundManager)
            {
                float volume = Mathf.Clamp01(velocity / 10f) * volumeMultiplier;
                float finalVolume = volume * soundManager.SfxVolume;

                audioSource.PlayOneShot(selectedClip, finalVolume);
                lastPlayTime = Time.time;
            }
        }
    }

    private AudioClip GetRandomSound()
    {
        if (impactSounds.Length == 1)
        {
            return impactSounds[0]; // 하나뿐이면 그냥 재생
        }

        // 이전과 다른 사운드 선택
        int randomIndex;
        int attempts = 0;
        do
        {
            randomIndex = Random.Range(0, impactSounds.Length);
            attempts++;
        } while (randomIndex == lastPlayedIndex && attempts < 10); // 최대 10번 시도

        lastPlayedIndex = randomIndex;
        return impactSounds[randomIndex];
    }

    // 리스폰 시 호출
    public void ResetToInitial()
    {
        if (!_isStorageObject) return;

        _hasBeenThrown = false;
        _landedPublished = false;
        _landedCheckDelay = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
    }
}