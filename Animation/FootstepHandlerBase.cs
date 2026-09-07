using UnityEngine;

/// <summary>
/// 왼발, 오른발, 착지 발소리 콜백 인터페이스를 정의하는 발소리 처리기의 추상 기반 클래스입니다.
/// </summary>
public abstract class FootstepHandlerBase : MonoBehaviour
{
    public abstract void OnFootstepLeft();
    public abstract void OnFootstepRight();
    public abstract void OnFootstepGrounded();
}