using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using FronkonGames.Glitches.Hacked;
using FronkonGames.Glitches.Interferences;

public abstract class GameManagerBass<TState> : MonoBehaviour where TState : System.Enum
{
    protected static GameManagerBass<TState> CurrentInstance;
    
    [Header("Scene Configuration")] [SerializeField]
    protected List<string> sceneNames = new List<string>();

    [SerializeField] protected int currentMapIndex = 0; // 현재 맵 인덱스

    [Header("Game State")] 
    [SerializeField] protected TState currentState;
    
    [Header("Input System")]
    [SerializeField] protected InputActionAsset inputActions;
    protected InputAction PlayerInteractAction;
    protected InputAction UIInteractAction;
    
    [Header("Player Spawn System")] [SerializeField]
    protected GameObject playerPrefab;

    [SerializeField] protected List<SavePoint> savePoints = new List<SavePoint>();
    [SerializeField] public List<Transform> teleportTransforms = new List<Transform>();
    protected GameObject CurrentPlayer;

    [Header("Manager References")]
    [SerializeField, ReadOnly] protected TimeLineManager timelineManager;
    [SerializeField, ReadOnly] protected EventBroker eventBroker;
    [SerializeField, ReadOnly] protected LoadingManager loadingManager;
    [SerializeField, ReadOnly] protected SaveDataManager saveDataManager;
    [SerializeField, ReadOnly] protected SoundManager soundManager;
    
    [Header("Timeline Events")]
    [SerializeField] protected List<TimelineEventHandler> timelineHandlers = new List<TimelineEventHandler>();
    
    [System.Serializable]
    public class TimelineEventHandler
    {
        // TimeLineManager의 timelines 리스트 인덱스
        public int timelineIndex;
        public UnityEvent onComplete;
    }
    
    // 런타임용 Dictionary
    protected Dictionary<string, UnityEvent> TimelineEventMap = new Dictionary<string, UnityEvent>();
    
    [Header("Events")]
    public UnityEvent OnSceneStart;
    public UnityEvent OnSceneComplete;
    
    [Header("Debug")]
    [SerializeField] protected bool enableDebugSceneTransition = true;

    protected Dictionary<string, bool> CompletedEvents = new Dictionary<string, bool>();

    [Header("Glitch Effects")]
    private Hacked hacked;
    private Interferences interferences;
    
    protected virtual void Awake()
    {
        CurrentInstance = this;
        GameManagerRegistry.Register(this);
        hacked = Hacked.Instance;
        if (hacked != null) hacked.SetActive(false);
        interferences = Interferences.Instance;
        if (interferences != null) interferences.SetActive(false);
#if UNITY_EDITOR
        // 에디터에서는 무제한으로 테스트 가능
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
#endif
    }
    
    protected virtual void Start()
    {
        CurrentInstance = this;
        GameManagerRegistry.Register(this);
        InitializeManagerReferences();
        
        // SaveDataManager가 준비될 때까지 대기
        StartCoroutine(InitializeWithSaveData());
    }
    
    // protected virtual void Update()
    // {
    //     // 디버그: X키로 다음 씬 전환
    //     if (enableDebugSceneTransition && Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
    //     {
    //         DebugTransitionToNextScene();
    //     }
    // }
    
    private void DebugTransitionToNextScene()
    {
        int nextSceneIndex = currentMapIndex + 1;
        
        if (nextSceneIndex >= sceneNames.Count)
        {
            DebugLogger.LogWarning($"[디버그] 다음 씬이 없습니다. 현재 맵: {currentMapIndex}, 전체 씬 수: {sceneNames.Count}");
            return;
        }
        
        DebugLogger.Log($"[디버그] X키로 다음 씬 전환: {sceneNames[currentMapIndex]} → {sceneNames[nextSceneIndex]}");
        TransitionToScene(nextSceneIndex);
    }

