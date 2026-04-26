#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// StreamingAssets/Campaign 폴더의 JSON 파일에서
    /// FragmentHintDataSO 데이터를 임포트하는 에디터 도구입니다.
    ///
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/hint_{stageId}.json
    ///   예: StreamingAssets/Campaign/hint_Stage_1_Phase2.json
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   1. StreamingAssets/Campaign/ 폴더에 JSON 파일 배치
    ///   2. FragmentHintDataSO 에셋 선택
    ///   3. Inspector 하단 → StageId 입력
    ///   4. "Import From StreamingAssets" 버튼 클릭
    ///
    /// ─── JSON 형식 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "Stage_1_Phase2",
    ///     "characters": [
    ///       {
    ///         "characterId": 1,
    ///         "hints": ["힌트1", "힌트2", "힌트3", "힌트4", "힌트5"]
    ///       }
    ///     ]
    ///   }
    /// </summary>
    [CustomEditor(typeof(FragmentHintDataSO))]
    public class FragmentHintDataImporter : UnityEditor.Editor
    {
        /// <summary>임포트할 JSON 파일명 (확장자 제외)입니다.</summary>
        private string _fileNameInput = "";

        /// <summary>StreamingAssets/Campaign 폴더 경로입니다.</summary>
        private static string CampaignFolderPath
            => Path.Combine(Application.streamingAssetsPath, "Campaign");

        public override void OnInspectorGUI()
        {
            // 기본 Inspector 표시
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── StreamingAssets 임포트 ──", EditorStyles.boldLabel);

            // 파일명 입력 필드
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("파일명", GUILayout.Width(50));
            _fileNameInput = EditorGUILayout.TextField(_fileNameInput);
            EditorGUILayout.LabelField(".json", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            // 경로 미리보기
            string previewPath = Path.Combine(CampaignFolderPath, $"{_fileNameInput}.json");
            EditorGUILayout.HelpBox($"경로: {previewPath}", MessageType.None);

            EditorGUILayout.Space(4);

            // 임포트 버튼
            GUI.enabled = !string.IsNullOrEmpty(_fileNameInput);
            if (GUILayout.Button("Import From StreamingAssets", GUILayout.Height(30)))
                ImportFromStreamingAssets(_fileNameInput);
            GUI.enabled = true;

            // 폴더 열기 버튼
            if (GUILayout.Button("StreamingAssets/Campaign 폴더 열기", GUILayout.Height(24)))
                OpenCampaignFolder();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "JSON 형식:\n" +
                "{\n" +
                "  \"stageId\": \"Stage_1_Phase2\",\n" +
                "  \"characters\": [\n" +
                "    {\n" +
                "      \"characterId\": 1,\n" +
                "      \"hints\": [\"힌트1\", \"힌트2\", ...]\n" +
                "    }\n" +
                "  ]\n" +
                "}",
                MessageType.Info);
        }

        /// <summary>
        /// StreamingAssets/Campaign/{fileName}.json을 읽어
        /// FragmentHintDataSO에 데이터를 입력합니다.
        /// </summary>
        private void ImportFromStreamingAssets(string fileName)
        {
            // 폴더 없으면 생성
            EnsureCampaignFolderExists();

            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[FragmentHintDataImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog(
                    "파일 없음",
                    $"파일을 찾을 수 없습니다.\n{path}",
                    "확인");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<FragmentHintJsonRoot>(json);

                if (data == null)
                {
                    Debug.LogError("[FragmentHintDataImporter] JSON 파싱 실패 — 형식을 확인해주세요.");
                    return;
                }

                var so = (FragmentHintDataSO)target;
                var serialized = new SerializedObject(so);

                // stageId 설정
                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                // characterHints 리스트 초기화 및 데이터 입력
                var hintsProp = serialized.FindProperty("_characterHints");
                hintsProp.ClearArray();

                if (data.characters != null)
                {
                    for (int i = 0; i < data.characters.Length; i++)
                    {
                        var charData = data.characters[i];
                        hintsProp.InsertArrayElementAtIndex(i);
                        var elem = hintsProp.GetArrayElementAtIndex(i);

                        // CharacterId 설정
                        elem.FindPropertyRelative("CharacterId").intValue = charData.characterId;

                        // Hints 리스트 설정
                        var hintList = elem.FindPropertyRelative("Hints");
                        hintList.ClearArray();

                        if (charData.hints != null)
                        {
                            for (int j = 0; j < charData.hints.Length; j++)
                            {
                                hintList.InsertArrayElementAtIndex(j);
                                hintList.GetArrayElementAtIndex(j).stringValue
                                    = charData.hints[j] ?? "";
                            }
                        }
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                Debug.Log($"[FragmentHintDataImporter] 임포트 완료 — " +
                          $"StageId: {data.stageId}, " +
                          $"캐릭터 수: {data.characters?.Length ?? 0}, " +
                          $"파일: {path}");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"StageId: {data.stageId}\n캐릭터 수: {data.characters?.Length ?? 0}",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FragmentHintDataImporter] 임포트 실패 — {e.Message}");
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
                Debug.Log($"[FragmentHintDataImporter] 폴더 생성 — {CampaignFolderPath}");
            }
        }
    }

    // ── JSON 데이터 구조 ──────────────────────────────────────────────────

    [Serializable]
    internal class FragmentHintJsonRoot
    {
        /// <summary>스테이지 ID입니다. 예: "Stage_1_Phase2"</summary>
        public string stageId;

        /// <summary>캐릭터별 힌트 데이터 배열입니다.</summary>
        public FragmentHintJsonCharacter[] characters;
    }

    [Serializable]
    internal class FragmentHintJsonCharacter
    {
        /// <summary>캐릭터 ID입니다. (1~7)</summary>
        public int characterId;

        /// <summary>힌트 텍스트 배열입니다.</summary>
        public string[] hints;
    }
}
#endif