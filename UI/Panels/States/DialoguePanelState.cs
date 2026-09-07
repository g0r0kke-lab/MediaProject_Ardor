using UnityEngine;

/// <summary>
/// 대화가 열릴 때 퀘스트 가이드 HUD를 숨기고 입력 모드를 UI로 전환하며, 닫힐 때 복원하는 대화용 패널 상태입니다.
/// </summary>
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