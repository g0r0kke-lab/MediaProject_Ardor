using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    /// <summary>
    /// 다중 입력 모드를 지원하는 3인칭 캐릭터 이동, 점프, 중력, Cinemachine 카메라 회전을 제어합니다.
    /// </summary>
    public class ThirdPersonController : MonoBehaviour
    {
        private bool _allowCameraInput = false;
        private float _cameraDelayTimer = 0.5f;

        [Header("Player")] [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")] [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)] [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;
        private bool _wasGroundedLastFrame = false;
        
        [Tooltip("Useful for rough ground")] public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground (can select multiple)")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;
        
        [Header("Camera Sensitivity")]
        public float MouseSensitivity = 1.0f;
        
        [Header("Debug - Read Only")]
        [Tooltip("List of colliders currently detected as ground")]
        public List<string> CurrentGroundColliders = new List<string>();

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        
        [Header("Animation Layers")]
        private int _upperBodyLayerIndex = -1;
        
        [Header("Character Rotation")]
        [Tooltip("Enable character rotation to follow camera")]
        public bool RotateWithCamera = false;
        [Tooltip("Smooth rotation speed when syncing with camera")]
        public float CameraSyncRotationSpeed = 30f;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private InputModeManager _inputManager;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _inputManager = GetComponent<InputModeManager>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
			DebugLogger.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();
            
            // UpperBody Layer 인덱스 찾기
            if (_hasAnimator)
            {
                _upperBodyLayerIndex = _animator.GetLayerIndex("UpperBody");
                if (_upperBodyLayerIndex >= 0)
                {
                    // 초기에는 Weight 0 (Base Layer만 적용)
                    _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
                }
            }

            // 카메라 타겟을 현재 transform 방향으로 초기화
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _cinemachineTargetPitch = 0f;

            // 초기 카메라 방향 설정
            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.rotation =
                    Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
            }

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            
            StartCoroutine(EnableCameraAfterDelay());
            EventBroker.Instance?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            EventBroker.Instance?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>씬 전환마다 EventBroker 인스턴스가 새로 교체되므로 재구독.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var broker = EventBroker.Instance;
            broker?.Unsubscribe("OnPlayerSpawned", OnPlayerSpawned);
            broker?.Subscribe("OnPlayerSpawned", OnPlayerSpawned);
        }

        private void OnPlayerSpawned(object data)
        {
            ResetCameraToFacing();
        }

        /// <summary>리스폰 후 현재 transform 방향으로 카메라 yaw/pitch를 초기화.</summary>
        public void ResetCameraToFacing()
        {
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _cinemachineTargetPitch = 0f;
            if (CinemachineCameraTarget != null)
                CinemachineCameraTarget.transform.rotation =
                    Quaternion.Euler(CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        private IEnumerator EnableCameraAfterDelay()
        {
            yield return new WaitForSeconds(_cameraDelayTimer);
            _allowCameraInput = true;
        }

        private void Update()
        {
            // 오브젝트가 파괴되었는지 확인
            if (this == null || gameObject == null) return;
    
            JumpAndGravity();
            GroundedCheck();
            Move();
            RotateCharacterWithCamera();
        }

        private void LateUpdate()
        {
            // 오브젝트가 파괴되었는지 확인
            if (this == null || gameObject == null) return;
    
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            bool previousGrounded = Grounded;
            
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // 충돌 중인 Collider들을 리스트에 저장
            CurrentGroundColliders.Clear();
            if (Grounded)
            {
                Collider[] hitColliders = Physics.OverlapSphere(spherePosition, GroundedRadius, GroundLayers,
                    QueryTriggerInteraction.Ignore);
        
                foreach (var col in hitColliders)
                {
                    string layerName = LayerMask.LayerToName(col.gameObject.layer);
                    CurrentGroundColliders.Add($"{col.gameObject.name} (Layer: {layerName})");
                }
            }

            // DebugLogger.Log($"Sphere Position: {spherePosition}, Player Position: {transform.position}");
            //     DebugLogger.Log($"GroundedOffset: {GroundedOffset}, GroundedRadius: {GroundedRadius}");

            // DebugLogger.Log($"Grounded: {Grounded}");

            // 공중에서 지면으로 착지했을 때 이벤트 발행
            if (!previousGrounded && Grounded)
            {
                EventBroker.Instance?.Publish("OnPlayerLanded");
            }
            
            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // UI 모드일 때는 카메라 회전 완전 차단
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.UI)
            {
                return;
            }
    
            // 카메라가 초기화되지 않았다면 입력 무시
            if (!_allowCameraInput)
            {
                return;
            }

            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier * MouseSensitivity;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier * MouseSensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }
        
        private void RotateCharacterWithCamera()
        {
            // 기능이 비활성화되어 있으면 실행하지 않음
            if (!RotateWithCamera) return;
    
            // UI 모드일 때는 회전하지 않음
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.UI) return;
    
            // 이동 입력 완전 차단
            if (_input.move != Vector2.zero)
            {
                _input.move = Vector2.zero;
            }

            // 카메라가 초기화되지 않았다면 실행하지 않음
            if (!_allowCameraInput) return;

            // 부드럽게 회전 (Lerp 사용)
            float currentYRotation = transform.eulerAngles.y;
            float targetYRotation = _cinemachineTargetYaw;
    
            float newYRotation = Mathf.LerpAngle(currentYRotation, targetYRotation, 
                Time.deltaTime * CameraSyncRotationSpeed);
    
            transform.rotation = Quaternion.Euler(0.0f, newYRotation, 0.0f);
        }

        public void SetRotateWithCamera(bool _value)
        {
            RotateWithCamera = _value;
        }

        private void Move()
        {
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.RepelSource)
            {
                _speed = 0f;
                _animationBlend = 0f;
                if (_hasAnimator)
                {
                    _animator.SetFloat(_animIDSpeed, 0f);
                    _animator.SetFloat(_animIDMotionSpeed, 0f);
                }
                return;
            }
            
            // 오브젝트가 파괴되었는지 확인
            if (this == null || transform == null || _controller == null) return;
    
            // 메인 카메라 유효성 검사
            if (_mainCamera == null || _mainCamera.transform == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (_mainCamera == null) return; // 카메라를 찾을 수 없으면 이동 처리 중단
            }
            
            // UI 모드일 때 처리
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.UI)
            {
                // 모든 이동 관련 변수 초기화
                _speed = 0f;
                _animationBlend = 0f;

                // 애니메이션 완전 초기화
                if (_hasAnimator)
                {
                    _animator.SetFloat(_animIDSpeed, 0f);
                    _animator.SetFloat(_animIDMotionSpeed, 0f);
                }

                // HeavyAnchor 부착 중이면 중력 적용도 스킵
                if (MagneticManager.Instance != null && MagneticManager.Instance.CurrentHoldingAnchor is HeavyAnchor)
                    return;

                // 수직 속도 초기화 (바닥에 있으면 -2f, 공중이면 중력 적용)
                if (Grounded && _verticalVelocity > 0.0f)
                    _verticalVelocity = -2f;

                _controller.Move(new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
                return;
            }
            
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                // RepelTarget 모드에서 후진 입력 감지
                bool isBackingUp = _inputManager != null
                                   && _inputManager.CurrentMode == InputMode.RepelTarget
                                   && _input.move.y < 0;

                if (isBackingUp)
                {
                    // 목표 회전은 카메라 방향으로 고정하되, SmoothDamp으로 부드럽게
                    _targetRotation = _cinemachineTargetYaw;
                }
                else
                {
                    _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                                      + _mainCamera.transform.eulerAngles.y;
                }

                // 공통으로 SmoothDamp 적용 (후진/전진 모두)
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation,
                    ref _rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            // 후진 시 이동 방향 반전 처리
            bool isBackingUpDir = _inputManager != null
                                  && _inputManager.CurrentMode == InputMode.RepelTarget
                                  && _input.move.y < 0;

            Vector3 targetDirection;
            if (isBackingUpDir)
            {
                // 캐릭터 뒤쪽 방향으로 이동
                targetDirection = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.back;
            }
            else
            {
                targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            }

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.RepelSource)
            {
                _input.jump = false;
                return;
            }
            
            // UI 모드일 때는 완전히 다른 처리
            if (_inputManager != null && _inputManager.CurrentMode == InputMode.UI)
            {
                _input.jump = false;
                
                // HeavyAnchor 부착 중이면 스킵
                if (MagneticManager.Instance != null && MagneticManager.Instance.CurrentHoldingAnchor is HeavyAnchor)
                    return;
        
                // 바닥에 있으면 수직 속도를 완전히 리셋
                if (Grounded)
                {
                    _verticalVelocity = -2f;
            
                    // 점프 관련 타이머도 리셋
                    _jumpTimeoutDelta = JumpTimeout;
                    _fallTimeoutDelta = FallTimeout;
                }
                // 공중에 있을 때만 중력 적용
                else
                {
                    if (_verticalVelocity < _terminalVelocity)
                    {
                        _verticalVelocity += Gravity * Time.deltaTime;
                    }
                }
        
                // 애니메이션 강제 초기화
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }
        
                return;
            }
            
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _input.jump = false;

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }
        
        /// <summary>
        /// 외부에서 물리 상태를 완전히 리셋 (UI 모드 전환 시 사용)
        /// </summary>
        public void ResetPhysicsState()
        {
            _verticalVelocity = -2f;
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
    
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center),
                        FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center),
                    FootstepAudioVolume);
            }
        }
    }
}