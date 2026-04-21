using UnityEditor;
using UnityEngine;

/// <summary>
/// StageSetupConfig 커스텀 인스펙터입니다.
///
/// Random 모드  → 런타임에 자동 생성
/// FixedSeed 모드 → 시드 도구 + 미리보기 테이블
/// (배치 설계는 MafiaGame → Stage Setup Tool 에서 수행합니다.)
/// </summary>
[CustomEditor(typeof(StageSetupConfig))]
public class StageSetupConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var config = (StageSetupConfig)target;

        // ── 참조 ──────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("에디터 미리보기용 참조", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_characterRegistry"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_stageRoleConfig"));
        EditorGUILayout.Space(6);

        // ── 시드 설정 ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("시드 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_useRandomSeed"), new GUIContent("Use Random Seed"));
        serializedObject.ApplyModifiedProperties();

        if (config.UseRandomSeed)
        {
            EditorGUILayout.HelpBox("Random 모드: 런타임에 자동 생성됩니다.", MessageType.None);
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_seed"), new GUIContent("Seed"));
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(4);

        // ── 시드 도구 ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("── 시드 도구 ──", EditorStyles.boldLabel);
        bool hasRefs = config.CharacterRegistry != null && config.StageRoleConfig != null;
        GUI.enabled = hasRefs;

        if (GUILayout.Button("랜덤 시드 생성 + 미리보기 갱신"))
        {
            Undo.RecordObject(config, "Generate Random Seed");
            int charCount = config.CharacterRegistry.Characters.Count;
            int roleCount = config.StageRoleConfig.Roles.Count;
            int maxSeed   = SeedEncoder.GetMaxSeed(charCount, roleCount, GameState.ZoneCount);
            serializedObject.FindProperty("_seed").intValue = Random.Range(0, maxSeed);
            serializedObject.ApplyModifiedProperties();
            config.RefreshPreviewFromSeed();
            EditorUtility.SetDirty(config);
        }

        if (GUILayout.Button("시드 → 미리보기 갱신"))
        {
            Undo.RecordObject(config, "Decode Seed");
            config.RefreshPreviewFromSeed();
            EditorUtility.SetDirty(config);
        }

        if (GUILayout.Button("미리보기 → 시드 생성"))
        {
            Undo.RecordObject(config, "Encode Preview");
            int encoded = config.EncodeFromPreview();
            serializedObject.FindProperty("_seed").intValue = encoded;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }

        GUI.enabled = true;

        if (!hasRefs)
            EditorGUILayout.HelpBox("CharacterRegistry와 StageRoleConfig를 연결해야 시드 도구를 사용할 수 있습니다.", MessageType.Warning);

        // ── 배치 미리보기 ──────────────────────────────────────────────────
        var preview = config.PreviewSetup;
        if (preview != null && preview.Length > 0 && hasRefs)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("── 배치 미리보기 ──", EditorStyles.boldLabel);

            var roles = config.StageRoleConfig.Roles;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("캐릭터",      GUILayout.Width(90));
            GUILayout.Label("직업",        GUILayout.Width(90));
            GUILayout.Label("역할 인덱스", GUILayout.Width(80));
            GUILayout.Label("Zone",        GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            var previewProp = serializedObject.FindProperty("_previewSetup");
            for (int i = 0; i < preview.Length; i++)
            {
                var entry    = preview[i];
                var charData = config.CharacterRegistry.GetById(entry.characterId);
                string roleName = (entry.roleIndex >= 0 && entry.roleIndex < roles.Count)
                    ? roles[entry.roleIndex].RoleName : "?";

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(charData?.CharacterName ?? $"ID:{entry.characterId}", GUILayout.Width(90));
                GUILayout.Label(roleName, GUILayout.Width(90));

                var entryProp = previewProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("roleIndex"),
                    GUIContent.none, GUILayout.Width(80));
                EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("zoneId"),
                    GUIContent.none, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(2);
    }
}
