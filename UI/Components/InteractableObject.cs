using UnityEngine;
using UnityEngine.InputSystem;

// 사용 X
/// <summary>플레이어와 E키 상호작용 및 팝업 UI를 처리하는 상호작용 오브젝트 컴포넌트</summary>
public class InteractableObject : MonoBehaviour
{
    [Header("UI 설정")]
    private bool isUIOpen = false;
    public string messageTextName = "PopUpText";
    public string myLocalizationKey = "default_message";
    
    [Header("Collider 설정")]
    [SerializeField] private GameObject triggerColliderObject;
    
    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;
    private InputAction _interactAction;
    
    [Header("Manager References")]
    private InputModeManager inputModeManager;
    [SerializeField, ReadOnly] private EventBroker eventBroker;
    [SerializeField, ReadOnly] private MagneticManager magneticManager;
    private bool _isInitialized = false;

    public GameObject popupImage;
    
    void Start()
    {
        if (popupImage != null)
        {
            popupImage.SetActive(false);
        }

        // EventBroker 초기화
        if (eventBroker == null)
        {
            eventBroker = EventBroker.Instance;
        }
        
        if (triggerColliderObject == null)
        {
            DebugLogger.LogError($"[InteractionUI] {gameObject.name}에 triggerColliderObject가 할당되지 않았습니다!");
        }
        
        // EventBroker 구독
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
        
        SetupInputActions();
    }
    
    // 플레이어 스폰 이벤트 핸들러
    private void OnPlayerSpawned(object data)
    {
        if (_isInitialized) return;
        
        magneticManager = MagneticManager.Instance;
        inputModeManager = InputModeManager.Instance;

        if (magneticManager != null)
        {
            _isInitialized = true;
            DebugLogger.Log($"[InteractionUI] {gameObject.name} - MagneticManager initialized via OnPlayerSpawned");
            
            // MagneticManager에 투명 콜라이더 오브젝트 등록
            if (triggerColliderObject != null)
            {
                magneticManager.RegisterInteractionObject(triggerColliderObject);
            }
        }
    }

    void SetupInputActions()
    {
        if (inputActions != null)
        {
            // E키 (상호작용)
            _interactAction = inputActions.FindAction("Player/Interact");
            if (_interactAction != null)
            {
                _interactAction.started -= OnInteract;
                _interactAction.started += OnInteract;
                _interactAction.Enable();
            }
        }
    }

    void OnInteract(InputAction.CallbackContext context)
    {
        // 이미 UI가 열려있으면 무시
        if (isUIOpen) return;
        
        // 매니저 재확인
        if (magneticManager == null)
        {
            magneticManager = MagneticManager.Instance;
        }
        
        // E키 입력 시점에 이 오브젝트가 활성화된 상태인지 확인
        if (magneticManager != null &&
            triggerColliderObject != null &&
            magneticManager.CurrentActiveInteractionObject == triggerColliderObject)
        {
            InputModeManager.Instance?.PlayActivate();
            OpenUI();
        }
    }
    
    private void OpenUI()
    {
        // 팝업 UI 표시
        if (popupImage != null)
        {
            popupImage.SetActive(true);
        }

        isUIOpen = true;
        
        // 콜라이더 비활성화
        SetTriggerColliderActive(false);
        
        // UI 표시 (GameUIManager가 자동으로 스택에 추가)
        GameUIManager.Instance.ShowPanel("PopUpPanel", isUIOpen);
        GameUIManager.Instance.ShowLocalizedMessage(messageTextName, myLocalizationKey);
        
        DebugLogger.Log($"[InteractionUI] UI 열림 - {gameObject.name}");
    }
    
    public void CloseUI()
    {
        if (popupImage != null)
        {
            popupImage.SetActive(false);
        }

        if (!isUIOpen) return;
        
        isUIOpen = false;
        
        // UI 닫기 (GameUIManager가 자동으로 스택에서 제거)
        GameUIManager.Instance.ShowPanel("PopUpPanel", isUIOpen);

        //인풋모드 전환
        if (inputModeManager != null)
            inputModeManager.SwitchInputMode(InputMode.Player);

        // 콜라이더 다시 활성화
        SetTriggerColliderActive(true);
        
        DebugLogger.Log($"[InteractionUI] UI 닫힘 - {gameObject.name}");
    }

    public void SetTriggerColliderActive(bool isActive)
    {
        if (triggerColliderObject != null)
        {
            triggerColliderObject.SetActive(isActive);
            
            if (!isActive && magneticManager != null)
            {
                magneticManager.HideInteractionObjectUI(triggerColliderObject);
            }
            else if (isActive && magneticManager != null)
            {
                magneticManager.RegisterInteractionObject(triggerColliderObject);
            }
            
            DebugLogger.Log($"[InteractionUI] Trigger Collider {(isActive ? "활성화" : "비활성화")} - {gameObject.name}");
        }
    }

    void OnDestroy()
    {
        if (popupImage != null)
        {
            GameUIManager.Instance.ShowPanel("PopUpPanel", isUIOpen);
            popupImage.SetActive(false);
        }

        if (_interactAction != null)
        {
            _interactAction.started -= OnInteract;
            _interactAction.Disable();
        }
        
        if (magneticManager != null && triggerColliderObject != null)
        {
            magneticManager.UnregisterInteractionObject(triggerColliderObject);
        }
        
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
    }
}