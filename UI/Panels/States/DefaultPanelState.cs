using UnityEngine;

/// <summary>
/// 패널 스택에 참여하지만 열기/닫기 시 입력 모드를 변경하지 않는 기본 폴백 패널 상태입니다.
/// </summary>
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