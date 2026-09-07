using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 내 이벤트 통신을 중재하는 매니저 (씬마다 배치되어 해당 씬의 Instance로 등록됨)
/// 씬 전환에도 살아남는 오브젝트(DontDestroyOnLoad)는 씬 로드마다 EventBroker.Instance에 재구독해야 함
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
        // 씬마다 배치된 EventBroker - DontDestroyOnLoad 없이 해당 씬의 Instance로 등록
        Instance = this;
        DebugLogger.Log("[EventBroker] 이벤트 중재 매니저 초기화 완료");
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
            Instance = null;
            ClearAllEvents();
        }
    }
}