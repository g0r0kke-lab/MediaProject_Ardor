// SomaShieldController.cs
using UnityEngine;

public class SomaShieldController : MonoBehaviour
{
    [Header("Source")]
    // ✨ Soma_modeling 하위의 모든 SMR 자동 수집
    public Transform somaRoot;

    [Header("Shield Settings")]
    public Material shieldMaterial;
    public EnergyShieldManager ESM;

    [Range(0.0001f, 0.1f)]
    public float inflateAmount = 0.02f;  // 🛡법선 방향 팽창량

    [Header("Idle Effect")]
// 🛡️ 항상 이펙트 재생 여부
    public bool alwaysOn = true;
    [Range(0.1f, 3.0f)]
    public float idleInterval = 1.0f;  // 🛡️ 이펙트 트리거 간격
    
    private MeshFilter _meshFilter;
    private SkinnedMeshRenderer[] _smrs;
    private Mesh[] _bakedMeshes;
    private Mesh _combinedMesh;
    private float _idleTimer;

    void Start()
    {
        float rootScale = somaRoot.root.localScale.x;
        transform.localScale = Vector3.one * (1f / rootScale);
        
        _meshFilter = GetComponent<MeshFilter>();
        GetComponent<MeshRenderer>().material = shieldMaterial;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one; // 🛡scale 고정, inflate로 크기 제어

        _smrs = somaRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
        _bakedMeshes = new Mesh[_smrs.Length];
        for (int i = 0; i < _smrs.Length; i++)
            _bakedMeshes[i] = new Mesh();

        _combinedMesh = new Mesh();
    }

    void Update()
    {
        if (!alwaysOn || ESM == null) return;

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= idleInterval)
        {
            _idleTimer = 0f;
            // 🛡️ 쉴드 메시 위의 랜덤 포인트에 Hit 트리거
            Vector3 randomPoint = _combinedMesh != null && _combinedMesh.vertexCount > 0
                ? transform.TransformPoint(_combinedMesh.vertices[Random.Range(0, _combinedMesh.vertexCount)])
                : transform.position;
            ESM.Hit(randomPoint);
        }
    }
    
    void LateUpdate()
    {
        // ✨ 각 SMR을 Bake 후 하나로 합치기
        for (int i = 0; i < _smrs.Length; i++)
            _smrs[i].BakeMesh(_bakedMeshes[i]);

        CombineMeshes();
    }

    void CombineMeshes()
    {
        CombineInstance[] combine = new CombineInstance[_smrs.Length];
        for (int i = 0; i < _smrs.Length; i++)
        {
            combine[i].mesh = _bakedMeshes[i];
            Matrix4x4 m = _smrs[i].transform.localToWorldMatrix;
            m.SetColumn(0, m.GetColumn(0).normalized);
            m.SetColumn(1, m.GetColumn(1).normalized);
            m.SetColumn(2, m.GetColumn(2).normalized);
            combine[i].transform = transform.worldToLocalMatrix * m;
        }

        _combinedMesh.Clear();
        _combinedMesh.CombineMeshes(combine, true, true);

        // 🛡️ inflate 적용
        if (inflateAmount > 0f)
        {
            Vector3[] verts = _combinedMesh.vertices;
            Vector3[] normals = _combinedMesh.normals;
            for (int i = 0; i < verts.Length; i++)
            {
                // 🛡️ 법선이 안쪽을 향하면(음수 z 또는 내향) 방향 반전
                Vector3 n = normals[i].magnitude > 0.01f ? normals[i] : -normals[i];
                verts[i] += n * inflateAmount;
            }
            _combinedMesh.vertices = verts;
            _combinedMesh.RecalculateBounds();
        }

        _meshFilter.mesh = _combinedMesh;
    }
}