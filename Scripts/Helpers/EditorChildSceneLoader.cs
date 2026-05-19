using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
 
/// <summary>
/// Allows setting a scene as a root scene and setting its child scenes. To use this, drag this component on any object in a scene to make that scene a root scene. In the background, ChildSceneLoader will automatically manage this.
/// </summary>
public class EditorChildSceneLoader : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] public List<SceneAsset> ChildScenesToLoadConfig;
 
    void Update()
    {
        // DO NOT DELETE keep this so we can enable/disable this script... (used in ChildSceneLoader)
    }
 
    public void SaveSceneSetup()
    {
        if (ChildScenesToLoadConfig == null)
        {
            ChildScenesToLoadConfig = new List<SceneAsset>();
        }
        else
        {
            ChildScenesToLoadConfig.Clear();
        }
        foreach (var sceneSetup in EditorSceneManager.GetSceneManagerSetup())
        {
            ChildScenesToLoadConfig.Add(AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneSetup.path));
        }

        AutoSyncRuntimeLoader();
    }
 
    public void ResetSceneSetupToConfig(bool syncRuntimeLoader = false)
    {
        if (syncRuntimeLoader)
        {
            AutoSyncRuntimeLoader();
        }

        var sceneAssetsToLoad = ChildScenesToLoadConfig;
 
        List<SceneSetup> sceneSetupToLoad = new List<SceneSetup>();
        foreach (var sceneAsset in sceneAssetsToLoad)
        {
            sceneSetupToLoad.Add(new SceneSetup() { path = AssetDatabase.GetAssetPath(sceneAsset), isActive = false, isLoaded = true });
        }
 
        sceneSetupToLoad[0].isActive = true;
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        EditorSceneManager.RestoreSceneManagerSetup(sceneSetupToLoad.ToArray());
    }

    private void AutoSyncRuntimeLoader()
    {
        var runtimeLoader = GetComponent<RuntimeChildSceneLoader>();
        if (runtimeLoader != null)
        {
            runtimeLoader.SyncFromEditorChildSceneLoader();
        }
    }
#endif
}
 
#if UNITY_EDITOR
 
[InitializeOnLoad]
public class ChildSceneLoader
{
    static ChildSceneLoader()
    {
        EditorSceneManager.sceneOpened += OnSceneLoaded;
    }
 
    static void OnSceneLoaded(Scene _, OpenSceneMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
        {
            return;
        }
        
        if (mode != OpenSceneMode.Single)
        {
            return;
        }
 
        var scenesToLoadObjects = GameObject.FindObjectsByType<EditorChildSceneLoader>(FindObjectsSortMode.None);
        if (scenesToLoadObjects.Length > 1)
        {
            throw new Exception("Should only have one root scene at once loaded");
        }
 
        if (scenesToLoadObjects.Length == 0 || !scenesToLoadObjects[0].enabled)
        {
            return;
        }
 
        scenesToLoadObjects[0].ResetSceneSetupToConfig();
    }
}
#endif