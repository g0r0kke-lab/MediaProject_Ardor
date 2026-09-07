using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.IO;
using System;

/// <summary>
/// 실시간 FPS·프레임 시간·메모리 통계를 화면에 표시하고, 이상 성능 이벤트를 종료 시 타임스탬프 파일로 저장합니다.
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    
    private float deltaTime = 0.0f;
    private float updateInterval = 0.5f;
    private float timeSinceUpdate = 0.0f;
    
    // 평균 계산용
    private Queue<float> fpsHistory = new Queue<float>();
    private const int HISTORY_SIZE = 100; // 100프레임 평균
    
    private const float WARNING_FPS = 45f;
    private const float CRITICAL_FPS = 30f;
    private const float WARNING_MEMORY = 2000f;
    private const float CRITICAL_MEMORY = 3000f;
    
    [SerializeField] private bool isVisible = true;
    [SerializeField] private bool enableLogSaving = false;
    private InputAction toggleVisibilityAction;

    // 통계 데이터 저장용
    private List<string> performanceLog = new List<string>();
    private float sessionStartTime;

    private void Awake()
    {
        // 시작 시 statsText의 활성화 상태 저장
        isVisible = statsText.gameObject.activeSelf;
        
        // ] 키로 토글
        toggleVisibilityAction = new InputAction(
            binding: "<Keyboard>/rightBracket",
            type: InputActionType.Button
        );
        
        toggleVisibilityAction.performed += _ => ToggleVisibility();
        toggleVisibilityAction.Enable();

        // 세션 시작 시간 기록
        sessionStartTime = Time.realtimeSinceStartup;
        performanceLog.Add($"=== Performance Monitor Log ===");
        performanceLog.Add($"Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        performanceLog.Add($"Device: {SystemInfo.deviceModel}");
        performanceLog.Add($"OS: {SystemInfo.operatingSystem}");
        performanceLog.Add($"GPU: {SystemInfo.graphicsDeviceName}");
        performanceLog.Add($"RAM: {SystemInfo.systemMemorySize} MB");
        performanceLog.Add($"================================\n");
    }
    
    // 토글 메서드
    private void ToggleVisibility()
    {
        isVisible = !isVisible;
        statsText.gameObject.SetActive(isVisible);
        DebugLogger.Log($"PerformanceMonitor: {(isVisible ? "표시" : "숨김")}");
    }
    
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float currentFPS = 1.0f / deltaTime;
        fpsHistory.Enqueue(currentFPS);
        if (fpsHistory.Count > HISTORY_SIZE)
            fpsHistory.Dequeue();

        timeSinceUpdate += Time.deltaTime;

        if (timeSinceUpdate >= updateInterval)
        {
            float fps = currentFPS;
            float frameTime = deltaTime * 1000f;
            float memoryUsed = System.GC.GetTotalMemory(false) / 1048576f;
    
            float avgFPS = CalculateAverageFPS();
            float avgFrameTime = (1.0f / avgFPS) * 1000f;
    
            // 이상치만 기록 (평균 대비 30% 이상 벗어날 때)
            if (performanceLog.Count < 1000)
            {
                bool isAnomalous = Mathf.Abs(fps - avgFPS) > avgFPS * 0.3f || // FPS가 평균 대비 ±30% 이상
                                   frameTime > 33.33f || // 30fps 이하로 떨어짐
                                   memoryUsed > WARNING_MEMORY; // 메모리 경고치 초과
            
                if (isAnomalous)
                {
                    performanceLog.Add($"⚠️ [{Time.realtimeSinceStartup - sessionStartTime:F1}s] " +
                                       $"FPS: {fps:F1} (Avg: {avgFPS:F1}), " +
                                       $"FrameTime: {frameTime:F2}ms (Avg: {avgFrameTime:F2}ms), " +
                                       $"Memory: {memoryUsed:F1}MB");
                }
            }
    
            if (isVisible)
            {
                string fpsColor = GetColorTag(fps, CRITICAL_FPS, WARNING_FPS, false);
                string memoryColor = GetColorTag(memoryUsed, WARNING_MEMORY, CRITICAL_MEMORY, true);
                string frameTimeColor = GetColorTag(frameTime, 33.33f, 16.67f, true);
        
                statsText.text = $"<color={fpsColor}>FPS: {fps:F1}</color> (Avg: {avgFPS:F1})\n" +
                                 $"<color={frameTimeColor}>Frame Time: {frameTime:F2}ms</color> (Avg: {avgFrameTime:F2}ms)\n" +
                                 $"<color={memoryColor}>Memory: {memoryUsed:F1} MB</color>";
            }
    
            timeSinceUpdate = 0.0f;
        }
    }
    
    // 평균 FPS 계산
    private float CalculateAverageFPS()
    {
        if (fpsHistory.Count == 0) return 0f;
        
        float sum = 0f;
        foreach (float fps in fpsHistory)
            sum += fps;
        
        return sum / fpsHistory.Count;
    }
    
    private string GetColorTag(float value, float threshold1, float threshold2, bool higherIsBad)
    {
        if (higherIsBad)
        {
            if (value >= threshold2) return "red";
            if (value >= threshold1) return "yellow";
            return "white";
        }
        else
        {
            if (value <= threshold1) return "red";
            if (value <= threshold2) return "yellow";
            return "white";
        }
    }

    // 종료 시에만 평균 계산
    private void OnApplicationQuit()
    {
        if (enableLogSaving)
            SaveLogToFile();
    }

    private void SaveLogToFile()
    {
        try
        {
            string fileName = $"{DateTime.Now:yyMMdd_HHmm}.txt";
            string folderPath = Path.Combine(Application.persistentDataPath, "PerformanceLogs");
        
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        
            string filePath = Path.Combine(folderPath, fileName);
        
            // 최종 통계 추가
            float avgFPS = CalculateAverageFPS();
            float avgFrameTime = (1.0f / avgFPS) * 1000f;
        
            performanceLog.Add($"\n================================");
            performanceLog.Add($"Session End: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            performanceLog.Add($"Total Duration: {Time.realtimeSinceStartup - sessionStartTime:F1}s");
            performanceLog.Add($"Average FPS: {avgFPS:F1}");
            performanceLog.Add($"Average Frame Time: {avgFrameTime:F2}ms");
            performanceLog.Add($"Anomaly Count: {performanceLog.Count - 7}"); // 이상치 발생 횟수
        
            File.WriteAllLines(filePath, performanceLog);
        
            DebugLogger.Log($"[PerformanceMonitor] 로그 저장 완료: {filePath}");
        }
        catch (Exception e)
        {
            DebugLogger.LogError($"[PerformanceMonitor] 로그 저장 실패: {e.Message}");
        }
    }
    
    private void OnDestroy()
    {
        if (toggleVisibilityAction != null)
        {
            toggleVisibilityAction.performed -= _ => ToggleVisibility();
            toggleVisibilityAction.Disable();
            toggleVisibilityAction.Dispose();
        }
    }
}