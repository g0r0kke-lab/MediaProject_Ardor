using System.Collections;
using UnityEngine;

/// <summary>
/// 당기기 애니메이션 진입 시 UpperBody 레이어 가중치를 즉시 1로 설정하고, 종료 시 부드럽게 블렌드 아웃하는 Animator StateMachineBehaviour입니다.
/// </summary>
public class PullStateBehaviour : StateMachineBehaviour
{
    public float weightBlendTime = 0.05f;
    
    // Idle 상태의 해시값 (Inspector에서 설정 가능하도록)
    private static readonly int HashIdle = Animator.StringToHash("Idle");
    
    // Pull 상태 진입 시 - Weight 즉시 1로 복구
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int upperBodyLayer = animator.GetLayerIndex("UpperBody");
        if (upperBodyLayer >= 0)
        {
            // Pull 애니메이션이 보이도록 Weight 즉시 복구
            animator.SetLayerWeight(upperBodyLayer, 1f);
        }
    }
    
    // Pull 상태 종료 시 - 다음 상태 확인
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Push 상태로 전환 중이면 가중치 유지
        if (animator.GetBool("IsPushing"))
        {
            return;
        }
        
        // 다음 상태가 Idle인지 확인
        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
        
        // Idle로 전환되는 경우에만 Weight 감소
        if (nextState.shortNameHash == HashIdle || !animator.GetBool("IsPulling"))
        {
            MonoBehaviour mb = animator.GetComponent<MonoBehaviour>();
            if (mb != null)
            {
                mb.StartCoroutine(BlendOutUpperBodyLayer(animator, weightBlendTime));
            }
        }
    }
    
    // Weight를 서서히 줄이는 Coroutine
    private IEnumerator BlendOutUpperBodyLayer(Animator animator, float duration)
    {
        int upperBodyLayer = animator.GetLayerIndex("UpperBody");
        if (upperBodyLayer < 0) yield break;
        
        float elapsed = 0f;
        float startWeight = animator.GetLayerWeight(upperBodyLayer);
        
        while (elapsed < duration)
        {
            // BlendOut 중에 Pull이나 Push가 다시 시작되면 중단
            if (animator.GetBool("IsPulling") || animator.GetBool("IsPushing"))
            {
                yield break;
            }
            
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            animator.SetLayerWeight(upperBodyLayer, Mathf.Lerp(startWeight, 0f, t));
            yield return null;
        }
        
        animator.SetLayerWeight(upperBodyLayer, 0f);
    }
}
