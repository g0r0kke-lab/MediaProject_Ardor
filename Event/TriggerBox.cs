using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Serialization;

/// <summary>플레이어 진입 시 대화, 타임라인, 가이드, 커스텀 이벤트 등을 트리거하는 콜라이더 컴포넌트</summary>
public class TriggerBox : MonoBehaviour
{
    private bool isUIOpen = false;
    private string targetTag = "Player"; // 충돌 감지할 태그
    [SerializeField, ReadOnly] private bool hasTriggered = false; // 한 번만 표시하기 위한 플래그
    [SerializeField, ReadOnly] private bool _isPlayerInside = false;

    [Header("상단팝업 띄우기")] public bool isCollisionOpen = false; // Collision Panel 띄우기
    private string messageTextName = "CollisionText"; // 메시지를 표시할 UI 텍스트 이름
    public string myLocalizationKey = "default_message"; // 이 물체만의 번역 키

    [Header("대화 띄우기")] public bool isDialogueOpen = false; // Dialogue Panel 띄우기

    //public DialogueManager dialogueManager;
    [FormerlySerializedAs("koreanFileName")]
    public string dialogueId;
    // [HideInInspector]
    // public string englishFileName; // 영어 CSV 파일명
    public bool turnToNPCOnDialogue = true;

    [Header("타임라인 띄우기")] public bool isTimelineOpen = false; // Timeline 띄우기
    public string timelineName;
    public bool playTimelineAfterDialogue = false; //이건 Dialogue에서 관리하도록 변경필요***

    [Header("물체 타임라인 띄우기")] public bool isObjectTimelineOpen = false; //
    [SerializeField] private GameObject triggerColliderObject;

    [Header("Input System")] [SerializeField]
    private InputActionAsset inputActions;

    private InputAction _interactAction;

    [Header("Manager References")] private InputModeManager inputModeManager;
    [SerializeField, ReadOnly] private MagneticManager magneticManager;
    private bool _isInitialized = false;

    [Header("가이드 띄우기")]
    public bool isGuideOpen = false; // Guide Panel 띄우기
    public string guideId = "GD1_00";

    [Header("상태 관리")] public bool progressStateOnTrigger = false; // 트리거 진입 시 상태 진행
    public bool progressStateOnDialogueEnd = false; // 대화 종료 시 상태 진행 여부

    [Header("커스텀 이벤트")]
    public bool isCustomEventOpen = false;          // 커스텀 이벤트 사용 여부
    public bool isOneShot = true;                   // true=일회성, false=반복 실행, 커스텀 이벤트에만 영향
    public UnityEvent onTriggerEvent;               // 인스펙터에서 할당할 이벤트
    
    // 정적 변수로 GameUIManager와 동기화
    private static TriggerBox currentActivePopup = null;

    [Header("UI 세이브 포인트 관리")] [SerializeField]
    private SaveDataManager saveDataManager;

    [Header("조건부 트리거")]
    public bool useConditionOnInteract = false;
    [SerializeField] private TriggerConditionHandler conditionHandler;
    
    private int savePointIndex;
    public int currentSavePointIndex;

    // 매니저 준비 완료 플래그
    private bool _managerReady = false;

    /// <summary>
    /// 세이브 포인트 기준으로 이미 통과한 트리거임이 확인됐을 때 호출.
    /// 서브클래스에서 override해 추가 동작(예: GameObject 비활성화) 구현 가능.
    /// </summary>
    protected virtual void OnAlreadyTriggered()
    {
        hasTriggered = true;
    }

    void Awake()
    {
        StopAllCoroutines();
        
        if (isObjectTimelineOpen || useConditionOnInteract)
        {
            SetupInputActions();
            inputModeManager = InputModeManager.Instance;
        }

        // 코루틴으로 매니저 찾기 (DontDestroyOnLoad 객체는 늦게 준비됨)
        StartCoroutine(WaitForManagers());
    }
    
