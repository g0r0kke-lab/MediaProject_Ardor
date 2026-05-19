using UnityEngine;

public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}

[UnityEditor.InitializeOnLoad]
public static class EditorTools
{
    static EditorTools()
    {
        // 메뉴 등록은 MenuItem으로 처리
    }

    [UnityEditor.MenuItem("Tools/Clear PlayerPrefs")]
    static void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        UnityEditor.EditorUtility.DisplayDialog("완료", "PlayerPrefs 초기화됨", "OK");
    }
}
#endif