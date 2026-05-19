using UnityEngine;
using Yarn.Unity;

#if UNITY_EDITOR
public class EditorDialogueSkip : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame)
        {
            if (dialogueRunner.IsDialogueRunning)
            {
                dialogueRunner.Stop();
            }
        }
    }
}
#endif