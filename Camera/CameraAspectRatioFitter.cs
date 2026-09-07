using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 카메라의 렌더 영역을 지정된 비율(기본 16:9)로 고정한다.
/// 화면 비율이 더 넓으면 좌우 필러박스, 더 좁으면 상하 레터박스를 적용한다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraAspectRatioFitter : MonoBehaviour
{
    [SerializeField] private float targetAspect = 16f / 9f;

    private Camera _camera;
    private Camera _letterboxBgCamera;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        SetupLetterboxBackground();
    }

    private void Start()
    {
        ApplyAspectRatio();
    }

    private void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            ApplyAspectRatio();
        }
    }

    // 씬 전환 후 새 Overlay 카메라가 스택에 추가됐을 때 rect 재적용
    public void ForceApply()
    {
        ApplyAspectRatio();
    }

    private void ApplyAspectRatio()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect;
        if (scaleHeight < 1f)
        {
            // 화면이 목표 비율보다 좁음 (세로로 김) -> 상하 레터박스
            rect = new Rect(0f, (1f - scaleHeight) / 2f, 1f, scaleHeight);
        }
        else
        {
            // 화면이 목표 비율보다 넓음 -> 좌우 필러박스
            float scaleWidth = 1f / scaleHeight;
            rect = new Rect((1f - scaleWidth) / 2f, 0f, scaleWidth, 1f);
        }

        _camera.rect = rect;
        ApplyToOverlayCameras(rect);
    }

    private void ApplyToOverlayCameras(Rect rect)
    {
        UniversalAdditionalCameraData cameraData = _camera.GetUniversalAdditionalCameraData();
        if (cameraData == null) return;

        foreach (Camera overlayCamera in cameraData.cameraStack)
        {
            if (overlayCamera != null)
                overlayCamera.rect = rect;
        }
    }

    // 레터박스/필러박스 영역을 검은색으로 채우는 배경 카메라 생성
    private void SetupLetterboxBackground()
    {
        if (_letterboxBgCamera != null) return;

        GameObject bgObj = new GameObject("LetterboxBackground");
        bgObj.transform.SetParent(transform.parent);

        _letterboxBgCamera = bgObj.AddComponent<Camera>();
        _letterboxBgCamera.clearFlags = CameraClearFlags.SolidColor;
        _letterboxBgCamera.backgroundColor = Color.black;
        _letterboxBgCamera.cullingMask = 0;
        _letterboxBgCamera.depth = _camera.depth - 1f;
        _letterboxBgCamera.rect = new Rect(0f, 0f, 1f, 1f);
        _letterboxBgCamera.allowHDR = false;
        _letterboxBgCamera.allowMSAA = false;
    }

    private void OnDestroy()
    {
        if (_letterboxBgCamera != null)
        {
            Destroy(_letterboxBgCamera.gameObject);
        }
    }
}
