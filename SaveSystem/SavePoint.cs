using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
/// <summary>
/// 플레이어가 진입하면 현재 체크포인트 인덱스를 저장하고, 시각 효과를 업데이트하며, 저장 알림을 표시하는 트리거 존입니다.
/// </summary>
public class SavePoint : MonoBehaviour
{
    [Header("Save Point Configuration")]
    [SerializeField] private int savePointIndex = 1; // 세이브 포인트 인덱스 (1~3)
    
    [Header("Activation Condition")]
    [SerializeField] private bool isConditionCleared = true; // 조건 클리어 여부 (기본값: true)
    
    // 조건 클리어 시 실행할 이벤트
    [Header("Condition Events")]
    [SerializeField] private UnityEvent onConditionCleared = new UnityEvent();
    
    [Header("Player Start Position")]
    [SerializeField] private Transform playerStartTransform; // PlayerStart 오브젝트의 Transform
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject inactiveVersionEffect; // 비활성화 버전 이펙트
    [SerializeField] private GameObject activeVersionEffect; // 활성화 버전 이펙트
    [Header("Manager References")]
    [SerializeField, ReadOnly] private SaveDataManager saveDataManager;
    [SerializeField, ReadOnly] private GameUIManager gameUIManager;
    
    private bool _hasBeenActivated = false;
    
    private void Awake()
    {
        // PlayerStart Transform이 없으면 자동으로 찾기
        if (playerStartTransform == null)
        {
            Transform playerStart = transform.Find("PlayerStart");
            if (playerStart != null)
            {
                playerStartTransform = playerStart;
            }
            else
            {
                DebugLogger.LogWarning($"SavePoint {savePointIndex}: PlayerStart 오브젝트를 찾을 수 없습니다!");
            }
        }
        
        // Collider를 Trigger로 설정
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }
    
    private void Start()
    {
        if (saveDataManager == null)
        {
            saveDataManager = SaveDataManager.Instance;
        }
        
        if (gameUIManager == null)
        {
            gameUIManager = GameUIManager.Instance;
        }
        
        // 게임 시작 시 이펙트 상태 설정
        UpdateEffectState();
    }
    
    /// <summary>
    /// 이펙트 상태 업데이트 (비활성화/활성화 버전 전환)
    /// </summary>
    private void UpdateEffectState()
    {
        int currentSaveIndex = saveDataManager.GetCurrentSavePointIndex();
        bool isActivated = savePointIndex <= currentSaveIndex;
        
        // 비활성화 버전 이펙트
        if (inactiveVersionEffect != null)
        {
            inactiveVersionEffect.SetActive(!isActivated);
        }
        
        // 활성화 버전 이펙트
        if (activeVersionEffect != null)
        {
            activeVersionEffect.SetActive(isActivated);
        }
        
        DebugLogger.Log($"세이브 포인트 {savePointIndex} 이펙트 상태: {(isActivated ? "활성화" : "비활성화")}");
    }

    public void ActivateEffect()
    {
        inactiveVersionEffect?.SetActive(false);
        activeVersionEffect?.SetActive(true);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 태그 확인
        if (other.CompareTag("Player") && !_hasBeenActivated)
        {
            // 조건이 클리어되지 않았으면 저장하지 않음
            if (!isConditionCleared)
            {
                DebugLogger.Log($"세이브 포인트 {savePointIndex}: 조건이 아직 클리어되지 않았습니다.");
                return;
            }
            
            // 현재 세이브 포인트 인덱스보다 높은 경우에만 저장
            int currentSaveIndex = saveDataManager.GetCurrentSavePointIndex();
            
            if (savePointIndex > currentSaveIndex)
            {
                // 세이브 포인트 저장
                saveDataManager.SetSavePointIndex(savePointIndex);
                _hasBeenActivated = true;
                
                // 이펙트 상태 업데이트 (활성화 버전으로 전환)
                UpdateEffectState();
                
                PlaySaveSound();
                GameUIManager.Instance?.OpenSaveDataPanel();
                
                DebugLogger.Log($"세이브 포인트 {savePointIndex}에 도달했습니다!");
                
                // UI 알림 (옵션)
                if (gameUIManager != null)
                {
                    gameUIManager.ShowLocalizedMessage("SaveNotification", "save_point_reached");
                }
            }
            else
            {
                DebugLogger.Log($"세이브 포인트 {savePointIndex}: 이미 더 높은 세이브 포인트({currentSaveIndex})에 도달했습니다.");
            }
        }
    }
    
    /// <summary>
    /// 외부에서 세이브 포인트의 조건 클리어 여부를 설정
    /// </summary>
    /// <param name="isCleared">true면 세이브 가능, false면 세이브 불가능</param>
    public void SetConditionCleared(bool isCleared)
    {
        isConditionCleared = isCleared;
        DebugLogger.Log($"세이브 포인트 {savePointIndex}: 조건 클리어 여부 = {isCleared}");
        
        // true로 변경될 때만 이벤트 실행
        if (isCleared)
        {
            onConditionCleared?.Invoke();
        }
    }
    
    /// <summary>
    /// 현재 조건 클리어 여부 반환
    /// </summary>
    public bool IsConditionCleared()
    {
        return isConditionCleared;
    }

    // private void ProgressToNext()
    // {
    //     GameManagerRegistry.ProgressState();
    // }
    
    private void PlaySaveSound()
    {
        SoundManager.Instance?.PlaySFX(20);
    }
    
    // 플레이어 스폰 위치 반환
    public Vector3 GetPlayerStartPosition()
    {
        if (playerStartTransform != null)
        {
            return playerStartTransform.position;
        }
        return transform.position; // PlayerStart가 없으면 세이브 포인트 위치 반환
    }
    
    public Quaternion GetPlayerStartRotation()
    {
        if (playerStartTransform != null)
        {
            return playerStartTransform.rotation;
        }
        return transform.rotation;
    }
    
    // SavePointIndex getter 메서드 (Map1GameManager에서 사용)
    public int GetSavePointIndex()
    {
        return savePointIndex;
    }
    
    // 에디터에서 디버깅용
    private void OnDrawGizmosSelected()
    {
        if (playerStartTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerStartTransform.position, 1f);
            Gizmos.DrawLine(transform.position, playerStartTransform.position);
        }
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
    }
}