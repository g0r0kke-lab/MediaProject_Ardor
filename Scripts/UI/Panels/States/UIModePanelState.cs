using UnityEngine;

public class UIModePanelState : IPanelState
{
    public bool ShouldManageStack => true;
    public bool ShouldHideQuestGuide => true;

    public void OnOpen(GameUIManager manager, string panelName)
    {
        manager.HandleQuestGuideHiding(panelName, true);
        manager.SwitchToUIMode();
    }

    public void OnClose(GameUIManager manager, string panelName)
    {
        manager.HandleQuestGuideHiding(panelName, false);
        manager.PlayCloseSFX();
    }
}