    // SaveDataManager가 준비될 때까지 대기
    private IEnumerator WaitForManagers()
    {
        // SaveDataManager.Instance가 준비될 때까지 최대 1초 대기
        float timeout = 1f;
        float elapsed = 0f;

        while (SaveDataManager.Instance == null && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        saveDataManager = SaveDataManager.Instance;

        if (saveDataManager == null)
        {
            DebugLogger.LogError($"[TriggerBox] {gameObject.name} - SaveDataManager를 찾을 수 없습니다!");
            _managerReady = false;
            yield break;
        }

        // 세이브 포인트 가져오기
        savePointIndex = saveDataManager.GetCurrentSavePointIndex();
        
        DebugLogger.Log($"[TriggerBox] {gameObject.name} - 세이브 데이터 로드 완료: 현재 세이브={savePointIndex}, 이 트리거={currentSavePointIndex}");

        // 이미 지나간 트리거 체크
        if (currentSavePointIndex < savePointIndex)
        {
            OnAlreadyTriggered();
            DebugLogger.Log($"[TriggerBox] {gameObject.name} - 이미 실행됨");
        }

        _managerReady = true;

        // Object Timeline용 매니저 초기화
        if (isObjectTimelineOpen)
        {
            yield return null; // 1프레임 대기
            
            magneticManager = MagneticManager.Instance;
            if (magneticManager != null && triggerColliderObject != null)
            {
                _isInitialized = true;
                magneticManager.RegisterInteractionObject(triggerColliderObject);
                DebugLogger.Log($"[TriggerBox] {gameObject.name} - MagneticManager 등록 완료");
            }
        }
    }

    void Start()
    {
        // SaveDataManager 안전 체크
        if (saveDataManager == null)
        {
            saveDataManager = FindFirstObjectByType<SaveDataManager>();

            if (saveDataManager == null)
            {
                DebugLogger.LogWarning("[TriggerBox] SaveDataManager not found.");
                hasTriggered = false; // 안전하게 false로 설정
                return;
            }
        }

        // 세이브 포인트 가져오기 (GetCurrentSavePointIndex 내부에서 null 처리됨)
        savePointIndex = saveDataManager.GetCurrentSavePointIndex();

        DebugLogger.Log(
            $"[TriggerBox] {gameObject.name} - 로드된 세이브 포인트: {savePointIndex}, 이 트리거의 세이브 포인트: {currentSavePointIndex}");

        // 이미 지나간 트리거인지 확인
        if (currentSavePointIndex < savePointIndex)
        {
            OnAlreadyTriggered();
            DebugLogger.Log($"[TriggerBox] {gameObject.name} - 이미 실행된 트리거로 표시");
        }

        if (isObjectTimelineOpen)
        {
            StartCoroutine(DelayedMagneticInit());
        }
    }

    // 새 메서드 추가
    private IEnumerator DelayedMagneticInit()
    {
        // 1프레임 대기 (다른 매니저들 초기화 완료 대기)
        yield return null;

        magneticManager = MagneticManager.Instance;
        if (magneticManager != null && triggerColliderObject != null)
        {
            _isInitialized = true;
            magneticManager.RegisterInteractionObject(triggerColliderObject);
            DebugLogger.Log($"[TriggerBox] {gameObject.name} - MagneticManager 등록 완료");
        }
        else
        {
            DebugLogger.LogWarning($"[TriggerBox] {gameObject.name} - MagneticManager 또는 triggerColliderObject가 null");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
            _isPlayerInside = true;
        
        if (!other.CompareTag(targetTag)) return;
        
        // 반복성 커스텀 이벤트: hasTriggered 체크 전에 먼저 처리
        if (isCustomEventOpen && !isOneShot && onTriggerEvent != null)
            onTriggerEvent.Invoke();

        if (hasTriggered) return;
        
        // 매니저 준비 안됐으면 무시
        if (!_managerReady || saveDataManager == null)
        {
            DebugLogger.LogWarning($"[TriggerBox] {gameObject.name} - 매니저 준비 안됨, 트리거 무시");
            return;
        }

        // 실시간으로 최신 세이브 포인트 가져오기
        savePointIndex = saveDataManager.GetCurrentSavePointIndex();
        
        DebugLogger.Log($"[TriggerBox] {gameObject.name} Trigger Enter - 이 트리거={currentSavePointIndex}, 현재 세이브={savePointIndex}");
        
        // 현재 세이브 포인트보다 크거나 같을 때만 실행
        if (currentSavePointIndex >= savePointIndex)
        {
            hasTriggered = true;

            if (progressStateOnTrigger)
                ProgressGameState("트리거 진입");
        
            if (isTimelineOpen)
            {
                GameUIManager.Instance.openTimeline(timelineName);
            }
            else if (isCollisionOpen)
            {
                ShowCollisionPanel(myLocalizationKey);
            }
            else if (isDialogueOpen)
            {
                Transform npcTransform = null;
                if (turnToNPCOnDialogue)
                {
                    GameObject npc = GameObject.FindWithTag("NPC");
                    if (npc != null)
                    {
                        npcTransform = npc.transform;
                        EventBroker.Instance?.Publish("TurnToNPC", npcTransform);
                    }
                }

                GameUIManager.Instance.OpenDialogue(dialogueId, npcTransform);
            }
            else if (isGuideOpen)
            {
                StartGuide();
            }
            
            // 일회성/반복성 모두 세이브 포인트 통과 후 실행
            if (isCustomEventOpen && onTriggerEvent != null)
                onTriggerEvent.Invoke();
        }
        else
        {
            DebugLogger.Log($"[TriggerBox] {gameObject.name} - 실행 조건 불만족 (세이브={savePointIndex})");
        }
    }
    
    // 인스펙터 할당 대사를 외부에서 직접 실행하는 public 메서드
    public void PlayDialogue()
    {
        Transform npcTransform = null;
        if (turnToNPCOnDialogue)
        {
            GameObject npc = GameObject.FindWithTag("NPC");
            if (npc != null)
            {
                npcTransform = npc.transform;
                EventBroker.Instance?.Publish("TurnToNPC", npcTransform);
            }
        }

        GameUIManager.Instance.OpenDialogue(dialogueId, npcTransform);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
            _isPlayerInside = false;
    }

    // ShowCollisionPanel 메서드 수정
    public void ShowCollisionPanel(string myLocalizationKey)
    {
        // 이전에 실행 중인 코루틴 중지
        StopAllCoroutines();

        isUIOpen = true;
        GameUIManager.Instance.ShowPanel("CollisionPanel", isUIOpen);
        GameUIManager.Instance.ShowLocalizedMessage("CollisionPanel_Text", myLocalizationKey);
        GameUIManager.Instance.ShowLocalizedImage("CollisionPanel_Image", myLocalizationKey);

        // 2초 후 페이드 아웃 시작
        StartCoroutine(HidePanelWithDelay("CollisionPanel", 2f));
    }

    public void ShowNotificationPanel(string myLocalizationKey)
    {
        if (!gameObject.activeInHierarchy) return;
        // 이전에 실행 중인 코루틴 중지
        StopAllCoroutines();

        isUIOpen = true;
        GameUIManager.Instance.ShowPanel("NotificationPanel", isUIOpen);
        GameUIManager.Instance.ShowLocalizedMessage("NotificationPanel_Text", myLocalizationKey);

        // 2초 후 페이드 아웃 시작
        StartCoroutine(HidePanelWithDelay("NotificationPanel", 2f));
    }

    // 새로 추가: 딜레이 후 패널 닫기
    private IEnumerator HidePanelWithDelay(string panelName, float delay)
    {
        yield return new WaitForSeconds(delay);

        isUIOpen = false;
        GameUIManager.Instance.HideMessage(panelName);
    }

    /*private void StartDialogue()
    {
        if (dialogueManager != null)
        {
            string currentLanguage = LocalizationSettings.SelectedLocale.Identifier.Code;

            if (currentLanguage == "ko-KR")
            {
                dialogueManager.StartDialogue(koreanFileName);
            }
            else
            {
                dialogueManager.StartDialogue(englishFileName);
            }

            // 타임라인 실행 OR 상태 진행 중 하나라도 필요하면 이벤트 등록
            if (progressStateOnDialogueEnd || (playTimelineAfterDialogue && timelineName != null))
            {
                dialogueManager.onDialogueEnd.RemoveListener(OnDialogueFinished);
                dialogueManager.onDialogueEnd.AddListener(OnDialogueFinished);
            }
        }
    }*/

    // 대화 종료 후 타임라인 실행 (있으면)
    private void OnDialogueFinished()
    {
        DebugLogger.Log("대화 종료종료종료");
        // 대화 종료 시 상태 진행
        if (progressStateOnDialogueEnd)
        {
            ProgressGameState("대화 종료");
        }

        // 타임라인이 설정되어 있을 때만 실행
        if (playTimelineAfterDialogue && timelineName != null)
        {
            GameUIManager.Instance.openTimeline(timelineName);
        }

        // 이벤트 즉시 해제
        //dialogueManager.onDialogueEnd.RemoveListener(OnDialogueFinished);
    }

    private void ProgressGameState(string context)
    {
        GameManagerRegistry.ProgressState();
        DebugLogger.Log($"[CollisionUI] 상태 진행 ({context}): {gameObject.name}");
    }

    //가이드 패널 관리
    public void StartGuide()
    {
        currentActivePopup = this;
        isUIOpen = true;
 
        if (GuideManager.Instance == null)
        {
            DebugLogger.LogWarning("[TriggerBox] GuideManager.Instance가 null! 씬에 GuideManager가 있는지 확인하세요.");
            return;
        }
        
        if (GuideManager.Instance) GuideManager.Instance.OpenGuide(guideId);
        if(GameUIManager.Instance) GameUIManager.Instance.ShowLocalizedMessage(messageTextName, myLocalizationKey);
        //popupImage.SetActive(true);
    }


    // 여기서 부터는 InteractableObject 코드의 혼합 실험입니다.
    //
    //
    //
    //
    //
    void SetupInputActions()
    {
        if (inputActions != null)
        {
            // Asset 복사본 사용 (다른 TriggerBox와 분리)
            var clonedActions = Instantiate(inputActions); // ← 추가
            var playerMap = clonedActions.FindActionMap("Player");
            playerMap?.Enable();

            _interactAction = clonedActions.FindAction("Player/Interact");

            if (_interactAction != null)
            {
                _interactAction.started -= OnInteract;
                _interactAction.started += OnInteract;
                // 개별 Enable 제거 (ActionMap에서 이미 활성화됨)
                // _interactAction.Enable();  

                DebugLogger.Log(
                    $"[TriggerBox] {gameObject.name} - Action 연결 완료, enabled: {_interactAction.enabled}");
            }
        }
    }

    void OnInteract(InputAction.CallbackContext context)
    {
        if (!isObjectTimelineOpen && !useConditionOnInteract) return;
        if (isUIOpen) return;
        
        // 플레이어가 이 트리거 안에 없으면 전부 무시
        if (!_isPlayerInside) return;
        
        // 조건 핸들러 먼저 처리
        if (useConditionOnInteract && _isPlayerInside && conditionHandler != null)
        {
            InputModeManager.Instance?.PlayActivate();
            conditionHandler.Check();
            return;
        }

        // 기존 Object Timeline 로직
        if (magneticManager == null)
            magneticManager = MagneticManager.Instance;

        if (magneticManager == null || triggerColliderObject == null)
            return;

        GameObject currentActive = magneticManager.CurrentActiveInteractionObject;
        if (currentActive != triggerColliderObject) return;

        DebugLogger.Log($"[TriggerBox] ★ OnInteract 실행 - {gameObject.name}");

        if (!string.IsNullOrEmpty(timelineName))
        {
            InputModeManager.Instance?.PlayActivate();
            DebugLogger.Log($"[TriggerBox] ✅ 타임라인 실행: {timelineName}");
            GameUIManager.Instance.openTimeline(timelineName);
            SetTriggerColliderActive(false);
        }
        else
        {
            DebugLogger.LogWarning($"[TriggerBox] timelineName이 비어있음!");
        }
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

            DebugLogger.Log($"[TriggerBox] Trigger Collider {(isActive ? "활성화" : "비활성화")} - {gameObject.name}");
        }
    }

    public void ReactivateTrigger()
    {
        SetTriggerColliderActive(true);
    }

    public void activateTrigger()
    {
        SetTriggerColliderActive(false);
    }
    
    // public void ForceSetTriggered()
    // {
    //     hasTriggered = true;
    //     MagneticManager.Instance?.RemoveCollisionActiveObject(gameObject);
    // }

    void OnEnable()
    {
        // OnEnable에서는 찾기만 시도 (초기화는 Start에서)
        if (saveDataManager == null)
        {
            saveDataManager = FindFirstObjectByType<SaveDataManager>();
        }

        // 사망 시 _isPlayerInside 리셋용 (텔레포트는 OnTriggerExit를 안정적으로 발생시키지 않음)
        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
        EventBroker.Instance?.Subscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);
    }

