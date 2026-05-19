#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class DialogueConfigAutoSetup : EditorWindow
{
    [MenuItem("Tools/Auto Setup Dialogue Configs")]
    static void Setup()
    {
        // GameUIManager 찾기
        GameUIManager uiManager = FindFirstObjectByType<GameUIManager>();
        
        if (uiManager == null)
        {
            Debug.LogError("씬에서 GameUIManager를 찾을 수 없습니다!");
            return;
        }

        // SerializedObject로 접근
        SerializedObject so = new SerializedObject(uiManager);
        SerializedProperty configsProp = so.FindProperty("dialogueNodeConfigs");

        if (configsProp == null)
        {
            Debug.LogError("dialogueNodeConfigs 필드를 찾을 수 없습니다!");
            return;
        }

        // 기존 리스트 클리어
        configsProp.ClearArray();

        // 기본값 노드들 (progressState=false, keepUIMode=false)
        string[] defaultNodes = {
        };
        
        // Progress State 노드들
        string[] progressNodes = {
            "DLG1_01_ko", "DLG1_01_en",
            "DLG1_02_ko", "DLG1_02_en",
            "DLG1_04_ko", "DLG1_04_en",
            "DLG1_05_ko", "DLG1_05_en",
            "DLG1_06_ko", "DLG1_06_en",
            "DLG1_07_ko", "DLG1_07_en",
            "DLG1_08_ko", "DLG1_08_en",
            "DLG1_09_ko", "DLG1_09_en",
            "DLG1_10_ko", "DLG1_10_en",
            "DLG2_00_ko", "DLG2_00_en",
            "DLG2_01_ko", "DLG2_01_en",
            "DLG2_02_ko", "DLG2_02_en",
            "DLG2_03_ko", "DLG2_03_en",
            "DLG2_04_ko", "DLG2_04_en",
        };
        
        // 기본값 노드 추가
        foreach (string nodeName in defaultNodes)
        {
            configsProp.arraySize++;
            SerializedProperty element = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
            element.FindPropertyRelative("nodeName").stringValue = nodeName;
            element.FindPropertyRelative("progressState").boolValue = false;
            element.FindPropertyRelative("keepUIMode").boolValue = false;
        }

        // Progress State 노드 추가
        foreach (string nodeName in progressNodes)
        {
            configsProp.arraySize++;
            SerializedProperty element = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
            element.FindPropertyRelative("nodeName").stringValue = nodeName;
            element.FindPropertyRelative("progressState").boolValue = true;
            element.FindPropertyRelative("keepUIMode").boolValue = false;
        }

        // UI Keep 노드들
        string[] uiKeepNodes = {
            "DLG1_999_ko", "DLG1_999_en"
        };

        // UI Keep 노드 추가
        foreach (string nodeName in uiKeepNodes)
        {
            configsProp.arraySize++;
            SerializedProperty element = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
            element.FindPropertyRelative("nodeName").stringValue = nodeName;
            element.FindPropertyRelative("progressState").boolValue = false;
            element.FindPropertyRelative("keepUIMode").boolValue = true;
        }

        // 적용
        so.ApplyModifiedProperties();

        Debug.Log($"✅ {configsProp.arraySize}개의 Dialogue Config 자동 설정 완료!");
        
        // Inspector 갱신
        EditorUtility.SetDirty(uiManager);
    }
}
#endif