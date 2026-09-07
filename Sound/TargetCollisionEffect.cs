using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 상호작용 오브젝트가 타겟에 충돌할 때 파티클 이펙트와 3D 사운드를 재생하고, 첫 타격 시 퀘스트 가이드를 표시합니다.
/// </summary>
public class TargetCollisionEffect : MonoBehaviour
{
    // 씬 내 모든 타깃이 공유하는 플래그. 씬이 (재)로드되면 핸들이 바뀌므로 자동 초기화된다.
    private static bool isFirstHitProcessed = false;
    private static int lastSceneHandle = -1;
    
    [Header("References")]
    [SerializeField, ReadOnly] private SoundManager soundManager;
    [SerializeField] private ParticleSystem hitParticle; // 충돌 시 재생할 파티클
    [SerializeField] private AudioClip[] hitSounds; // 충돌 사운드 클립 배열
    
    [Header("Settings")]
    [SerializeField] private float minVelocity = 1f; // 최소 속도
    [SerializeField] private float volumeMultiplier = 1f; // 볼륨 배율
    [SerializeField] private float effectCooldown = 0.1f; // 연속 재생 방지
    
    private AudioSource audioSource;
    private float lastPlayTime;
    private int lastPlayedIndex = -1; // 마지막 재생 인덱스 (연속 같은 소리 방지)

    void Awake()
    {
        // 씬이 (재)로드되어 핸들이 바뀌었으면 첫 타격 플래그 초기화
        int currentSceneHandle = SceneManager.GetActiveScene().handle;
        if (lastSceneHandle != currentSceneHandle)
        {
            isFirstHitProcessed = false;
            lastSceneHandle = currentSceneHandle;
        }

        // AudioSource 초기화
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
        
        // ParticleSystem 자동 찾기 (미할당 시)
        if (hitParticle == null)
        {
            hitParticle = GetComponentInChildren<ParticleSystem>();
            if (hitParticle == null)
            {
                DebugLogger.LogWarning($"⚠️ {gameObject.name}: ParticleSystem을 찾을 수 없습니다.");
            }
        }
        
        // 시작 직후 3초간 충돌 무시
        lastPlayTime = Time.time + 3f;
    }

    void OnCollisionEnter(Collision collision)
    {
        // InteractionObject 태그와 Interactable 레이어 체크
        if (collision.gameObject.tag != "InteractionObject") return;
        
        float velocity = collision.relativeVelocity.magnitude;
        
        // 쿨다운 체크 (시작 3초 포함)
        if (Time.time < lastPlayTime) return;
        if (Time.time - lastPlayTime < effectCooldown) return;
        
        if (velocity >= minVelocity)
        {
            ContactPoint contact = collision.contacts[0];
            hitParticle.transform.position = contact.point;
            PlayHitEffect();
            
            // 사운드 재생
            PlayHitSound(velocity);
            
            lastPlayTime = Time.time;
        }
        
        // 공통 변수 체크 및 로직 실행
        if (!isFirstHitProcessed)
        {
            isFirstHitProcessed = true;
            soundManager?.PlaySFX(11);
            GameUIManager.Instance.OpenQuestGuide("Q1_02_GoOverContainer");
        }
    }

    // 파티클 재생 메서드
    private void PlayHitEffect()
    {
        if (hitParticle != null)
        {
            hitParticle.Play();
        }
    }

    // 사운드 재생 메서드
    private void PlayHitSound(float velocity)
    {
        if (hitSounds == null || hitSounds.Length == 0) return;
        
        AudioClip selectedClip = GetRandomSound();
        
        if (selectedClip != null)
        {
            float volume = Mathf.Clamp01(velocity / 10f) * volumeMultiplier;
            float finalVolume = volume * soundManager.SfxVolume;
            
            audioSource.PlayOneShot(selectedClip, finalVolume);
        }
    }

    // 랜덤 사운드 선택 (이전과 다른 사운드)
    private AudioClip GetRandomSound()
    {
        if (hitSounds.Length == 1)
        {
            return hitSounds[0]; // 하나뿐이면 그냥 재생
        }
        
        // 이전과 다른 사운드 선택
        int randomIndex;
        int attempts = 0;
        do
        {
            randomIndex = Random.Range(0, hitSounds.Length);
            attempts++;
        } while (randomIndex == lastPlayedIndex && attempts < 10); // 최대 10번 시도
        
        lastPlayedIndex = randomIndex;
        return hitSounds[randomIndex];
    }
}