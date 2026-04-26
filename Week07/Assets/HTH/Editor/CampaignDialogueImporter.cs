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
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/dialogue_{stageId}.json
    ///   예: dialogue_Stage_1_Phase2.json
    ///
    /// ─── JSON 구조 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "Stage_1_Phase2",
    ///     "groupDialogues": [
    ///       {
    ///         "comboId": "C001",
    ///         "comboKey": "#1|#2",
    ///         "participantIds": [1, 2],
    ///         "situationType": "2인 대화",
    ///         "trigger": "",
    ///         "visibility": "",
    ///         "fragmentId": "",
    ///         "triggerDeadIds": [],
    ///         "unlockConditionId": "",
    ///         "hintOfConditionId": "",
    ///         "lines": [
    ///           {
    ///             "speakerId": 1,
    ///             "text": "대사 내용",
    ///             "isProfileClue": false,
    ///             "profileClueId": "",
    ///             "profileCategory": ""
    ///           }
    ///         ]
    ///       }
    ///     ],
    ///     "soloDialogues": []
    ///   }
    /// </summary>
    [CustomEditor(typeof(CampaignDialogueSO))]
    public class CampaignDialogueImporter : Editor
    {
        /// <summary>임포트할 JSON 파일명 (확장자 제외)입니다.</summary>
        private string _fileNameInput = "";

        /// <summary>StreamingAssets/Campaign 폴더 경로입니다.</summary>
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
                "FragmentId 형식: P01_01 (group_1_3_5_frag0 폐기)\n" +
                "총 216개 GroupDialogue 임포트 지원",
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
                    Debug.LogError("[CampaignDialogueImporter] JSON 파싱 실패");
                    return;
                }

                var so = (CampaignDialogueSO)target;
                var serialized = new SerializedObject(so);

                // stageId
                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                // groupDialogues 초기화
                var groupProp = serialized.FindProperty("_groupDialogues");
                groupProp.ClearArray();

                if (data.groupDialogues != null)
                {
                    for (int i = 0; i < data.groupDialogues.Length; i++)
                    {
                        var gd = data.groupDialogues[i];
                        groupProp.InsertArrayElementAtIndex(i);
                        var elem = groupProp.GetArrayElementAtIndex(i);

                        // 식별
                        elem.FindPropertyRelative("ComboId").stringValue
                            = gd.comboId ?? "";
                        elem.FindPropertyRelative("ComboKey").stringValue
                            = gd.comboKey ?? "";

                        // 참가자 ID
                        var participantsProp = elem.FindPropertyRelative("ParticipantIds");
                        participantsProp.ClearArray();
                        if (gd.participantIds != null)
                        {
                            for (int j = 0; j < gd.participantIds.Length; j++)
                            {
                                participantsProp.InsertArrayElementAtIndex(j);
                                participantsProp.GetArrayElementAtIndex(j).intValue
                                    = gd.participantIds[j];
                            }
                        }

                        // 상황
                        elem.FindPropertyRelative("SituationType").stringValue
                            = gd.situationType ?? "";
                        elem.FindPropertyRelative("Trigger").stringValue
                            = gd.trigger ?? "";
                        elem.FindPropertyRelative("Visibility").stringValue
                            = gd.visibility ?? "";

                        // ProfileClue 연결
                        elem.FindPropertyRelative("FragmentId").stringValue
                            = gd.fragmentId ?? "";

                        // 사망 트리거 ID
                        var deadIdsProp = elem.FindPropertyRelative("TriggerDeadIds");
                        deadIdsProp.ClearArray();
                        if (gd.triggerDeadIds != null)
                        {
                            for (int j = 0; j < gd.triggerDeadIds.Length; j++)
                            {
                                deadIdsProp.InsertArrayElementAtIndex(j);
                                deadIdsProp.GetArrayElementAtIndex(j).intValue
                                    = gd.triggerDeadIds[j];
                            }
                        }

                        // 조건
                        elem.FindPropertyRelative("UnlockConditionId").stringValue
                            = gd.unlockConditionId ?? "";
                        elem.FindPropertyRelative("HintOfConditionId").stringValue
                            = gd.hintOfConditionId ?? "";

                        // 대사 라인
                        var linesProp = elem.FindPropertyRelative("Lines");
                        linesProp.ClearArray();
                        if (gd.lines != null)
                        {
                            for (int j = 0; j < gd.lines.Length; j++)
                            {
                                var ld = gd.lines[j];
                                linesProp.InsertArrayElementAtIndex(j);
                                var lineProp = linesProp.GetArrayElementAtIndex(j);

                                lineProp.FindPropertyRelative("SpeakerId").intValue
                                    = ld.speakerId;
                                lineProp.FindPropertyRelative("Text").stringValue
                                    = ld.text ?? "";
                                lineProp.FindPropertyRelative("RevealCharacterId").intValue
                                    = ld.revealCharacterId;
                                lineProp.FindPropertyRelative("RevealCharacterName").stringValue
                                    = ld.revealCharacterName ?? "";
                                lineProp.FindPropertyRelative("IsProfileClue").boolValue
                                    = ld.isProfileClue;
                                lineProp.FindPropertyRelative("ProfileClueId").stringValue
                                    = ld.profileClueId ?? "";
                                lineProp.FindPropertyRelative("ProfileCategory").stringValue
                                    = ld.profileCategory ?? "";
                            }
                        }

                        // Condition (하위 호환)
                        var condProp = elem.FindPropertyRelative("Condition");
                        condProp.FindPropertyRelative("RequiredFragmentId").stringValue = "";
                        condProp.FindPropertyRelative("MinLoopCount").intValue = 0;
                    }
                }

                // soloDialogues 초기화
                var soloProp = serialized.FindProperty("_soloDialogues");
                soloProp.ClearArray();

                if (data.soloDialogues != null)
                {
                    for (int i = 0; i < data.soloDialogues.Length; i++)
                    {
                        var sd = data.soloDialogues[i];
                        soloProp.InsertArrayElementAtIndex(i);
                        var elem = soloProp.GetArrayElementAtIndex(i);

                        elem.FindPropertyRelative("CharacterId").intValue = sd.characterId;
                        elem.FindPropertyRelative("FragmentId").stringValue = sd.fragmentId ?? "";

                        var linesProp = elem.FindPropertyRelative("Lines");
                        linesProp.ClearArray();
                        if (sd.lines != null)
                        {
                            for (int j = 0; j < sd.lines.Length; j++)
                            {
                                var ld = sd.lines[j];
                                linesProp.InsertArrayElementAtIndex(j);
                                var lineProp = linesProp.GetArrayElementAtIndex(j);

                                lineProp.FindPropertyRelative("SpeakerId").intValue = ld.speakerId;
                                lineProp.FindPropertyRelative("Text").stringValue = ld.text ?? "";
                                lineProp.FindPropertyRelative("RevealCharacterId").intValue = ld.revealCharacterId;
                                lineProp.FindPropertyRelative("RevealCharacterName").stringValue = ld.revealCharacterName ?? "";
                                lineProp.FindPropertyRelative("IsProfileClue").boolValue = ld.isProfileClue;
                                lineProp.FindPropertyRelative("ProfileClueId").stringValue = ld.profileClueId ?? "";
                                lineProp.FindPropertyRelative("ProfileCategory").stringValue = ld.profileCategory ?? "";
                            }
                        }
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                int groupCount = data.groupDialogues?.Length ?? 0;
                int fragCount = 0;
                if (data.groupDialogues != null)
                    foreach (var g in data.groupDialogues)
                        if (!string.IsNullOrEmpty(g.fragmentId)) fragCount++;

                Debug.Log($"[CampaignDialogueImporter] 임포트 완료 — " +
                          $"StageId: {data.stageId}, " +
                          $"GroupDialogue: {groupCount}개, " +
                          $"ProfileClue 연결: {fragCount}개");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"StageId: {data.stageId}\n" +
                    $"GroupDialogue: {groupCount}개\n" +
                    $"ProfileClue 연결: {fragCount}개",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignDialogueImporter] 임포트 실패 — {e.Message}");
                EditorUtility.DisplayDialog("임포트 실패", e.Message, "확인");
            }
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

    // ── JSON 데이터 구조 ──────────────────────────────────────────────────

    [Serializable]
    internal class DialogueJsonRoot
    {
        public string stageId;
        public DialogueJsonGroup[] groupDialogues;
        public DialogueJsonSolo[] soloDialogues;
    }

    [Serializable]
    internal class DialogueJsonGroup
    {
        public string comboId;
        public string comboKey;
        public int[] participantIds;
        public string situationType;
        public string trigger;
        public string visibility;
        public string fragmentId;
        public int[] triggerDeadIds;
        public string unlockConditionId;
        public string hintOfConditionId;
        public DialogueJsonLine[] lines;
    }

    [Serializable]
    internal class DialogueJsonSolo
    {
        public int characterId;
        public string fragmentId;
        public DialogueJsonLine[] lines;
    }

    [Serializable]
    internal class DialogueJsonLine
    {
        public int speakerId;
        public string text;
        public int revealCharacterId = -1;
        public string revealCharacterName = "";
        public bool isProfileClue;
        public string profileClueId;
        public string profileCategory;
    }
}
#endif