using UnityEngine;

/// <summary>
/// GameUIManager 패널 시스템 내에서 열기/닫기 동작을 처리하는 패널 상태 객체의 계약을 정의하는 인터페이스입니다.
/// </summary>
public interface IPanelState
{
    void OnOpen(GameUIManager manager, string panelName);
    void OnClose(GameUIManager manager, string panelName);
    bool ShouldManageStack { get; }
    bool ShouldHideQuestGuide { get; }
}