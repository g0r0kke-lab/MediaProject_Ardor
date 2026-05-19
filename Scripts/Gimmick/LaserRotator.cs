using UnityEngine;

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