using UnityEngine;

// Player.cs
/// <summary>
/// 활성 플레이어 게임오브젝트 인스턴스를 추적하고, 런타임에 중복 인스턴스를 제거하는 싱글톤 MonoBehaviour입니다.
/// </summary>
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
