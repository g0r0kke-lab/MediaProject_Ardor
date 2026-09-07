using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>퀘스트 패널 UI를 열고 닫으며 아이템 정보를 표시하는 매니저</summary>
public class QuestUIManager : MonoBehaviour
{
    public static QuestUIManager Instance { get; private set; }

    [Header("QuestUI Text 관리")]
    public string itemName = "itemName";
    public string itemDescription = "itemDescription";
    public string topText = "topText";
    public string closeText = "closeText";
    
    [Header("QuestUI Localization 관리")]
    public string topTextKey;
    private int currentParts;
    public string closeTextKey = "default_message";

    [Header("QuestUI Image 관리")]
    public Image itemImage;

    [Header("아이템 데이터 관리")]
    public ItemData[] allItems;

    private InputAction _interactAction;
    private Dictionary<string, ItemData> itemDataDict = new Dictionary<string, ItemData>();
    private Coroutine _waitForCloseCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // ItemData 딕셔너리 초기화
        if (allItems != null)
        {
            foreach (var item in allItems)
            {
                if (item != null)
                {
                    itemDataDict[item.name] = item;
                }
            }
            DebugLogger.Log($"[QuestUI] {itemDataDict.Count}개의 ItemData 등록됨");
        }
        else
        {
            DebugLogger.LogWarning("[QuestUI] allItems 배열이 null입니다!");
        }
    }

    void Start()
    {
        if (itemImage == null)
            DebugLogger.LogError("[QuestUI] itemImage가 Inspector에 할당되지 않았습니다! QuestUIManager의 Item Image 슬롯을 확인하세요.");

        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    // string 파라미터를 받는 오버로드 메서드
    public void OpenQuestPanel(string partName)
    {
        DebugLogger.Log($"[QuestUI] OpenQuestPanel(string) 호출: {partName}");
        
        if (itemDataDict.TryGetValue(partName, out ItemData itemData))
        {
            DebugLogger.Log($"[QuestUI] ItemData 찾음: {itemData.name}");
            OpenQuestPanel(itemData);
        }
        else
        {
            DebugLogger.LogError($"[QuestUI] ItemData를 찾을 수 없습니다: {partName}");
            DebugLogger.LogError($"[QuestUI] 등록된 키 목록: {string.Join(", ", itemDataDict.Keys)}");
        }
    }

    // ItemData를 직접 받는 메서드
    public void OpenQuestPanel(ItemData itemData)
    {
        DebugLogger.Log($"[QuestUI] OpenQuestPanel(ItemData) 호출");
        
        // itemData null 체크
        if (itemData == null)
        {
            DebugLogger.LogError("[QuestUI] itemData가 null입니다!");
            return;
        }

        // itemImage null 체크
        if (itemImage == null)
        {
            DebugLogger.LogError("[QuestUI] itemImage가 Inspector에 할당되지 않았습니다!");
            DebugLogger.LogError("[QuestUI] Hierarchy에서 QuestPanel → ItemImage 오브젝트를 찾아 Inspector의 Item Image 슬롯에 할당하세요!");
        }
        else
        {
            // 이미지 설정
            if (itemData.thumbnail != null)
            {
                itemImage.sprite = itemData.thumbnail;
                DebugLogger.Log($"[QuestUI] 이미지 설정 완료: {itemData.name}");
            }
            else
            {
                DebugLogger.LogWarning($"[QuestUI] {itemData.name}의 thumbnail이 없습니다!");
            }
        }
        
        // GameUIManager null 체크
        if (GameUIManager.Instance == null)
        {
            DebugLogger.LogError("[QuestUI] GameUIManager.Instance가 null입니다!");
            return;
        }

        // UI 패널 표시
        GameUIManager.Instance.ShowPanel("QuestPanel", true);
        
        // 번역 키 null 체크
        if (itemData.itemNameKey == null)
        {
            DebugLogger.LogError($"[QuestUI] {itemData.name}의 itemNameKey가 null입니다!");
            return;
        }
        if (itemData.itemDescriptionKey == null)
        {
            DebugLogger.LogError($"[QuestUI] {itemData.name}의 itemDescriptionKey가 null입니다!");
            return;
        }

        // 번역된 값을 가져와서 직접 ShowMessage로 설정
        string translatedName = itemData.itemNameKey.GetLocalizedString();
        string translatedDesc = itemData.itemDescriptionKey.GetLocalizedString();
    
        GameUIManager.Instance.ShowMessage(itemName, translatedName);
        GameUIManager.Instance.ShowMessage(itemDescription, translatedDesc);
        GameUIManager.Instance.ShowLocalizedMessage(topText, topTextKey);
        GameUIManager.Instance.ShowLocalizedMessage(closeText, closeTextKey);
        
        DebugLogger.Log("[QuestUI] QuestPanel 열기 완료");

        // E키 대기 코루틴 시작
        if (_waitForCloseCoroutine != null)
            StopCoroutine(_waitForCloseCoroutine);
        _waitForCloseCoroutine = StartCoroutine(WaitForCloseInput());
    }

    private IEnumerator WaitForCloseInput()
    {
        // 열린 직후 입력 무시 (같은 프레임 방지)
        yield return null;

        // UI 모드에서는 Player 액션맵이 꺼지므로 이 액션만 강제 활성화
        _interactAction?.Enable();

        while (GameUIManager.Instance != null && GameUIManager.Instance.IsPanelOpen("QuestPanel"))
        {
            if (_interactAction != null && _interactAction.WasPressedThisFrame())
            {
                InputModeManager.Instance?.PlayDeactivate();
                CloseQuestPanel();
                yield break;
            }
            yield return null;
        }
    }

    public void CloseQuestPanel()
    {
        if (_waitForCloseCoroutine != null)
        {
            StopCoroutine(_waitForCloseCoroutine);
            _waitForCloseCoroutine = null;
        }
        
        if (GameUIManager.Instance != null)
            GameUIManager.Instance.ShowPanel("QuestPanel", false);
        
        if (InputModeManager.Instance != null)
            InputModeManager.Instance.SwitchInputMode(InputMode.Player);
        
        DebugLogger.Log("[QuestUI] QuestPanel 닫힘");
    }

    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player == null) return;

        var playerInput = player.GetComponentInChildren<PlayerInput>(true);
        if (playerInput != null)
        {
            _interactAction = playerInput.actions.FindAction("Player/Interact");
            _interactAction?.Enable();
        }
    }

    void OnDestroy()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);

        if (_waitForCloseCoroutine != null)
        {
            StopCoroutine(_waitForCloseCoroutine);
            _waitForCloseCoroutine = null;
        }
    }
}