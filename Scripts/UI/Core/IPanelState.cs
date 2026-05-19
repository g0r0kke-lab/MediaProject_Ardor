using UnityEngine;

public interface IPanelState
{
    void OnOpen(GameUIManager manager, string panelName);
    void OnClose(GameUIManager manager, string panelName);
    bool ShouldManageStack { get; }
    bool ShouldHideQuestGuide { get; }
}