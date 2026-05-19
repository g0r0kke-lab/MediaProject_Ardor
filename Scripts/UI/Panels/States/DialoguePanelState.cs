using UnityEngine;

public class DialoguePanelState : IPanelState
{
    public bool ShouldManageStack => false;
    public bool ShouldHideQuestGuide => true;

    public void OnOpen(GameUIManager manager, string panelName)
    {
        manager.HandleQuestGuideHiding("Dialogue", true);
        manager.SwitchToUIMode();
    }

    public void OnClose(GameUIManager manager, string panelName)
    {
        manager.HandleQuestGuideHiding("Dialogue", false);
    }
}