using UnityEngine;

/// <summary>
/// 현재 입력 모드를 변경하지 않고 패널 스택에 참여하는 HUD 오버레이용 패널 상태입니다.
/// </summary>
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