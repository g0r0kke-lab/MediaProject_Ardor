using UnityEngine;

/// <summary>
/// 패널이 열릴 때 UI 입력 모드로 전환하고 퀘스트 가이드를 숨기며, 닫힐 때 닫기 효과음을 재생하는 패널 상태입니다.
/// </summary>
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