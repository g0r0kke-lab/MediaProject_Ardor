using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// 그림자 라이트 스캔, 미싱 스크립트 감지, SFX 클립 자동 할당, LightAnchor 설정 등 ARDORBIT 개발용 에디터 메뉴 유틸리티 모음입니다.
/// </summary>
public class ArdorbitHelper
{
    [MenuItem("ARDORBIT/Find Shadow Casting Lights")]
    static void FindShadowLights()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int shadowCount = 0;

        foreach (Light light in lights)
        {
            if (light.shadows != LightShadows.None && light.enabled)
            {
                shadowCount++;
                Debug.Log(
                    $"🔦 [{shadowCount}] {light.gameObject.name} | Type: {light.type} | Mode: {light.lightmapBakeType} | Shadows: {light.shadows}",
                    light.gameObject);
            }
        }

        Debug.Log($"Total: {shadowCount} shadow-casting lights");
    }

    [MenuItem("ARDORBIT/Find Missing Scripts")]
    static void Find()
    {
        // 씬 내 오브젝트 검색
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        foreach (var comp in go.GetComponents<Component>())
            if (comp == null)
                Debug.Log("Missing script on: " + go.name, go);

        // 프리팹 포함 전체 에셋 검색
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var comp in prefab.GetComponentsInChildren<Component>(true))
                if (comp == null)
                    Debug.Log("프리팹 - Missing: " + path, prefab);
        }
    }

    [MenuItem("ARDORBIT/Auto Assign SFX Clips (62~150)")]
    static void AutoAssignSFXClips()
    {
        // SoundManager 찾기
        SoundManager soundManager = Object.FindFirstObjectByType<SoundManager>();
        if (soundManager == null)
        {
            Debug.LogError("SoundManager를 씬에서 찾을 수 없습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(soundManager);
        SerializedProperty sfxArray = so.FindProperty("sfxClipsData");

        // 현재 배열 크기가 150 미만이면 151로 확장
        if (sfxArray.arraySize < 151)
            sfxArray.arraySize = 151;

        // 인덱스-파일명 테이블 (문서 기준)
        var entries = new (int index, string path)[]
        {
            (62, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_0.wav"),
            (63, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_1.wav"),
            (64, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_2.wav"),
            (65, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_3.wav"),
            (66, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_4.wav"),
            (67, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_5.wav"),
            (68, "Assets/PJY/Sounds/Memory/DLG2_00/Soma/DLG2_00-Soma_6.wav"),
            (69, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_0.wav"),
            (70, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_1.wav"),
            (71, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_2.wav"),
            (72, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_3.wav"),
            (73, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_4.wav"),
            (74, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_5.wav"),
            (75, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_6.wav"),
            (76, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_7.wav"),
            (77, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_8.wav"),
            (78, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_9.wav"),
            (79, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_10.wav"),
            (80, "Assets/PJY/Sounds/SOMA/DLG2_01/DLG2_01-Soma_11.wav"),
            (81, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_0.wav"),
            (82, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_1.wav"),
            (83, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_2.wav"),
            (84, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_3.wav"),
            (85, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_4.wav"),
            (86, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_5.wav"),
            (87, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_6.wav"),
            (88, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_7.wav"),
            (89, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_8.wav"),
            (90, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_9.wav"),
            (91, "Assets/PJY/Sounds/Memory/DLG2_02/Soma/DLG2_02-Soma_10.wav"),
            (92, "Assets/PJY/Sounds/SOMA/DLG2_03-Soma.wav"),
            (93, "Assets/PJY/Sounds/SOMA/DLG2_04-Soma.wav"),
            (94, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_0.wav"),
            (95, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_1.wav"),
            (96, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_2.wav"),
            (97, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_3.wav"),
            (98, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_4.wav"),
            (99, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_5.wav"),
            (100, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_6.wav"),
            (101, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_7.wav"),
            (102, "Assets/PJY/Sounds/Memory/DLG2_00/Boy/DLG2_00-Boy_8.wav"),
            (103, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_0.wav"),
            (104, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_1.wav"),
            (105, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_2.wav"),
            (106, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_3.wav"),
            (107, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_4.wav"),
            (108, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_5.wav"),
            (109, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_6.wav"),
            (110, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_7.wav"),
            (111, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_8.wav"),
            (112, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_9.wav"),
            (113, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_10.wav"),
            (114, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_11.wav"),
            (115, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_12.wav"),
            (116, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_13.wav"),
            (117, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_14.wav"),
            (118, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_15.wav"),
            (119, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_16.wav"),
            (120, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_17.wav"),
            (121, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_18.wav"),
            (122, "Assets/PJY/Sounds/Memory/DLG2_02/Boy/DLG2_02-Boy_19.wav"),
            (123, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_0.wav"),
            (124, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_1.wav"),
            (125, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_2.wav"),
            (126, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_3.wav"),
            (127, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_4.wav"),
            (128, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_5.wav"),
            (129, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_6.wav"),
            (130, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_7.wav"),
            (131, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_8.wav"),
            (132, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_9.wav"),
            (133, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_10.wav"),
            (134, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_11.wav"),
            (135, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_12.wav"),
            (136, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_13.wav"),
            (137, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_14.wav"),
            (138, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_15.wav"),
            (139, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_16.wav"),
            (140, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_17.wav"),
            (141, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_18.wav"),
            (142, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_19.wav"),
            (143, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_20.wav"),
            (144, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_21.wav"),
            (145, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_22.wav"),
            (146, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_23.wav"),
            (147, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_24.wav"),
            (148, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_25.wav"),
            (149, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_26.wav"),
            (150, "Assets/PJY/Sounds/AbandonedRobot/DLG2_01-Robot_27.wav"),
        };

        int successCount = 0;
        int failCount = 0;

        foreach (var (index, path) in entries)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            SerializedProperty element = sfxArray.GetArrayElementAtIndex(index);
            SerializedProperty clipProp = element.FindPropertyRelative("clip");
            SerializedProperty volumeProp = element.FindPropertyRelative("volume");

            if (clip != null)
            {
                clipProp.objectReferenceValue = clip;
                volumeProp.floatValue = 1f;
                successCount++;
            }
            else
            {
                Debug.LogWarning($"⚠️ [{index}] 클립 없음: {path}");
                failCount++;
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(soundManager);

        Debug.Log($"✅ SFX 자동 할당 완료 — 성공: {successCount}, 실패: {failCount}");
    }

    [MenuItem("ARDORBIT/Set LightAnchor MinThrowDirY = 0.25 (Current Scene)")]
    static void SetLightAnchorMinThrowDirY()
    {
        var anchors = Object.FindObjectsByType<LightAnchor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (anchors.Length == 0)
        {
            EditorUtility.DisplayDialog("LightAnchor", "현재 씬에 LightAnchor가 없습니다.", "확인");
            return;
        }

        int changed = 0;

        foreach (var anchor in anchors)
        {
            SerializedObject so = new SerializedObject(anchor);
            SerializedProperty prop = so.FindProperty("_minThrowDirY");

            if (prop == null)
            {
                Debug.LogWarning($"[LightAnchor] _minThrowDirY 필드를 찾을 수 없음: {anchor.name}");
                continue;
            }

            if (Mathf.Approximately(prop.floatValue, 0.25f)) continue;

            so.Update();
            prop.floatValue = 0.25f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(anchor);
            changed++;

            Debug.Log($"[LightAnchor] 변경 완료: {anchor.name}");
        }

        if (changed > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );

        EditorUtility.DisplayDialog("LightAnchor",
            $"완료! {anchors.Length}개 중 {changed}개 변경됨.", "확인");
    }
}