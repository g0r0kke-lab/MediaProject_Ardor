using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	/// <summary>
	/// Unity 새 입력 시스템의 원시 입력값을 수집하고 ThirdPersonController 등 플레이어 컴포넌트에 노출합니다.
	/// </summary>
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool crouch;
		
		private float _sprintAfterCrouchCooldown = 0f;
		private const float SPRINT_AFTER_CROUCH_DELAY = 0.5f;

#if ENABLE_INPUT_SYSTEM
		private InputAction _sprintAction;
#endif

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;
		
		private InputMode lastInputMode;
		
		private float _jumpInputCooldown = 0f;
		private const float JUMP_COOLDOWN_DURATION = 1f;

		public event System.Action<Vector2> onMoveInput;
		private PlayerCrouchDeath _playerCrouchDeath;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			// 쪼그린 상태에서 jump 차단
			if (_playerCrouchDeath != null && _playerCrouchDeath.IsCrouching) return;
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			// 쪼그린 상태 또는 일어서는 중이면 sprint 차단
			if (_playerCrouchDeath != null && (_playerCrouchDeath.IsCrouching || _playerCrouchDeath.IsStandingUp || _sprintAfterCrouchCooldown > 0f)) return;
			SprintInput(value.isPressed);
		}
		
		// PlayerInput 컴포넌트가 Input Action 이름 기반으로 "On"+액션명 메서드를 자동 호출 (SendMessage 방식)
		public void OnCrouch(InputValue value)
		{
			Debug.Log($"OnCrouch called: {value.isPressed}, move: {move}");
			CrouchInput(value.isPressed);
		}
#endif

		// Start 메서드에서 초기 커서 상태 설정
		private void Start()
		{
			// 초기 입력 모드에 따른 커서 상태 설정
			lastInputMode = InputModeManager.GetCurrentMode();
			UpdateCursorState(lastInputMode);
			_playerCrouchDeath = GetComponent<PlayerCrouchDeath>();
			_playerCrouchDeath.OnStandComplete += OnStandComplete;
#if ENABLE_INPUT_SYSTEM
			var playerInput = GetComponent<PlayerInput>();
			if (playerInput != null)
				_sprintAction = playerInput.actions["Sprint"];
#endif
		}
		
		private void Update()
		{
			// 쿨다운 감소
			if (_jumpInputCooldown > 0f)
			{
				_jumpInputCooldown -= Time.unscaledDeltaTime;
			}
			
			// 입력 모드 변경 감지 및 커서 상태 업데이트
			InputMode currentMode = InputModeManager.GetCurrentMode();
			if (currentMode != lastInputMode)
			{
				// UI → Player 전환 시 점프 쿨다운 활성화
				if (lastInputMode == InputMode.UI && currentMode == InputMode.Player)
				{
					_jumpInputCooldown = JUMP_COOLDOWN_DURATION;
					jump = false;
				}
				
				UpdateCursorState(currentMode);
				lastInputMode = currentMode;
			}
			
			// UI 모드일 때는 sprint 값을 false로 유지
			if (InputModeManager.GetCurrentMode() == InputMode.UI)
			{
				sprint = false;
			}
			// 쪼그린 상태 또는 일어서는 중이면 sprint 강제 false
			else if (_playerCrouchDeath != null && (_playerCrouchDeath.IsCrouching || _playerCrouchDeath.IsStandingUp || _sprintAfterCrouchCooldown > 0f))
			{
				sprint = false;
			}
			// 프레임 드랍 시 이벤트 유실로 sprint가 true로 고착되는 버그 방지: 매 프레임 실제 액션 상태로 재검증
#if ENABLE_INPUT_SYSTEM
			else if (sprint && _sprintAction != null && !_sprintAction.IsPressed())
			{
				sprint = false;
			}
#endif
			
			if (_sprintAfterCrouchCooldown > 0f)
				_sprintAfterCrouchCooldown -= Time.unscaledDeltaTime;
		}
		
		// 입력 모드에 따른 커서 상태 업데이트 메서드
		private void UpdateCursorState(InputMode inputMode)
		{
			switch (inputMode)
			{
				case InputMode.UI:
					// UI 모드: 커서를 보이게 하고 자유롭게 움직일 수 있게 함
					SetCursorState(false);
					break;
             
				case InputMode.Player:
				case InputMode.RepelTarget:
				case InputMode.RepelSource:
				default:
					// 다른 모든 모드: 커서를 숨기고 화면 중앙에 고정
					SetCursorState(cursorLocked);
					break;
			}
		}

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
			onMoveInput?.Invoke(move);
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			// 쿨다운 중이면 점프 무시
			if (_jumpInputCooldown > 0f)
			{
				jump = false;
				return;
			}
			
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			// 일어서는 중이면 sprint 차단
			if (_playerCrouchDeath != null && (_playerCrouchDeath.IsStandingUp || _sprintAfterCrouchCooldown > 0f)) return;
			sprint = newSprintState;
		}
		
		public void CrouchInput(bool newCrouchState)
		{
			// pressed 순간에만 토글 요청, bool 누적 없음
			if (newCrouchState)
				_playerCrouchDeath?.ToggleCrouch();
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			// 현재 입력 모드를 고려한 커서 상태 설정
			InputMode currentMode = InputModeManager.GetCurrentMode();
			if (currentMode == InputMode.UI)
			{
				SetCursorState(false); // UI 모드에서는 항상 커서를 보이게 함
			}
			else
			{
				SetCursorState(cursorLocked); // 다른 모드에서는 설정값에 따라
			}
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
		
		private void OnDisable()
		{
			if (_playerCrouchDeath != null)
				_playerCrouchDeath.OnStandComplete -= OnStandComplete;
		}
		
		private void OnStandComplete()
		{
			_sprintAfterCrouchCooldown = SPRINT_AFTER_CROUCH_DELAY;
		}
	}
}