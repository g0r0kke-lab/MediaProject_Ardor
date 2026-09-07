using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

/// <summary>탭/ESC 키 입력으로 인벤토리와 메뉴 패널을 제어하는 UI 컨트롤러</summary>
public class KeyboardUI : MonoBehaviour
{
    public static KeyboardUI Instance;

    [Header("상호작용 설정")]
    public InteractableObject targetObject;
    
    [Header("탭 색상")]
    public Color selectedColor = new Color(1f, 1f, 1f, 1f);
    public Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private bool inventoryOpen = false;
    private bool popUpOpen = false;
    private Dictionary<string, (Button button, GameObject panel)> tabs = new Dictionary<string, (Button, GameObject)>();
    
    [Header("인벤토리")]
    public InventoryUI inventoryUI; 

    private InputModeManager inputModeManager;
    private EventBroker eventBroker;
    
    private void Start()
    {
        eventBroker = EventBroker.Instance;
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
            eventBroker.Subscribe("OnDialogueStarted", OnDialogueStarted);
        }

        RefreshSettingTexts();
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

    private void OnDialogueStarted(object data)
    {
        CloseInventory();
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleESC();
        }
    }
    
    // 인벤토리 
    public void CloseInventory()
    {
        if (!inventoryOpen) return;
        
        inventoryOpen = false;
        GameUIManager.Instance.ShowPanel("InventoryPanel", false);
        
        if (inputModeManager != null)
            inputModeManager.SwitchInputMode(InputMode.Player);
    }

    private void ToggleInventory()
    {
        // 대화 중이면 인벤토리 열기 차단
        if (GameUIManager.Instance != null && GameUIManager.Instance.dialogueRunner != null
            && GameUIManager.Instance.dialogueRunner.IsDialogueRunning)
        {
            DebugLogger.Log("[KeyboardUI] 대화 중 - 인벤토리 열기 차단");
            return;
        }

        // 타임라인 재생 중이면 인벤토리 열기 차단
        if (TimeLineManager.Instance != null && TimeLineManager.Instance.IsTimelinePlaying)
        {
            DebugLogger.Log("[KeyboardUI] 타임라인 재생 중 - 인벤토리 열기 차단");
            return;
        }

        // 비디오 재생 중이면 인벤토리 열기 차단
        VideoManager videoManager = FindFirstObjectByType<VideoManager>();
        if (videoManager != null && videoManager.IsVideoPlaying)
        {
            DebugLogger.Log("[KeyboardUI] 비디오 재생 중 - 인벤토리 열기 차단");
            return;
        }
        
        // 수정: 애니메이션 중일 때는 토글 차단
        if (UIAnimationManager.Instance != null
            && UIAnimationManager.Instance.IsAnimating("InventoryPanel"))
        {
            DebugLogger.Log("[KeyboardUI] 인벤토리 애니메이션 진행 중 - 입력 무시");
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        DebugLogger.Log($"I 키 누름 - 캔버스 활성화 상태: {canvas?.gameObject.activeSelf}");

        // 수정: 실제 패널 상태를 기준으로 판단
        bool isPanelCurrentlyOpen = GameUIManager.Instance.IsPanelOpen("InventoryPanel");

        if (!isPanelCurrentlyOpen && GameUIManager.Instance.IsAnyUIOpen())
        {
            DebugLogger.Log("지금 다른 UI가 켜져있음");
            return;
        }

        // 수정: 패널 상태 기반으로 토글
        inventoryOpen = !isPanelCurrentlyOpen;
        GameUIManager.Instance.ShowPanel("InventoryPanel", inventoryOpen);

        if (!inventoryOpen)
        {
            if (inputModeManager != null)
                inputModeManager.SwitchInputMode(InputMode.Player);
            return;
        }

        DebugLogger.Log($"ShowPanel 호출 후 - 캔버스 활성화 상태: {canvas?.gameObject.activeSelf}");

        if (inventoryOpen)
        {
            // 추가: InputMode를 UI로 전환
            if (inputModeManager != null)
                inputModeManager.SwitchInputMode(InputMode.UI);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX(16);

            GameUIManager.Instance.ShowLocalizedMessage("InventoryText", "inventory");
            if (inventoryUI != null)
            {
                inventoryUI.UpdateUI();
            }
        }
    }

    private void CloseAllPopUps()
    {
        popUpOpen = false;
        GameUIManager.Instance.ShowPanel("PopUpPanel", false);
        TimelineController.Instance.Resume();
        inputModeManager.SwitchInputMode(InputMode.Player);
    }

    public void HandleESC()
    {
        // 추가: 비디오 재생 중이면 ESC 무시
        VideoManager videoManager = FindFirstObjectByType<VideoManager>();
        if (videoManager != null && videoManager.IsVideoPlaying)
        {
            DebugLogger.Log("[KeyboardUI] 비디오 재생 중 - ESC 무시");
            return;
        }

        // 타임라인 재생 중이면 ESC 무시
        if (TimeLineManager.Instance != null && TimeLineManager.Instance.IsTimelinePlaying)
        {
            DebugLogger.Log("[KeyboardUI] 타임라인 재생 중 - ESC 무시");
            return;
        }
        
        string topPanel = GameUIManager.Instance.GetTopPanel();
        
        // MenuPanel 닫기 처리
        if (topPanel == "MenuPanel")
        {
            GameUIManager.Instance.ShowPanel("MenuPanel", false);
            return;
        }
        
        if (topPanel == "QuestPanel")
        {
            GameUIManager.Instance.ShowPanel("QuestPanel", false);
            return;
        }
        
        // HUD 패널은 ESC로 닫을 수 없음 - MenuPanel 열기
        if (topPanel == "QuestGuidePanel" 
            || topPanel == "ButtonGuidePanel" || topPanel == "CollisionPanel")
        {
            OpenMenu();
            return;
        }
        
        if (topPanel == "SettingPanel")
        {
            GameUIManager.Instance.ShowPanel("SettingPanel", false);
            RefreshSettingTexts();
            return;
        }

        if (topPanel == "GuidePanel")
        {
            GuideManager.Instance.CloseGuide();
            return;
        }

        if (topPanel != null)
        {
            GameUIManager.Instance.ShowPanel(topPanel, false);
            return;
        }

        // 아무 패널도 열려있지 않을 때만 MenuPanel 열기
        OpenMenu();
    }

    public void OpenMenu()
    {
        Time.timeScale = 0f;
        GameUIManager.Instance.ShowPanel("MenuPanel", true);
    }

    public void OpenMenuGuide()
    {
        GameUIManager.Instance.ShowPanel("MenuGuidePanel", true);
        RefreshSettingTexts();
    }
    
    public void OpenSetting()
    {
        GameUIManager.Instance.ShowPanel("SettingPanel", true);
    }
    public void ExitGame()
    {
        Application.Quit();
    }

    // 언어 변경 후 호출 (GameUIManager에서)
    public void OnLanguageChanged()
    {
        if (GameUIManager.Instance.IsPanelOpen("SettingPanel"))
        {
            RefreshSettingTexts();
        }
        if (GameUIManager.Instance.IsPanelOpen("MenuPanel"))
        {
            RefreshSettingTexts();
        }
    }

    // 설정 패널 텍스트 갱신
    private void RefreshSettingTexts()
    {
        
        GameUIManager.Instance.ShowLocalizedMessage("MenuText", "Menu");
        GameUIManager.Instance.ShowLocalizedMessage("GuideBtnText", "GuideBtn");
        GameUIManager.Instance.ShowLocalizedMessage("SettingBtnText", "SettingBtn");
        GameUIManager.Instance.ShowLocalizedMessage("ExitBtnText", "ExitBtn");

        //세팅 글자 변경
        GameUIManager.Instance.ShowLocalizedMessage("Txt_SettingTitle", "T_Settings");
        GameUIManager.Instance.ShowLocalizedMessage("S0_Language", "T_Language");
        GameUIManager.Instance.ShowLocalizedMessage("S1_Bgm", "T_BGM");
        GameUIManager.Instance.ShowLocalizedMessage("S2_Sfx", "T_SFX");
        GameUIManager.Instance.ShowLocalizedMessage("S3_Sensitivity", "T_Sensitivity");

        //버튼 가이드 글자 변경
        GameUIManager.Instance.ShowLocalizedMessage("MenuGuideText", "MG_Guide");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Move", "MG_Move");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Look", "MG_Mouse");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Run", "MG_Shift");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Jump", "MG_Space");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Interact", "MG_E");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Crouch", "MG_C");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Inventory", "MG_Tab");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Menu", "MG_Esc");
        GameUIManager.Instance.ShowLocalizedMessage("Tmp_Esc", "MG_CloseBtn");
    }
    
    private void OnDestroy()
    {
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
            eventBroker.Unsubscribe("OnDialogueStarted", OnDialogueStarted);
        }
    }
}