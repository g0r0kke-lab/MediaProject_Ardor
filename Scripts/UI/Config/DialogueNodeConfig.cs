using UnityEngine;

[System.Serializable]
public class DialogueNodeConfig
{
    [Tooltip("대화 노드 이름 (예: DLG1_01_ko)")]
    public string nodeName;
    
    [Tooltip("대화 완료 시 게임 상태를 진행할지 여부")]
    public bool progressState;
    
    [Tooltip("대화 완료 후 UI 모드를 유지할지 여부")]
    public bool keepUIMode;
}