using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Timeline;
using System.Collections;

/// <summary>씬 내 타임라인 재생, 스킵, 카메라 블렌딩 및 페이드 효과를 통합 관리하는 매니저</summary>
public class TimeLineManager : MonoBehaviour
{
    public static TimeLineManager Instance; // 추가

    public enum FadeType
    {
        None,
        FadeIn,
        FadeOutIn
    }

    public enum InputModeOnComplete
    {
        Player,
        UI,
        DoNotChange,
    }

    [System.Serializable]
    public class TimelineData
    {
        public string timelineName;
        public PlayableDirector timeline;
        public CinemachineCamera subCamera;
        [HideInInspector] public int originalPriority = 5;

        [Header("카메라 블렌딩 설정")] public bool blendFromMainCamera = true;

        [Header("페이드 설정")] public FadeType fadeOnStart = FadeType.None;
        public float fadeAfterStartTime = 0f;
        public FadeType fadeOnEnd = FadeType.None;
        public float fadeBeforeEndTime = 0f;

        [Header("스킵 설정")] public bool singlePressSkip = false;

        [Header("종료 후 입력 모드")] public InputModeOnComplete inputModeOnComplete = InputModeOnComplete.Player;

        public UnityEvent onTimelineComplete = new UnityEvent();
    }

    [Header("카메라 블렌딩 설정")] public bool blendFromMainCamera = true;

    [Header("타임라인 목록")] public List<TimelineData> timelines = new List<TimelineData>();

    [Header("스킵 설정")] [SerializeField] private float skipInputDelay = 0.5f;

    [Header("Manager References")] [SerializeField, ReadOnly]
    private EventBroker eventBroker;

    [SerializeField, ReadOnly] private InputModeManager inputModeManager;

    private Dictionary<PlayableDirector, TimelineData> activeTimelines =
        new Dictionary<PlayableDirector, TimelineData>();

    private HashSet<PlayableDirector> boundDirectors = new HashSet<PlayableDirector>();

    private float lastEnterPressTime = -999f;
    private bool isTimelinePlaying = false;
    public bool IsTimelinePlaying => isTimelinePlaying;

    public static System.Action<string> OnAnyTimelineComplete;

    private bool wasQuestGuideVisible = false;
    
