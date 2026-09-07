using UnityEngine;

/// <summary>
/// O 키를 누르면 현재 실행 중인 Yarn 대화를 중단하며, 에디터 또는 DevBuildMode가 활성화된 경우에만 동작합니다.
/// </summary>
public class EditorDialogueSkip : MonoBehaviour
{
    [SerializeField] private Yarn.Unity.DialogueRunner dialogueRunner;

    void Update()
    {
#if !UNITY_EDITOR
        if (!DevBuildMode.IsActive) return;
#endif
        if (UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame)
        {
            if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
                dialogueRunner.Stop();
        }
    }
}
