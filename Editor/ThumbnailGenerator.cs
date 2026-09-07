#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 3D 모델 프리팹을 투명 배경 PNG 썸네일로 생성하는 에디터 윈도우로, 카메라 각도·줌·해상도를 설정할 수 있습니다.
/// </summary>
public class ItemThumbnailGenerator : EditorWindow
{
    private GameObject targetModel;
    private int thumbnailSize = 256;
    private string savePath = "Assets/KHJ/Thumbnails/";

    // 카메라 각도 옵션
    private enum CameraAngle
    {
        Default, // 유니티 기본 (비스듬히)
        Front, // 정면
        Top, // 위에서 내려다보기
        Side // 옆에서
    }

    private CameraAngle selectedAngle = CameraAngle.Default;
    private int rotationMultiplier = 0; // 90도 회전 배수 (0, 1, 2, 3)
    private float zoomPercent = 0f;  // 음수 = 확대, 양수 = 축소(여백)

    [MenuItem("ARDORBIT/Item Thumbnail Generator")]
    public static void ShowWindow()
    {
        GetWindow<ItemThumbnailGenerator>("썸네일 생성기");
    }

    void OnGUI()
    {
        GUILayout.Label("3D 모델 썸네일 생성 (투명 배경)", EditorStyles.boldLabel);

        targetModel = (GameObject)EditorGUILayout.ObjectField("3D 모델", targetModel, typeof(GameObject), false);
        thumbnailSize = EditorGUILayout.IntField("썸네일 크기", thumbnailSize);
        savePath = EditorGUILayout.TextField("저장 경로", savePath);

        EditorGUILayout.Space();
        GUILayout.Label("카메라 각도", EditorStyles.boldLabel);
        selectedAngle = (CameraAngle)EditorGUILayout.EnumPopup("각도 선택", selectedAngle);
        rotationMultiplier = EditorGUILayout.IntSlider("이미지 회전 (90도 배수)", rotationMultiplier, 0, 3);

        EditorGUILayout.Space();
        GUILayout.Label("줌 설정", EditorStyles.boldLabel);
        zoomPercent = EditorGUILayout.Slider("줌 (-확대 / +축소)", zoomPercent, -30f, 40f);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("투명 배경 + 선택한 각도로 썸네일을 생성합니다.\n회전: 0=없음, 1=90°, 2=180°, 3=270°\n여백: 오브젝트 주변 투명 공간", MessageType.Info);

        if (GUILayout.Button("썸네일 생성", GUILayout.Height(30)))
        {
            if (targetModel != null)
            {
                GenerateThumbnail();
            }
            else
            {
                EditorUtility.DisplayDialog("오류", "3D 모델을 선택해주세요!", "확인");
            }
        }
    }

    void GenerateThumbnail()
    {
        // 폴더 없으면 생성
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 임시 씬 오브젝트 생성
        GameObject previewScene = new GameObject("PreviewScene");

        // 임시 카메라 생성
        GameObject camObj = new GameObject("PreviewCamera");
        camObj.transform.parent = previewScene.transform;
        Camera cam = camObj.AddComponent<Camera>();
        cam.backgroundColor = new Color(0, 0, 0, 0); // 완전 투명
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = true;
        cam.cullingMask = 1 << 31; // Layer 31만 렌더링

        // 조명 추가 (유니티 프리뷰 스타일)
        GameObject lightObj = new GameObject("PreviewLight");
        lightObj.transform.parent = previewScene.transform;
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0); // 유니티 프리뷰 각도

        // 임시 모델 인스턴스 생성
        GameObject instance = Instantiate(targetModel);
        instance.transform.parent = previewScene.transform;

        // 모델을 Layer 31로 설정 (다른 오브젝트와 완전 분리)
        SetLayerRecursively(instance, 31);

        // URP 조명 문제 우회: 머티리얼을 Unlit으로 임시 교체
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture"); // 폴백

        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                Texture mainTex = mat.mainTexture;   // 기존 텍스처 보존
                Color mainColor = mat.color;          // 기존 색상 보존
                mat.shader = unlitShader;
                mat.mainTexture = mainTex;
                mat.color = mainColor;
            }
        }
        
        // 모델 중심 계산 및 카메라 배치
        // Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            
            // 직교 카메라 크기로 줌 제어
            float zoomMultiplier = 1f + (zoomPercent / 100f);
            cam.orthographicSize = maxExtent * zoomMultiplier;

            // 카메라 거리는 충분히 멀리 (클리핑 방지)
            Vector3 cameraDirection = GetCameraDirection(selectedAngle);
            cam.transform.position = bounds.center - cameraDirection * (maxExtent * 5f);
            cam.transform.LookAt(bounds.center);
        }

        // RenderTexture 생성 (알파 채널 포함)
        RenderTexture rt = new RenderTexture(thumbnailSize, thumbnailSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // 렌더링
        cam.Render();

        // Texture2D로 변환
        RenderTexture.active = rt;
        Texture2D thumbnail = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false);
        thumbnail.ReadPixels(new Rect(0, 0, thumbnailSize, thumbnailSize), 0, 0);
        thumbnail.Apply();

        // 이미지 회전 적용
        if (rotationMultiplier > 0)
        {
            thumbnail = RotateTexture90(thumbnail, rotationMultiplier);
        }

        // PNG로 저장
        byte[] bytes = thumbnail.EncodeToPNG();
        string fileName = $"{savePath}{targetModel.name}_Thumbnail.png";
        File.WriteAllBytes(fileName, bytes);

        // 정리
        RenderTexture.active = null;
        cam.targetTexture = null;
        DestroyImmediate(rt);
        DestroyImmediate(previewScene); // 전체 씬 삭제

        AssetDatabase.Refresh();

        // Import Settings 설정 (투명도 활성화)
        TextureImporter importer = AssetImporter.GetAtPath(fileName) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        DebugLogger.Log($"썸네일 생성 완료: {fileName}");
        EditorUtility.DisplayDialog("완료", $"투명 배경 썸네일이 생성되었습니다!\n{fileName}", "확인");

        // 생성된 파일 하이라이트
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(fileName);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    // 텍스처 90도 회전 (시계방향)
    Texture2D RotateTexture90(Texture2D original, int times)
    {
        Texture2D result = original;

        for (int i = 0; i < times; i++)
        {
            Texture2D rotated = new Texture2D(result.height, result.width, TextureFormat.RGBA32, false);

            for (int y = 0; y < result.height; y++)
            {
                for (int x = 0; x < result.width; x++)
                {
                    rotated.SetPixel(result.height - 1 - y, x, result.GetPixel(x, y));
                }
            }

            rotated.Apply();
            result = rotated;
        }

        return result;
    }

    // 오브젝트와 모든 자식의 레이어 변경
    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // 선택한 각도에 따른 카메라 방향 반환
    Vector3 GetCameraDirection(CameraAngle angle)
    {
        switch (angle)
        {
            case CameraAngle.Front:
                return new Vector3(0, 0, -1); // 정면

            case CameraAngle.Top:
                return new Vector3(0, 1, -0.3f).normalized; // 위에서 내려다보기

            case CameraAngle.Side:
                return new Vector3(-1, 0, 0); // 왼쪽에서 (오른쪽에서 보려면 1로 변경)

            case CameraAngle.Default:
            default:
                return new Vector3(-0.7f, 0.5f, -0.7f).normalized; // 유니티 기본 스타일
        }
    }
}
#endif