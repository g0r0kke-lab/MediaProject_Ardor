using UnityEngine;

public enum Map3State
{
    Start,
    // TODO: Map3만의 상태 추가
}

public class Map3GameManager : GameManagerBass<Map3State>
{
    public static Map3GameManager Instance;
    
    // [Header("Map3 State")]
    // TODO: Map3만의 상태 enum 추가
    
    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentState = Map3State.Start;  // 초기값 설정 (있다면)
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    
        // 부모 Awake 호출
        base.Awake();
    }
    
    protected override void Initialize()
    {
        DebugLogger.Log("Map3GameManager 초기화");
        
        // TODO: Map3 초기화 로직
    }
    
    // 상태 로드
    protected override void LoadState()
    {
        int savePointIndex = saveDataManager.GetCurrentSavePointIndex();
        
        // TODO: 세이브포인트에 따른 상태 설정
        switch (savePointIndex)
        {
            case 0:
                SetState(Map3State.Start);
                break;
            default:
                SetState(Map3State.Start);
                break;
        }
    }
    
    // 상태 진입 처리
    protected override void HandleStateEnter(Map3State state)
    {
        switch (state)
        {
            case Map3State.Start:
                // TODO: Start 상태 진입 처리
                break;
        }
    }
}