using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;

/// <summary>
/// Xbox 컨트롤러 입력 테스트용. 빈 GameObject에 붙여서 사용.
/// </summary>
public class GamepadTester : MonoBehaviour
{
    private readonly Queue<string> _log = new Queue<string>();
    private const int MaxLines = 22;
    private GUIStyle _style;

    // 연결된 모든 디바이스 목록 (헤더용)
    private string _deviceList = "";

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Start()
    {
        RefreshDeviceList();
    }

    private void RefreshDeviceList()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var dev in InputSystem.devices)
            sb.AppendLine($"  [{dev.GetType().Name}] {dev.displayName}");
        _deviceList = sb.Length > 0 ? sb.ToString().TrimEnd() : "  (없음)";
        Log($"연결된 디바이스 목록 갱신:\n{_deviceList}");
    }

    private void Update()
    {
        // --- Gamepad 경로로 읽기 ---
        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 ls = pad.leftStick.ReadValue();
            Vector2 rs = pad.rightStick.ReadValue();
            if (ls.sqrMagnitude > 0.01f) Log($"[Gamepad] LeftStick {ls:F2}");
            if (rs.sqrMagnitude > 0.01f) Log($"[Gamepad] RightStick {rs:F2}");

            if (pad.buttonSouth.wasPressedThisFrame)      Log("[Gamepad] A → Jump");
            if (pad.buttonNorth.wasPressedThisFrame)      Log("[Gamepad] Y → Interact");
            if (pad.buttonEast.wasPressedThisFrame)       Log("[Gamepad] B");
            if (pad.buttonWest.wasPressedThisFrame)       Log("[Gamepad] X");
            if (pad.leftStickButton.wasPressedThisFrame)  Log("[Gamepad] L3 → Sprint");
            if (pad.rightStickButton.wasPressedThisFrame) Log("[Gamepad] R3");
            if (pad.leftShoulder.wasPressedThisFrame)     Log("[Gamepad] LB");
            if (pad.rightShoulder.wasPressedThisFrame)    Log("[Gamepad] RB");
            if (pad.startButton.wasPressedThisFrame)      Log("[Gamepad] Start");
            if (pad.selectButton.wasPressedThisFrame)     Log("[Gamepad] Select/Back");
            float lt = pad.leftTrigger.ReadValue();
            float rt = pad.rightTrigger.ReadValue();
            if (lt > 0.1f) Log($"[Gamepad] LT {lt:F2}");
            if (rt > 0.1f) Log($"[Gamepad] RT {rt:F2}");
        }

        // --- Joystick 경로로 읽기 (Xbox가 Joystick으로 잡힌 경우) ---
        var joy = Joystick.current;
        if (joy != null)
        {
            Vector2 stick = joy.stick.ReadValue();
            if (stick.sqrMagnitude > 0.01f) Log($"[Joystick] stick {stick:F2}");

            // 버튼 순회
            foreach (var ctrl in joy.allControls)
            {
                if (ctrl is ButtonControl btn && btn.wasPressedThisFrame)
                    Log($"[Joystick] 버튼 눌림: {ctrl.name} / path: {ctrl.path}");
            }
        }

        // R키: 디바이스 목록 재출력
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RefreshDeviceList();

        // S키: 현재 PlayerInput Control Scheme 출력
        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            var pi = FindObjectOfType<PlayerInput>();
            Log(pi != null ? $"PlayerInput Scheme: {pi.currentControlScheme}" : "PlayerInput 없음");
        }
    }

    private void Log(string msg)
    {
        string line = $"[{Time.time:F1}s] {msg}";
        _log.Enqueue(line);
        Debug.Log("[GamepadTester] " + msg);
        while (_log.Count > MaxLines) _log.Dequeue();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        Log($"디바이스: [{device.GetType().Name}] {device.displayName} → {change}");
        RefreshDeviceList();
    }

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false };
            _style.normal.textColor = Color.white;
        }

        float w = 560f, lineH = 20f;
        float h = (MaxLines + 2) * lineH + 10f;

        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.Box(new Rect(10, 10, w, h), "");
        GUI.color = Color.white;

        float y = 14f;
        GUI.Label(new Rect(16, y, w, lineH),
            $"Gamepad.current: {(Gamepad.current != null ? Gamepad.current.displayName : "null")}  " +
            $"Joystick.current: {(Joystick.current != null ? Joystick.current.displayName : "null")}", _style);
        y += lineH;
        GUI.Label(new Rect(16, y, w, lineH), "[R키] 디바이스 새로고침  [S키] Control Scheme 출력", _style);
        y += lineH + 2f;

        foreach (string line in _log)
        {
            GUI.Label(new Rect(16, y, w, lineH), line, _style);
            y += lineH;
        }
    }
}
