using UnityEngine;

// Player.cs
public class Player : MonoBehaviour
{
    public static GameObject Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != gameObject)
        {
            Destroy(gameObject);  // 중복 플레이어 제거
            return;
        }
        Instance = gameObject;
    }
    
    void OnDestroy()
    {
        if (Instance == gameObject)
            Instance = null;
    }
}
