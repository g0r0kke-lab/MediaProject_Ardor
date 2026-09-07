#if UNITY_EDITOR
 
using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(EditorChildSceneLoader))]
/// <summary>
/// 저장된 설정에서 멀티씬 구성을 저장하거나 복원하는 버튼을 추가한 EditorChildSceneLoader의 커스텀 인스펙터입니다.
/// </summary>
public class ChildSceneLoaderInspectorGUI : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
 
        var currentInspectorObject = (EditorChildSceneLoader)target;
 
        if (GUILayout.Button("Save scene setup to config"))
        {
            currentInspectorObject.SaveSceneSetup();
        }
 
        if (GUILayout.Button("Reset scene setup from config..."))
        {
            currentInspectorObject.ResetSceneSetupToConfig(syncRuntimeLoader: true); // 버튼 클릭 시 동기화
        }
    }
}
 
#endif