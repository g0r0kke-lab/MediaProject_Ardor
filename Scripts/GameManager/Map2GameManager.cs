using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

public enum Map2State
{
    Start,
    FindKey,
    OpenDoor,
    SavePoint1,
    CctvRoom,
    RobotInteract,
    RobotTalk,
    CctvRoomExit,
    Memory1Start,
    SavePoint2,
    OpenDoor2,
    Memory2Start,
    SavePoint3,
    SkiaCome,
    StorageRoom,
    SavePoint4,
    RepairFuseBox,
    RepairedFuseBox,
    TurnOnLight,
    Skia1Coming,
    SafeFromSkia,
    Map2End
}

public class Map2GameManager : GameManagerBass<Map2State>
{
    public static Map2GameManager Instance;

    [ReadOnly] private PlayerCrouchDeath playerCrouchDeath;
    
    [Header("Give Items")]
    [SerializeField] private ItemData key1ItemData;

    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentState = Map2State.Start;
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
        DebugLogger.Log("Map2GameManager 초기화");
        
        // TODO: Map2 초기화 로직
    }
    
    protected override void InitializePlayer(GameObject player)
    {
        base.InitializePlayer(player);
    
        playerCrouchDeath = player.GetComponentInChildren<PlayerCrouchDeath>();
        
        // 로드 시 SavePoint1 이후 상태면 crouch 즉시 활성화
        if (currentState >= Map2State.SavePoint1)
        {
            playerCrouchDeath?.SetEnabled(true);
        }
    }
    
    // 상태 로드
    protected override void LoadState()
    {
        int savePointIndex = saveDataManager.GetCurrentSavePointIndex();
        saveDataManager.ClearMapProgress(currentMapIndex);
        
        switch (savePointIndex)
        {
            case 0:
                SetState(Map2State.Start);
                saveDataManager.ClearMapProgress(currentMapIndex);
                break;
            case 1:
                SetState(Map2State.SavePoint1);
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                break;
            case 2:
                SetState(Map2State.SavePoint2);
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                break;
            case 3:
                SetState(Map2State.SavePoint3);
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                break;
            case 4:
                SetState(Map2State.SavePoint4);
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                break;
            default:
                SetState(Map2State.Start);
                break;
        }
    }
    
    // 상태 진입 처리
    protected override void HandleStateEnter(Map2State state)
    {
        switch (state)
        {
            case Map2State.Start:
                break;
            case Map2State.FindKey:
                GameUIManager.Instance.ShowMapTitlePanel(); 
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_00_FindKey");
                break;
            case Map2State.OpenDoor:
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_01_OpenDoor");
                eventBroker?.Publish("ShowDoorMarker");
                break;
            case Map2State.SavePoint1:
                soundManager?.PlaySFX(11);
                eventBroker?.Publish("HideDoorMarker");
                GameUIManager.Instance.OpenQuestGuide("Q1_01_MoveToNextArea");
                
                if (playerCrouchDeath != null)
                {
                    playerCrouchDeath.SetEnabled(true);
                }
                else
                {
                    DebugLogger.LogWarning("[Map2] PlayerCrouch 컴포넌트를 찾을 수 없습니다.");
                }
                break;
            case Map2State.CctvRoom:
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_02_CctvRoom");
                eventBroker?.Publish("ActivateRobot");
                break;
            case Map2State.RobotInteract:
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_03_TalkToRobot");
                eventBroker?.Publish("ActivateNPC0");
                break;
            case Map2State.RobotTalk:
                eventBroker?.Publish("DeactivateRobot");
                GameUIManager.Instance.CloseQuestGuide();
                soundManager?.PlayBGM(1);
                break;
            case Map2State.CctvRoomExit:
                soundManager?.PlayBGM(0);
                soundManager?.PlaySFX(11);
                eventBroker?.Publish("DeactivateNPC0");
                GameUIManager.Instance.OpenQuestGuide("Q2_01_OpenDoor");
                GiveKey1Item();
                break;
            case Map2State.Memory1Start:
                GameUIManager.Instance.CloseQuestGuide();
                StartCoroutine(StartMemorySequence("DLG2_00", 0));
                break;
            case Map2State.SavePoint2:
                StartCoroutine(FinishMemorySequence());
                soundManager?.PlayBGM(0);
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_01_MoveToNextArea");
                break;
            case Map2State.OpenDoor2:
                GameUIManager.Instance.OpenQuestGuide("Q2_01_OpenDoor");
                break;
            case Map2State.Memory2Start:
                GameUIManager.Instance.CloseQuestGuide();
                StartCoroutine(StartMemorySequence("DLG2_02", 3));
                break;
            case Map2State.SavePoint3:
                StartCoroutine(FinishMemorySequence());
                soundManager?.PlayBGM(0);
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_04_OpenStorageDoor");
                eventBroker?.Publish("ShowStorageMarker");
                break;
            case Map2State.SkiaCome:
                eventBroker?.Publish("HideStorageMarker");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map2State.StorageRoom:
                soundManager?.PlayBGM(3);
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_05_SafeMove");
                break;
            case Map2State.SavePoint4:
                eventBroker?.Publish("ActivateNPC1");
                eventBroker?.Publish("HideSkiaMarker");
                soundManager?.PlayBGM(0);
                break;
            case Map2State.RepairFuseBox:
                GameUIManager.Instance.openTimeline("TL2_10");
                break;
            case Map2State.RepairedFuseBox:
                GameUIManager.Instance.OpenDialogue("DLG2_04");
                eventBroker?.Publish("TurnOnLight");
                soundManager?.PlaySFX(55);
                break;
            case Map2State.TurnOnLight:
                GameUIManager.Instance.openTimeline("TL2_05");
                break;
            case Map2State.Skia1Coming:
                eventBroker?.Publish("DeactivateNPC1");
                soundManager?.PlayBGM(4);
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q2_06_Escape");
                break;
            case Map2State.SafeFromSkia:
                soundManager?.PlayBGM(5);
                break;
            case Map2State.Map2End:
                TransitionToScene(0);
                break;
        }
    }
    
    // Key1 아이템을 인벤토리에 직접 추가하는 함수
    public void GiveKey1Item()
    {
        // 고정 ID 설정
        string fixedItemId = "Map2_Key1_Fixed";
    
        var saveDataManager = SaveDataManager.Instance;
        if (saveDataManager == null) return;
    
        // 이미 가지고 있으면 중복 방지
        if (saveDataManager.IsPartTypeCollected(fixedItemId)) return;

        // Key1 ItemData를 인스펙터에서 연결
        if (key1ItemData == null)
        {
            DebugLogger.LogError("Key1 ItemData가 할당되지 않았습니다!");
            return;
        }

        saveDataManager.CollectFieldItem(fixedItemId, key1ItemData);
    }
    
    private IEnumerator StartMemorySequence(string dialogueId, int imageIndex)
    {
        yield return StartCoroutine(PlayHackedEffect(0.3f));
    
        soundManager?.PlayBGM(2);

        GameUIManager.Instance.OpenDialogue(dialogueId);
        GuideUIManager.Instance.OpenMemoryImage(imageIndex);
    }
    
    private IEnumerator FinishMemorySequence()
    {
        GuideUIManager.Instance.CloseMemoryImage();
        yield return StartCoroutine(PlayHackedEffect(0.3f));
    }
}