using UnityEngine;
using System.Diagnostics;

/// <summary>
/// Project Settings > Player > Scripting Define Symbols에서:
/// 로그 켜고 싶을 때: ENABLE_DEBUG_LOG 추가
/// 에디터: ENABLE_DEBUG_LOG 있을 때만 로그 출력
/// 빌드: 항상 로그 제거 (ENABLE_DEBUG_LOG 있어도 무시)
/// </summary>
public static class DebugLogger
{
    // 이 심볼이 정의되어 있을 때만 로그 작동
    [Conditional("ENABLE_DEBUG_LOG")]
    public static void Log(string message)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.Log(message);
#endif
    }

    [Conditional("ENABLE_DEBUG_LOG")]
    public static void LogWarning(string message)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning(message);
#endif
    }

    [Conditional("ENABLE_DEBUG_LOG")]
    public static void LogError(string message)
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogError(message);
#endif
    }
}