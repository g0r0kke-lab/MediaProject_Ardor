using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[System.Serializable]
public class GuideEntry
{
    public string guideId;
    public GameObject guideObject; // 씬 내 오브젝트 직접 참조
    public bool progressStateOnClose; // 닫을 때 상태 진행 여부
}

/// <summary>
/// ID로 가이드 패널을 열고 닫는 싱글톤으로, 가이드가 닫힐 때 선택적으로 게임 상태를 진행시킵니다.
/// </summary>
public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance;

    [SerializeField] private List<GuideEntry> guideEntries = new List<GuideEntry>();
    private GuideEntry _currentEntry = null;

    private InputAction _panelLeftAction;
    private InputAction _panelRightAction;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 모두 비활성화 (초기 상태 보장)
        foreach (var entry in guideEntries)
            entry.guideObject?.SetActive(false);
    }

    private void Start()
    {
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    private void OnDestroy()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    private void OnPlayerSpawned(object data)
    {
        var player = data as GameObject;
        if (player == null) return;

        var playerInput = player.GetComponentInChildren<PlayerInput>(true);
        if (playerInput != null)
        {
            _panelLeftAction  = playerInput.actions.FindAction("UI/PanelLeft");
            _panelRightAction = playerInput.actions.FindAction("UI/PanelRight");
        }
    }

    public void OpenGuide(string guideId)
    {
        _currentEntry?.guideObject.SetActive(false);

        // 리스트에서 찾기
        GuideEntry target = guideEntries.Find(e => e.guideId == guideId);

        if (target == null || target.guideObject == null)
        {
            DebugLogger.LogWarning($"[GuideManager] 가이드 '{guideId}'를 찾을 수 없음!");
            return;
        }

        _currentEntry = target;
        target.guideObject.SetActive(true);

        var panelManager = target.guideObject.GetComponent<PanelManager>();
        panelManager?.SetPanelActions(_panelLeftAction, _panelRightAction);
        panelManager?.ShowPanel(0);

        if (GameUIManager.Instance) GameUIManager.Instance.ShowPanel("GuidePanel", true);
        if (SoundManager.Instance) SoundManager.Instance.PlaySFX(47);
    }

    public void CloseGuide()
    {
        // 상태 진행 여부를 GuideEntry가 결정
        if (_currentEntry != null && _currentEntry.progressStateOnClose)
            GameManagerRegistry.ProgressState();

        _currentEntry?.guideObject.SetActive(false);
        _currentEntry = null;
        GameUIManager.Instance.ShowPanel("GuidePanel", false);
    }
}