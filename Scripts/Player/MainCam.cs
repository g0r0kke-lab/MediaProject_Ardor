using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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

        _uiCamera = uiCamObj.GetComponent<Camera>();
        Camera mainCamera = GetComponent<Camera>();

        if (_uiCamera != null && mainCamera != null)
        {
            UniversalAdditionalCameraData cameraData =
                mainCamera.GetUniversalAdditionalCameraData();

            if (!cameraData.cameraStack.Contains(_uiCamera))
            {
                cameraData.cameraStack.Add(_uiCamera);
                DebugLogger.Log("✅ UICamera added to stack!");
            }
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
