using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 로드·언로드 시 URP 카메라 스택에 UI 오버레이 카메라를 추가하거나 제거합니다.
/// </summary>
public class MainCam : MonoBehaviour
{
    private Camera _uiCamera;
    
    void Start()
    {
        // 성공 여부 무관하게 이벤트 구독 유지
        TrySetupUICamera();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 성공해도 구독 해제 안 함
        TrySetupUICamera();
    }

    private bool TrySetupUICamera()
    {
        GameObject uiCamObj = GameObject.Find("UICamera");
        if (uiCamObj == null) return false;

        Camera newUiCamera = uiCamObj.GetComponent<Camera>();
        Camera mainCamera = GetComponent<Camera>();

        if (newUiCamera != null && mainCamera != null)
        {
            UniversalAdditionalCameraData cameraData =
                mainCamera.GetUniversalAdditionalCameraData();

            // 씬 전환으로 파괴된(null이 된) 이전 UICamera 참조 및
            // 갱신 전 이전 UICamera 참조를 스택에서 제거
            cameraData.cameraStack.RemoveAll(cam => cam == null || cam == _uiCamera);

            _uiCamera = newUiCamera;

            // Overlay 카메라는 인스펙터에 Output 항목이 숨겨져 있어도
            // Camera 컴포넌트 자체의 값은 Base와 별개로 저장되어 있어 자동 동기화되지 않음.
            // URP가 Base와 다르면 경고를 띄우므로 Base 기준으로 맞춰준다.
            _uiCamera.allowHDR = mainCamera.allowHDR;
            _uiCamera.allowMSAA = mainCamera.allowMSAA;
            _uiCamera.targetDisplay = mainCamera.targetDisplay;
            _uiCamera.targetTexture = mainCamera.targetTexture;

            if (!cameraData.cameraStack.Contains(_uiCamera))
            {
                cameraData.cameraStack.Add(_uiCamera);
                DebugLogger.Log("✅ UICamera added to stack!");
            }

            // 새 UICamera가 추가됐으므로 rect 재적용 (씬 전환 시 새 카메라 rect가 기본값으로 초기화되는 문제 방지)
            GetComponent<CameraAspectRatioFitter>()?.ForceApply();

            return true;
        }
        return false;
    }

    
    // 파괴될 때 스택에서 제거
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        if (_uiCamera != null)
        {
            Camera mainCamera = GetComponent<Camera>();
            if (mainCamera != null)
            {
                UniversalAdditionalCameraData cameraData = 
                    mainCamera.GetUniversalAdditionalCameraData();
                
                if (cameraData.cameraStack.Contains(_uiCamera))
                {
                    cameraData.cameraStack.Remove(_uiCamera);
                    DebugLogger.Log("🗑️ UICamera removed from stack!");
                }
            }
        }
    }
}
