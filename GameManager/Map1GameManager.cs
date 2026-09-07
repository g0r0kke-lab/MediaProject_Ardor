using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

public enum Map1State
{
    Start,
    Tutorial,
    ReadySP1,
    SavePoint1,
    Training,
    SomaCalledSkia,
    SkiaSpawn,
    Rs1Spawn,
    Rs1Talk0,
    Rs1Guide,
    Rs1Talk1,
    RepairCenterTry,
    Rs1Talk2,
    Rs1MissionGuide,
    PartsMissionStart,
    Rs1Talk3,
    PartsCollected,
    Rs1Talk4,
    RepairCenterRetry,
    RepairCenterSuccess,
    Memory0Start,
    Rs1Talk5,
    Rs1ToTunnel,
    IdeaAudio,
    Rs1Talk6,
    Rs1KeepToTunnel,
    TunnelEntrance,
    TunnelEnter,
    Rs1Talk7,
    Map1End,
}

/// <summary>
/// 튜토리얼, 친절한 로봇 NPC 가이드, 파츠 수집 미션, 전체 내러티브 상태 진행을 총괄하는 맵 1 게임 매니저입니다.
/// </summary>
public class Map1GameManager : GameManagerBass<Map1State>
{
    public static Map1GameManager Instance;

    [Header("KindRobot")] [SerializeField] private KindRobotController KindRobot;

    [Header("Skia Settings")] [SerializeField]
    private GameObject Skia;

    [SerializeField] private SkiaSimpleController skia;
    [SerializeField] private Transform skiaSpawnPoint;
    [SerializeField] private Transform skiaWalkTarget;

    private Coroutine skiaRoutine;

    [Header("Dummy Player")] [SerializeField]
    private GameObject dummyPlayer;

    [Header("Map1 Settings")] [SerializeField]
    private SavePoint savePoint2;

    [SerializeField] private int TotalParts = 3;
    [SerializeField, ReadOnly] private int currentParts = 0;
    private readonly string[] PART_ITEM_NAMES = { "Gear", "Batteries", "ModuleCard" };

