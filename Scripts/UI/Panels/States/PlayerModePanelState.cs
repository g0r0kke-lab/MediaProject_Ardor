using UnityEngine;

public class PlayerModePanelState : IPanelState
{
    public bool ShouldManageStack => true;
    public bool ShouldHideQuestGuide => false;

    public void OnOpen(GameUIManager manager, string panelName)
    {
        manager.SwitchToPlayerMode();
    }

    public void OnClose(GameUIManager manager, string panelName)
    {
        // 닫을 때는 스택 확인 후 모드 전환
    }
}