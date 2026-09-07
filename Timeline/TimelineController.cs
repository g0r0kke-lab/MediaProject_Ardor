using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>개별 타임라인의 재생, 일시정지, 재개를 제어하는 컨트롤러</summary>
public class TimelineController : MonoBehaviour
{
    public static TimelineController Instance;

    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private GameObject bottomText;
    [SerializeField] private GameObject detailText;
    [SerializeField] private Button resumeButton;
    private bool isPaused = false;
    private bool waitingInput = false;
    [Header("대화 설정")]
    public string dialogueNodeName;

    private InputAction _interactAction;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnDestroy()
    {
        EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        if (_interactAction != null)
            _interactAction.performed -= OnEKeyPressed;
    }

    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player == null) return;

        var playerInput = player.GetComponentInChildren<PlayerInput>(true);
        if (playerInput != null)
        {
            _interactAction = playerInput.actions.FindAction("UI/Interact");
            if (_interactAction != null)
            {
                _interactAction.performed += OnEKeyPressed;
                _interactAction.Enable();
            }
        }
    }

    void OnEnable()  => _interactAction?.Enable();
    void OnDisable() => _interactAction?.Disable();

    void Start()
    {
        if (playableDirector == null)
        {
            playableDirector = GetComponent<PlayableDirector>();
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(() => {
                SoundManager.Instance.PlaySFX(10);
                Resume();
            });
        }
        
        EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
    }

    public void Play()
    {
        if (playableDirector != null)
        {
            playableDirector.Play();
            isPaused = false;
        }
    }

    private void OnEKeyPressed(InputAction.CallbackContext context)
    {
        if (waitingInput)
        {
            // 중간 단계 없이 바로 Resume
            SoundManager.Instance.PlaySFX(10);
            Resume();
        }
    }


    // 멈추고 글자 보여주기
    public void PauseAndShow()
    {
        Pause();
        if (bottomText) bottomText.SetActive(true);
        if (detailText) detailText.SetActive(true);
        if (resumeButton) resumeButton.gameObject.SetActive(true);
        waitingInput = true;
    }

    // TimelineController.cs에만 추가
    public void PauseAndDialogue()
    {
        Pause();
        GameUIManager.Instance.OpenDialogue(dialogueNodeName);
    }

    //타임라인 멈추기
    public void Pause()
    {
        if (playableDirector != null)
        {
            playableDirector.Pause();
            isPaused = true;
        }
    }

    public void Resume()
    {
        if (playableDirector != null && isPaused)
        {
            // GameUIManager.Instance.ShowLocalizedMessage("Object Info Text 1", "???");
            // GameUIManager.Instance.ShowLocalizedMessage("Object Info Text 2", "???");
            // GameUIManager.Instance.ShowLocalizedMessage("Object Info Text 3", "???");
            playableDirector.Resume();
            if (bottomText) bottomText.SetActive(false);
            if (detailText) detailText.SetActive(false);
            if (resumeButton) resumeButton.gameObject.SetActive(false);
            isPaused = false;
            waitingInput = false;
        }
    }

    public void Stop()
    {
        if (playableDirector != null)
        {
            playableDirector.Stop();
            isPaused = false;
        }
    }

    public bool IsPaused()
    {
        return isPaused;
    }
}