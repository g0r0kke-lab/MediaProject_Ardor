using UnityEngine;
using Yarn.Unity;
using TMPro;

/// <summary>Yarn Spinner 대화 시스템의 커맨드 핸들러 및 대화 이벤트를 처리하는 컴포넌트</summary>
public class DialogueCommands : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private LineAdvancer lineAdvancer;

    private string currentCharacter = "";
    private bool isDialogueActive = false;
    private System.Action _stopLineAudioCallback;
    
    [Header("Audio Timing")]
    [Tooltip("오디오 끝부분에서 제외할 시간 (초)")]
    [SerializeField] private float audioTrimEnd = 2f;

    void Start()
    {
        dialogueRunner.AddCommandHandler<string>("character", SetCharacter);
        dialogueRunner.AddCommandHandler<string>("ShowEmotion", ShowEmotion);
        dialogueRunner.AddCommandHandler<string>("ShowAction", ShowAction);
        dialogueRunner.AddCommandHandler<int>("PlaySFX", PlaySFX);
        dialogueRunner.AddCommandHandler<int>("ShowMemoryImage", ShowMemoryImage);

        if (lineAdvancer != null)
        {
            _stopLineAudioCallback = () => SoundManager.Instance.StopSFX();
            lineAdvancer.onStopLineAudio += _stopLineAudioCallback;
        }

        // 추가: 대화 시작/종료 이벤트 구독
        dialogueRunner.onDialogueStart.AddListener(OnDialogueStart);
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }

    void SetCharacter(string characterName)
    {
        currentCharacter = characterName;
        nameText.text = characterName;

        // 캐릭터별 색상 변경도 여기서
        switch (characterName)
        {
            case "soma":
                nameText.color = Color.cyan;
                break;
            case "robot":
                nameText.color = Color.yellow;
                break;
            case "Family":
                nameText.color = Color.yellow;
                break;
            case "close":
                if (InputModeManager.Instance != null)
                    InputModeManager.Instance.SwitchInputMode(InputMode.Player);
                GameManagerRegistry.ProgressState();
                break;
        }
    }

    public void PlaySFX(int idx)
    {
        var clipData = SoundManager.Instance.GetSFXClipData(idx);
        if (clipData != null && clipData.clip != null)
        {
            SoundManager.Instance.PlaySFXExclusive(clipData.clip.name);

            if (lineAdvancer != null && lineAdvancer.waitForLineAudio)
            {
                lineAdvancer.OnLineAudioPlayed(clipData.clip);
            }
        }
    }

    public void ShowEmotion(string emotionName)
    {
        switch (emotionName)
        {
            case "question":
                DebugLogger.Log("question");
                break;
            case "agree":
                DebugLogger.Log("agree");
                break;
        }
    }
    
    public void ShowMemoryImage(int imageIndex)
    {
        GuideUIManager.Instance.OpenMemoryImage(imageIndex);
    }

    public void ShowAction(string actionName)
    {
        switch (actionName)
        {
            case "glitch":
                GameManagerRegistry.PlayInterferences(0.3f);
                break;
        }
    }

    private void OnDialogueStart()
    {
        isDialogueActive = true;
        DebugLogger.Log("대화 시작 - ESC 차단 활성화");
    }

    private void OnDialogueComplete()
    {
        isDialogueActive = false;
        DebugLogger.Log("대화 종료 - ESC 차단 해제");
    }

    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueStart.RemoveListener(OnDialogueStart);
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }

        if (lineAdvancer != null && _stopLineAudioCallback != null)
            lineAdvancer.onStopLineAudio -= _stopLineAudioCallback;
    }
}