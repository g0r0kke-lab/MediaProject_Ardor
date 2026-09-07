using UnityEngine;

/// <summary>
/// 패널이 열릴 때 입력 모드를 플레이어 모드로 전환하는 패널 상태로, 인게임 충돌 피드백 패널에 사용됩니다.
/// </summary>
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