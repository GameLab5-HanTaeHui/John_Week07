#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// StreamingAssets/Campaign 폴더의 JSON 파일에서
    /// ProfileClueDataSO 데이터를 임포트하는 에디터 도구입니다.
    ///
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/profileclue_{stageId}.json
    ///   예: profileclue_Stage_1_Phase2.json
    ///
    /// ─── JSON 형식 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "Stage_1_Phase2",
    ///     "clues": [
    ///       {
    ///         "profileClueId": "P01_01",
    ///         "characterId": 1,
    ///         "clueIndex": 1,
    ///         "profileKeyword": "유기 / 결핍",
    ///         "profileTruth": "엔비는 어린 시절...",
    ///         "clueText": "이유라도 알았다면...",
    ///         "clueSpeakerId": 1,
    ///         "hintText": "엔비가 혼자 남으면...",
    ///         "acquireCondition": "엔비 혼자 있는 조사 구역",
    ///         "conditionType": "solo",
    ///         "recommendedCombo": "#1 단독",
    ///         "designerNote": ""
    ///       }
    ///     ]
    ///   }
    /// </summary>
    [CustomEditor(typeof(ProfileClueDataSO))]
    public class ProfileClueDataImporter : UnityEditor.Editor
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
                "JSON 형식:\n" +
                "{\n" +
                "  \"stageId\": \"Stage_1_Phase2\",\n" +
                "  \"clues\": [\n" +
                "    {\n" +
                "      \"profileClueId\": \"P01_01\",\n" +
                "      \"characterId\": 1,\n" +
                "      \"clueIndex\": 1,\n" +
                "      \"clueText\": \"진실 대사\",\n" +
                "      \"hintText\": \"유도 대사\",\n" +
                "      ...\n" +
                "    }\n" +
                "  ]\n" +
                "}",
                MessageType.Info);
        }

        /// <summary>
        /// JSON 파일을 읽어 ProfileClueDataSO에 데이터를 입력합니다.
        /// </summary>
        private void ImportFromStreamingAssets(string fileName)
        {
            EnsureCampaignFolderExists();

            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[ProfileClueDataImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog("파일 없음", $"파일을 찾을 수 없습니다.\n{path}", "확인");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<ProfileClueJsonRoot>(json);

                if (data == null)
                {
                    Debug.LogError("[ProfileClueDataImporter] JSON 파싱 실패");
                    return;
                }

                var so = (ProfileClueDataSO)target;
                var serialized = new SerializedObject(so);

                // stageId 설정
                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                // clues 리스트 초기화
                var cluesProp = serialized.FindProperty("_clues");
                cluesProp.ClearArray();

                if (data.clues != null)
                {
                    for (int i = 0; i < data.clues.Length; i++)
                    {
                        var clueData = data.clues[i];
                        cluesProp.InsertArrayElementAtIndex(i);
                        var elem = cluesProp.GetArrayElementAtIndex(i);

                        elem.FindPropertyRelative("ProfileClueId").stringValue
                            = clueData.profileClueId ?? "";
                        elem.FindPropertyRelative("CharacterId").intValue
                            = clueData.characterId;
                        elem.FindPropertyRelative("ClueIndex").intValue
                            = clueData.clueIndex;
                        elem.FindPropertyRelative("ProfileKeyword").stringValue
                            = clueData.profileKeyword ?? "";
                        elem.FindPropertyRelative("ProfileTruth").stringValue
                            = clueData.profileTruth ?? "";
                        elem.FindPropertyRelative("ClueText").stringValue
                            = clueData.clueText ?? "";
                        elem.FindPropertyRelative("ClueSpeakerId").intValue
                            = clueData.clueSpeakerId;
                        elem.FindPropertyRelative("HintText").stringValue
                            = clueData.hintText ?? "";
                        elem.FindPropertyRelative("AcquireCondition").stringValue
                            = clueData.acquireCondition ?? "";
                        elem.FindPropertyRelative("ConditionType").stringValue
                            = clueData.conditionType ?? "";
                        elem.FindPropertyRelative("RecommendedCombo").stringValue
                            = clueData.recommendedCombo ?? "";
                        elem.FindPropertyRelative("DesignerNote").stringValue
                            = clueData.designerNote ?? "";
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                Debug.Log($"[ProfileClueDataImporter] 임포트 완료 — " +
                          $"StageId: {data.stageId}, " +
                          $"조각 수: {data.clues?.Length ?? 0}");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"StageId: {data.stageId}\n조각 수: {data.clues?.Length ?? 0}",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileClueDataImporter] 임포트 실패 — {e.Message}");
                EditorUtility.DisplayDialog("임포트 실패", e.Message, "확인");
            }
        }

        /// <summary>StreamingAssets/Campaign 폴더를 OS 탐색기에서 엽니다.</summary>
        private void OpenCampaignFolder()
        {
            EnsureCampaignFolderExists();
            EditorUtility.RevealInFinder(CampaignFolderPath);
        }

        /// <summary>StreamingAssets/Campaign 폴더가 없으면 생성합니다.</summary>
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
    internal class ProfileClueJsonRoot
    {
        public string stageId;
        public ProfileClueJsonEntry[] clues;
    }

    [Serializable]
    internal class ProfileClueJsonEntry
    {
        public string profileClueId;
        public int characterId;
        public int clueIndex;
        public string profileKeyword;
        public string profileTruth;
        public string clueText;
        public int clueSpeakerId;
        public string hintText;
        public string acquireCondition;
        public string conditionType;
        public string recommendedCombo;
        public string designerNote;
    }
}
#endif