using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 개발자 전용 텔레포트 UI.
/// mapIndex / savePointIndex InputField 입력 후 확인 버튼을 누르면 해당 위치로 이동.
/// 유효하지 않으면 errorObject를 3초간 표시.
/// </summary>
public class DevTeleportUI : MonoBehaviour
{
    [Serializable]
    public struct MapRange
    {
        public int mapIndex;
        public int maxSavePointIndex; // 포함 (예: 4면 0~4까지 유효)
    }

    [Header("맵별 유효 세이브포인트 범위")]
    [SerializeField] private List<MapRange> _mapRanges = new List<MapRange>
    {
        new MapRange { mapIndex = 1, maxSavePointIndex = 2 },
        new MapRange { mapIndex = 2, maxSavePointIndex = 4 },
        new MapRange { mapIndex = 3, maxSavePointIndex = 4 },
    };

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField _mapIndexInput;
    [SerializeField] private TMP_InputField _savePointIndexInput;

    [Header("Buttons")]
    [SerializeField] private Button _confirmButton;

    [Header("Error Feedback")]
    [SerializeField] private GameObject _errorObject; // 유효하지 않을 때 3초간 표시

    private Coroutine _errorRoutine;

    private void Awake()
    {
        if (_errorObject != null)
            _errorObject.SetActive(false);
    }

    private void Start()
    {
        _confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnDestroy()
    {
        _confirmButton.onClick.RemoveListener(OnConfirm);
    }

    private void OnConfirm()
    {
        if (!int.TryParse(_mapIndexInput.text, out int mapIndex) ||
            !int.TryParse(_savePointIndexInput.text, out int savePointIndex))
        {
            ShowError();
            return;
        }

        if (!IsValid(mapIndex, savePointIndex))
        {
            DebugLogger.LogWarning($"[DevTeleport] 유효하지 않은 입력 — mapIndex: {mapIndex}, savePointIndex: {savePointIndex}");
            ShowError();
            return;
        }

        if (!GameManagerRegistry.DevTeleport(mapIndex, savePointIndex))
        {
            ShowError();
            return;
        }

        DevBuildMode.Activate();
        gameObject.SetActive(false);
    }

    private bool IsValid(int mapIndex, int savePointIndex)
    {
        foreach (var range in _mapRanges)
        {
            if (range.mapIndex == mapIndex)
                return savePointIndex >= 0 && savePointIndex <= range.maxSavePointIndex;
        }
        return false; // 등록되지 않은 맵
    }

    private void ShowError()
    {
        if (_errorRoutine != null)
            StopCoroutine(_errorRoutine);
        _errorRoutine = StartCoroutine(ErrorRoutine());
    }

    private IEnumerator ErrorRoutine()
    {
        _errorObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        _errorObject.SetActive(false);
        _errorRoutine = null;
    }
}
