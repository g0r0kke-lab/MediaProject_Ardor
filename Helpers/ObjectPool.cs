using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범용 오브젝트 풀 시스템
/// 싱글톤 패턴으로 구현
/// </summary>
public class ObjectPool : MonoBehaviour
{
    private static ObjectPool _instance;
    public static ObjectPool Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ObjectPool>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("ObjectPool");
                    _instance = obj.AddComponent<ObjectPool>();
                }
            }
            return _instance;
        }
    }

    // 각 키별로 풀을 관리
    private Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 풀 생성
    /// </summary>
    public void CreatePool(string key, GameObject prefab, int initialSize)
    {
        if (_pools.ContainsKey(key))
        {
            DebugLogger.LogWarning($"풀 '{key}'는 이미 존재합니다.");
            return;
        }

        Pool newPool = new Pool(prefab, initialSize, transform);
        _pools[key] = newPool;
        DebugLogger.Log($"오브젝트 풀 생성: {key} (초기 크기: {initialSize})");
    }

    /// <summary>
    /// 풀에서 오브젝트 가져오기
    /// </summary>
    public GameObject Get(string key)
    {
        if (!_pools.ContainsKey(key))
        {
            DebugLogger.LogError($"풀 '{key}'가 존재하지 않습니다!");
            return null;
        }

        return _pools[key].Get();
    }

    /// <summary>
    /// 풀에 오브젝트 반환
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null)
        {
            DebugLogger.LogWarning("null 오브젝트를 반환하려 했습니다.");
            return;
        }

        // 오브젝트가 어느 풀에 속하는지 찾기
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            DebugLogger.LogWarning($"{obj.name}은 풀링된 오브젝트가 아닙니다.");
            Destroy(obj);
            return;
        }

        if (_pools.ContainsKey(pooledObj.PoolKey))
        {
            _pools[pooledObj.PoolKey].Return(obj);
        }
        else
        {
            DebugLogger.LogWarning($"풀 '{pooledObj.PoolKey}'를 찾을 수 없습니다. 오브젝트를 파괴합니다.");
            Destroy(obj);
        }
    }

    /// <summary>
    /// 특정 풀이 존재하는지 확인
    /// </summary>
    public bool HasPool(string key)
    {
        return _pools.ContainsKey(key);
    }

    /// <summary>
    /// 모든 풀 초기화
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var pool in _pools.Values)
        {
            pool.Clear();
        }
        _pools.Clear();
    }

    /// <summary>
    /// 특정 풀 제거
    /// </summary>
    public void RemovePool(string key)
    {
        if (_pools.ContainsKey(key))
        {
            _pools[key].Clear();
            _pools.Remove(key);
            DebugLogger.Log($"풀 '{key}' 제거됨");
        }
    }
}

/// <summary>
/// 개별 풀 클래스
/// </summary>
public class Pool
{
    private GameObject _prefab;
    private Queue<GameObject> _availableObjects = new Queue<GameObject>();
    private List<GameObject> _allObjects = new List<GameObject>();
    private Transform _poolParent;
    private string _poolKey;

    public Pool(GameObject prefab, int initialSize, Transform parent)
    {
        this._prefab = prefab;
        this._poolParent = parent;
        this._poolKey = prefab.name;

        // 초기 오브젝트 생성
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    private GameObject CreateNewObject()
    {
        GameObject obj = Object.Instantiate(_prefab, _poolParent);
        obj.name = $"{_prefab.name}_{_allObjects.Count}";
        obj.SetActive(false);

        // PooledObject 컴포넌트 추가
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            pooledObj = obj.AddComponent<PooledObject>();
        }
        pooledObj.PoolKey = _poolKey;

        _allObjects.Add(obj);
        _availableObjects.Enqueue(obj);
        return obj;
    }

    public GameObject Get()
    {
        GameObject obj;

        // 사용 가능한 오브젝트가 있으면 재사용
        if (_availableObjects.Count > 0)
        {
            obj = _availableObjects.Dequeue();
        }
        else
        {
            // 없으면 새로 생성 (동적 확장)
            obj = CreateNewObject();
            DebugLogger.LogWarning($"풀 '{_poolKey}'의 오브젝트가 부족하여 동적으로 생성되었습니다.");
        }

        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(_poolParent);
        
        // 중복 반환 방지
        if (!_availableObjects.Contains(obj))
        {
            _availableObjects.Enqueue(obj);
        }
    }

    public void Clear()
    {
        foreach (var obj in _allObjects)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }
        _allObjects.Clear();
        _availableObjects.Clear();
    }
}

/// <summary>
/// 풀링된 오브젝트에 붙는 컴포넌트
/// 어느 풀에 속하는지 추적
/// </summary>
public class PooledObject : MonoBehaviour
{
    public string PoolKey { get; set; }
}