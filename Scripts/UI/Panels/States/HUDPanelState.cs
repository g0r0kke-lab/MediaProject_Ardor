using UnityEngine;

public class HUDPanelState : IPanelState
{
    public bool ShouldManageStack => true;
    public bool ShouldHideQuestGuide => false;

    public void OnOpen(GameUIManager manager, string panelName)
    {
        // HUD는 InputMode 변경 없음
    }

    public void OnClose(GameUIManager manager, string panelName)
    {
        // HUD는 InputMode 변경 없음
    }
}