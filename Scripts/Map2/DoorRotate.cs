using System.Collections;
using UnityEngine;

public class DoorRotate : MonoBehaviour
{
    [Header("ȸ�� ����")]
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 1f, 0f);

    [Header("�̺�Ʈ ����")]
    [SerializeField] private string openEventKey = "OpenTunnelDoor";
    [SerializeField] private string closeEventKey = "";
    
    private bool isOpen = false;
    private bool isRotating = false;
    private Quaternion closedRotation;
    private Quaternion openedRotation;

    private void Awake()
    {
        closedRotation = transform.rotation;
        openedRotation = closedRotation * Quaternion.Euler(rotationAxis * openAngle);
    }

    public void OpenDoor()
    {
        if (isOpen || isRotating) return;
        StartCoroutine(RotateDoor(openedRotation));
        isOpen = true;
    }

    public void CloseDoor()
    {
        if (!isOpen || isRotating) return;
        StartCoroutine(RotateDoor(closedRotation));
        isOpen = false;
    }

    private IEnumerator RotateDoor(Quaternion targetRotation)
    {
        isRotating = true;
        Quaternion startRotation = transform.rotation;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        transform.rotation = targetRotation;
        isRotating = false;
    }

    private void OnEnable()
    {
        EventBroker.Instance?.Subscribe(openEventKey, OpenDoor);
        EventBroker.Instance?.Subscribe(closeEventKey, CloseDoor);
    }

    private void OnDisable()
    {
        EventBroker.Instance?.Unsubscribe(openEventKey, OpenDoor);
        EventBroker.Instance?.Unsubscribe(closeEventKey, CloseDoor);
    }
}