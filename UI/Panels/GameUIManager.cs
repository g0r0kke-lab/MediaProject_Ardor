using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Linq;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>씬 내 모든 UI 패널과 텍스트를 통합 관리하는 싱글턴 매니저</summary>
public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    public DialogueRunner dialogueRunner;
    public TimeLineManager timelineManager;

    private Dictionary<string, GameObject> uiPanels = new Dictionary<string, GameObject>();
    private Dictionary<string, TextMeshProUGUI> uiTexts = new Dictionary<string, TextMeshProUGUI>();
    private Dictionary<string, Image> uiImages = new Dictionary<string, Image>();

    private Stack<string> openPanelStack = new Stack<string>();
    
    [Header("Localization 설정")]
    public List<string> stringTableNames = new List<string>();
    public List<string> assetTableNames = new List<string>();
    private LocalizedString _currentLocalizedString;
    private TextMeshProUGUI _pendingTextComponent;
    
    [Header("Manager References")]
    [SerializeField, ReadOnly] private EventBroker eventBroker;
    [SerializeField, ReadOnly] private InputModeManager inputModeManager;
    [SerializeField, ReadOnly] private SoundManager soundManager;
    
    // 타임라인 중 숨길 HUD 패널 목록
    private static readonly string[] HUD_PANELS =
        { "QuestGuidePanel", "CollisionPanel", "MapTitlePanel" };
    private HashSet<string> _hudHiddenByTimeline = new HashSet<string>();
    private Coroutine _mapTitleCoroutine;
    
    // 대화 노드 설정 (Inspector에서 관리)
    [Header("Dialogue Settings")]
    [SerializeField] private List<DialogueNodeConfig> dialogueNodeConfigs = new List<DialogueNodeConfig>();
    
    private string _currentYarnNode;
    private string _pendingDialogueId;
    
    private HashSet<string> _questGuideHidingPanels = new HashSet<string>();
    private bool _questGuideWasVisible = false;
    private bool _questGuideLogicallyVisible = false;
    
    private string currentQuestKey = null;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            CollectUIElements();
        }
        else 
        {
            Destroy(gameObject);
        }
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
            eventBroker.Subscribe("DialogueMoveComplete", OnDialogueMoveComplete);
            DebugLogger.Log("[GameUIManager] DialogueMoveComplete 구독 완료");
        }

        if (!soundManager)
        {
            soundManager = SoundManager.Instance;
        }

        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.AddListener(OnYarnDialogueComplete);
            DebugLogger.Log("[GameUIManager] Yarn Spinner 이벤트 구독 완료");
        }
        else
        {
            DebugLogger.LogWarning("[GameUIManager] DialogueRunner가 할당되지 않았습니다!");
        }
    }
    
    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player != null)
        {
            inputModeManager = player.GetComponentInChildren<InputModeManager>(true);
            if (inputModeManager == null)
            {
                DebugLogger.LogWarning("[GameUIManager] 플레이어에서 InputModeManager를 찾을 수 없습니다.");
            }
        }
    }
    
    private void CollectUIElements()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
    
        if (allCanvases.Length == 0)
        {
            DebugLogger.LogWarning("[GameUIManager] Canvas를 찾을 수 없음!");
            return;
        }
    
        DebugLogger.Log($"[GameUIManager] 발견된 Canvas: {allCanvases.Length}개");
    
        foreach (Canvas canvas in allCanvases)
        {
            DebugLogger.Log($"[GameUIManager] {canvas.name}에서 UI 수집 중...");
        
            Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
        
            foreach (Transform child in allChildren)
            {
                if (child.name.EndsWith("Panel"))
                {
                    if (!uiPanels.ContainsKey(child.name))
                    {
                        uiPanels[child.name] = child.gameObject;
                        if (child.name == "CollisionPanel" || child.name == "GuidePanel"
                            || child.name == "MapTitlePanel")
                        {
                            child.gameObject.SetActive(false);
                            DebugLogger.Log($"[GameUIManager] {child.name} 강제로 닫음");
                        }
                        DebugLogger.Log($"[GameUIManager] Panel 등록: {child.name}");
                    }
                }
            
                TextMeshProUGUI textComponent = child.GetComponent<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    if (!uiTexts.ContainsKey(child.name))
                    {
                        uiTexts[child.name] = textComponent;
                    }
                }
            
                Image imageComponent = child.GetComponent<Image>();
                if (imageComponent != null)
                {
                    if (!uiImages.ContainsKey(child.name))
                    {
                        uiImages[child.name] = imageComponent;
                    }
                }
            }
        }
    
        DebugLogger.Log($"[GameUIManager] 총 등록된 Panel: {uiPanels.Count}개");
        foreach (var panel in uiPanels)
        {
            DebugLogger.Log($"  - {panel.Key}");
        }
    }


    public void ShowPanel(string panelName, bool show = true)
    {
        DebugLogger.Log($"[GameUIManager] ShowPanel 호출 - Panel: {panelName}, show: {show}");

        if (uiPanels.TryGetValue(panelName, out GameObject panel))
        {
            if (panel == null)
            {
                DebugLogger.LogWarning($"[GameUIManager] {panelName}이 이미 파괴됨 (씬 전환 중)");
                return;
            }

            bool useAnimation = UIAnimationManager.Instance != null 
                            && UIAnimationManager.Instance.IsAnimatedPanel(panelName);

            if (show)
            {
                if (useAnimation)
                    UIAnimationManager.Instance.ShowPanel(panelName);
                else
                    panel.SetActive(true);
                
                DebugLogger.Log($"[GameUIManager] {panelName} 패널 열림");
                HandlePanelOpened(panelName);
            }
            else
            {
                if (useAnimation)
                {
                    UIAnimationManager.Instance.HidePanel(panelName, () =>
                    {
                        HandlePanelClosed(panelName);
                    });
                }
                else
                {
                    panel.SetActive(false);
                    HandlePanelClosed(panelName);
                }
                
                DebugLogger.Log($"[GameUIManager] {panelName} 패널 닫힘");
            }
        }
        else
        {
            DebugLogger.LogWarning($"[GameUIManager] {panelName}을 찾을 수 없음!");
        }
    }

    // State 패턴 적용
    private void HandlePanelOpened(string panelName)
    {
        IPanelState state = PanelStateFactory.GetState(panelName);
        
        state.OnOpen(this, panelName);
        
        if (state.ShouldManageStack)
        {
            RemoveFromStack(panelName);
            openPanelStack.Push(panelName);
        }
    }

    // State 패턴 적용
    private void HandlePanelClosed(string panelName)
    {
        if (panelName == "MenuPanel")
            Time.timeScale = 1f;

        IPanelState state = PanelStateFactory.GetState(panelName);
        
        if (state.ShouldManageStack)
        {
            RemoveFromStack(panelName);
        }
        
        state.OnClose(this, panelName);
        
        // 대화 중 체크
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
        {
            DebugLogger.Log($"[GameUIManager] 대화 진행 중 - {panelName} 닫혀도 UI 모드 유지");

            if (panelName == "InventoryPanel" && inputModeManager != null)
            {
                inputModeManager.SwitchInputMode(InputMode.UI);
            }
            return;
        }

        // 타임라인 재생 중 체크
        if (TimeLineManager.Instance != null && TimeLineManager.Instance.IsTimelinePlaying)
        {
            DebugLogger.Log($"[GameUIManager] 타임라인 재생 중 - {panelName} 닫혀도 UI 모드 유지");
            return;
        }

        CheckAndSwitchInputMode();
    }

    // 새 메서드: InputMode 전환 로직 분리
    private void CheckAndSwitchInputMode()
    {
        DebugLogger.Log($"[GameUIManager] CheckAndSwitch | Stack: [{string.Join(",", openPanelStack)}]");
        bool hasNonHUDPanel = false;
        foreach (var panelInStack in openPanelStack)
        {
            if (panelInStack != "QuestGuidePanel" && panelInStack != "ButtonGuidePanel"
                && panelInStack != "CollisionPanel" && panelInStack != "SaveDataPanel"
                && panelInStack != "MapTitlePanel")
            {
                hasNonHUDPanel = true;
                break;
            }
        }

        if (!hasNonHUDPanel)
        {
            // 타임라인 재생 중 체크
            if (TimeLineManager.Instance != null && TimeLineManager.Instance.IsTimelinePlaying)
            {
                DebugLogger.Log("[GameUIManager] 타임라인 재생 중 - Player 모드 전환 차단");
                return;
            }
            
            // HeavyAnchor 부착 중 체크
            if (MagneticManager.Instance != null &&
                MagneticManager.Instance.CurrentHoldingAnchor is HeavyAnchor)
            {
                DebugLogger.Log("[GameUIManager] HeavyAnchor 부착 중 - RepelSource 모드 복원");
                if (inputModeManager) inputModeManager.SwitchInputMode(InputMode.RepelSource);
                return;
            }

            if (inputModeManager != null)
                inputModeManager.SwitchInputMode(InputMode.Player);
        }
    }

    private void RemoveFromStack(string panelName)
    {
        if (!openPanelStack.Contains(panelName)) return;
        
        var tempStack = new Stack<string>();
        while (openPanelStack.Count > 0)
        {
            string item = openPanelStack.Pop();
            if (item != panelName)
                tempStack.Push(item);
        }
        while (tempStack.Count > 0)
            openPanelStack.Push(tempStack.Pop());
    }

    // State에서 호출할 public 메서드들
    public void SwitchToPlayerMode()
    {
        if (inputModeManager != null)
            inputModeManager.SwitchInputMode(InputMode.Player);
    }

    public void SwitchToUIMode()
    {
        if (inputModeManager != null)
            inputModeManager.SwitchInputMode(InputMode.UI);
    }

    public void PlayCloseSFX()
    {
        soundManager?.PlaySFX(8);
    }

    public void HandleQuestGuideHiding(string panelName, bool isOpening)
    {
        if (isOpening)
        {
            DebugLogger.Log($"[QG] OPEN {panelName} | HidingPanels: [{string.Join(",", _questGuideHidingPanels)}] | LogicallyVisible: {_questGuideLogicallyVisible} | WasVisible: {_questGuideWasVisible}");
            
            if (_questGuideHidingPanels.Count == 0)
                _questGuideWasVisible = _questGuideLogicallyVisible;

            _questGuideHidingPanels.Add(panelName);

            DebugLogger.Log($"[QG] AFTER ADD | HidingPanels: [{string.Join(",", _questGuideHidingPanels)}] | WasVisible: {_questGuideWasVisible}");

            if (IsQuestGuideVisible())
                HideQuestGuide();
        }
        else
        {
            _questGuideHidingPanels.Remove(panelName);

            bool hasOpenHidingPanel = false;
            foreach (var p in _questGuideHidingPanels)
            {
                if (p == "Dialogue")
                {
                    if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
                    {
                        hasOpenHidingPanel = true;
                        break;
                    }
                }
                else if (IsPanelOpen(p))
                {
                    hasOpenHidingPanel = true;
                    break;
                }
            }

            if (!hasOpenHidingPanel)
            {
                DebugLogger.Log($"[QG] CLOSE CHECK | WasVisible: {_questGuideWasVisible}, LogicallyVisible: {_questGuideLogicallyVisible}, Will Restore: {_questGuideWasVisible || _questGuideLogicallyVisible}");
                
                if (_questGuideWasVisible || _questGuideLogicallyVisible)
                {
                    DebugLogger.Log($"[QG] RESTORING QuestGuide!");
                    ShowQuestGuide();
                }
                _questGuideWasVisible = false;
                _questGuideHidingPanels.Clear();
            }
        }
    }

    public void ShowInteractionPopup(InteractableObject interactableObject, string panelName)
    {
        ShowPanel(panelName, true);
    }
    
    public bool IsAnyUIOpen()
    {
        foreach (var panel in openPanelStack)
        {
            if (panel == "QuestGuidePanel" || panel == "ButtonGuidePanel" || panel == "CollisionPanel" || panel == "SaveDataPanel")
            continue;

            if (panel != "SettingPanel" && uiPanels.ContainsKey(panel) && uiPanels[panel].activeInHierarchy)
                return true;
        }
        return false;
    }
    
    public string GetTopPanel()
    {
        return openPanelStack.Count > 0 ? openPanelStack.Peek() : null;
    }
    
    public bool IsPanelOpen(string panelName)
    {
        return uiPanels.ContainsKey(panelName) && uiPanels[panelName].activeInHierarchy;
    }
    
    public void ShowMessage(string textName, string text)
    {
        if (uiTexts.TryGetValue(textName, out TextMeshProUGUI textComponent))
        {
            textComponent.text = text;
        }
    }
    
    public void ShowLocalizedMessage(string textName, string localizationKey)
    {
        if (!uiTexts.TryGetValue(textName, out TextMeshProUGUI textComponent)) return;

        DebugLogger.Log($"[Localization] 찾는 Key: {localizationKey}");

        // 이전 구독 해제
        if (_currentLocalizedString != null)
            _currentLocalizedString.StringChanged -= OnLocalizedStringChanged;

        _pendingTextComponent = textComponent;

        foreach (string tableName in stringTableNames)
        {
            var localizedString = new LocalizedString(tableName, localizationKey);
            var translatedText = localizedString.GetLocalizedString();

            if (!string.IsNullOrEmpty(translatedText)
                && !translatedText.Contains("No translation"))
            {
                textComponent.text = translatedText;
                // 구독 등록 — 언어 변경 시 자동 갱신
                _currentLocalizedString = localizedString;
                _currentLocalizedString.StringChanged += OnLocalizedStringChanged;
                return;
            }
        }

        DebugLogger.LogWarning($"[Localization] Key '{localizationKey}'를 찾지 못했습니다.");
        textComponent.text = localizationKey;
    }
    
    private void OnLocalizedStringChanged(string value)
    {
        if (_pendingTextComponent != null)
            _pendingTextComponent.text = value;
    }
    
    public void ShowLocalizedImage(string imageName, string localizationKey)
    {
        if (uiImages.TryGetValue(imageName, out Image imageComponent))
        {
            DebugLogger.Log($"[Localization Asset] 찾는 Key: {localizationKey}");
        
            foreach (string tableName in assetTableNames)
            {
                DebugLogger.Log($"[Localization Asset] 테이블 '{tableName}'에서 검색 중...");
            
                var localizedAsset = new LocalizedAsset<Sprite>
                {
                    TableReference = tableName,
                    TableEntryReference = localizationKey
                };
                var sprite = localizedAsset.LoadAsset();
            
                if (sprite != null)
                {
                    imageComponent.sprite = sprite;
                    DebugLogger.Log($"[Localization Asset] 성공! '{tableName}' 테이블에서 찾음");
                    return;
                }
            }
        
            DebugLogger.LogWarning($"[Localization Asset] Key '{localizationKey}'에 해당하는 이미지를 찾지 못했습니다.");
        }
    }
    
    public void ShowLocalizedImageAsync(string imageName, string localizationKey, System.Action onComplete = null)
    {
        if (uiImages.TryGetValue(imageName, out Image imageComponent))
        {
            DebugLogger.Log($"[Localization Asset Async] 찾는 Key: {localizationKey}");
            
            StartCoroutine(LoadLocalizedImageCoroutine(imageComponent, localizationKey, onComplete));
        }
    }
    
    private System.Collections.IEnumerator LoadLocalizedImageCoroutine(Image imageComponent, string localizationKey, System.Action onComplete)
    {
        foreach (string tableName in assetTableNames)
        {
            DebugLogger.Log($"[Localization Asset Async] 테이블 '{tableName}'에서 검색 중...");
            
            var localizedAsset = new LocalizedAsset<Sprite>
            {
                TableReference = tableName,
                TableEntryReference = localizationKey
            };
            var operation = localizedAsset.LoadAssetAsync();
            
            yield return operation;
            
            if (operation.Result != null)
            {
                imageComponent.sprite = operation.Result;
                DebugLogger.Log($"[Localization Asset Async] 성공! '{tableName}' 테이블에서 찾음");
                onComplete?.Invoke();
                yield break;
            }
        }
        
        DebugLogger.LogWarning($"[Localization Asset Async] Key '{localizationKey}'에 해당하는 이미지를 찾지 못했습니다.");
        onComplete?.Invoke();
    }
    
    public void ChangeLanguage(string localeCode)
    {
        var availableLocales = LocalizationSettings.AvailableLocales.Locales;
        var targetLocale = availableLocales.FirstOrDefault(locale => locale.Identifier.Code == localeCode);

        if (targetLocale != null)
        {
            LocalizationSettings.SelectedLocale = targetLocale;
            DebugLogger.Log($"언어가 {targetLocale.LocaleName}로 변경되었습니다.");

            RefreshQuestGuideText();

            KeyboardUI keyboardUI = FindFirstObjectByType<KeyboardUI>();
            keyboardUI?.OnLanguageChanged();
        }
    }
    
    public void OpenDialogue(string dialogueId, Transform npcTransform)
    {
        _pendingDialogueId = dialogueId;
    
        if (npcTransform != null)
        {
            DebugLogger.Log($"[GameUIManager] NPC 바라보기 시작, 대화 예약: {dialogueId}");
            eventBroker?.Publish("DialogueStart", npcTransform);
        }
        else
        {
            DebugLogger.Log($"[GameUIManager] NPC 없음, 바로 대화 시작: {dialogueId}");
            StartDialogueImmediate(dialogueId);
        }
    }
    
    public void OpenDialogue(string fileName)
    {
        StartDialogueImmediate(fileName);
    }
    
    // State 패턴 적용
    private void StartDialogueImmediate(string fileName)
    {
        if (dialogueRunner != null)
        {
            // suffix 추가
            string currentLanguage = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "ko-KR";
            string fullId = currentLanguage == "ko-KR" ? fileName + "_ko" : fileName + "_en";

            _currentYarnNode = fullId;

            IPanelState dialogueState = PanelStateFactory.GetState("Dialogue");
            dialogueState.OnOpen(this, "Dialogue");
        
            dialogueRunner.StartDialogue(fullId);
            DebugLogger.Log($"[GameUIManager] Yarn 대화 시작: {fullId}");
        }
    }
    
    private void OnDialogueMoveComplete(object data)
    {
        DebugLogger.Log($"[GameUIManager] OnDialogueMoveComplete 호출됨! _pendingDialogueId: {_pendingDialogueId}");
    
        if (string.IsNullOrEmpty(_pendingDialogueId)) return;
    
        DebugLogger.Log($"[GameUIManager] 이동 완료, 대화 시작: {_pendingDialogueId}");
        StartDialogueImmediate(_pendingDialogueId);
    
        _pendingDialogueId = null;
    }

    // 설정 기반으로 변경 (하드코딩 제거)
    private void OnYarnDialogueComplete()
    {
        DebugLogger.Log($"[GameUIManager] Yarn 대화 완료! 노드: {_currentYarnNode}");

        bool needsProgressState = IsProgressNode(_currentYarnNode);
        bool needsUIMode = IsUIKeepNode(_currentYarnNode);

        EventBroker.Instance?.Publish("DialogueEnd");

        DebugLogger.Log($"[GameUIManager] NeedsProgressState: {needsProgressState}, NeedsUIMode: {needsUIMode}");

        if (needsProgressState)
        {
            GameManagerRegistry.ProgressState();
            DebugLogger.Log("[GameUIManager] 상태 진행 실행");
        }

        if (needsUIMode)
        {
            if (inputModeManager != null)
            {
                inputModeManager.SwitchInputMode(InputMode.UI);
                DebugLogger.Log("[GameUIManager] UI 모드 유지");
            }
        }
        else
        {
            if (inputModeManager != null)
            {
                // 타임라인이 시작됐으면 Player 전환 차단
                if (TimeLineManager.Instance != null && TimeLineManager.Instance.IsTimelinePlaying)
                {
                    DebugLogger.Log("[GameUIManager] 타임라인 재생 중 - Player 모드 전환 차단");
                }
                else
                {
                    inputModeManager.SwitchInputMode(InputMode.Player);
                    DebugLogger.Log("[GameUIManager] Player 모드로 전환");
                }
            }
        }

        IPanelState dialogueState = PanelStateFactory.GetState("Dialogue");
        dialogueState.OnClose(this, "Dialogue");
        
        RemoveFromStack("DialoguePanel");
    }

    // 노드 타입 체크 메서드
    private bool IsProgressNode(string nodeName)
    {
        var config = dialogueNodeConfigs.Find(c => c.nodeName == nodeName);
        return config != null && config.progressState;
    }

    private bool IsUIKeepNode(string nodeName)
    {
        var config = dialogueNodeConfigs.Find(c => c.nodeName == nodeName);
        return config != null && config.keepUIMode;
    }

    public void CloseDialogue()
    {
        RemoveFromStack("DialoguePanel");
        DebugLogger.Log("[GameUIManager] CloseDialogue 호출됨");
    }

    public void openTimeline(string timelineName)
    {
        if (timelineManager != null)
        {
            timelineManager.PlayTimeline(timelineName);
            DebugLogger.Log("타임라인 이름 전달;");
        }
    }

    public void OpenQuestGuide(string QuestMessage)
    {
        DebugLogger.Log($"[QG] OpenQuestGuide | HidingPanels: [{string.Join(",", _questGuideHidingPanels)}] | Setting LogicallyVisible=true");

        currentQuestKey = QuestMessage;
        _questGuideLogicallyVisible = true;
        
        if (_questGuideHidingPanels.Count > 0)
        {
            _questGuideWasVisible = true;
            DebugLogger.Log($"[QG] UI가 열려있어서 WasVisible=true 설정");
        }
        ShowPanel("QuestGuidePanel",true);
        ShowLocalizedMessage("QuestMessage", QuestMessage);

        RefreshQuestGuideText();
    }

    private void RefreshQuestGuideText()
    {
        if (!string.IsNullOrEmpty(currentQuestKey))
        {
            ShowLocalizedMessage("QuestMessage", currentQuestKey);
            DebugLogger.Log($"[QG] 퀘스트 텍스트 갱신: {currentQuestKey}");
        }
    }

    // QuestGuide 의도적으로 닫음 (복원 x)
    public void CloseQuestGuide()
    {
        currentQuestKey = null;
        _questGuideLogicallyVisible = false;
        _questGuideWasVisible = false; // 패널이 숨기고 있는 중에 닫혔어도 복원 차단
        ShowPanel("QuestGuidePanel", false);
    }

    public void OpenSaveDataPanel()
    {
        ShowPanel("SaveDataPanel",true);
        ShowLocalizedMessage("SaveDataUIText", "Save Data Now");
        Invoke("CloseSaveDataPanel", 2f);
    }

    private void CloseSaveDataPanel()
    {
        HideMessage("SaveDataPanel");
    }

    public void HideMessage(string panelName)
    {
        ShowPanel(panelName, false);
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ShowPanel("CollisionPanel", false);
    }
    
    private void OnDestroy()
    {
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
            eventBroker.Unsubscribe("DialogueMoveComplete", OnDialogueMoveComplete);
        }

        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueComplete.RemoveListener(OnYarnDialogueComplete);
        }
    }
    
    public bool IsQuestGuideVisible()
    {
        return uiPanels.TryGetValue("QuestGuidePanel", out GameObject panel) 
               && panel != null && panel.activeSelf;
    }

    // QuestGuide 잠깐 가려두는 용도 (복원 대상)
    public void HideQuestGuide()
    {
        if (uiPanels.TryGetValue("QuestGuidePanel", out GameObject panel) && panel != null)
            panel.SetActive(false);
    }

    public void ShowQuestGuide()
    {
        if (uiPanels.TryGetValue("QuestGuidePanel", out GameObject panel) && panel != null)
        {
            panel.SetActive(true);
            
            if (!string.IsNullOrEmpty(currentQuestKey))
            {
                ShowLocalizedMessage("QuestMessage", currentQuestKey);
                DebugLogger.Log($"[QG] 퀘스트 복원 시 텍스트 갱신: {currentQuestKey}");
            }
        }
    }
    
    public void HideHUDForTimeline()
    {
        _hudHiddenByTimeline.Clear();
        foreach (var panelName in HUD_PANELS)
        {
            if (IsPanelOpen(panelName))
            {
                // 열려있던 것만 기록
                _hudHiddenByTimeline.Add(panelName);
                HideMessage(panelName);
            }
        }
    }

    public void RestoreHUDAfterTimeline()
    {
        foreach (var panelName in _hudHiddenByTimeline)
        {
            // 퀘스트 가이드는 타임라인 중 CloseQuestGuide()로 닫혔을 수 있으므로 논리적 상태 확인
            if (panelName == "QuestGuidePanel" && !_questGuideLogicallyVisible)
                continue;

            ShowPanel(panelName, true);
        }
        _hudHiddenByTimeline.Clear();
    }
    
    public void ClosePanel(string panelName)
    {
        ShowPanel(panelName, false);
    }

    /// <summary>
    /// MapTitlePanel을 애니메이션과 함께 표시하고 duration 초 후 자동으로 닫습니다.
    /// </summary>
    /// <param name="duration">자동으로 닫히기까지 대기 시간 (초)</param>
    public void ShowMapTitlePanel(float duration = 3f)
    {
        if (_mapTitleCoroutine != null)
        {
            StopCoroutine(_mapTitleCoroutine);
            _mapTitleCoroutine = null;
        }

        ShowPanel("MapTitlePanel", true);
        _mapTitleCoroutine = StartCoroutine(HideMapTitleAfterDelay(duration));
    }

    private System.Collections.IEnumerator HideMapTitleAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        ShowPanel("MapTitlePanel", false);
        _mapTitleCoroutine = null;
    }
}