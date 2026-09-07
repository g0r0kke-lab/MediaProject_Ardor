using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum InputMode
{
    Player,
    UI,
    RepelTarget,
    RepelSource
}

/// <summary>
/// 다양한 입력 모드를 관리하는 매니저 클래스
/// PlayerInput 컴포넌트와 함께 사용하여 ActionMap을 동적으로 전환
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class InputModeManager : MonoBehaviour
{
    public static InputModeManager Instance { get; private set; }
    
    [Header("Current Status (Runtime Only)")] 
    [SerializeField] private InputMode currentMode = InputMode.Player;

    [Header("Settings")] 
    [SerializeField] private bool debugMode = true;

    private PlayerInput _playerInput;
    private Dictionary<InputMode, InputActionMap> _actionMaps;
    private StarterAssets.StarterAssetsInputs _starterInputs;

    public InputMode CurrentMode => currentMode;

    // 모드 변경 이벤트
    public System.Action<InputMode> OnModeChanged;
    
    private static bool _isInitialized = false;
    public static bool IsInitialized() => _isInitialized;

    private void Awake()
    {
        // 싱글톤 패턴 구현 추가
        if (Instance == null)
        {
            Instance = this;
            InitializeComponents();
            InitializeActionMaps();
            SwitchInputMode(InputMode.Player);
            SubscribeToEventBroker();
            _isInitialized = true;
        }
        else
        {
            DebugLogger.LogWarning("InputModeManager 인스턴스가 이미 존재합니다! 중복 제거합니다.");
            Destroy(gameObject);
        }
    }

    private void InitializeComponents()
    {
        _playerInput = GetComponent<PlayerInput>();
        _starterInputs = GetComponent<StarterAssets.StarterAssetsInputs>();

        if (_playerInput == null)
        {
            Debug.LogError("PlayerInput component not found!", this);
            return;
        }

        if (_playerInput.actions == null)
        {
            Debug.LogError("No Input Actions asset assigned to PlayerInput!", this);
            return;
        }

        if (_starterInputs == null)
        {
            Debug.LogWarning("StarterAssetsInputs component not found! Input values won't be cleared on mode switch.", this);
        }
    }

    private void InitializeActionMaps()
    {
        _actionMaps = new Dictionary<InputMode, InputActionMap>();

        // 각 InputMode에 해당하는 ActionMap을 찾아서 등록
        foreach (InputMode mode in System.Enum.GetValues(typeof(InputMode)))
        {
            var actionMap = _playerInput.actions.FindActionMap(mode.ToString());
            if (actionMap != null)
            {
                _actionMaps[mode] = actionMap;
                if (debugMode)
                    DebugLogger.Log($"ActionMap '{mode}' found and registered.");
            }
            else
            {
                DebugLogger.LogWarning($"ActionMap '{mode}' not found in Input Actions asset.");
            }
        }
    }

    /// <summary>
    /// 입력 모드를 전환
    /// </summary>
    /// <param name="newMode">전환할 새로운 모드</param>
    public void SwitchInputMode(InputMode newMode)
    {
        DebugLogger.Log($"[InputModeManager] {currentMode} → {newMode} 전환 요청\n호출 스택:\n{System.Environment.StackTrace}");

        // 커서는 ActionMap 상태와 무관하게 항상 동기화
        // (타임라인이 Cursor.lockState를 강제 잠근 후 얼리 리턴으로 인해 커서가 복구 안 되는 문제 방지)
        if (newMode == InputMode.UI)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (currentMode == newMode)
        {
            if (debugMode)
                DebugLogger.Log($"Already in {newMode} mode.");
            return;
        }
    
        // 이전 모드의 입력값 초기화
        ClearInputValues(currentMode);
    
        // PlayerInput의 currentActionMap 변경
        if (_actionMaps.ContainsKey(newMode))
        {
            // ActionMap 전환 전에 모든 입력 비활성화
            // _playerInput.DeactivateInput();
        
            // Input이 활성화된 상태에서 ActionMap 전환
            if (!_playerInput.inputIsActive)
            {
                _playerInput.ActivateInput();
            }
            
            _playerInput.SwitchCurrentActionMap(newMode.ToString());
            currentMode = newMode;

            // 새로운 모드의 입력값도 초기화
            ClearInputValues(newMode);
        
            // 1프레임 대기 후 입력 재활성화 (코루틴 필요)
            // StartCoroutine(ReactivateInputNextFrame());

            if (debugMode)
                DebugLogger.Log($"Switched to ActionMap: {newMode}");
        }
        else
        {
            DebugLogger.LogWarning($"ActionMap for mode '{newMode}' not found!");
            return;
        }

        OnModeChanged?.Invoke(currentMode);
    }
    
    /// <summary>
    /// 게임 시간을 정지하고 물리 상태를 초기화
    /// </summary>
    public void PauseGame()
    {
        Time.timeScale = 0f;
        ResetPhysicsState();
    
        if (debugMode)
            DebugLogger.Log($"[InputModeManager] Game Paused (TimeScale: {Time.timeScale})");
    }
    
    /// <summary>
    /// 게임 시간을 재개
    /// </summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
    
        if (debugMode)
            DebugLogger.Log($"[InputModeManager] Game Resumed (TimeScale: {Time.timeScale})");
    }
    
    private void SubscribeToEventBroker()
    {
        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Subscribe("SwitchToPlayer", OnSwitchToPlayer);
            EventBroker.Instance.Subscribe("SwitchToUI", OnSwitchToUI);
        
            if (debugMode)
                DebugLogger.Log($"[InputModeManager] EventBroker 구독 완료");
        }
        else
        {
            DebugLogger.LogWarning("[InputModeManager] EventBroker 인스턴스가 없습니다!");
        }
    }

    // Player 모드 전환 핸들러
    private void OnSwitchToPlayer()
    {
        SwitchInputMode(InputMode.Player);
        if (debugMode)
            DebugLogger.Log($"[InputModeManager] EventBroker를 통한 Player 모드 전환");
    }

    // UI 모드 전환 핸들러
    private void OnSwitchToUI()
    {
        SwitchInputMode(InputMode.UI);
        if (debugMode)
            DebugLogger.Log($"[InputModeManager] EventBroker를 통한 UI 모드 전환");
    }

    private IEnumerator ReactivateInputNextFrame()
    {
        yield return null; // 1프레임 대기
        _playerInput.ActivateInput();
    }

    private void ResetPhysicsState()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 모드 전환 시 불필요한 입력값을 초기화
    /// </summary>
    private void ClearInputValues(InputMode newMode)
    {
        if (_starterInputs == null) return;

        switch (newMode)
        {
            case InputMode.UI:
                // UI 모드로 전환할 때 모든 플레이어 입력 초기화
                _starterInputs.look = Vector2.zero;
                _starterInputs.move = Vector2.zero;  // 이동 입력 초기화
                _starterInputs.jump = false;
                _starterInputs.sprint = false;
                break;
                
            case InputMode.RepelTarget:
            case InputMode.RepelSource:
                // RepelTarget/Source 모드는 look 입력을 유지할 수 있음
                break;
                
            case InputMode.Player:
                // Player 모드로 돌아올 때는 look만 초기화 (부드러운 전환을 위해)
                _starterInputs.look = Vector2.zero;
                break;
        }
    }
    
    /// <summary>
    /// 현재 입력 모드 가져오기 (정적 메서드)
    /// </summary>
    public static InputMode GetCurrentMode()
    {
        return Instance != null ? Instance.CurrentMode : InputMode.Player;
    }

    private void OnDestroy()
    {
        if (EventBroker.Instance != null)
        {
            EventBroker.Instance.Unsubscribe("SwitchToPlayer", OnSwitchToPlayer);
            EventBroker.Instance.Unsubscribe("SwitchToUI", OnSwitchToUI);
        }
        _isInitialized = false;

        if (Instance == this)
            Instance = null;
    }

    // ========== Haptic Feedback ==========

    public void PlayActivate(float duration = 0.1f)
    {
        StartCoroutine(VibrateRoutine(0.1f, 0.8f, duration));
    }

    public void PlayDeactivate(float duration = 0.1f)
    {
        StartCoroutine(VibrateRoutine(0.6f, 0.1f, duration));
    }

    public void PlayImpact(float duration = 0.15f)
    {
        StartCoroutine(VibrateRoutine(0.5f, 1.0f, duration));
    }

    private IEnumerator VibrateRoutine(float lowFreq, float highFreq, float duration)
    {
        foreach (var gamepad in Gamepad.all)
            gamepad.SetMotorSpeeds(lowFreq, highFreq);
        yield return new WaitForSeconds(duration);
        foreach (var gamepad in Gamepad.all)
            gamepad.ResetHaptics();
    }
}