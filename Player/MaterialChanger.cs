using UnityEngine;

/// <summary>
/// SkinnedMeshRenderer 또는 MeshRenderer의 머티리얼 슬롯을 이전/이후 머티리얼로 교체하며, UnityEvent나 코드에서 호출 가능합니다.
/// </summary>
public class MaterialChanger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private SkinnedMeshRenderer targetSkinRenderer;
    [SerializeField] private MeshRenderer targetRenderer;
    
    [Header("Materials")]
    [SerializeField] private Material beforeMaterial;
    [SerializeField] private Material afterMaterial;
    
    // 매터리얼을 이후 매터리얼로 변경하는 함수
    public void ChangeMaterialToAfter()
    {
        if (afterMaterial == null)
        {
            DebugLogger.LogWarning("After Material is not assigned!");
            return;
        }

        // SkinnedMeshRenderer 처리
        if (targetSkinRenderer != null)
        {
            Material[] materials = targetSkinRenderer.materials;
            materials[2] = afterMaterial;
            targetSkinRenderer.materials = materials;
        }
        // MeshRenderer 처리
        else if (targetRenderer != null)
        {
            Material[] materials = targetRenderer.materials;
            materials[0] = afterMaterial;
            targetRenderer.materials = materials;
        }
        else
        {
            DebugLogger.LogWarning("No Renderer is assigned!");
        }
    }

// 매터리얼을 이전 매터리얼로 되돌리는 함수
    public void ChangeMaterialToBefore()
    {
        if (beforeMaterial == null)
        {
            DebugLogger.LogWarning("Before Material is not assigned!");
            return;
        }

        // SkinnedMeshRenderer 처리
        if (targetSkinRenderer != null)
        {
            Material[] materials = targetSkinRenderer.materials;
            materials[2] = beforeMaterial;
            targetSkinRenderer.materials = materials;
        }
        // MeshRenderer 처리
        else if (targetRenderer != null)
        {
            Material[] materials = targetRenderer.materials;
            materials[0] = beforeMaterial;
            targetRenderer.materials = materials;
        }
        else
        {
            DebugLogger.LogWarning("No Renderer is assigned!");
        }
    }
}