    [Header("Video Settings")] [SerializeField]
    private VideoManager introVideoManager;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera uiCamera;

    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            currentState = Map1State.Start;
        }
        else
        {
            Destroy(gameObject);
        }

        base.Awake();
    }

    protected override void Initialize()
    {
    }

    // 세이브포인트 0에서는 인트로 영상+타임라인이 끝난 뒤(OnIntroTimelineComplete)에야
    // 플레이어가 스폰되므로, 그 전까지는 MagneticManager/InputModeManager 초기화를 기다리면 안 됨
    protected override bool IsPlayerSpawnDeferred()
    {
        return saveDataManager.GetCurrentSavePointIndex() == 0;
    }

    /// <summary>
    /// 씬에서 Skia를 자동으로 찾는 메서드
    /// </summary>
    protected override void SpawnPlayerAtSavePoint()
    {
        int currentSaveIndex = saveDataManager.GetCurrentSavePointIndex();

        if (currentSaveIndex == 0)
        {
            DebugLogger.Log("[Map1] Intro video playback requested");

            if (mainCamera != null)
            {
                mainCamera.enabled = false;
                DebugLogger.Log("[Map1] Main camera disabled for video playback");
            }

            if (uiCamera != null)
            {
                uiCamera.enabled = false;
                DebugLogger.Log("[Map1] UI camera disabled for video playback");
            }

            if (introVideoManager != null)
            {
                introVideoManager.PlayVideo(() =>
                {
                    DebugLogger.Log("[Map1] Video completed, playing intro timeline");

                    // 비디오 종료 후 카메라 활성화
                    if (mainCamera != null)
                    {
                        mainCamera.enabled = true;
                        DebugLogger.Log("[Map1] Main camera enabled after video");
                    }

                    if (uiCamera != null)
                    {
                        uiCamera.enabled = true;
                        DebugLogger.Log("[Map1] UI camera enabled after video");
                    }

                    PlayTimelineByIndex(0);
                });
            }
            else
            {
                DebugLogger.LogWarning("[Map1] VideoManager not assigned. Playing timeline only");

                // VideoManager 없으면 카메라 다시 켜기
                if (mainCamera != null)
                {
                    mainCamera.enabled = true;
                }

                if (uiCamera != null)
                {
                    uiCamera.enabled = true;
                }

                PlayTimelineByIndex(0);
            }

            return;
        }

        if (dummyPlayer != null)
        {
            Destroy(dummyPlayer);
        }

        base.SpawnPlayerAtSavePoint();
    }

    public void OnIntroTimelineComplete()
    {
        if (dummyPlayer != null)
        {
            Destroy(dummyPlayer);
        }

        // 타임라인 완료 후 카메라 확실히 켜기
        if (mainCamera != null && !mainCamera.enabled)
        {
            mainCamera.enabled = true;
            DebugLogger.Log("[Map1] Main camera enabled after timeline complete");
        }

        if (uiCamera != null && !uiCamera.enabled)
        {
            uiCamera.enabled = true;
            DebugLogger.Log("[Map1] UI camera enabled after timeline complete");
        }


        base.SpawnPlayerAtSavePoint();
    }

    protected override void LoadState()
    {
        int savePointIndex = saveDataManager.GetCurrentSavePointIndex();

        switch (savePointIndex)
        {
            case 0:
                SetState(Map1State.Start);
                saveDataManager.ClearMapProgress(currentMapIndex);
                break;
            case 1:
                SetState(Map1State.ReadySP1);
                saveDataManager.ClearMapProgress(currentMapIndex);
                UpdateCurrentPartsCount();
                break;
            case 2:
                EnsurePartsCollected();
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                UpdateCurrentPartsCount();
                SetState(Map1State.Rs1Talk5);
                break;
        }
    }

    protected override void HandleStateEnter(Map1State state)
    {
        switch (state)
        {
            case Map1State.Start:
                break;
            case Map1State.Tutorial:
                eventBroker?.Publish("EyeSet_Broken");
                soundManager?.PlaySFX(11);
                MagneticManager.Instance?.SetDetectUIVisible(false);
                GameUIManager.Instance.OpenQuestGuide("Q1_00_ExploreSurroundings");
                break;
            case Map1State.ReadySP1:
                break;
            case Map1State.SavePoint1:
                eventBroker?.Publish("EyeSet_Broken");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.Training:
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_01_MoveToNextArea");
                MagneticManager.Instance?.SetDetectUIVisible(true);
                break;
            case Map1State.SomaCalledSkia:
                if (soundManager) soundManager.StopBGM();
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.SkiaSpawn:
                eventBroker?.Publish("SkiaSpawn");
                break;
            case Map1State.Rs1Spawn:
                eventBroker?.Publish("KindRobotSpawn");
                break;
            case Map1State.Rs1Talk0:
                PublishDialogueStart("DLG1_00");
                eventBroker?.Publish("Continue_On");
                break;
            case Map1State.Rs1Guide:
                eventBroker?.Publish("KindRobotMove_1");
                eventBroker?.Publish("ShowRs1Marker");
                eventBroker?.Publish("Continue_Off");
                //퀘스트 추가
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_03_FollowRS1");
                break;
            case Map1State.Rs1Talk1:
                eventBroker?.Publish("HideRs1Marker");
                PublishTurnToNPC();
                break;
            case Map1State.RepairCenterTry:
                eventBroker?.Publish("ShowCenterMarker");
                eventBroker.Publish("TriggerOff_8");
                eventBroker.Publish("TriggerOn_9");
                //퀘스트2
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_04_UseRepairCenter");
                break;
            case Map1State.Rs1Talk2:
                eventBroker?.Publish("HideCenterMarker");
                PublishDialogueStart("DLG1_02");
                eventBroker.Publish("TriggerOff_9");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.Rs1MissionGuide:
                eventBroker?.Publish("ShowRs1Marker");
                eventBroker?.Publish("KIndMoveToStop2_1");
                eventBroker?.Publish("ST1_01_Off");
                //퀘스트 추가
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_03_FollowRS1");
                break;
            case Map1State.PartsMissionStart:
                eventBroker?.Publish("ST1_01_On");
                if (soundManager) soundManager.PlayBGM(2);
                //퀘스트3
                // soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_05_Find3Parts");
                break;
            case Map1State.Rs1Talk3:
                eventBroker?.Publish("HideRs1Marker");
                if (soundManager) soundManager.PlayBGM(1);
                PublishDialogueStart("DLG1_04");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.PartsCollected:
                eventBroker?.Publish("KIndMoveToStop2");
                //퀘스트4
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_03_FollowRS1");
                break;
            case Map1State.Rs1Talk4:
                GameUIManager.Instance.CloseQuestGuide();
                PublishDialogueStart("DLG1_05");
                break;
            case Map1State.RepairCenterRetry:
                eventBroker?.Publish("ShowCenterMarker");
                eventBroker.Publish("TriggerOn_13");
                //퀘스트5
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_04_UseRepairCenter");
                break;
            case Map1State.RepairCenterSuccess:
                eventBroker?.Publish("EyeSet_Repaired");
                eventBroker?.Publish("HideCenterMarker");
                // 인벤토리만 초기화 (수집 기록은 유지)
                int currentMapIndex = saveDataManager.GetCurrentMapIndex();
                saveDataManager.ClearInventoryForMap(currentMapIndex);
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.Memory0Start:
                StartCoroutine(StartMemory0Sequence());
                eventBroker.Publish("TriggerOff_13");
                break;
            case Map1State.Rs1Talk5:
                if (KindRobot == null)
                {
                    eventBroker?.Publish("KindRobotSpawn");
                }
                GuideUIManager.Instance.CloseMemoryImage();
                StartCoroutine(FinishMemory0Sequence());
                break;
            case Map1State.Rs1ToTunnel:
                eventBroker?.Publish("ShowRs1Marker");
                DebugLogger.Log("KindRobotToTunnel event published");
                eventBroker.Publish("ToTunnel");
                //퀘스트6
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_03_FollowRS1");
                break;
            case Map1State.IdeaAudio:
                eventBroker?.Publish("HideRs1Marker");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.Rs1Talk6:
                PublishDialogueStart("DLG1_08");
                eventBroker.Publish("TriggerOff_14");
                break;
            case Map1State.Rs1KeepToTunnel:
                //퀘스트6
                eventBroker?.Publish("ShowRs1Marker");
                eventBroker.Publish("KeepToTunnel");
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_03_FollowRS1");
                break;
            case Map1State.TunnelEntrance:
                eventBroker?.Publish("HideRs1Marker");
                // PublishDialogueStart("DLG1_09");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.TunnelEnter:
                eventBroker?.Publish("TriggerOn_16");
                eventBroker?.Publish("ST1_02_Off");
                eventBroker?.Publish("ShowTunnelMarker");
                //퀘스트7
                soundManager?.PlaySFX(11);
                GameUIManager.Instance.OpenQuestGuide("Q1_09_EnterTunnel");
                break;
            case Map1State.Rs1Talk7:
                //TransitionToScene(2);
                eventBroker.Publish("KindTeleportStop_4");
                eventBroker?.Publish("HideTunnelMarker");
                GameUIManager.Instance.CloseQuestGuide();
                break;
            case Map1State.Map1End:
                // 맵1 완료 플래그 설정
                // if (saveDataManager != null)
                // {
                //     saveDataManager.SetMap1Completed(true);
                //     saveDataManager.SetComingFromIntro(true);
                // }
                
                TransitionToScene(2);
                break;
        }
    }

    private IEnumerator StartMemory0Sequence()
    {
        yield return StartCoroutine(PlayHackedEffect(0.3f));

        if (soundManager) soundManager.PlayBGM(3);

        GameUIManager.Instance.OpenDialogue("DLG1_06");
        GuideUIManager.Instance.OpenMemoryImage(0);
    }

    private IEnumerator FinishMemory0Sequence()
    {
        yield return StartCoroutine(PlayHackedEffect(0.3f));
    
        eventBroker?.Publish("ST1_00_Off");
        eventBroker.Publish("TriggerOn_14");
        if (soundManager) soundManager.PlayBGM(1);
        if (savePoint2 != null) savePoint2.SetConditionCleared(true);
    
        PublishTurnToNPC();

        GameUIManager.Instance.OpenDialogue("DLG1_07");
    }

    private void UpdateCurrentPartsCount()
    {
        currentParts = saveDataManager.GetCollectedPartsCount(PART_ITEM_NAMES);
        DebugLogger.Log($"[Map1] 현재 수집된 파츠: {currentParts}/{TotalParts}");
    }

    public int GetCurrentPartsNum()
    {
        return currentParts;
    }

    public void OnPartCollected(string uniqueItemId)
    {
        if (currentState != Map1State.PartsMissionStart)
            return;

        bool isPart = false;
        foreach (string partName in PART_ITEM_NAMES)
        {
            if (uniqueItemId.Contains($"_{partName}_"))
            {
                isPart = true;
                break;
            }
        }

        if (!isPart)
            return;

        // 세이브데이터에서 최신 값 가져오기
        UpdateCurrentPartsCount();
        DebugLogger.Log($"[Map1] 파츠 수집! ({currentParts}/{TotalParts})");
        
        string currentLanguage = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "ko-KR";
        UpdatePartsQuestGuide(currentLanguage);

        if (currentParts >= TotalParts)
        {
            DebugLogger.Log("[Map1] 모든 파츠 수집 완료!");
            eventBroker?.Publish("ST1_01_Off");
        }
    }
    
    private void UpdatePartsQuestGuide(string language)
    {
        soundManager?.PlaySFX(11);
        
        if (currentParts >= TotalParts)
        {
            GameUIManager.Instance.OpenQuestGuide("Q1_08_ReturnToCenter");
        }
        else if (currentParts == 1)
        {
            GameUIManager.Instance.OpenQuestGuide("Q1_06_FindParts_1_3");
        }
        else if (currentParts == 2)
        {
            GameUIManager.Instance.OpenQuestGuide("Q1_07_FindParts_2_3");
        }
    }
        
    private IEnumerator HideCollisionPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameUIManager.Instance.HideMessage("CollisionPanel");
    }
    
    private void PublishDialogueStart(string dialogueId)
    {
        GameObject npc = GameObject.FindWithTag("NPC");
        Transform npcTransform = npc != null ? npc.transform : null;
    
        if (npcTransform != null)
        {
            DebugLogger.Log($"[Map1] NPC 찾음: {npc.name}, 대화 시작 요청: {dialogueId}");
        }
        else
        {
            DebugLogger.LogWarning("[Map1] NPC 태그를 가진 오브젝트를 찾을 수 없음!");
        }
    
        GameUIManager.Instance.OpenDialogue(dialogueId, npcTransform);
    }
    
    private void PublishTurnToNPC()
    {
        GameObject npc = GameObject.FindWithTag("NPC");
        if (npc != null)
            eventBroker?.Publish("TurnToNPC", npc.transform);
    }
    
    private void EnsurePartsCollected()
    {
        int currentMap = saveDataManager.GetCurrentMapIndex();
    
        foreach (string partName in PART_ITEM_NAMES)
        {
            // 해당 파츠 종류가 이미 있으면 스킵
            if (saveDataManager.HasPartByName(currentMap, partName))
            {
                DebugLogger.Log($"[Map1] {partName} 이미 수집됨 - 스킵");
                continue;
            }
        
            // 없을 때만 지급
            string fakeUniqueId = $"Map1_{partName}_SavePoint2";
            ItemData itemData = Resources.Load<ItemData>($"Items/{partName}");
            if (itemData != null)
            {
                saveDataManager.CollectFieldItem(fakeUniqueId, itemData);
                DebugLogger.Log($"[Map1] 세이브포인트2 - {partName} 자동 지급");
            }
        }

        UpdateCurrentPartsCount();
    }
}