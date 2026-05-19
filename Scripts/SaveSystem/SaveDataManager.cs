using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Serialization;

// ============================================
// 데이터 구조
// ============================================
[Serializable]
public class MapItemCollection
{
    public int mapIndex;
    public List<string> collectedItemIds = new List<string>();
}

[Serializable]
public class SaveData
{
    // 진행도 정보
    public int currentMapIndex = 0;
    public int currentSavePointIndex = 0;

    // 맵별 수집 아이템 목록
    public List<MapItemCollection> mapCollections = new List<MapItemCollection>();
    public List<string> inventoryItemPaths = new List<string>();

    // 🎯🎯🎯 데모 완료 플래그
    public bool hasCompletedMap1 = false;
    
    // 플레이어 정보 (예시 - 나중에 추가)
    // public float playerHealth = 100f;
    // public List<int> inventoryItemIds = new List<int>();
    // public List<int> inventoryItemCounts = new List<int>();
    // public Dictionary<string, bool> unlockedAbilities = new Dictionary<string, bool>();
}

// ============================================
// 2. 데이터 저장 전략 인터페이스
// ============================================
public interface ISaveStrategy
{
    string FileExtension { get; }
    string Save(string jsonData);
    string Load(string fileData);
}

// ============================================
// 3. 평문 저장 전략 (개발용)
// ============================================
public class PlainTextSaveStrategy : ISaveStrategy
{
    public string FileExtension => ".json";

    public string Save(string jsonData)
    {
        return jsonData; // 그대로 반환
    }

    public string Load(string fileData)
    {
        return fileData; // 그대로 반환
    }
}

// ============================================
// 4. 암호화 저장 전략 (배포용)
// ============================================
public class EncryptedSaveStrategy : ISaveStrategy
{
    private readonly string _encryptionKey;

    public string FileExtension => ".dat";

    public EncryptedSaveStrategy(string key)
    {
        _encryptionKey = key;
    }

    public string Save(string jsonData)
    {
        return Encrypt(jsonData);
    }

    public string Load(string fileData)
    {
        return Decrypt(fileData);
    }

    // AES 암호화
    private string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetValidKey(_encryptionKey);
            aes.IV = new byte[16];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }

                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }
    }

    // AES 복호화
    private string Decrypt(string cipherText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetValidKey(_encryptionKey);
            aes.IV = new byte[16];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (MemoryStream msDecrypt = new MemoryStream(buffer))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }
    }

    private byte[] GetValidKey(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] validKey = new byte[32];
        Array.Copy(keyBytes, validKey, Math.Min(keyBytes.Length, validKey.Length));
        return validKey;
    }
}

// ============================================
// 5. 메인 SaveDataManager (책임 분리)
// ============================================
public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance;

    [Header("Save Configuration")] [SerializeField]
    private string saveFileName = "SaveData";

    [SerializeField] private string encryptionKey = "MyGame2025SecretKey!@#$%^&*";
    private bool _isComingFromIntro = false;

    // ============================================
    // 컴파일 타임 상수 - 빌드 시 자동으로 암호화 강제
    // ============================================
#if UNITY_EDITOR
    private const bool USE_PLAIN_TEXT = true; // 에디터: 평문 (개발용)
#else
    private const bool USE_PLAIN_TEXT = false; // 빌드: 암호화 (배포용)
