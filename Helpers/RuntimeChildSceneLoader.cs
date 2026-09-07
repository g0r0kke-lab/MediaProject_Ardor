using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 런타임에서 child 씬들을 자동으로 로드합니다.
/// EditorChildSceneLoader와 함께 같은 오브젝트에 붙여서 사용하세요.
/// </summary>
public class RuntimeChildSceneLoader : MonoBehaviour
{
    [SerializeField] [Tooltip("로드할 child 씬들의 이름 (Build Settings에 포함되어야 함)")]
    public List<string> childSceneNames = new List<string>();

    public static bool IsLoading { get; private set; } = true;
    public static float LoadingProgress { get; private set; } = 0f;
    public static bool IsLoadComplete { get; private set; } = false;

    private void Awake()
    {
#if UNITY_EDITOR
        if (IsFirstPlayInEditor())
        {
            CompleteLoading();
            return;
        }
#endif

        IsLoading = true;
        IsLoadComplete = false;
        StartCoroutine(LoadChildScenesCoroutine());
    }

#if UNITY_EDITOR
    private bool IsFirstPlayInEditor()
    {
        var editorLoader = GetComponent<EditorChildSceneLoader>();

        if (editorLoader == null || !editorLoader.enabled)
        {
            return false;
        }

        int existingCount = 0;

        foreach (string sceneName in childSceneNames)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid())
                {
                    existingCount++;
                }
            }
        }

        return existingCount > 0 && existingCount == childSceneNames.Count;
    }
#endif

    private IEnumerator LoadChildScenesCoroutine()
    {
        if (childSceneNames == null || childSceneNames.Count == 0)
        {
            DebugLogger.LogWarning("로드할 child 씬이 없습니다.");
            CompleteLoading();
            yield break;
        }

        // 모든 씬 로드를 동시에 시작 (I/O가 겹쳐져 순차 로드보다 빠름)
        List<AsyncOperation> loadOperations = new List<AsyncOperation>();

        foreach (string sceneName in childSceneNames)
        {
            if (string.IsNullOrEmpty(sceneName))
                continue;

            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.isLoaded)
                continue;

            DebugLogger.Log($"씬 '{sceneName}' 로딩 시작...");

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (asyncLoad == null)
            {
                DebugLogger.LogError($"씬 '{sceneName}'을(를) 로드할 수 없습니다.");
                continue;
            }

            loadOperations.Add(asyncLoad);
        }

        if (loadOperations.Count == 0)
        {
            CompleteLoading();
            yield break;
        }

        bool allDone = false;
        while (!allDone)
        {
            float progressSum = 0f;
            allDone = true;

            foreach (AsyncOperation op in loadOperations)
            {
                progressSum += op.progress;
                if (!op.isDone)
                    allDone = false;
            }

            LoadingProgress = progressSum / loadOperations.Count;

            if (!allDone)
                yield return null;
        }

        CompleteLoading();
    }

    private void CompleteLoading()
    {
        IsLoading = false;
        IsLoadComplete = true;
        LoadingProgress = 1f;
        DebugLogger.Log("모든 child 씬 로딩 완료!");
    }

#if UNITY_EDITOR
    public void SyncFromEditorChildSceneLoader()
    {
        var editorLoader = GetComponent<EditorChildSceneLoader>();
        if (editorLoader == null || editorLoader.ChildScenesToLoadConfig == null)
        {
            DebugLogger.LogWarning("EditorChildSceneLoader를 찾을 수 없거나 설정이 비어있습니다.");
            return;
        }

        childSceneNames.Clear();

        bool isFirst = true;
        foreach (var sceneAsset in editorLoader.ChildScenesToLoadConfig)
        {
            if (sceneAsset != null)
            {
                string scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (!isFirst)
                {
                    childSceneNames.Add(sceneName);
                }

                isFirst = false;
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}