    protected virtual void InitializeManagerReferences()
    {
        // EventBroker 참조 설정
        if (eventBroker == null)
            eventBroker = EventBroker.Instance;
    
        // LoadingManager 참조 설정
        if (loadingManager == null)
            loadingManager = LoadingManager.Instance;
    
        // SaveDataManager 참조 설정
        if (saveDataManager == null)
            saveDataManager = SaveDataManager.Instance;
        
        if (soundManager == null)
            soundManager = SoundManager.Instance;
        
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnProgressState", OnProgressState);
        }
    }
    
    // 세이브 데이터 로드 후 초기화
    protected virtual IEnumerator InitializeWithSaveData()
    {
        yield return new WaitUntil(() => SaveDataManager.Instance != null);
        yield return new WaitForEndOfFrame();

        // 로딩 화면 표시
        if (loadingManager != null)
        {
            loadingManager.StartFadeOut();
            // 페이드 완료까지 대기
            yield return new WaitUntil(() => !loadingManager.IsFading && !loadingManager.IsBGMFading);
        }

        // Child 씬 로드 대기
        yield return new WaitUntil(() => !RuntimeChildSceneLoader.IsLoading);
        DebugLogger.Log("Child 씬 로드 완료, 플레이어 스폰 준비");
    
        // 타임라인 이벤트 맵 초기화
        InitializeTimelineEvents();
        
        int savedMapIndex = saveDataManager.GetCurrentMapIndex();
        bool isComingFromIntro = saveDataManager.IsComingFromIntro();

        if (isComingFromIntro)
        {
            DebugLogger.Log($"[인트로에서 진입] 저장된 맵 인덱스 {savedMapIndex}, 저장된 세이브포인트에 스폰");
            saveDataManager.SetComingFromIntro(false);
        
            if (savedMapIndex != currentMapIndex)
            {
                DebugLogger.LogError($"인트로에서 잘못된 씬으로 전환됨! 저장된 맵={savedMapIndex}, 현재 맵={currentMapIndex}");
            }
        }
        else if (savedMapIndex == currentMapIndex)
        {
            DebugLogger.Log($"[맵 인덱스 일치] 맵 {currentMapIndex}, 저장된 세이브포인트에 스폰");
        }
        else
        {
            DebugLogger.LogWarning($"[에디터 테스트] 맵 인덱스 불일치: 저장된 맵={savedMapIndex}, 현재 맵={currentMapIndex}");
            DebugLogger.Log($"현재 맵({currentMapIndex})으로 진행도 초기화, 세이브포인트 0에 스폰");
            saveDataManager.ForceSetProgress(currentMapIndex, 0);
        }

        // Initialize()를 먼저 호출 (EventBroker 구독 등)
        Initialize();
        OnSceneStart?.Invoke();
    
        saveDataManager?.ClearInventoryExceptMap(currentMapIndex);
        
        LoadState();
        
        // 그 다음에 플레이어 스폰
        SpawnPlayerAtSavePoint();
    
        FadeInOnSceneStart();
        
        // LoadingManager에게 알림
        if (loadingManager != null)
        {
            loadingManager.NotifyGameFullyLoaded();
        }
    }
    
    // 타임라인 이벤트 시스템 초기화
    protected virtual void InitializeTimelineEvents()
    {
        if (timelineManager == null)
        {
            timelineManager = FindFirstObjectByType<TimeLineManager>();
            if (timelineManager == null)
            {
                DebugLogger.LogError("[GameManager] TimeLineManager를 찾을 수 없습니다!");
                return;
            }
        }
    
        TimelineEventMap.Clear();
        foreach (var handler in timelineHandlers)
        {
            // TimeLineManager의 timelines에서 실제 TimelineData 가져오기
            if (handler.timelineIndex >= 0 && handler.timelineIndex < timelineManager.timelines.Count)
            {
                var timelineData = timelineManager.timelines[handler.timelineIndex];
                TimelineEventMap[timelineData.timelineName] = handler.onComplete;
                DebugLogger.Log($"[Timeline] [{handler.timelineIndex}] '{timelineData.timelineName}' 이벤트 핸들러 등록됨");
            }
            else
            {
                DebugLogger.LogError($"[Timeline] 유효하지 않은 타임라인 인덱스: {handler.timelineIndex}");
            }
        }
    
        if (eventBroker != null)
        {
            eventBroker.Subscribe("OnTimelineComplete", OnTimelineComplete);
        }
    }
    
    // 상태 로드 (추상 메서드)
    protected abstract void LoadState();
    
    // 상태 설정
    public virtual void SetState(TState newState)
    {
        if (currentState.Equals(newState)) return;

        DebugLogger.Log($"[{GetType().Name}] State: {currentState} → {newState}");
        currentState = newState;
        HandleStateEnter(newState);
    }
    
    // 다음 상태로 진행
    public virtual void ProgressToNext()
    {
        int nextValue = System.Convert.ToInt32(currentState) + 1;
        var values = System.Enum.GetValues(typeof(TState));
        
        if (nextValue < values.Length)
        {
            TState nextState = (TState)values.GetValue(nextValue);
            SetState(nextState);
        }
        else
        {
            DebugLogger.LogWarning($"[{GetType().Name}] 더 이상 진행할 상태가 없습니다.");
        }
    }
    
    // 상태 진입 처리 (추상 메서드)
    protected abstract void HandleStateEnter(TState state);
    
    // 현재 상태 가져오기
    public TState GetCurrentState() => currentState;
    
    // 타임라인 완료 통합 핸들러
    protected virtual void OnTimelineComplete(object data)
    {
        string timelineName = data as string;
    
        if (string.IsNullOrEmpty(timelineName))
            return;
    
        // DebugLogger.Log($"[{GetType().Name}] 타임라인 '{timelineName}' 완료됨");
    
        // 안전하게 호출
        if (TimelineEventMap.TryGetValue(timelineName, out UnityEvent eventToInvoke))
        {
            if (eventToInvoke != null && eventToInvoke.GetPersistentEventCount() > 0)
            {
                try
                {
                    eventToInvoke.Invoke();
                    // DebugLogger.Log($"[Timeline] '{timelineName}' UnityEvent 실행됨");
                }
                catch (System.Exception e)
                {
                    DebugLogger.LogError($"[Timeline] UnityEvent 실행 중 오류: {e.Message}");
                }
            }
        }
    }
    
    // 인덱스로 타임라인 재생
    protected void PlayTimelineByIndex(int index)
    {
        if (timelineManager == null)
        {
            DebugLogger.LogError("TimeLineManager가 할당되지 않았습니다!");
            return;
        }
    
        if (index >= 0 && index < timelineManager.timelines.Count)
        {
            // TimeLineManager의 timelines에서 직접 가져와서 재생
            timelineManager.PlayTimeline(timelineManager.timelines[index]);
        }
        else
        {
            DebugLogger.LogError($"타임라인 인덱스 {index}가 유효하지 않습니다! (전체: {timelineManager.timelines.Count})");
        }
    }

    protected abstract void Initialize();
    
    // 플레이어 스폰
    protected virtual void SpawnPlayerAtSavePoint()
    {
        int currentSaveIndex = saveDataManager.GetCurrentSavePointIndex();

        // 유효성 검사
        if (savePoints.Count == 0)
        {
            DebugLogger.LogError($"[맵{currentMapIndex}] 세이브 포인트가 하나도 등록되지 않았습니다!");
            return;
        }

        if (currentSaveIndex < 0 || currentSaveIndex >= savePoints.Count || savePoints[currentSaveIndex] == null)
        {
            DebugLogger.LogWarning($"유효하지 않은 세이브 포인트: {currentSaveIndex}. 기본 위치(0)로 변경");
            currentSaveIndex = 0;

            if (savePoints[0] == null)
            {
                DebugLogger.LogError("세이브 포인트 0도 설정되지 않았습니다!");
                return;
            }
        }

        SavePoint spawnPoint = savePoints[currentSaveIndex];
        Vector3 spawnPosition = spawnPoint.GetPlayerStartPosition();
        Quaternion spawnRotation = spawnPoint.GetPlayerStartRotation();
    
        DestroySceneMainCamera();
    
        // 기존 플레이어 확인 (DontDestroyOnLoad에 남아있는 경우)
        if (CurrentPlayer == null)
        {
            CurrentPlayer = Player.Instance;
        }
        
        // 플레이어가 이미 존재하면 위치만 이동
        if (CurrentPlayer != null)
        {
            var soma = CurrentPlayer.transform.Find("Soma");
            var cc = CurrentPlayer.GetComponentInChildren<CharacterController>(true);
    
            if (cc != null) cc.enabled = false;
    
            // Soma에 위치/회전 설정
            if (soma != null)
            {
                soma.SetPositionAndRotation(spawnPosition, spawnRotation);
            }
            else
            {
                CurrentPlayer.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            }
    
            CurrentPlayer.SetActive(true);
    
            if (cc != null) cc.enabled = true;
    
            DebugLogger.Log($"[맵{currentMapIndex}] 기존 플레이어 위치 이동: 세이브 포인트 {currentSaveIndex} at {spawnPosition}");
        }
        else
        {
            // 플레이어가 없으면 새로 생성
            if (playerPrefab == null)
            {
                DebugLogger.LogError("Player Prefab이 할당되지 않았습니다!");
                return;
            }

            CurrentPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            CurrentPlayer.name = "Player";
            DontDestroyOnLoad(CurrentPlayer); // 씬 전환 시 파괴되지 않도록 설정
        
            DebugLogger.Log($"[맵{currentMapIndex}] 플레이어 생성: 세이브 포인트 {currentSaveIndex} at {spawnPosition}");
        }
    
        // 플레이어 초기화 (필요시)
        InitializePlayer(CurrentPlayer);
    
        // 플레이어 스폰 이벤트 발행
        if (eventBroker != null)
        {
            eventBroker.Publish("OnPlayerSpawned", CurrentPlayer);
        }
    }
    
    /// <summary>
    /// 씬에 배치된 기존 메인 카메라 제거
    /// </summary>
    protected virtual void DestroySceneMainCamera()
    {
        // "MainCamera" 태그를 가진 모든 카메라 찾기
        GameObject[] mainCameras = GameObject.FindGameObjectsWithTag("MainCamera");
    
        foreach (GameObject cam in mainCameras)
        {
            // 플레이어 프리팹의 자식이 아닌 씬에 배치된 카메라만 제거
            if (cam.transform.parent == null || !cam.transform.IsChildOf(CurrentPlayer?.transform))
            {
                DebugLogger.Log($"씬의 메인 카메라 제거: {cam.name}");
                Destroy(cam);
            }
        }
    }
    
    /// <summary>
    /// 플레이어 초기화 (스폰/리스폰 시 호출)
    /// 상속받은 클래스에서 필요한 초기화 로직 구현
    /// </summary>
    protected virtual void InitializePlayer(GameObject player)
    {
        // 체력 초기화, 상태 리셋 등
        // 예: player.GetComponent<PlayerHealth>()?.ResetHealth();
        
        var animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            int upperBodyIndex = animator.GetLayerIndex("UpperBody");
            if (upperBodyIndex >= 0)
                animator.SetLayerWeight(upperBodyIndex, 0f);
        }
    }

    // 플레이어 리스폰 (죽었을 때)
    public virtual void RespawnPlayer()
    {
        DebugLogger.Log($"[맵{currentMapIndex}] 플레이어 리스폰");
        SpawnPlayerAtSavePoint();
    }
    
    // 퀘스트/이벤트 완료 처리
    public virtual void CompleteEvent(string eventId)
    {
        if (!CompletedEvents.ContainsKey(eventId))
        {
            CompletedEvents[eventId] = true;
            DebugLogger.Log($"Event completed: {eventId}");
        }
    }

    /// <summary>
    /// 페이드와 함께 씬 전환
    /// </summary>
    /// <param name="sceneIndex">전환할 씬의 인덱스</param>
    public void TransitionToScene(int sceneIndex)
    {
        if (sceneIndex < 0 || sceneIndex >= sceneNames.Count)
        {
            DebugLogger.LogError($"유효하지 않은 씬 인덱스: {sceneIndex}");
            return;
        }

        string targetSceneName = sceneNames[sceneIndex];
        StartCoroutine(TransitionToSceneWithFadeCoroutine(targetSceneName, sceneIndex));
    }
    
    public virtual void TransitionToTitleScene()
    {
        DebugLogger.Log("[GameManager] 타이틀 씬으로 전환 시작");
        StartCoroutine(TransitionToTitleCoroutine());
    }
    
    protected virtual IEnumerator TransitionToTitleCoroutine()
    {
        // 페이드아웃 시작
        if (loadingManager != null)
        {
            loadingManager.StartFadeOut();
            yield return new WaitUntil(() => !loadingManager.IsFading && !loadingManager.IsBGMFading);
        }

        // 플레이어 제거 (타이틀에서는 필요 없음)
        if (CurrentPlayer != null)
        {
            Destroy(CurrentPlayer);
            CurrentPlayer = null;
        }

        // 타이틀 씬 로드 (빌드 인덱스 0)
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// 씬 시작 시 페이드인
    /// </summary>
    protected virtual void FadeInOnSceneStart()
    {
    
        if (loadingManager != null)
        {
            DebugLogger.Log("페이드인 실행중");
            loadingManager.StartFadeIn();
        }
    }
    
    /// <summary>
    /// 페이드아웃 → 씬 전환 → 새 씬에서 페이드인
    /// </summary>
    protected virtual IEnumerator TransitionToSceneWithFadeCoroutine(string sceneName, int mapIndex)
    {
        // 페이드 아웃 시작 및 완료 대기
        if (loadingManager != null)
        {
            loadingManager.StartFadeOut(mapIndex); // ← mapIndex 전달
            
            // 페이드 완료까지 대기
            yield return new WaitUntil(() => !loadingManager.IsFading);
        }

        OnBeforeSceneTransition(sceneName, mapIndex);
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
    
        if (asyncLoad != null)
        {
            yield return asyncLoad; // isDone까지 자동 대기
        }
    }

    /// <summary>
    /// 씬 전환 직전에 호출되는 가상 메서드
    /// </summary>
    protected virtual void OnBeforeSceneTransition(string sceneName, int mapIndex)
    {
        // 씬 0(인트로/메인메뉴)으로 돌아가는 경우 플레이어 파괴
        if (mapIndex == 0 && CurrentPlayer != null)
        {
            DebugLogger.Log("[GameManager] 인트로로 복귀 - 플레이어 파괴");
            Destroy(CurrentPlayer);
            CurrentPlayer = null;
        }
        else if (CurrentPlayer != null)
        {
            CurrentPlayer.SetActive(false);
            DebugLogger.Log("[GameManager] 씬 전환 중 플레이어 비활성화");
        }
        
        bool isComingFromIntro = saveDataManager.IsComingFromIntro();
    
        if (!isComingFromIntro)
        {
            // 일반 씬 전환(게임 진행 중)일 때만 SetMapIndex 호출
            // 새로운 맵으로 진입하므로 세이브포인트 0으로 초기화하는 것이 맞음
            saveDataManager.SetMapIndex(mapIndex);
        }
        // 인트로에서 온 경우는 이미 저장된 맵 인덱스와 세이브포인트를 유지
    
        DebugLogger.Log($"씬 전환: {sceneName} (맵 인덱스: {mapIndex})");
    }
    
    public IEnumerator PlayHackedEffect(float duration)
    {
        soundManager?.PlaySFX(19);
        hacked.SetActive(true);
        yield return new WaitForSeconds(duration);
        hacked.SetActive(false);
    }
    
    public IEnumerator PlayInterferencesEffect(float duration)
    {
        soundManager?.PlaySFX(35);
        interferences.SetActive(true);
        yield return new WaitForSeconds(duration);
        interferences.SetActive(false);
    }
    
    private void OnProgressState(object data)
    {
        ProgressToNext();
    }
    
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    protected virtual void OnDisable()
    {
        if (hacked != null)
        {
            hacked.SetActive(false);
        }
        if (interferences != null)
        {
            interferences.SetActive(false);
        }
        if (eventBroker != null)
        {
            eventBroker.Unsubscribe("OnTimelineComplete", OnTimelineComplete);
            eventBroker.Unsubscribe("OnProgressState", OnProgressState);
        }
        
        if (timelineManager != null)
        {
            timelineManager.CleanupAllTimelines();
        }
        
        if (CurrentInstance == this)
        {
            CurrentInstance = null;
            GameManagerRegistry.Unregister(this);
        }
    }
    
    public void OpenURL(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            DebugLogger.LogWarning("[IntroGameManager] URL이 비어있습니다.");
            return;
        }
        
        Application.OpenURL(url);
    }
}