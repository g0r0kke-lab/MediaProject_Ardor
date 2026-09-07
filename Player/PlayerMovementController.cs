using System.Collections;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 특수 이동 행동을 제어하는 컴포넌트
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;

    private ThirdPersonController _thirdPersonController;
    private CharacterController _characterController;
    [SerializeField, ReadOnly] private Animator _animator;
    private bool _isMoving = false;

    // 애니메이션 ID
    private int _animIDSpeed;
    private int _animIDMotionSpeed;
    [SerializeField, ReadOnly] private int _animIDBackwalk; // 추가

    private void Awake()
    {
        _thirdPersonController = GetComponent<ThirdPersonController>();
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();

        // Animator 확인
        DebugLogger.Log($"Animator 찾음: {_animator != null}");
        if (_animator != null)
        {
            DebugLogger.Log($"Animator GameObject: {_animator.gameObject.name}");

            // 파라미터 리스트 출력
            foreach (var param in _animator.parameters)
            {
                DebugLogger.Log($"파라미터: {param.name} (Hash: {Animator.StringToHash(param.name)})");
            }
        }

        // 애니메이션 파라미터 ID 할당
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        _animIDBackwalk = Animator.StringToHash("IsBackwalk"); // 추가

        DebugLogger.Log($"IsBackwalk Hash: {_animIDBackwalk}");
    }

    private bool _subscribed = false;

    private void Update()
    {
        if (!_subscribed && EventBroker.Instance != null)
        {
            EventBroker.Instance.Subscribe("SkiaSpawn", OnSkiaSpawnDelayed);
            _subscribed = true; // 이후로 다시 안 들어감
        }
    }

    // 이벤트를 받으면, 2초 기다렸다가 실행
    private void OnSkiaSpawnDelayed()
    {
        StartCoroutine(DelayedStartBackwardWalk());
    }

    // 코루틴으로 2초 대기 후 실행
    private IEnumerator DelayedStartBackwardWalk()
    {
        DebugLogger.Log("SkiaSpawn 이벤트 수신 → 2초 대기 중...");
        yield return new WaitForSeconds(0f); 
        StartBackwardWalk(0,1);
    }

    // <summary>
    /// 세이브포인트로 텔레포트 후 목표 지점까지 뒷걸음질 이동
    /// </summary>
    /// <param name="startTransformIdx">시작 트랜스폼 인덱스</param>
    /// <param name="endTransformIdx">끝 트랜스폼 인덱스</param>
    public void StartBackwardWalk(int startTransformIdx, int endTransformIdx) // 매개변수 변경
    {
        if (_isMoving)
        {
            DebugLogger.LogWarning("이미 이동 중입니다.");
            return;
        }

        StartCoroutine(BackwardWalkCoroutine(startTransformIdx, endTransformIdx)); // 매개변수 전달
    }


    private IEnumerator BackwardWalkCoroutine(int startTransformIdx, int endTransformIdx) // 매개변수명 수정
    {
        _isMoving = true;

        // GameManager로부터 트랜스폼 가져오기
        var gameManager = GameManagerRegistry.currentManager;

        if (gameManager == null)
        {
            DebugLogger.LogError("GameManagerRegistry에 등록된 GameManager가 없습니다!");
            _isMoving = false;
            yield break;
        }

        var teleportTransformsField = gameManager.GetType().GetField("teleportTransforms",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (teleportTransformsField == null)
        {
            DebugLogger.LogError("teleportTransforms 필드를 찾을 수 없습니다!");
            _isMoving = false;
            yield break;
        }

        var teleportTransforms =
            teleportTransformsField.GetValue(gameManager) as System.Collections.Generic.List<Transform>;

        if (teleportTransforms == null || startTransformIdx < 0 || startTransformIdx >= teleportTransforms.Count
            || endTransformIdx < 0 || endTransformIdx >= teleportTransforms.Count)
        {
            DebugLogger.LogError($"유효하지 않은 트랜스폼 인덱스: Start={startTransformIdx}, End={endTransformIdx}");
            _isMoving = false;
            yield break;
        }

        Transform startTransform = teleportTransforms[startTransformIdx];
        Transform endTransform = teleportTransforms[endTransformIdx];

        if (startTransform == null || endTransform == null)
        {
            DebugLogger.LogError("트랜스폼이 null입니다!");
            _isMoving = false;
            yield break;
        }

        // 텔레포트 메서드 호출
        TeleportToTransform(startTransform);

        // 착지 대기
        float timer = 0f;
        while (!_thirdPersonController.Grounded && timer < 1f)
        {
            _characterController.Move(Vector3.down * 5f * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForEndOfFrame();

        Vector3 targetPos = endTransform.position; // 끝 트랜스폼 위치

        // 이동 방향의 반대 방향 바라보기 (뒷걸음질이므로)
        Vector3 moveDirection = (targetPos - transform.position).normalized;
        Quaternion fixedRotation = Quaternion.LookRotation(-moveDirection);
        transform.rotation = fixedRotation;

        // 백워크 애니메이션 시작
        if (_animator != null)
        {
            _animator.SetBool(_animIDBackwalk, true);
        }

        // CharacterController 사용하여 이동
        Vector3 startPos = transform.position;
        float elapsedTime = 0f;
        float duration = Vector3.Distance(startPos, targetPos) / moveSpeed;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration); // 1.0 초과 방지

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            Vector3 movement = newPos - transform.position;

            _characterController.Move(movement);
            transform.rotation = fixedRotation;

            // 걷기 애니메이션 재생
            if (_animator != null)
            {
                _animator.SetFloat(_animIDSpeed, moveSpeed);
                _animator.SetFloat(_animIDMotionSpeed, 1f);
            }

            yield return null;
        }

        float smoothDistance = Vector3.Distance(transform.position, targetPos);
        if (smoothDistance > 0.01f && smoothDistance < 0.5f) // 짧은 거리만 보정
        {
            Vector3 smoothStart = transform.position;
            float smoothTime = 0f;
            float smoothDuration = 0.5f;

            while (smoothTime < smoothDuration)
            {
                smoothTime += Time.deltaTime;
                _characterController.enabled = false;
                transform.position = Vector3.Lerp(smoothStart, targetPos, smoothTime / smoothDuration);
                _characterController.enabled = true;

                yield return null;
            }
        }

        // 백워크 애니메이션 종료
        if (_animator != null)
        {
            _animator.SetBool(_animIDBackwalk, false);
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
        }

        _isMoving = false;
        DebugLogger.Log($"뒷걸음질 이동 완료. 최종 위치: {transform.position}");
    }

    /// <summary>
    /// 지정된 트랜스폼으로 플레이어 텔레포트
    /// </summary>
    private void TeleportToTransform(Transform targetTransform) // 새로 추가
    {
        if (targetTransform == null)
        {
            DebugLogger.LogError("텔레포트할 트랜스폼이 null입니다!");
            return;
        }

        Vector3 teleportPosition = targetTransform.position;
        Quaternion teleportRotation = targetTransform.rotation;

        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.SetPositionAndRotation(teleportPosition, teleportRotation);
            _characterController.enabled = true;

            // 텔레포트 직후 강제로 아래로 이동 (착지 시뮬레이션)
            _characterController.Move(Vector3.down * 0.1f);
        }
        else
        {
            transform.SetPositionAndRotation(teleportPosition, teleportRotation);
        }

        DebugLogger.Log($"트랜스폼으로 텔레포트 완료: {teleportPosition}");
    }

    /// <summary>
    /// 현재 이동 중인지 확인
    /// </summary>
    public bool IsMoving => _isMoving;

    /// <summary>
    /// 강제로 이동 중단
    /// </summary>
    public void StopMovement()
    {
        StopAllCoroutines();
        _isMoving = false;

        if (_animator != null)
        {
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
        }
    }
}