using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GuideEntry
{
    public string guideId;
    public GameObject guideObject; // 씬 내 오브젝트 직접 참조
    public bool progressStateOnClose; // 닫을 때 상태 진행 여부
}

public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance;

    [SerializeField] private List<GuideEntry> guideEntries = new List<GuideEntry>();
    private GuideEntry _currentEntry = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 모두 비활성화 (초기 상태 보장)
        foreach (var entry in guideEntries)
            entry.guideObject?.SetActive(false);
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

        target.guideObject.GetComponent<PanelManager>()?.ShowPanel(0);

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