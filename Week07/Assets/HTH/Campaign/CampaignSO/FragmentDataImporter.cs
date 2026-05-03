#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// StreamingAssets/Campaign 폴더의 JSON 파일에서
    /// FragmentDataSO 데이터를 임포트하는 에디터 도구입니다.
    ///
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/fragment_CampaignMode.json
    ///
    /// ─── JSON 형식 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "CampaignMode",
    ///     "fragments": [
    ///       {
    ///         "profileClueId": "P02_01",
    ///         "characterId": 2,
    ///         "clueIndex": 1,
    ///         "clueText": "메이는 용병단의 침묵이...",
    ///         "hintText": "메이는 용병단이 원래부터...",
    ///         "hintDescription": "훈련장에서 용병단 신입과의 첫 인사",
    ///         "combo": [1, 2],
    ///         "placeName": "훈련장",
    ///         "deadRequired": [],
    ///         "prerequisites": [],
    ///         "simultaneousIds": [],
    ///         "isForcedExitFragment": false,
    ///         "lines": [
    ///           { "textId": "1", "text": "메이님 안녕하세요!" },
    ///           { "textId": "2", "text": "신입? 무슨 일이야." }
    ///         ],
    ///         "designerNote": ""
    ///       }
    ///     ]
    ///   }
    /// </summary>
    [CustomEditor(typeof(FragmentDataSO))]
    public class FragmentDataImporter : UnityEditor.Editor
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
                "필드 구조:\n" +
                "  profileClueId       : \"P02_01\"\n" +
                "  characterId / clueIndex\n" +
                "  clueText            : 진실 문장 조각\n" +
                "  hintText            : 거짓/유도 문장 조각\n" +
                "  hintDescription     : 미획득 힌트 설명\n" +
                "  combo               : [1, 2] (같은 Zone 캐릭터)\n" +
                "  placeName           : \"훈련장\" / \"예배실\" / \"창고\" / \"여관\" / \"\"\n" +
                "  deadRequired        : [3, 4] (사망 필수)\n" +
                "  prerequisites       : [\"P02_01\"] (사전 획득)\n" +
                "  simultaneousIds     : [\"P05_03\"] (동시 획득)\n" +
                "  isForcedExitFragment: true / false\n" +
                "  lines               : [{textId, text}, ...]",
                MessageType.Info);
        }

        private void ImportFromStreamingAssets(string fileName)
        {
            EnsureFolderExists();
            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[FragmentDataImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog("파일 없음", $"파일을 찾을 수 없습니다.\n{path}", "확인");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<FragmentJsonRoot>(json);
                if (data == null)
                {
                    Debug.LogError("[FragmentDataImporter] JSON 파싱 실패");
                    return;
                }

                var so = (FragmentDataSO)target;
                var serialized = new SerializedObject(so);

                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                var fragsProp = serialized.FindProperty("_fragments");
                fragsProp.ClearArray();

                if (data.fragments != null)
                {
                    for (int i = 0; i < data.fragments.Length; i++)
                    {
                        var f = data.fragments[i];
                        fragsProp.InsertArrayElementAtIndex(i);
                        var elem = fragsProp.GetArrayElementAtIndex(i);

                        // 식별
                        elem.FindPropertyRelative("ProfileClueId").stringValue = f.profileClueId ?? "";
                        elem.FindPropertyRelative("CharacterId").intValue = f.characterId;
                        elem.FindPropertyRelative("ClueIndex").intValue = f.clueIndex;

                        // 인물 기록장
                        elem.FindPropertyRelative("ClueText").stringValue = f.clueText ?? "";
                        elem.FindPropertyRelative("HintText").stringValue = f.hintText ?? "";
                        elem.FindPropertyRelative("HintDescription").stringValue = f.hintDescription ?? "";

                        // 획득 조건
                        WriteIntList(elem.FindPropertyRelative("Combo"), f.combo);
                        elem.FindPropertyRelative("PlaceName").stringValue = f.placeName ?? "";
                        WriteIntList(elem.FindPropertyRelative("DeadRequired"), f.deadRequired);
                        WriteStringList(elem.FindPropertyRelative("Prerequisites"), f.prerequisites);

                        // 특수 규칙
                        WriteStringList(elem.FindPropertyRelative("SimultaneousIds"), f.simultaneousIds);
                        elem.FindPropertyRelative("IsForcedExitFragment").boolValue = f.isForcedExitFragment;

                        // 대사
                        WriteLines(elem.FindPropertyRelative("Lines"), f.lines);

                        // 메모
                        elem.FindPropertyRelative("DesignerNote").stringValue = f.designerNote ?? "";
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                int count = data.fragments?.Length ?? 0;
                Debug.Log($"[FragmentDataImporter] 임포트 완료 — StageId:{data.stageId}, 조각:{count}개");
                EditorUtility.DisplayDialog("임포트 완료",
                    $"StageId: {data.stageId}\n조각 수: {count}개", "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FragmentDataImporter] 실패 — {e.Message}");
                EditorUtility.DisplayDialog("임포트 실패", e.Message, "확인");
            }
        }

        private static void WriteIntList(SerializedProperty prop, int[] values)
        {
            prop.ClearArray();
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).intValue = values[i];
            }
        }

        private static void WriteStringList(SerializedProperty prop, string[] values)
        {
            prop.ClearArray();
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
            }
        }

        private static void WriteLines(SerializedProperty prop, FragmentJsonLine[] lines)
        {
            prop.ClearArray();
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                var lp = prop.GetArrayElementAtIndex(i);
                lp.FindPropertyRelative("TextId").stringValue = lines[i].textId ?? "";
                lp.FindPropertyRelative("Text").stringValue = lines[i].text ?? "";
            }
        }

        private void OpenCampaignFolder()
        {
            EnsureFolderExists();
            EditorUtility.RevealInFinder(CampaignFolderPath);
        }

        private static void EnsureFolderExists()
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
    internal class FragmentJsonRoot
    {
        public string stageId;
        public FragmentJsonEntry[] fragments;
    }

    [Serializable]
    internal class FragmentJsonEntry
    {
        public string profileClueId;
        public int characterId;
        public int clueIndex;
        public string clueText;
        public string hintText;
        public string hintDescription;
        public int[] combo;
        public string placeName;
        public int[] deadRequired;
        public string[] prerequisites;
        public string[] simultaneousIds;
        public bool isForcedExitFragment;
        public FragmentJsonLine[] lines;
        public string designerNote;
    }

    [Serializable]
    internal class FragmentJsonLine
    {
        public string textId;
        public string text;
    }
}
#endif