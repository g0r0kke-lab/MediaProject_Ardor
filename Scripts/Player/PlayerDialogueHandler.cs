using System.Collections;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

public class PlayerDialogueHandler : MonoBehaviour
{
    [Header("Distance Settings")] [SerializeField]
    private float idealDistance = 2.0f;

    [SerializeField] private float distanceThreshold = 1.0f;

    [Header("Move Settings")] [SerializeField]
    private float walkSpeed = 1.5f;

    [SerializeField] private float minAngleToTurn = 15f;

    private CharacterController _characterController;
    private Animator _animator;
    private PlayerInput _playerInput;
    private ThirdPersonController _thirdPersonController;
    private Transform _dialogueNPC;

    private int _animIDSpeed;
    private int _animIDMotionSpeed;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _playerInput = GetComponent<PlayerInput>();
        _thirdPersonController = GetComponent<ThirdPersonController>();

        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        DebugLogger.Log($"[PlayerDialogueHandler] Animator 찾음: {_animator != null}");
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (EventBroker.Instance == null)
            yield return null;

        EventBroker.Instance.Subscribe("DialogueStart", OnDialogueStart);
        EventBroker.Instance.Subscribe("TurnToNPC", OnTurnToNPC);
        EventBroker.Instance.Subscribe("DialogueEnd", OnDialogueEnd);
    }

    private void OnDisable()
    {
        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Unsubscribe("DialogueStart", OnDialogueStart);
            EventBroker.Instance.Unsubscribe("TurnToNPC", OnTurnToNPC);
            EventBroker.Instance.Unsubscribe("DialogueEnd", OnDialogueEnd);
        }
    }

    private void OnDialogueStart(object data)
    {
        _dialogueNPC = data as Transform;
        if (_dialogueNPC == null) return;

        StartCoroutine(WalkToNPC(true));
    }

    private void OnDialogueEnd()
    {
        DebugLogger.Log("[PlayerDialogueHandler] 대화 종료");
    }

    private void OnTurnToNPC(object data)
    {
        _dialogueNPC = data as Transform;
        if (_dialogueNPC == null) return;

        StartCoroutine(WalkToNPC(false));
    }

    private IEnumerator WalkToNPC(bool openDialogue)
    {
        // 1) 입력 막기
        _playerInput?.actions["Move"].Disable();
        _playerInput?.actions["Jump"].Disable();
        _playerInput?.actions["Sprint"].Disable();

        // 2) 현재 NPC와의 거리 확인
        Vector3 directionToNPC = (_dialogueNPC.position - transform.position).normalized;
        directionToNPC.y = 0;

        float currentDistance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(_dialogueNPC.position.x, 0, _dialogueNPC.position.z)
        );

        // 최소 거리보다 가까우면 회전만
        if (currentDistance <= idealDistance)
        {
            yield return StartCoroutine(TurnToNPC());
            FinishMove(openDialogue);
            yield break;
        }

        // 3) 목표 위치 계산
        Vector3 targetPosition = _dialogueNPC.position - directionToNPC * idealDistance;
        targetPosition.y = transform.position.y;

        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // 이미 적정 거리면 회전만
        if (distanceToTarget < distanceThreshold)
        {
            yield return StartCoroutine(TurnToNPC());
            FinishMove(openDialogue);
            yield break;
        }

        // 4) 걷기 애니메이션 시작
        if (_animator != null)
        {
            _animator.SetFloat(_animIDSpeed, walkSpeed);
            _animator.SetFloat(_animIDMotionSpeed, 1f);
        }

        Quaternion targetRot = Quaternion.LookRotation(directionToNPC);

        // 타임아웃
        float timeout = 4f;
        float elapsed = 0f;

        // 5) 목표 지점까지 걸어가기
        while (distanceToTarget > distanceThreshold && elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // 회전
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * 5f
            );

            // 이동
            Vector3 moveDir = (targetPosition - transform.position).normalized;
            moveDir.y = 0;

            if (_characterController != null && _characterController.enabled)
                _characterController.Move(moveDir * walkSpeed * Time.deltaTime);

            // 애니메이션 유지
            if (_animator != null)
            {
                _animator.SetFloat(_animIDSpeed, walkSpeed);
                _animator.SetFloat(_animIDMotionSpeed, 1f);
            }

            distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            yield return null;
        }
        
        // 타임아웃 로그
        if (elapsed >= timeout)
        {
            DebugLogger.LogWarning($"[PlayerDialogueHandler] 이동 타임아웃 ({timeout}초), 현재 위치에서 진행");
        }
        
        // 타임아웃이든 도착이든 NPC 방향으로 회전
        yield return StartCoroutine(TurnToNPC());

        // 6) 걷기 애니메이션 종료
        if (_animator != null)
        {
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
        }

        FinishMove(openDialogue);
    }

    private IEnumerator TurnToNPC()
    {
        Vector3 direction = (_dialogueNPC.position - transform.position).normalized;
        direction.y = 0;

        float angle = Vector3.Angle(transform.forward, direction);
        if (angle < minAngleToTurn) yield break;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        float duration = Mathf.Clamp(angle / 180f, 0.2f, 0.4f);
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;

        if (_animator != null)
        {
            _animator.SetFloat(_animIDSpeed, walkSpeed * 0.5f);
            _animator.SetFloat(_animIDMotionSpeed, 1f);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.rotation = targetRot;

        if (_animator != null)
        {
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
        }
    }

    private void FinishMove(bool openDialogue)
    {
        if (openDialogue)
        {
            DebugLogger.Log("[PlayerDialogueHandler] DialogueMoveComplete 이벤트 발행!");
            EventBroker.Instance?.Publish("DialogueMoveComplete", null);
        }
        else
        {
            _playerInput?.actions["Move"].Enable();
            _playerInput?.actions["Jump"].Enable();
            _playerInput?.actions["Sprint"].Enable();
        }

        DebugLogger.Log($"[PlayerDialogueHandler] 이동 완료 (대화: {openDialogue})");
    }
}