    private void OnPlayerDeath(object data)
    {
        _isPlayerInside = false;
    }

    // 외부에서 hasTriggered를 초기화할 수 있는 public 메서드
    public void ResetTrigger()
    {
        hasTriggered = false;
        DebugLogger.Log($"[TriggerBox] {gameObject.name} - hasTriggered 리셋");
    }

    void OnDisable()
    {
        // 코루틴 정리
        StopAllCoroutines();

        EventBroker.Instance?.Unsubscribe(PlayerCrouchDeath.EVENT_DEATH, OnPlayerDeath);

        // 비활성화 시 플레이어 내부 상태 초기화
        _isPlayerInside = false;

        // Panel 닫기 (OnDestroy보다 안전)
        if (isUIOpen && GameUIManager.Instance != null)
        {
            isUIOpen = false;
            GameUIManager.Instance.ShowPanel("CollisionPanel", false);
        }
    }

    void OnDestroy()
    {
        // 이벤트 정리만
        /*if (dialogueManager != null)
        {
            dialogueManager.onDialogueEnd.RemoveListener(OnDialogueFinished);
        }*/

        if (currentActivePopup == this)
        {
            currentActivePopup = null;
        }

        if (isObjectTimelineOpen)
        {
            if (_interactAction != null)
            {
                _interactAction.started -= OnInteract;
                _interactAction.Disable();
            }

            if (magneticManager != null && triggerColliderObject != null)
            {
                magneticManager.UnregisterInteractionObject(triggerColliderObject);
            }
        }
    }
}