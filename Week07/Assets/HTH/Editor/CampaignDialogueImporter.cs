#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// StreamingAssets/Campaign 폴더의 JSON 파일에서
    /// CampaignDialogueSO 데이터를 임포트하는 에디터 도구입니다.
    ///
    /// ─── JSON 키 규칙 (PascalCase) ───────────────────────────────────────
    ///   StageId / Dialogues / TimeOfDayDialogues / AlreadySeenLines
    ///   Dialogues[]: DialogueId / ParticipantsRaw / Type / Scope / Alive
    ///                Gimmick / SituationCharactersRaw / UnlockConditionId
    ///                RewardFragmentId / Lines / DeveloperNote
    ///   Lines[]: TextId / Text
    /// </summary>
    [CustomEditor(typeof(CampaignDialogueSO))]
    public class CampaignDialogueImporter : Editor
    {
        private string _fileNameInput = "";

        private static string CampaignFolderPath
            => Path.Combine(Application.streamingAssetsPath, "Campaign");

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── StreamingAssets 임포트 ──", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("파일명", GUILayout.Width(50));
            _fileNameInput = EditorGUILayout.TextField(_fileNameInput);
            EditorGUILayout.LabelField(".json", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            string previewPath = Path.Combine(CampaignFolderPath, $"{_fileNameInput}.json");
            EditorGUILayout.HelpBox($"경로: {previewPath}", MessageType.None);
            EditorGUILayout.Space(4);

            GUI.enabled = !string.IsNullOrEmpty(_fileNameInput);
            if (GUILayout.Button("Import From StreamingAssets", GUILayout.Height(30)))
                ImportFromStreamingAssets(_fileNameInput);
            GUI.enabled = true;

            if (GUILayout.Button("StreamingAssets/Campaign 폴더 열기", GUILayout.Height(24)))
                OpenCampaignFolder();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "JSON 키는 PascalCase를 사용합니다.\n" +
                "예: StageId / Dialogues / RewardFragmentId / TextId",
                MessageType.Info);
        }

        private void ImportFromStreamingAssets(string fileName)
        {
            EnsureCampaignFolderExists();
            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[CampaignDialogueImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog("파일 없음", $"파일을 찾을 수 없습니다.\n{path}", "확인");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<DialogueJsonRoot>(json);

                if (data == null)
                {
                    Debug.LogError("[CampaignDialogueImporter] JSON 파싱 실패 — null 반환");
                    return;
                }

                Debug.Log($"[CampaignDialogueImporter] 파싱 결과 — StageId:{data.StageId}, " +
                          $"Dialogues:{data.Dialogues?.Length ?? 0}개");

                var so = (CampaignDialogueSO)target;
                var serialized = new SerializedObject(so);

                serialized.FindProperty("_stageId").stringValue = data.StageId ?? "";

                // dialogues
                var dialoguesProp = serialized.FindProperty("_dialogues");
                dialoguesProp.ClearArray();

                if (data.Dialogues != null)
                {
                    for (int i = 0; i < data.Dialogues.Length; i++)
                    {
                        var d = data.Dialogues[i];
                        dialoguesProp.InsertArrayElementAtIndex(i);
                        var elem = dialoguesProp.GetArrayElementAtIndex(i);

                        elem.FindPropertyRelative("DialogueId").stringValue = d.DialogueId ?? "";
                        elem.FindPropertyRelative("ParticipantsRaw").stringValue = d.ParticipantsRaw ?? "";
                        elem.FindPropertyRelative("Type").enumValueIndex = ParseEnum<DialogueType>(d.Type, DialogueType.Normal);
                        elem.FindPropertyRelative("Scope").enumValueIndex = ParseEnum<SituationScope>(d.Scope, SituationScope.InZone);
                        elem.FindPropertyRelative("Alive").enumValueIndex = ParseEnum<SituationAlive>(d.Alive, SituationAlive.AllSurvived);
                        elem.FindPropertyRelative("Gimmick").enumValueIndex = ParseEnum<SituationGimmick>(d.Gimmick, SituationGimmick.None);
                        elem.FindPropertyRelative("SituationCharactersRaw").stringValue = d.SituationCharactersRaw ?? "";
                        elem.FindPropertyRelative("UnlockConditionId").stringValue = d.UnlockConditionId ?? "";
                        elem.FindPropertyRelative("RewardFragmentId").stringValue = d.RewardFragmentId ?? "";
                        elem.FindPropertyRelative("DeveloperNote").stringValue = d.DeveloperNote ?? "";

                        SetLines(elem.FindPropertyRelative("Lines"), d.Lines);
                    }
                }

                // timeOfDayDialogues
                var todProp = serialized.FindProperty("_timeOfDayDialogues");
                todProp.ClearArray();

                if (data.TimeOfDayDialogues != null)
                {
                    for (int i = 0; i < data.TimeOfDayDialogues.Length; i++)
                    {
                        var tod = data.TimeOfDayDialogues[i];
                        todProp.InsertArrayElementAtIndex(i);
                        var elem = todProp.GetArrayElementAtIndex(i);

                        elem.FindPropertyRelative("TimeOfDay").enumValueIndex = ParseEnum<TimeOfDay>(tod.TimeOfDay, TimeOfDay.Morning);
                        elem.FindPropertyRelative("DeveloperNote").stringValue = tod.DeveloperNote ?? "";
                        SetLines(elem.FindPropertyRelative("Lines"), tod.Lines);
                    }
                }

                // alreadySeenLines
                var seenProp = serialized.FindProperty("_alreadySeenLines");
                SetLines(seenProp, data.AlreadySeenLines);

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                int dialogueCount = data.Dialogues?.Length ?? 0;
                int coreCount = 0;
                if (data.Dialogues != null)
                    foreach (var d in data.Dialogues)
                        if (string.Equals(d.Type, "Core", StringComparison.OrdinalIgnoreCase))
                            coreCount++;

                Debug.Log($"[CampaignDialogueImporter] 임포트 완료 — " +
                          $"StageId:{data.StageId}, Dialogue:{dialogueCount}개, Core:{coreCount}개");

                EditorUtility.DisplayDialog("임포트 완료",
                    $"StageId: {data.StageId}\nDialogue: {dialogueCount}개\nCore: {coreCount}개", "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignDialogueImporter] 임포트 실패 — {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("임포트 실패", e.Message, "확인");
            }
        }

        private static void SetLines(SerializedProperty linesProp, DialogueJsonLine[] lines)
        {
            linesProp.ClearArray();
            if (lines == null) return;

            for (int j = 0; j < lines.Length; j++)
            {
                linesProp.InsertArrayElementAtIndex(j);
                var lineProp = linesProp.GetArrayElementAtIndex(j);
                lineProp.FindPropertyRelative("TextId").stringValue = lines[j].TextId ?? "";
                lineProp.FindPropertyRelative("Text").stringValue = lines[j].Text ?? "";
            }
        }

        private static int ParseEnum<T>(string value, T defaultValue) where T : Enum
        {
            if (string.IsNullOrEmpty(value)) return Convert.ToInt32(defaultValue);
            if (Enum.TryParse(typeof(T), value, ignoreCase: true, out var result))
                return Convert.ToInt32(result);
            Debug.LogWarning($"[CampaignDialogueImporter] 알 수 없는 enum '{value}' ({typeof(T).Name})");
            return Convert.ToInt32(defaultValue);
        }

        private void OpenCampaignFolder()
        {
            EnsureCampaignFolderExists();
            EditorUtility.RevealInFinder(CampaignFolderPath);
        }

        private static void EnsureCampaignFolderExists()
        {
            if (!Directory.Exists(CampaignFolderPath))
            {
                Directory.CreateDirectory(CampaignFolderPath);
                AssetDatabase.Refresh();
            }
        }
    }

    // ── JSON 데이터 구조 (PascalCase — JSON 파일과 일치) ─────────────────

    [Serializable]
    public class DialogueJsonRoot
    {
        public string StageId;
        public DialogueJsonEntry[] Dialogues;
        public DialogueJsonTimeOfDay[] TimeOfDayDialogues;
        public DialogueJsonLine[] AlreadySeenLines;
    }

    [Serializable]
    public class DialogueJsonEntry
    {
        public string DialogueId;
        public string ParticipantsRaw;
        public string Type;
        public string Scope;
        public string Alive;
        public string Gimmick;
        public string SituationCharactersRaw;
        public string UnlockConditionId;
        public string RewardFragmentId;
        public DialogueJsonLine[] Lines;
        public string DeveloperNote;
    }

    [Serializable]
    public class DialogueJsonTimeOfDay
    {
        public string TimeOfDay;
        public DialogueJsonLine[] Lines;
        public string DeveloperNote;
    }

    [Serializable]
    public class DialogueJsonLine
    {
        public string TextId;
        public string Text;
    }
}
#endif