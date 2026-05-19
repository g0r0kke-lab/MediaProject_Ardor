using UnityEngine;

public enum Map4State
{
    Start,
    // TODO: Map4만의 상태 추가
}

public class Map4GameManager : GameManagerBass<Map4State>
{
    public static Map4GameManager Instance;
    
    // [Header("Map4 State")]
    // TODO: Map4만의 상태 enum 추가
    
    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentState = Map4State.Start;  // 초기값 설정 (있다면)
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
        DebugLogger.Log("Map4GameManager 초기화");
        
        // TODO: Map4 초기화 로직
    }
    
    // 상태 로드
    protected override void LoadState()
    {
        int savePointIndex = saveDataManager.GetCurrentSavePointIndex();
        
        // TODO: 세이브포인트에 따른 상태 설정
        switch (savePointIndex)
        {
            case 0:
                SetState(Map4State.Start);
                break;
            default:
                SetState(Map4State.Start);
                break;
        }
    }
    
    // 상태 진입 처리
    protected override void HandleStateEnter(Map4State state)
    {
        switch (state)
        {
            case Map4State.Start:
                // TODO: Start 상태 진입 처리
                break;
        }
    }
}