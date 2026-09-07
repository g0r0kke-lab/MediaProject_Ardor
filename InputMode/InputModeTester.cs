using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InputModeManager))]
/// <summary>
/// 에디터에서 InputModeManager의 입력 모드 전환을 테스트하기 위한 헬퍼 컴포넌트로, 더블 탭 이스터 에그 토글 기능을 선택적으로 제공합니다.
/// </summary>
public class InputModeTester : MonoBehaviour
{
    [Header("Easter Egg")]
    [SerializeField] private bool enableEasterEgg = false;
    [SerializeField] private GameObject toggleTarget;
    [SerializeField] private float doubleTapInterval = 0.3f;

    private float _lastBacktickTime = -1f;

    private void Update()
    {
        if (!enableEasterEgg || toggleTarget == null) return;

        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            float now = Time.unscaledTime;
            if (now - _lastBacktickTime <= doubleTapInterval)
            {
                toggleTarget.SetActive(!toggleTarget.activeSelf);
                _lastBacktickTime = -1f;
            }
            else
            {
                _lastBacktickTime = now;
            }
        }
    }

#if UNITY_EDITOR
    [Header("Test Controls")]
    [SerializeField] private bool enableTesting = true;
    [SerializeField] private bool showDebugInfo = true;

    private InputModeManager _inputModeManager;
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

    private void OnGUI()
    {
        if (!showDebugInfo || !enableTesting) return;

        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, 10, 200, 30), $"Current Mode: {_inputModeManager.CurrentMode}");

        GUI.color = Color.white;
        GUI.Label(new Rect(10, 40, 200, 20), "1,2,3,4,Q - Switch modes");
    }
#endif
}
