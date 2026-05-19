using System.Collections;
using UnityEngine;

public enum IntroState
{
    None
}

public class IntroGameManager : GameManagerBass<IntroState>
{
    public static IntroGameManager Instance;
 
    // 🎯🎯🎯 데모 완료 팝업 UI
    [Header("Demo Complete Popup")]
    [SerializeField] private GameObject demoCompletePopup;
    
    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentState = IntroState.None;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    
        // 부모 Awake 호출
        base.Awake();
    }
    
    // 인트로는 세이브 데이터 초기화 로직을 실행하지 않음
    protected override IEnumerator InitializeWithSaveData()
    {
        yield return new WaitUntil(() => SaveDataManager.Instance != null);
        yield return new WaitForEndOfFrame();
        
        // Child 씬 로드 대기
        yield return new WaitUntil(() => !RuntimeChildSceneLoader.IsLoading);
        
        // 인트로는 저장된 데이터를 건드리지 않음!
        DebugLogger.Log("[인트로] 저장 데이터 유지, 초기화 스킵");
        
        Initialize();
        OnSceneStart?.Invoke();
        if (soundManager) soundManager.PlayBGM(0);
        FadeInOnSceneStart();
    }
    
    protected override void Initialize()
    {
        Cursor.lockState = CursorLockMode.None;
        
        // 🎯🎯🎯 맵1 완료했고 + 인트로에서 온 경우 팝업 표시
        if (saveDataManager != null && 
            saveDataManager.HasCompletedMap1() && 
            saveDataManager.IsComingFromIntro())
        {
            ShowDemoCompletePopup();
        }
        
        // 🎯🎯🎯 플래그 초기화
        if (saveDataManager != null)
        {
            saveDataManager.SetComingFromIntro(false);
        }
    }
    
    // 🎯🎯🎯 팝업 표시 메서드
    private void ShowDemoCompletePopup()
    {
        if (demoCompletePopup != null)
        {
            demoCompletePopup.SetActive(true);
        }
        else
        {
            DebugLogger.LogWarning("[인트로] 데모 완료 팝업 UI가 할당되지 않음");
        }
    }

    // 새 게임 시작
    public void OnNewGameButtonClicked()
    {
        DebugLogger.Log("새 게임 시작");
        
        if (saveDataManager == null)
        {
            DebugLogger.LogError("[IntroGameManager] saveDataManager가 null입니다!");
            return;
        }
    
        // 게임 데이터 초기화
        saveDataManager.ResetGameData();
    
        // 새 게임이므로 맵 1, 세이브포인트 0으로 강제 설정
        saveDataManager.ForceSetProgress(1, 0);
        
        // 인트로에서 왔다는 플래그 설정
        saveDataManager.SetComingFromIntro(true);
    
        // 맵 1로 이동
        TransitionToScene(1);
    }

    // 이어하기
    public void OnContinueButtonClicked()
    {
        if (saveDataManager == null)
        {
            DebugLogger.LogError("[IntroGameManager] saveDataManager가 null입니다!");
            return;
        }
        
        int savedMapIndex = saveDataManager.GetCurrentMapIndex();
    
        DebugLogger.Log($"이어하기: 저장된 맵 인덱스 = {savedMapIndex}");
    
        // 혹시 맵 0이면 맵 1로 보정
        int targetMapIndex = Mathf.Max(savedMapIndex, 1);
    
        saveDataManager.SetComingFromIntro(true);
        TransitionToScene(targetMapIndex);
    }
    
    // 플레이어 스폰 비활성화
    protected override void SpawnPlayerAtSavePoint()
    {
        // 인트로는 플레이어 스폰 안 함
        DebugLogger.Log("IntroGameManager: 플레이어 스폰 생략");
    }
    
    // 상태 로드 (인트로는 상태 없음)
    protected override void LoadState()
    {
        SetState(IntroState.None);
    }
    
    // 상태 진입 처리 (인트로는 처리 없음)
    protected override void HandleStateEnter(IntroState state)
    {
        // 인트로는 상태 변화 처리 없음
    }
}