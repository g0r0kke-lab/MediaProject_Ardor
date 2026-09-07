// CinemachineVirtualCamera 에 붙이는 Extension.
// 카메라가 플레이어에게 가까워질수록 Soma_modeling 을 서서히 투명하게 한다.
// 재질은 Custom/URPLitDitherFade 셰이더(Opaque) 여야 함.
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 가상 카메라가 설정 거리보다 가까워질수록 플레이어 캐릭터 모델을 서서히 투명하게 만드는 Cinemachine 확장입니다.
/// </summary>
public class SomaCollisionHider : CinemachineExtension
{
    [SerializeField] private GameObject _somaModeling;
    [Tooltip("이 거리 이하면 완전히 투명")]
    [SerializeField] private float _hideDistance = 1.5f;
    [Tooltip("_hideDistance + 이 값 이상이면 완전히 불투명. 이 사이에서 서서히 페이드.")]
    [SerializeField] private float _fadeRange = 0.5f;
    [Tooltip("본에 직접 붙인 렌더러(생일모자 등) — Soma Modeling 계층 밖에 있는 것을 여기에 할당")]
    [SerializeField] private Renderer[] _boneAttachments;

    private Material[][] _matCache;
    private Material[][] _boneMatCache;
    private float _currentAlpha = 1f;

    protected override void Awake()
    {
        base.Awake();
        CacheMaterials();
        ApplyAlpha(1f);
    }

    private void CacheMaterials()
    {
        if (_somaModeling != null)
        {
            var renderers = _somaModeling.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _matCache = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                // .materials getter는 내부적으로 Instantiate를 호출해 이름에 " (Instance)"를 붙임.
                // new Material()로 직접 복사하면 이름이 보존되어 EyeBlinkAnimator 등 이름 검색이 정상 동작함.
                Material[] shared = renderers[i].sharedMaterials;
                Material[] instances = new Material[shared.Length];
                for (int j = 0; j < shared.Length; j++)
                    instances[j] = shared[j] != null ? new Material(shared[j]) : null;
                renderers[i].sharedMaterials = instances;
                _matCache[i] = instances;
            }
        }

        if (_boneAttachments != null && _boneAttachments.Length > 0)
        {
            _boneMatCache = new Material[_boneAttachments.Length][];
            for (int i = 0; i < _boneAttachments.Length; i++)
            {
                if (_boneAttachments[i] == null) continue;
                Material[] shared = _boneAttachments[i].sharedMaterials;
                Material[] instances = new Material[shared.Length];
                for (int j = 0; j < shared.Length; j++)
                    instances[j] = shared[j] != null ? new Material(shared[j]) : null;
                _boneAttachments[i].sharedMaterials = instances;
                _boneMatCache[i] = instances;
            }
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;
        if (_somaModeling == null || vcam.Follow == null) return;

        Vector3 camPos = state.RawPosition + state.PositionCorrection;
        float dist = Vector3.Distance(camPos, vcam.Follow.position);

        // dist <= _hideDistance          → alpha 0  (완전 투명)
        // dist >= _hideDistance+_fadeRange → alpha 1  (완전 불투명)
        // 그 사이                         → 선형 보간
        float range = Mathf.Max(0.01f, _fadeRange);
        float alpha = Mathf.Clamp01((dist - _hideDistance) / range);

        if (Mathf.Approximately(alpha, _currentAlpha)) return;
        _currentAlpha = alpha;
        ApplyAlpha(_currentAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        if (_matCache != null)
        {
            foreach (var mats in _matCache)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    mat.SetFloat("_Fade", alpha);
                }
            }
        }

        if (_boneMatCache != null)
        {
            foreach (var mats in _boneMatCache)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    mat.SetFloat("_Fade", alpha);
                }
            }
        }
    }
}
