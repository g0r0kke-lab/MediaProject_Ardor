using UnityEngine;

[System.Serializable]
/// <summary>
/// Yarn 대화 노드 이름을 상태 진행 여부 및 UI 모드 유지 여부 플래그와 매핑하는 직렬화 가능 설정 항목입니다.
/// </summary>
public class DialogueNodeConfig
{
    [Tooltip("대화 노드 이름 (예: DLG1_01_ko)")]
    public string nodeName;
    
    [Tooltip("대화 완료 시 게임 상태를 진행할지 여부")]
    public bool progressState;
    
    [Tooltip("대화 완료 후 UI 모드를 유지할지 여부")]
    public bool keepUIMode;
}