#endif

    // 현재 게임 데이터 (메모리에 유지)
    private SaveData _currentSaveData;

    private ISaveStrategy _saveStrategy;

    private string SaveFilePath => Path.Combine(
        Application.persistentDataPath,
        saveFileName + _saveStrategy.FileExtension
    );

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 저장 전략 선택
            InitializeSaveStrategy();

            LoadGameData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 저장 전략 초기화 (컴파일 타임에 결정)
    private void InitializeSaveStrategy()
    {
        if (USE_PLAIN_TEXT)
        {
            _saveStrategy = new PlainTextSaveStrategy();
            DebugLogger.Log("[개발 모드] 평문 JSON 사용 - 파일 직접 수정 가능");
        }
        else
        {
            _saveStrategy = new EncryptedSaveStrategy(encryptionKey);
            DebugLogger.Log("[배포 모드] 암호화 사용 - 보안 강화");
        }
    }

    private void OnEnable()
    {
        // 씬 로드 이벤트 구독
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬이 로드될 때마다 자동으로 세이브 파일 다시 읽기
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 데이터가 null이면 강제로 새로 생성
        if (_currentSaveData == null)
        {
            DebugLogger.LogWarning("[SaveDataManager] OnSceneLoaded - _currentSaveData가 null, 강제 생성");
            _currentSaveData = new SaveData();
        }
    
        LoadGameData();
    
        // 로드 후에도 null이면 에러 (이건 심각한 문제)
        if (_currentSaveData == null)
        {
            DebugLogger.LogError("[SaveDataManager] LoadGameData 실행 후에도 null!");
            _currentSaveData = new SaveData();
        }
    
        DebugLogger.Log($"[SaveDataManager] 씬 '{scene.name}' 로드 완료 - 맵: {_currentSaveData.currentMapIndex}, 세이브포인트: {_currentSaveData.currentSavePointIndex}");
    }

    // 게임 데이터 로드
    public void LoadGameData()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                string fileData = File.ReadAllText(SaveFilePath);
                string jsonData = _saveStrategy.Load(fileData);

                SaveData loadedData = JsonUtility.FromJson<SaveData>(jsonData);

                // 로드된 데이터가 null이 아닐 때만 교체
                if (loadedData != null)
                {
                    _currentSaveData = loadedData;
                    DebugLogger.Log($"세이브 로드 완료: 맵={_currentSaveData.currentMapIndex}, 세이브포인트={_currentSaveData.currentSavePointIndex}");
                }
                else
                {
                    DebugLogger.LogWarning("JSON 파싱 실패. 기존 데이터 유지 또는 새 데이터 생성.");
                    if (_currentSaveData == null)
                    {
                        _currentSaveData = new SaveData();
                    }
                }
            }
            else
            {
                DebugLogger.Log("세이브 파일 없음. 새 게임 시작.");
                if (_currentSaveData == null)
                {
                    _currentSaveData = new SaveData();
                }
            }
        }
        catch (Exception e)
        {
            DebugLogger.LogError($"세이브 로드 실패: {e.Message}");
            if (_currentSaveData == null)
            {
                _currentSaveData = new SaveData();
            }
        }

        // 최종 안전망
        if (_currentSaveData == null)
        {
            DebugLogger.LogError("[SaveDataManager] 모든 시도 실패, 강제로 새 데이터 생성");
            _currentSaveData = new SaveData();
        }
    }

    // 게임 데이터 저장
    public void SaveGameData()
    {
        try
        {
            string jsonData = JsonUtility.ToJson(_currentSaveData, true);
            string fileData = _saveStrategy.Save(jsonData);

            File.WriteAllText(SaveFilePath, fileData);

            DebugLogger.Log(
                $"세이브 저장 완료: 맵={_currentSaveData.currentMapIndex}, 세이브포인트={_currentSaveData.currentSavePointIndex}");
        }
        catch (Exception e)
        {
            DebugLogger.LogError($"세이브 저장 실패: {e.Message}");
        }
    }

    public void SetComingFromIntro(bool value)
    {
        _isComingFromIntro = value;
    }

    public bool IsComingFromIntro()
    {
        return _isComingFromIntro;
    }


    // ============================================
    // Getter/Setter (외부에서 데이터 접근)
    // ============================================

    // 맵 인덱스
    public int GetCurrentMapIndex()
    {
        if (_currentSaveData == null)
        {
            // DebugLogger.LogError("[SaveDataManager] _currentSaveData가 null입니다!");
            return 0;
        }

        return _currentSaveData.currentMapIndex;
    }

    public void SetMapIndex(int mapIndex)
    {
        // 맵이 진행된 경우에만 저장 (이전 맵으로 돌아가는 건 막음)
        if (mapIndex >= _currentSaveData.currentMapIndex)
        {
            _currentSaveData.currentMapIndex = mapIndex;
            _currentSaveData.currentSavePointIndex = 0; // 새 맵 진입 시 세이브포인트 0으로 초기화
            SaveGameData();
        }
    }

    // 세이브 포인트 인덱스
    public int GetCurrentSavePointIndex()
    {
        if (_currentSaveData == null)
        {
            DebugLogger.LogError("[SaveDataManager] _currentSaveData가 null입니다!");
            return 0;
        }

        return _currentSaveData.currentSavePointIndex;
    }

    public void SetSavePointIndex(int savePointIndex)
    {
        // 같은 맵 내에서 세이브 포인트가 진행된 경우에만 저장
        if (savePointIndex > _currentSaveData.currentSavePointIndex)
        {
            _currentSaveData.currentSavePointIndex = savePointIndex;
            SaveGameData();
        }
    }

    // 맵 + 세이브포인트 동시 설정 (씬 전환 시 사용)
    public void SetProgress(int mapIndex, int savePointIndex)
    {
        bool needsSave = false;

        // 맵이 진행되었거나, 같은 맵에서 세이브포인트가 진행된 경우
        if (mapIndex > _currentSaveData.currentMapIndex)
        {
            _currentSaveData.currentMapIndex = mapIndex;
            _currentSaveData.currentSavePointIndex = savePointIndex;
            needsSave = true;
        }
        else if (mapIndex == _currentSaveData.currentMapIndex &&
                 savePointIndex > _currentSaveData.currentSavePointIndex)
        {
            _currentSaveData.currentSavePointIndex = savePointIndex;
            needsSave = true;
        }

        if (needsSave)
        {
            SaveGameData();
        }
    }

    // 강제 설정 (맵 불일치 등 특수 상황용)
    public void ForceSetProgress(int mapIndex, int savePointIndex)
    {
        _currentSaveData.currentMapIndex = mapIndex;
        _currentSaveData.currentSavePointIndex = savePointIndex;

        // 해당 맵의 수집 아이템 초기화
        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == mapIndex);

        if (mapCollection != null)
        {
            _currentSaveData.mapCollections.Remove(mapCollection);
        }

        // 인벤토리도 초기화
        _currentSaveData.inventoryItemPaths.Clear();

        SaveGameData();

        DebugLogger.Log($"진행도 강제 설정: 맵={mapIndex}, 세이브포인트={savePointIndex}");
    }

    // 현재 맵 외의 모든 맵 인벤토리 정리
    public void ClearInventoryExceptMap(int keepMapIndex)
    {
        // 현재 맵 수집 아이템 경로만 추출
        var keepPaths = new HashSet<string>();
        var keepCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == keepMapIndex);

        if (keepCollection != null)
        {
            foreach (string itemId in keepCollection.collectedItemIds)
            {
                string[] parts = itemId.Split('_');
                if (parts.Length >= 2)
                    keepPaths.Add($"Items/{parts[1]}");
            }
        }

        // 현재 맵 아이템만 남기고 전부 제거
        _currentSaveData.inventoryItemPaths.RemoveAll(path => !keepPaths.Contains(path));

        SaveGameData();
        DebugLogger.Log($"[SaveDataManager] 맵 {keepMapIndex} 외 인벤토리 정리 완료");
    }
    
    // 필드 아이템 수집 처리
    public void CollectFieldItem(string uniqueItemId, ItemData itemData)
    {
        int currentMap = _currentSaveData.currentMapIndex;

        // 해당 맵의 컬렉션 찾기
        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == currentMap);

        // 없으면 생성
        if (mapCollection == null)
        {
            mapCollection = new MapItemCollection { mapIndex = currentMap };
            _currentSaveData.mapCollections.Add(mapCollection);
        }

        // 아이템 ID 추가
        if (!mapCollection.collectedItemIds.Contains(uniqueItemId))
        {
            mapCollection.collectedItemIds.Add(uniqueItemId);
        }

        // 인벤토리에도 추가
        string resourcePath = GetResourcePath(itemData);
        if (!_currentSaveData.inventoryItemPaths.Contains(resourcePath))
        {
            _currentSaveData.inventoryItemPaths.Add(resourcePath);
        }

        SaveGameData();
    }

    // 리소스 경로 추출 헬퍼 메서드
    private string GetResourcePath(ItemData itemData)
    {
#if UNITY_EDITOR
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(itemData);
        // "Assets/Resources/Items/Gear.asset" → "Items/Gear"
        int resourcesIndex = assetPath.IndexOf("Resources/");
        if (resourcesIndex >= 0)
        {
            string path = assetPath.Substring(resourcesIndex + "Resources/".Length);
            return path.Replace(".asset", "");
        }
#endif

        // 런타임에서는 itemData.name 기반으로 추정
        return $"Items/{itemData.name}";
    }

    public bool IsItemCollected(string uniqueItemId)
    {
        int currentMap = _currentSaveData.currentMapIndex;

        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == currentMap);

        if (mapCollection == null) return false;

        return mapCollection.collectedItemIds.Contains(uniqueItemId);
    }

    /// <summary>
    /// 특정 파츠 종류가 이미 수집되었는지 확인 (ID 전체 또는 파츠명 기반)
    /// </summary>
    public bool IsPartTypeCollected(string uniqueItemId)
    {
        int currentMap = _currentSaveData.currentMapIndex;

        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == currentMap);

        if (mapCollection == null) return false;

        // 1. 정확한 ID로 먼저 체크
        if (mapCollection.collectedItemIds.Contains(uniqueItemId))
            return true;

        // 2. 파츠 종류 기반 체크 (Gear, Batteries, ModuleCard 등)
        string[] partNames = { "Gear", "Batteries", "ModuleCard" };
        foreach (string partName in partNames)
        {
            if (uniqueItemId.Contains($"_{partName}_"))
            {
                // 같은 파츠 종류가 이미 수집되었는지 확인
                return mapCollection.collectedItemIds.Exists(id => id.Contains($"_{partName}_"));
            }
        }

        return false;
    }

    // 수집한 필드 아이템 목록 초기화 (새 게임 시작 시)
    public void ClearCollectedItems()
    {
        _currentSaveData.mapCollections.Clear();
        _currentSaveData.inventoryItemPaths.Clear();
        SaveGameData();
    }

    public List<ItemData> GetInventoryItems()
    {
        List<ItemData> items = new List<ItemData>();

        foreach (string itemPath in _currentSaveData.inventoryItemPaths)
        {
            ItemData item = Resources.Load<ItemData>(itemPath);

            if (item != null)
            {
                items.Add(item);
            }
            else
            {
                DebugLogger.LogError($"아이템 로드 실패: {itemPath}");
            }
        }

        return items;
    }

    // 특정 아이템 이름 목록에 해당하는 수집 아이템 개수 반환
    public int GetCollectedPartsCount(string[] itemNames)
    {
        int currentMap = _currentSaveData.currentMapIndex;

        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == currentMap);

        if (mapCollection == null)
            return 0;

        int count = 0;
        foreach (string itemName in itemNames)
        {
            // 해당 파츠 종류가 하나라도 있으면 1개로 카운트
            foreach (string uniqueId in mapCollection.collectedItemIds)
            {
                if (uniqueId.Contains($"_{itemName}_"))
                {
                    count++;
                    break; // 같은 종류는 1번만 카운트하고 다음 파츠로
                }
            }
        }

        return count;
    }

    public bool RemoveInventoryItem(ItemData itemData)
    {
        string resourcePath = GetResourcePath(itemData);

        if (_currentSaveData.inventoryItemPaths.Remove(resourcePath))
        {
            SaveGameData();
            DebugLogger.Log($"인벤토리에서 아이템 제거: {resourcePath}");
            return true;
        }

        DebugLogger.LogWarning($"인벤토리에 해당 아이템 없음: {resourcePath}");
        return false;
    }
    
    // 🎯🎯🎯 맵1 완료 플래그 설정
    public void SetMap1Completed(bool completed)
    {
        _currentSaveData.hasCompletedMap1 = completed;
        SaveGameData();
        DebugLogger.Log($"맵1 완료 플래그 설정: {completed}");
    }

    // 🎯🎯🎯 맵1 완료 여부 확인
    public bool HasCompletedMap1()
    {
        return _currentSaveData.hasCompletedMap1;
    }

    
    // 특정 맵에서 해당 파츠 종류가 있는지 확인
    public bool HasPartByName(int mapIndex, string partName)
    {
        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == mapIndex);

        if (mapCollection == null)
            return false;

        return mapCollection.collectedItemIds.Exists(id => id.Contains($"_{partName}_"));
    }
    
    // 특정 맵의 인벤토리만 초기화 (수집 기록은 유지)
    public void ClearInventoryForMap(int mapIndex)
    {
        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == mapIndex);

        if (mapCollection == null)
        {
            DebugLogger.Log($"맵 {mapIndex}의 수집 기록이 없습니다.");
            return;
        }

        // 해당 맵에서 수집한 아이템들의 리소스 경로 찾기
        List<string> pathsToRemove = new List<string>();
    
        foreach (string itemId in mapCollection.collectedItemIds)
        {
            // itemId 형식: "Map1_Gear_-42_81"
            string[] parts = itemId.Split('_');
            if (parts.Length >= 2)
            {
                string itemName = parts[1]; // "Gear"
                string resourcePath = $"Items/{itemName}";
            
                if (_currentSaveData.inventoryItemPaths.Contains(resourcePath))
                {
                    pathsToRemove.Add(resourcePath);
                }
            }
        }

        // 인벤토리에서 제거
        foreach (string path in pathsToRemove)
        {
            _currentSaveData.inventoryItemPaths.Remove(path);
        }

        DebugLogger.Log($"맵 {mapIndex}의 인벤토리 {pathsToRemove.Count}개 아이템 제거 (수집 기록은 유지)");
        SaveGameData();
    }

    // 특정 맵의 수집 기록과 인벤토리 모두 초기화
    public void ClearMapProgress(int mapIndex)
    {
        MapItemCollection mapCollection = _currentSaveData.mapCollections
            .Find(m => m.mapIndex == mapIndex);

        if (mapCollection != null)
        {
            // 해당 맵의 아이템들을 인벤토리에서도 제거
            foreach (string itemId in mapCollection.collectedItemIds)
            {
                string[] parts = itemId.Split('_');
                if (parts.Length >= 2)
                {
                    string itemName = parts[1];
                    string resourcePath = $"Items/{itemName}";
                    _currentSaveData.inventoryItemPaths.Remove(resourcePath);
                }
            }

            // 수집 기록도 제거
            _currentSaveData.mapCollections.Remove(mapCollection);
        }

        DebugLogger.Log($"맵 {mapIndex}의 진행도 완전 초기화 (수집 기록 + 인벤토리)");
        SaveGameData();
    }

    // ============================================
    // 유틸리티 메서드
    // ============================================

    // 게임 데이터 초기화
    public void ResetGameData()
    {
        // 🎯🎯🎯 맵1 완료 여부는 유지
        bool wasMap1Completed = _currentSaveData.hasCompletedMap1;
        
        _currentSaveData = new SaveData();
        
        // 🎯🎯🎯 맵1 완료 기록은 복원
        _currentSaveData.hasCompletedMap1 = wasMap1Completed;
        
        SaveGameData();
        DebugLogger.Log("게임 데이터 초기화");
    }

    public void PrintSaveFilePath()
    {
        DebugLogger.Log($"=== 세이브 파일 위치 ===");
        DebugLogger.Log($"전체 경로: {SaveFilePath}");
        DebugLogger.Log($"파일 존재 여부: {File.Exists(SaveFilePath)}");

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Application.OpenURL("file://" + Application.persistentDataPath);
#endif
    }
}