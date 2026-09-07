using UnityEngine;

/// <summary>
/// 사인파 함수를 사용하여 레이저 장애물의 회전을 설정 가능한 각도와 속도로 앞뒤로 진동시킵니다.
/// </summary>
public class LaserRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float angle = 70f;
    public float speed = 1f;
    public bool useYAxis = false;

    private Quaternion _startRotation;
    private Quaternion _fromRotation;
    private Quaternion _toRotation;

    void Start()
    {
        _startRotation = transform.rotation;

        Vector3 axis = useYAxis ? Vector3.up : Vector3.right;
        _fromRotation = _startRotation * Quaternion.AngleAxis(-angle / 2f, axis);
        _toRotation   = _startRotation * Quaternion.AngleAxis( angle / 2f, axis);
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
        transform.rotation = Quaternion.Slerp(_fromRotation, _toRotation, t);
    }
}