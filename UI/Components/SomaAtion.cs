using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Localization.Settings;

/// <summary>소마 캐릭터의 눈뜨기 연출 및 머티리얼 변경을 관리하는 컴포넌트</summary>
public class SomaAtion : MonoBehaviour
{
    public static SomaAtion Instance;

    [SerializeField] private PlayableDirector playableDirector;
    // 매터리얼 변경을 위한 참조 추가
    [SerializeField] private MaterialChanger materialChanger;

    // 이벤트 키 상수 정의
    private const string EVENT_SOMA_WAKE_UP = "SomaWakeUp";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // EventBroker 구독
        if (EventBroker.Instance) EventBroker.Instance.Subscribe(EVENT_SOMA_WAKE_UP, OnSomaWakeUp);
    }

    private void OnDestroy()
    {
        // EventBroker 구독 해제
        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Unsubscribe(EVENT_SOMA_WAKE_UP, OnSomaWakeUp);
        }
    }

    // 이벤트 핸들러
    private void OnSomaWakeUp()
    {
        SomaWakeUp();
    }

    //소마가 눈뜨는 함수 실행
    public void SomaWakeUp()
    {
        // 매터리얼 변경 실행
        if (materialChanger != null)
        {
            materialChanger.ChangeMaterialToAfter();
        }
        else
        {
            DebugLogger.LogWarning("[SomaAtion] MaterialChanger가 할당되지 않았습니다!");
        }
    }
}