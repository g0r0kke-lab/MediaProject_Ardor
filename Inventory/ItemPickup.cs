using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어의 상호작용으로 월드 공간의 아이템을 획득하고 세이브 데이터 인벤토리에 추가한 뒤 픽업 오브젝트를 파괴합니다.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    private string _uniqueItemId;
    
    [Header("Events")]
    [SerializeField] private string[] pickupEventKeys;
    
    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;
    private InputAction _interactAction;
    
    [Header("Manager References")]
    [SerializeField, ReadOnly] private SaveDataManager saveDataManager;
    [SerializeField, ReadOnly] private Map1GameManager map1GameManager;
    [SerializeField, ReadOnly] private EventBroker eventBroker;
    [SerializeField, ReadOnly] private MagneticManager magneticManager;
    [SerializeField, ReadOnly] private QuestUIManager questUIManager;
    [SerializeField, ReadOnly] private SoundManager soundManager;
    private bool _isInitialized = false;
    
    void Start()
    {
        if (saveDataManager == null)
        {
            saveDataManager = SaveDataManager.Instance;
        }
        
        // EventBroker 초기화
        if (eventBroker == null)
        {
            eventBroker = EventBroker.Instance;
        }
        
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }

        if (questUIManager == null)
        {
            questUIManager = QuestUIManager.Instance;
        }

        if (map1GameManager == null)
        {
            map1GameManager = Map1GameManager.Instance;
        }

        if (soundManager == null)
        {
            soundManager = SoundManager.Instance;
        }
        
        SetupInputActions();
        
        if (itemData != null)
        {
            Vector3 pos = transform.position;
            _uniqueItemId = $"{GetCurrentMapName()}_{itemData.name}_{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.z)}";
        }
    }
    
    // 플레이어 스폰 이벤트 핸들러
    private void OnPlayerSpawned(object data)
    {
        if (_isInitialized) return;
        
        magneticManager = MagneticManager.Instance;
        if (magneticManager != null)
        {
            _isInitialized = true;
            // DebugLogger.Log($"[ItemPickup] {gameObject.name} - MagneticManager initialized via OnPlayerSpawned");
        }
        
        // 플레이어 스폰 시점에 수집 여부 체크
        CheckIfAlreadyCollected();
    }
    
    string GetCurrentMapName()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    void SetupInputActions()
    {
        if (inputActions != null)
        {
            _interactAction = inputActions.FindAction("Player/Interact");
            
            if (_interactAction != null)
            {
                _interactAction.started -= OnInteract;
                _interactAction.started += OnInteract;
                _interactAction.Enable();
            }
        }
    }
    
    void CheckIfAlreadyCollected()
    {
        // 이미 수집한 아이템이면 파괴
        if (saveDataManager.IsPartTypeCollected(_uniqueItemId))
        {
            Destroy(gameObject);
        }
    }

    void OnInteract(InputAction.CallbackContext context)
    {
        if (this == null || gameObject == null) return;
        
        // 매니저가 없으면 한 번 더 시도
        if (magneticManager == null)
        {
            magneticManager = MagneticManager.Instance;
        }
        
        if (magneticManager != null && magneticManager.CurrentActiveInteractionObject == gameObject)
        {
            InputModeManager.Instance?.PlayActivate();
            PickupItem();
        }
    }

    void PickupItem()
    {
        if (saveDataManager == null)
        {
            DebugLogger.LogError("SaveDataManager가 없습니다!");
            return;
        }
    
        if (itemData == null)
        {
            DebugLogger.LogError("ItemData가 할당되지 않았습니다!");
            return;
        }
        
        if (magneticManager != null)
        {
            magneticManager.HideInteractionObjectUI(gameObject);
        }
        
        // 아이템 획득 처리
        saveDataManager.CollectFieldItem(_uniqueItemId, itemData);
        soundManager?.PlaySFX(17);
        
        // Map1GameManager에 수집 알림 (ItemData 전달)
        if (map1GameManager) map1GameManager.OnPartCollected(_uniqueItemId);
        
        if (questUIManager)
        {
            DebugLogger.Log("questUIManager.OpenQuestPanel");
            questUIManager.OpenQuestPanel(itemData);
        }
        
        // 인스펙터에 등록된 이벤트 키들 발행
        if (eventBroker != null)
        {
            foreach (var key in pickupEventKeys)
            {
                eventBroker.Publish(key, _uniqueItemId);
            }
        }
        
        // 오브젝트 파괴
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // InputAction 정리
        if (_interactAction != null)
        {
            _interactAction.started -= OnInteract;
        }
        
        // MagneticManager에서 등록 해제
        if (magneticManager != null)
        {
            magneticManager.UnregisterInteractionObject(gameObject);
        }
        
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
    }
}