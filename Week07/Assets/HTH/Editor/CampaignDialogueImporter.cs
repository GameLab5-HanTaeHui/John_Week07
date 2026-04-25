#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign.Editor
{
    /// <summary>
    /// StreamingAssets의 JSON 파일을 읽어 CampaignDialogueSO에 임포트하는 에디터 도구입니다.
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   1. StreamingAssets/Campaign/ 폴더에 JSON 파일을 저장합니다.
    ///      예: StreamingAssets/Campaign/dialogue_Stage_1_Phase2.json
    ///
    ///   2. Project에서 CampaignDialogueSO 에셋을 선택합니다.
    ///
    ///   3. Inspector 하단의 "JSON 임포트" 버튼을 클릭합니다.
    ///      또는 메뉴: HTH → Campaign → Import Dialogue JSON
    ///
    ///   4. 임포트 완료 후 SO에 데이터가 채워집니다.
    ///
    /// ─── JSON 파일 경로 규칙 ─────────────────────────────────────────────
    ///   StreamingAssets/Campaign/dialogue_{stageId}.json
    ///   예: dialogue_Stage_1_Phase2.json
    ///
    /// ─── 주의사항 ────────────────────────────────────────────────────────
    ///   임포트 시 기존 데이터를 덮어씁니다.
    ///   임포트 전 SO 백업을 권장합니다.
    ///   에디터 전용 스크립트입니다. 빌드에 포함되지 않습니다.
    /// </summary>
    [CustomEditor(typeof(CampaignDialogueSO))]
    public class CampaignDialogueImporter : UnityEditor.Editor
    {
        private const string JSON_FOLDER = "Campaign";

        public override void OnInspectorGUI()
        {
            // 기본 Inspector 표시
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── JSON 임포트 ──────────────────", EditorStyles.boldLabel);

            var so = (CampaignDialogueSO)target;

            // JSON 파일 경로 표시
            string expectedPath = GetJsonPath(so.StageId);
            EditorGUILayout.HelpBox(
                $"JSON 경로:\n{expectedPath}",
                string.IsNullOrEmpty(so.StageId)
                    ? MessageType.Warning
                    : File.Exists(expectedPath) ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(4);

            // 임포트 버튼
            GUI.enabled = !string.IsNullOrEmpty(so.StageId);
            if (GUILayout.Button("JSON에서 대사 임포트", GUILayout.Height(36)))
            {
                ImportFromJson(so);
            }
            GUI.enabled = true;

            // JSON 템플릿 생성 버튼
            EditorGUILayout.Space(4);
            if (GUILayout.Button("JSON 템플릿 생성 (현재 SO 데이터 기준)", GUILayout.Height(28)))
            {
                ExportToJson(so);
            }
        }

        // ── 임포트 ───────────────────────────────────────────────────────

        private void ImportFromJson(CampaignDialogueSO so)
        {
            string path = GetJsonPath(so.StageId);

            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog(
                    "JSON 임포트 실패",
                    $"파일을 찾을 수 없습니다:\n{path}\n\n" +
                    $"StreamingAssets/Campaign/ 폴더에 JSON 파일을 배치해주세요.",
                    "확인");
                return;
            }

            // 임포트 전 확인
            bool confirm = EditorUtility.DisplayDialog(
                "JSON 임포트",
                $"'{so.StageId}' 다이얼로그 데이터를 JSON에서 임포트합니다.\n\n" +
                $"기존 데이터가 모두 덮어씌워집니다. 계속하시겠습니까?",
                "임포트", "취소");

            if (!confirm) return;

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var jsonData = JsonUtility.FromJson<DialogueJsonData>(json);

                if (jsonData == null)
                {
                    Debug.LogError("[CampaignDialogueImporter] JSON 파싱 실패 — 형식을 확인해주세요.");
                    return;
                }

                // SerializedObject로 SO 수정
                var serialized = new SerializedObject(so);

                // 그룹 대사 임포트
                var groupProp = serialized.FindProperty("_groupDialogues");
                ApplyGroupDialogues(groupProp, jsonData.groupDialogues);

                // 단독 대사 임포트
                var soloProp = serialized.FindProperty("_soloDialogues");
                ApplySoloDialogues(soloProp, jsonData.soloDialogues);

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                Debug.Log($"[CampaignDialogueImporter] 임포트 완료 — " +
                          $"그룹 대사 {jsonData.groupDialogues?.Count ?? 0}개, " +
                          $"단독 대사 {jsonData.soloDialogues?.Count ?? 0}개");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"그룹 대사: {jsonData.groupDialogues?.Count ?? 0}개\n" +
                    $"단독 대사: {jsonData.soloDialogues?.Count ?? 0}개",
                    "확인");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CampaignDialogueImporter] 임포트 오류: {e.Message}");
                EditorUtility.DisplayDialog("임포트 오류", e.Message, "확인");
            }
        }

        private void ApplyGroupDialogues(SerializedProperty prop,
                                          List<GroupDialogueJsonData> dataList)
        {
            prop.ClearArray();
            if (dataList == null) return;

            prop.arraySize = dataList.Count;

            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                var elem = prop.GetArrayElementAtIndex(i);

                // ParticipantIds
                var ids = elem.FindPropertyRelative("ParticipantIds");
                ids.ClearArray();
                if (data.participantIds != null)
                {
                    ids.arraySize = data.participantIds.Count;
                    for (int j = 0; j < data.participantIds.Count; j++)
                        ids.GetArrayElementAtIndex(j).intValue = data.participantIds[j];
                }

                // FragmentId
                elem.FindPropertyRelative("FragmentId").stringValue = data.fragmentId ?? "";

                // Condition
                var cond = elem.FindPropertyRelative("Condition");
                cond.FindPropertyRelative("RequiredFragmentId").stringValue =
                    data.condition?.requiredFragmentId ?? "";
                cond.FindPropertyRelative("MinLoopCount").intValue =
                    data.condition?.minLoopCount ?? 0;

                // Lines
                var lines = elem.FindPropertyRelative("Lines");
                ApplyDialogueLines(lines, data.lines);
            }
        }

        private void ApplySoloDialogues(SerializedProperty prop,
                                         List<SoloDialogueJsonData> dataList)
        {
            prop.ClearArray();
            if (dataList == null) return;

            prop.arraySize = dataList.Count;

            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                var elem = prop.GetArrayElementAtIndex(i);

                elem.FindPropertyRelative("CharacterId").intValue = data.characterId;
                elem.FindPropertyRelative("FragmentId").stringValue = data.fragmentId ?? "";

                var cond = elem.FindPropertyRelative("Condition");
                cond.FindPropertyRelative("RequiredFragmentId").stringValue =
                    data.condition?.requiredFragmentId ?? "";
                cond.FindPropertyRelative("MinLoopCount").intValue =
                    data.condition?.minLoopCount ?? 0;

                var lines = elem.FindPropertyRelative("Lines");
                ApplyDialogueLines(lines, data.lines);
            }
        }

        private void ApplyDialogueLines(SerializedProperty prop,
                                         List<DialogueLineJsonData> dataList)
        {
            prop.ClearArray();
            if (dataList == null) return;

            prop.arraySize = dataList.Count;

            for (int i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                var elem = prop.GetArrayElementAtIndex(i);

                elem.FindPropertyRelative("SpeakerId").intValue = data.speakerId;
                elem.FindPropertyRelative("Text").stringValue = data.text ?? "";
                elem.FindPropertyRelative("RevealCharacterId").intValue = data.revealCharacterId;
                elem.FindPropertyRelative("RevealCharacterName").stringValue =
                    data.revealCharacterName ?? "";
            }
        }

        // ── 내보내기 (현재 SO → JSON 템플릿) ─────────────────────────────

        private void ExportToJson(CampaignDialogueSO so)
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, JSON_FOLDER);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string path = GetJsonPath(so.StageId);

            var jsonData = new DialogueJsonData
            {
                stageId = so.StageId,
                groupDialogues = new List<GroupDialogueJsonData>(),
                soloDialogues = new List<SoloDialogueJsonData>()
            };

            // 현재 SO 데이터를 JSON 형식으로 변환
            foreach (var entry in so.GroupDialogues)
            {
                if (entry == null) continue;

                var gData = new GroupDialogueJsonData
                {
                    participantIds = new List<int>(entry.ParticipantIds ?? new List<int>()),
                    fragmentId = entry.FragmentId ?? "",
                    condition = new ConditionJsonData
                    {
                        requiredFragmentId = entry.Condition?.RequiredFragmentId ?? "",
                        minLoopCount = entry.Condition?.MinLoopCount ?? 0
                    },
                    lines = new List<DialogueLineJsonData>()
                };

                foreach (var line in entry.Lines ?? new List<DialogueLine>())
                {
                    if (line == null) continue;
                    gData.lines.Add(new DialogueLineJsonData
                    {
                        speakerId = line.SpeakerId,
                        text = line.Text ?? "",
                        revealCharacterId = line.RevealCharacterId,
                        revealCharacterName = line.RevealCharacterName ?? ""
                    });
                }

                jsonData.groupDialogues.Add(gData);
            }

            string json = JsonUtility.ToJson(jsonData, prettyPrint: true);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[CampaignDialogueImporter] JSON 내보내기 완료 — {path}");
            EditorUtility.DisplayDialog("내보내기 완료", $"저장 위치:\n{path}", "확인");
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        private string GetJsonPath(string stageId)
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                JSON_FOLDER,
                $"dialogue_{stageId}.json");
        }

        // ── 메뉴 항목 ─────────────────────────────────────────────────────

        [MenuItem("HTH/Campaign/Import Dialogue JSON (선택된 SO)")]
        private static void ImportSelectedSO()
        {
            var so = Selection.activeObject as CampaignDialogueSO;
            if (so == null)
            {
                EditorUtility.DisplayDialog(
                    "임포트 실패",
                    "Project에서 CampaignDialogueSO 에셋을 선택한 후 실행해주세요.",
                    "확인");
                return;
            }

            var importer = CreateEditor(so) as CampaignDialogueImporter;
            importer?.ImportFromJson(so);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JSON 데이터 구조 (JsonUtility용)
    // ═══════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class DialogueJsonData
    {
        public string stageId;
        public List<GroupDialogueJsonData> groupDialogues;
        public List<SoloDialogueJsonData> soloDialogues;
    }

    [System.Serializable]
    public class GroupDialogueJsonData
    {
        public List<int> participantIds;
        public string fragmentId;
        public ConditionJsonData condition;
        public List<DialogueLineJsonData> lines;
    }

    [System.Serializable]
    public class SoloDialogueJsonData
    {
        public int characterId;
        public string fragmentId;
        public ConditionJsonData condition;
        public List<DialogueLineJsonData> lines;
    }

    [System.Serializable]
    public class DialogueLineJsonData
    {
        public int speakerId;
        public string text;
        public int revealCharacterId = -1;
        public string revealCharacterName = "";
    }

    [System.Serializable]
    public class ConditionJsonData
    {
        public string requiredFragmentId = "";
        public int minLoopCount = 0;
    }
}
#endif