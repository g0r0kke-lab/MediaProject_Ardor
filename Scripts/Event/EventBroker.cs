using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 간 이벤트 통신을 중재하는 싱글톤 매니저
/// DontDestroyOnLoad로 씬 전환 시에도 유지됨
/// string 키 기반으로 범용적으로 사용 가능
/// </summary>
public class EventBroker : MonoBehaviour
{
    public static EventBroker Instance;

    // 이벤트 저장소
    private Dictionary<string, Action> _eventDictionary = new Dictionary<string, Action>();
    private Dictionary<string, Action<object>> _eventWithDataDictionary = new Dictionary<string, Action<object>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLogger.Log("[EventBroker] 이벤트 중재 매니저 초기화 완료");
        }
        else
        {
            Destroy(Instance.gameObject); // 기존 것을 파괴
            Instance = this; // 새 씬의 것으로 교체
            DontDestroyOnLoad(gameObject);
        }
    }

    // ============================================
    // 데이터 없는 이벤트
    // ============================================
    
    /// <summary>
    /// 이벤트 구독
    /// </summary>
    public void Subscribe(string eventKey, Action listener)
    {
        if (!_eventDictionary.ContainsKey(eventKey))
        {
            _eventDictionary[eventKey] = null;
        }
        _eventDictionary[eventKey] += listener;
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    public void Unsubscribe(string eventKey, Action listener)
    {
        if (_eventDictionary.ContainsKey(eventKey))
        {
            _eventDictionary[eventKey] -= listener;
        }
    }

    /// <summary>
    /// 이벤트 발행
    /// </summary>
    public void Publish(string eventKey)
    {
        if (_eventDictionary.ContainsKey(eventKey))
        {
            _eventDictionary[eventKey]?.Invoke();
            DebugLogger.Log($"[EventBroker] 이벤트 발행: {eventKey}");
        }
    }

    // ============================================
    // 데이터 있는 이벤트
    // ============================================
    
    /// <summary>
    /// 데이터와 함께 이벤트 구독
    /// </summary>
    public void Subscribe(string eventKey, Action<object> listener)
    {
        if (!_eventWithDataDictionary.ContainsKey(eventKey))
        {
            _eventWithDataDictionary[eventKey] = null;
        }
        _eventWithDataDictionary[eventKey] += listener;
    }

    /// <summary>
    /// 데이터와 함께 이벤트 구독 해제
    /// </summary>
    public void Unsubscribe(string eventKey, Action<object> listener)
    {
        if (_eventWithDataDictionary.ContainsKey(eventKey))
        {
            _eventWithDataDictionary[eventKey] -= listener;
        }
    }

    /// <summary>
    /// 데이터와 함께 이벤트 발행
    /// </summary>
    public void Publish(string eventKey, object data)
    {
        if (_eventWithDataDictionary.ContainsKey(eventKey))
        {
            _eventWithDataDictionary[eventKey]?.Invoke(data);
            DebugLogger.Log($"[EventBroker] 이벤트 발행 (데이터 포함): {eventKey}");
        }
    }

    // ============================================
    // 유틸리티 메서드
    // ============================================
    
    /// <summary>
    /// 특정 이벤트의 모든 구독 해제
    /// </summary>
    public void ClearEvent(string eventKey)
    {
        if (_eventDictionary.ContainsKey(eventKey))
        {
            _eventDictionary[eventKey] = null;
        }
        if (_eventWithDataDictionary.ContainsKey(eventKey))
        {
            _eventWithDataDictionary[eventKey] = null;
        }
    }

    /// <summary>
    /// 모든 이벤트 구독 해제
    /// </summary>
    public void ClearAllEvents()
    {
        _eventDictionary.Clear();
        _eventWithDataDictionary.Clear();
        DebugLogger.Log("[EventBroker] 모든 이벤트 구독 해제 완료");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ClearAllEvents();
        }
    }
}