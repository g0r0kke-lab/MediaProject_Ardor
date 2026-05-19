using UnityEngine;
using UnityEditor;

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