    private float _timelineEndCooldown = 0f;
    public bool IsTimelineTransitioning => _timelineEndCooldown > 0f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (eventBroker == null)
            eventBroker = EventBroker.Instance;
        
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }
        
        // 씬 시작 시 각 타임라인의 원본 Priority 저장
        foreach (var data in timelines)
        {
            if (data.subCamera != null)
            {
                data.originalPriority = data.subCamera.Priority;
            }
        }
    }
    
    private void Update()
    {
        if (_timelineEndCooldown > 0f)
            _timelineEndCooldown -= Time.deltaTime;
        
        if (isTimelinePlaying && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            // 현재 재생 중인 타임라인이 singlePressSkip인지 확인
            bool isSinglePressSkip = false;
            foreach (var kvp in activeTimelines)
            {
                if (kvp.Key != null && kvp.Key.state == PlayState.Playing && kvp.Value.singlePressSkip)
                {
                    isSinglePressSkip = true;
                    break;
                }
            }

            // single press skip이면 바로 스킵
            if (isSinglePressSkip)
            {
                SkipCurrentTimeline();
                lastEnterPressTime = -999f;
                return;
            }

#if UNITY_EDITOR
            bool allowSkip = true;
#else
            bool allowSkip = DevBuildMode.IsActive;
#endif
            if (allowSkip)
            {
                float currentTime = Time.time;

                if (currentTime - lastEnterPressTime <= skipInputDelay)
                {
                    SkipCurrentTimeline();
                    lastEnterPressTime = -999f;
                }
                else
                {
                    lastEnterPressTime = currentTime;
                    DebugLogger.Log("[TimeLineManager] 엔터키 1번 감지 (한 번 더 누르면 스킵)");
                }
            }
        }
    }

    public void PlayTimeline(string timelineName)
    {
        TimelineData data = GetTimelineByName(timelineName);

        
        if (inputModeManager != null)
        {
            inputModeManager.SwitchInputMode(InputMode.UI);
        }

        Cursor.lockState = CursorLockMode.Locked;

        if (data == null)
        {
            DebugLogger.LogError($"타임라인 '{timelineName}'을 찾을 수 없습니다!");
            return;
        }

        PlayTimeline(data);
    }

    public void PlayTimeline(TimelineData data)
    {
        if (data == null || data.timeline == null)
        {
            DebugLogger.LogError("TimelineData 또는 PlayableDirector가 null입니다!");
            return;
        }
        
        // 앵커 드랍 및 HUD 숨기기
        var lightAnchor = MagneticManager.Instance?.CurrentHoldingAnchor as LightAnchor;
        lightAnchor?.ForceDropOnDeath();
        var heavyAnchor = MagneticManager.Instance?.CurrentHoldingAnchor as HeavyAnchor;
        heavyAnchor?.ForceDetachOnDeath();
        GameUIManager.Instance?.HideHUDForTimeline();

        if (inputModeManager != null)
        {
            inputModeManager.SwitchInputMode(InputMode.UI);
        }

        wasQuestGuideVisible = GameUIManager.Instance.IsQuestGuideVisible();
        if (wasQuestGuideVisible)
        {
            GameUIManager.Instance.HideQuestGuide();
        }

        Cursor.lockState = CursorLockMode.Locked;

        // 바로 타임라인 재생
        StartTimelinePlayback(data);

        // 시작 페이드 처리
        if (data.fadeOnStart != FadeType.None)
        {
            if (data.fadeAfterStartTime > 0f)
            {
                StartCoroutine(FadeAfterStartCoroutine(data));
            }
            else
            {
                ExecuteFade(data.fadeOnStart);
            }
        }

        // 종료 전 페이드 처리
        if (data.fadeOnEnd != FadeType.None && data.fadeBeforeEndTime > 0f)
        {
            StartCoroutine(FadeBeforeEndCoroutine(data));
        }
    }

    /// <summary>
    /// UI 버튼에서 호출할 수 있는 타임라인 스킵 함수
    /// </summary>
    public void OnSkipButtonPressed()
    {
        if (!isTimelinePlaying)
        {
            DebugLogger.LogWarning("[TimeLineManager] 재생 중인 타임라인이 없습니다.");
            return;
        }

        // 현재 재생 중인 타임라인이 singlePressSkip인지 확인
        bool canSkip = false;
        foreach (var kvp in activeTimelines)
        {
            if (kvp.Key != null && kvp.Key.state == PlayState.Playing)
            {
                if (kvp.Value.singlePressSkip)
                {
                    canSkip = true;
                    break;
                }
            }
        }

        if (canSkip)
        {
            SkipCurrentTimeline();
            lastEnterPressTime = -999f;
            DebugLogger.Log("[TimeLineManager] 버튼으로 타임라인 스킵됨");
        }
        else
        {
            DebugLogger.LogWarning("[TimeLineManager] 현재 타임라인은 스킵이 비활성화되어 있습니다.");
        }
    }

    private void StartTimelinePlayback(TimelineData data)
    {
        if (!boundDirectors.Contains(data.timeline))
        {
            // MainCamera 태그로 명시적 지정
            CinemachineBrain brain = Camera.main?.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                var timelineAsset = data.timeline.playableAsset as TimelineAsset;
                foreach (var track in timelineAsset.GetOutputTracks())
                {
                    if (track is CinemachineTrack)
                    {
                        data.timeline.SetGenericBinding(track, brain);
                        break;
                    }
                }
            }

            boundDirectors.Add(data.timeline);
        }

        if (data.blendFromMainCamera && data.subCamera != null)
        {
            // data.originalPriority = data.subCamera.Priority;
            data.subCamera.Priority = 20;
        }

        data.timeline.Play();
        DebugLogger.Log($"Timeline '{data.timelineName}'을 실행합니다.");
        isTimelinePlaying = true;

        if (!activeTimelines.ContainsKey(data.timeline))
        {
            activeTimelines.Add(data.timeline, data);
            data.timeline.stopped += OnTimelineStopped;
        }
    }

    private IEnumerator FadeAfterStartCoroutine(TimelineData data)
    {
        yield return new WaitForSeconds(data.fadeAfterStartTime);
        ExecuteFade(data.fadeOnStart);
    }

    private IEnumerator FadeBeforeEndCoroutine(TimelineData data)
    {
        float waitTime = (float)data.timeline.duration - data.fadeBeforeEndTime;
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        ExecuteFade(data.fadeOnEnd);
    }

    private void ExecuteFade(FadeType fadeType)
    {
        switch (fadeType)
        {
            case FadeType.FadeIn:
                UIAnimationManager.Instance?.SetBlack();
                UIAnimationManager.Instance?.FadeIn();
                break;

            case FadeType.FadeOutIn:
                UIAnimationManager.Instance?.FadeOutIn();
                break;
        }
    }

    private void SkipCurrentTimeline()
    {
        if (activeTimelines.Count == 0)
        {
            DebugLogger.LogWarning("[TimeLineManager] 스킵할 타임라인이 없습니다.");
            return;
        }

        foreach (var kvp in activeTimelines)
        {
            PlayableDirector director = kvp.Key;
            if (director != null && director.state == PlayState.Playing)
            {
                director.time = director.duration;
                director.Evaluate();

                DebugLogger.Log($"[TimeLineManager] 타임라인 '{kvp.Value.timelineName}' 스킵됨!");
            }
        }
    }

    private TimelineData GetTimelineByName(string name)
    {
        foreach (TimelineData data in timelines)
        {
            if (data.timelineName == name)
            {
                return data;
            }
        }

        return null;
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        isTimelinePlaying = false;

        if (activeTimelines.TryGetValue(director, out TimelineData data))
        {
            // Priority 복구를 graph 파괴보다 먼저
            if (data.subCamera != null)
            {
                data.subCamera.Priority = data.originalPriority;
            }

            if (director.playableGraph.IsValid())
            {
                director.playableGraph.Destroy(); // 복구 후 파괴
            }

            DebugLogger.Log($"타임라인 '{data.timelineName}' 종료됨!");

            activeTimelines.Remove(director);
            director.stopped -= OnTimelineStopped;

            // fadeBeforeEndTime이 0이면 종료 후 페이드 실행
            if (data.fadeBeforeEndTime <= 0f && data.fadeOnEnd != FadeType.None)
            {
                switch (data.fadeOnEnd)
                {
                    case FadeType.FadeIn:
                        UIAnimationManager.Instance?.SetBlack();
                        UIAnimationManager.Instance?.FadeIn(() => OnTimelineFullyComplete(data));
                        return;

                    case FadeType.FadeOutIn:
                        UIAnimationManager.Instance?.FadeOutIn(null, () => OnTimelineFullyComplete(data));
                        return;
                }
            }

            OnTimelineFullyComplete(data);
        }
    }

    private void OnTimelineFullyComplete(TimelineData data)
    {
        _timelineEndCooldown = 3f;
            
        // HUD 복구
        GameUIManager.Instance?.RestoreHUDAfterTimeline();
        
        if (inputModeManager != null && data.inputModeOnComplete != InputModeOnComplete.DoNotChange)
        {
            InputMode targetMode = data.inputModeOnComplete == InputModeOnComplete.UI ? InputMode.UI : InputMode.Player;
            inputModeManager.SwitchInputMode(targetMode);
        }

        if (wasQuestGuideVisible)
        {
            GameUIManager.Instance.ShowQuestGuide();
        }

        wasQuestGuideVisible = false;

        data.onTimelineComplete?.Invoke();

        if (eventBroker != null)
        {
            eventBroker.Publish("OnTimelineComplete", data.timelineName);
        }
    }

    private void OnPlayerSpawned(object data)
    {
        GameObject player = data as GameObject;
        if (player != null)
        {
            inputModeManager = player.GetComponentInChildren<InputModeManager>(true);
            if (inputModeManager == null)
            {
                DebugLogger.Log("플레이어에서 찾을 수 없습니다.");
            }
        }
    }

    private void OnDestroy()
    {
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
        }

        foreach (var kvp in activeTimelines)
        {
            if (kvp.Key != null)
            {
                if (kvp.Key.playableGraph.IsValid())
                {
                    kvp.Key.playableGraph.Destroy();
                }

                kvp.Key.stopped -= OnTimelineStopped;
            }
        }

        activeTimelines.Clear();
    }

    public void CleanupAllTimelines()
    {
        foreach (var data in timelines)
        {
            if (data.timeline != null && data.timeline.playableGraph.IsValid())
            {
                data.timeline.playableGraph.Destroy();
                data.timeline.Stop();
            }
        }

        foreach (var kvp in activeTimelines)
        {
            if (kvp.Key != null && kvp.Key.playableGraph.IsValid())
            {
                kvp.Key.playableGraph.Destroy();
            }
        }
    }
}