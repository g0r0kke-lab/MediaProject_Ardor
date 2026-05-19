using System.Collections;
using UnityEngine;

public class PushStateBehaviour : StateMachineBehaviour
{
    public float weightBlendTime = 0.3f;
    private Coroutine blendCoroutine;

    // Push 상태 진입 시 - 입력 차단 플래그 설정
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("IsPulling", false);
        // Push 애니메이션 재생 중임을 알림
        animator.SetBool("IsPushing", true);
    }
    
    // Push 상태가 끝날 때 - 입력 차단 해제
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Push 애니메이션 종료를 알림
        animator.SetBool("IsPushing", false);
        
        // Pull 상태로 전환 중이면 BlendOut 하지 않음
        if (animator.GetBool("IsPulling"))
        {
            // Pull이 시작되면 Weight는 PullStateBehaviour가 관리
            return;
        }
        
        // Weight는 Coroutine으로 부드럽게 감소
        MonoBehaviour mb = animator.GetComponent<MonoBehaviour>();
        if (mb != null)
        {
            mb.StartCoroutine(BlendOutUpperBodyLayer(animator, weightBlendTime));
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
            // BlendOut 중에 Pull이 시작되면 즉시 중단
            if (animator.GetBool("IsPulling"))
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