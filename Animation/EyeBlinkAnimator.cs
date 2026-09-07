using UnityEngine;

namespace KHJ.Animation
{
    /// <summary>
    /// URP/Lit 머티리얼의 _EmissionMap을 시간에 따라 교체해 눈 깜빡임을 표현한다.
    /// 눈뜸(대기) -> 중간뜸 -> 감음 -> 중간뜸 -> 눈뜸 순으로 반복된다.
    /// EyeSet_Broken / EyeSet_Repaired 이벤트로 텍스쳐 세트를 전환한다.
    /// </summary>
    public class EyeBlinkAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct EyeTextureSet
        {
            public Texture eyeOpen;
            public Texture eyeHalf;
            public Texture eyeClosed;
        }

        [SerializeField] private Renderer targetRenderer;

        [Tooltip("교체할 머티리얼의 이름 (sharedMaterials 중 이 이름을 가진 슬롯을 찾아서 적용)")]
        [SerializeField] private string materialName = "face_s";

        private int materialIndex = -1;

        [Header("Eye Texture Sets")]
        [SerializeField] private EyeTextureSet repairedTextures;
        [SerializeField] private EyeTextureSet brokenTextures;

        [Tooltip("눈을 뜨고 있는 평균 대기 시간(초)")]
        [SerializeField] private float openHoldTime = 3f;

        [Tooltip("깜빡임 중 중간뜸/감음 각 단계가 유지되는 시간(초)")]
        [SerializeField] private float frameTime = 0.08f;

        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

        private MaterialPropertyBlock _propertyBlock;
        private Texture _currentTexture;
        private bool _isBroken = false;

        private EyeTextureSet CurrentSet => _isBroken ? brokenTextures : repairedTextures;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name == materialName)
                {
                    materialIndex = i;
                    break;
                }
            }

            if (materialIndex < 0)
                Debug.LogWarning($"[EyeBlinkAnimator] '{materialName}' 머티리얼을 {targetRenderer.name}에서 찾지 못했습니다.", this);
        }

        private void OnEnable()
        {
            if (EventBroker.Instance != null)
            {
                EventBroker.Instance.Subscribe("EyeSet_Broken", OnSetBroken);
                EventBroker.Instance.Subscribe("EyeSet_Repaired", OnSetRepaired);
            }
        }

        private void OnDisable()
        {
            if (EventBroker.Instance != null)
            {
                EventBroker.Instance.Unsubscribe("EyeSet_Broken", OnSetBroken);
                EventBroker.Instance.Unsubscribe("EyeSet_Repaired", OnSetRepaired);
            }
        }

        private void OnSetBroken()
        {
            if (_isBroken) return;
            _isBroken = true;
            _currentTexture = null;
        }

        private void OnSetRepaired()
        {
            if (!_isBroken) return;
            _isBroken = false;
            _currentTexture = null;
        }

        private void Update()
        {
            if (materialIndex < 0)
                return;

            Texture texture = GetTextureForTime(Time.time);
            if (texture == _currentTexture)
                return;

            _currentTexture = texture;
            targetRenderer.GetPropertyBlock(_propertyBlock, materialIndex);
            _propertyBlock.SetTexture(EmissionMapId, texture);
            targetRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
        }

        private Texture GetTextureForTime(float time)
        {
            EyeTextureSet set = CurrentSet;
            float cycle = openHoldTime + frameTime * 4f;
            float t = time % cycle;

            if (t < openHoldTime)
                return set.eyeOpen;

            float local = t - openHoldTime;
            if (local < frameTime)
                return set.eyeHalf;
            if (local < frameTime * 2f)
                return set.eyeClosed;
            if (local < frameTime * 3f)
                return set.eyeHalf;
            return set.eyeOpen;
        }
    }
}
