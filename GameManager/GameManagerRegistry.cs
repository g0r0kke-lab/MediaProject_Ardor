using System.Collections;
using UnityEngine;

/// <summary>
/// 제네릭 타입 무관하게 현재 게임 매니저를 추적하는 정적 클래스
/// </summary>
public static class GameManagerRegistry
{
    public static MonoBehaviour currentManager;
    public static int CurrentMapIndex = 0;

    public static void Register(MonoBehaviour manager)
    {
        currentManager = manager;
        // 매니저 이름에서 맵 번호 파싱 (Map1GameManager → 1)
        var name = manager.GetType().Name;
        if (name.Contains("Map") && int.TryParse(
                System.Text.RegularExpressions.Regex.Match(name, @"\d+").Value, out int idx))
            CurrentMapIndex = idx;
        DebugLogger.Log($"[GameManagerRegistry] {name} 등록됨 (Map {CurrentMapIndex})");
    }

    public static void Unregister(MonoBehaviour manager)
    {
        if (currentManager == manager)
        {
            currentManager = null;
            DebugLogger.Log($"[GameManagerRegistry] {manager.GetType().Name} 등록 해제됨");
        }
    }

    public static void ProgressState()
    {
        DebugLogger.Log($"[GameManagerRegistry] ProgressState 호출, currentManager = {currentManager}");
        
        if (currentManager != null)
        {
            var method = currentManager.GetType().GetMethod("ProgressToNext");
            if (method != null)
            {
                method.Invoke(currentManager, null);
                DebugLogger.Log($"[GameManagerRegistry] ProgressToNext 실행됨");
            }
            else
            {
                DebugLogger.LogError("[GameManagerRegistry] ProgressToNext 메서드를 찾을 수 없습니다!");
            }
        }
        else
        {
            DebugLogger.LogWarning("[GameManagerRegistry] 활성화된 게임 매니저가 없습니다.");
        }
    }
    
    public static void SetState(object state)
    {
        if (currentManager != null)
        {
            var method = currentManager.GetType().GetMethod("SetState");
            if (method != null)
            {
                method.Invoke(currentManager, new object[] { state });
                DebugLogger.Log($"[GameManagerRegistry] SetState({state}) 실행됨");
            }
            else
            {
                DebugLogger.LogError("[GameManagerRegistry] SetState 메서드를 찾을 수 없습니다!");
            }
        }
        else
        {
            DebugLogger.LogWarning("[GameManagerRegistry] 활성화된 게임 매니저가 없습니다.");
        }
    }
    
    public static object GetCurrentState()
    {
        if (currentManager != null)
        {
            var method = currentManager.GetType().GetMethod("GetCurrentState");
            if (method != null)
            {
                return method.Invoke(currentManager, null);
            }
        }
        return null;
    }
    
    public static MonoBehaviour GetCurrentManager()
    {
        return currentManager;
    }
    
    public static void PlayInterferences(float duration)
    {
        var manager = GetCurrentManager();
        if (manager != null)
        {
            var method = manager.GetType().GetMethod("PlayInterferencesEffect");
            if (method != null)
            {
                manager.StartCoroutine((IEnumerator)method.Invoke(manager, new object[] { duration }));
            }
        }
    }
    
    public static void TransitionToTitle()
    {
        if (currentManager != null)
        {
            var method = currentManager.GetType().GetMethod("TransitionToTitleScene");
            if (method != null)
            {
                method.Invoke(currentManager, null);
                DebugLogger.Log("[GameManagerRegistry] 타이틀 씬으로 전환 요청");
            }
            else
            {
                DebugLogger.LogError("[GameManagerRegistry] TransitionToTitleScene 메서드를 찾을 수 없습니다!");
            }
        }
        else
        {
            // 매니저 없이도 직접 전환 시도
            DebugLogger.LogWarning("[GameManagerRegistry] 매니저 없음, 직접 타이틀 씬 로드");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
    
    public static bool DevTeleport(int mapIndex, int savePointIndex)
    {
        if (currentManager == null)
        {
            DebugLogger.LogWarning("[GameManagerRegistry] DevTeleport: 활성화된 게임 매니저가 없습니다.");
            return false;
        }
        var method = currentManager.GetType().GetMethod("TryDevTeleport");
        if (method == null)
        {
            DebugLogger.LogError("[GameManagerRegistry] TryDevTeleport 메서드를 찾을 수 없습니다!");
            return false;
        }
        return (bool)method.Invoke(currentManager, new object[] { mapIndex, savePointIndex });
    }

    public static void RespawnPlayer()
    {
        if (currentManager != null)
        {
            var method = currentManager.GetType().GetMethod("RespawnPlayer");
            if (method != null)
            {
                method.Invoke(currentManager, null);
                DebugLogger.Log("[GameManagerRegistry] RespawnPlayer 실행됨");
            }
            else
            {
                DebugLogger.LogError("[GameManagerRegistry] RespawnPlayer 메서드를 찾을 수 없습니다!");
            }
        }
        else
        {
            DebugLogger.LogWarning("[GameManagerRegistry] 활성화된 게임 매니저가 없습니다.");
        }
    }
}