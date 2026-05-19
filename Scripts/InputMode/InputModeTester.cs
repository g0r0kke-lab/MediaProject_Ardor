#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InputModeManager 테스트용 스크립트
/// 키보드로 4개 모드를 간단하게 전환할 수 있습니다
/// </summary>
[RequireComponent(typeof(InputModeManager))]
public class InputModeTester : MonoBehaviour
{
    [Header("Test Controls")]
    [SerializeField] private bool enableTesting = true;
    [SerializeField] private bool showDebugInfo = true;
    
    private InputModeManager _inputModeManager;
    
    // Input Actions
    private InputActionMap _testActionMap;
    private InputAction _toggleModeAction;
    
    private void Awake()
    {
        _inputModeManager = GetComponent<InputModeManager>();
        
        if (_inputModeManager == null)
        {
            Debug.LogError("InputModeManager not found!", this);
            enabled = false;
            return;
        }
        
        if (enableTesting)
        {
            CreateTestInputActions();
        }
    }
    
    private void CreateTestInputActions()
    {
        var testInputActions = ScriptableObject.CreateInstance<InputActionAsset>();
        _testActionMap = testInputActions.AddActionMap("TestControls");
    
        // 0번 키로 Player ↔ UI 토글
        _toggleModeAction = _testActionMap.AddAction("ToggleMode", InputActionType.Button);
        _toggleModeAction.AddBinding("<Keyboard>/0");
        _toggleModeAction.performed += ctx =>
        {
            if (_inputModeManager.CurrentMode == InputMode.Player)
                _inputModeManager.SwitchInputMode(InputMode.UI);
            else
                _inputModeManager.SwitchInputMode(InputMode.Player);
        };
    }
    
    private void OnEnable()
    {
        if (enableTesting)
        {
            _testActionMap?.Enable();
            
            if (showDebugInfo)
            {
                DebugLogger.Log("=== InputMode Test Controls ===");
                DebugLogger.Log("1 - Player Mode");
                DebugLogger.Log("2 - UI Mode");
                DebugLogger.Log("3 - RepelTarget Mode");
                DebugLogger.Log("4 - RepelSource Mode");
                DebugLogger.Log("Q - Quick UI Mode");
                DebugLogger.Log("==============================");
            }
        }
        
        if (_inputModeManager != null)
        {
            _inputModeManager.OnModeChanged += OnModeChanged;
        }
    }
    
    private void OnDisable()
    {
        _testActionMap?.Disable();
        
        if (_inputModeManager != null)
        {
            _inputModeManager.OnModeChanged -= OnModeChanged;
        }
    }
    
    private void OnModeChanged(InputMode newMode)
    {
        if (showDebugInfo)
        {
            DebugLogger.Log($"Mode switched to: {newMode}");
        }
    }
    
    // GUI로 현재 모드만 간단히 표시
    private void OnGUI()
    {
        if (!showDebugInfo || !enableTesting) return;
        
        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, 10, 200, 30), $"Current Mode: {_inputModeManager.CurrentMode}");
        
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 40, 200, 20), "1,2,3,4,Q - Switch modes");
    }
}
#endif