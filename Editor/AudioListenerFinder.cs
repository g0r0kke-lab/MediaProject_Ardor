using UnityEngine;
using UnityEditor;

/// <summary>
/// 씬 내 모든 AudioListener 컴포넌트를 찾아 콘솔에 출력하는 에디터 유틸리티입니다.
/// </summary>
public class AudioListenerFinder
{
    [MenuItem("ARDORBIT/Find Audio Listeners")]
    static void FindAudioListeners()
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"🔊 Found {listeners.Length} Audio Listeners:");
        
        foreach (var listener in listeners)
        {
            Debug.Log($"- {listener.gameObject.name}", listener.gameObject);
        }
    }
}