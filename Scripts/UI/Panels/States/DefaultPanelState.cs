using UnityEngine;

public class DefaultPanelState : IPanelState
{
    public bool ShouldManageStack => true;
    public bool ShouldHideQuestGuide => false;

    public void OnOpen(GameUIManager manager, string panelName)
    {
        // 기본 동작 없음
    }

    public void OnClose(GameUIManager manager, string panelName)
    {
        // 기본 동작 없음
    }
}