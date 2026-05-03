#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// StreamingAssets/Campaign 폴더의 JSON 파일에서
    /// ProfileDataSO 데이터를 임포트하는 에디터 도구입니다.
    ///
    /// ─── ProfileDataSO 구조 변경 반영 ────────────────────────────────────
    ///   ProfileItems / ConceptCard / EpilogueLines / Question 제거.
    ///   FinalTalkData(도입 대사 + 선택지 4개 + 결과 대화) 기반으로 재작성.
    ///
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/profile_{stageId}.json
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   1. StreamingAssets/Campaign/ 폴더에 JSON 파일 배치
    ///   2. ProfileDataSO 에셋 선택
    ///   3. Inspector 하단 → 파일명 입력
    ///   4. "Import From StreamingAssets" 버튼 클릭
    ///
    /// ─── 주의사항 ─────────────────────────────────────────────────────────
    ///   CharacterIcon (Sprite) 은 JSON으로 설정 불가합니다.
    ///   임포트 후 Inspector에서 직접 연결하세요.
    ///
    /// ─── JSON 형식 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "CampaignMode",
    ///     "characters": [
    ///       {
    ///         "characterId": 2,
    ///         "characterFullName": "메이",
    ///         "characterRole": "(전위 돌격형)",
    ///         "finalTalk": {
    ///           "introLines": [
    ///             { "speakerId": 2, "text": "무슨 일이지, 엔비?" },
    ///             { "speakerId": 1, "text": "그날 일 때문에요." }
    ///           ],
    ///           "choices": ["선택지1", "선택지2", "선택지3", "선택지4"],
    ///           "correctIndex": 2,
    ///           "resultDialogues": [
    ///             {
    ///               "choiceIndex": 2,
    ///               "isSuccess": true,
    ///               "lines": [
    ///                 { "speakerId": 2, "text": "...그때도 결국 루이스였다는 거네." }
    ///               ]
    ///             }
    ///           ]
    ///         }
    ///       }
    ///     ]
    ///   }
    /// </summary>
    [CustomEditor(typeof(ProfileDataSO))]
    public class ProfileDataImporter : UnityEditor.Editor
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
                "⚠ CharacterIcon (Sprite) 은 JSON으로 설정 불가합니다.\n" +
                "임포트 후 Inspector에서 직접 연결하세요.",
                MessageType.Warning);

            EditorGUILayout.HelpBox(
                "FinalTalk 구조:\n" +
                "  introLines  : [{speakerId, text}, ...]\n" +
                "  choices     : [\"선택지0\", \"선택지1\", \"선택지2\", \"선택지3\"]\n" +
                "  correctIndex: 0~3\n" +
                "  resultDialogues: [{choiceIndex, isSuccess, lines:[...]}]\n\n" +
                "미구현 캐릭터는 finalTalk 항목 자체를 생략하세요.",
                MessageType.Info);
        }

        private void ImportFromStreamingAssets(string fileName)
        {
            EnsureCampaignFolderExists();

            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[ProfileDataImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog("파일 없음", $"파일을 찾을 수 없습니다.\n{path}", "확인");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<ProfileJsonRoot>(json);

                if (data == null)
                {
                    Debug.LogError("[ProfileDataImporter] JSON 파싱 실패 — 형식을 확인해주세요.");
                    return;
                }

                var so = (ProfileDataSO)target;
                var serialized = new SerializedObject(so);

                // stageId
                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                // _characterProfiles 초기화
                var profilesProp = serialized.FindProperty("_characterProfiles");
                profilesProp.ClearArray();

                if (data.characters != null)
                {
                    for (int i = 0; i < data.characters.Length; i++)
                    {
                        var charData = data.characters[i];
                        profilesProp.InsertArrayElementAtIndex(i);
                        var charProp = profilesProp.GetArrayElementAtIndex(i);

                        // ── 기본 필드 ─────────────────────────────────────
                        charProp.FindPropertyRelative("CharacterId").intValue
                            = charData.characterId;
                        charProp.FindPropertyRelative("CharacterFullName").stringValue
                            = charData.characterFullName ?? "";
                        charProp.FindPropertyRelative("CharacterRole").stringValue
                            = charData.characterRole ?? "";

                        // ── FinalTalkData ─────────────────────────────────
                        var ftProp = charProp.FindPropertyRelative("FinalTalk");

                        if (charData.finalTalk != null)
                        {
                            var ft = charData.finalTalk;

                            // IntroLines
                            WriteLines(ftProp.FindPropertyRelative("IntroLines"), ft.introLines);

                            // Question — 사용하지 않으므로 공란으로 설정
                            ftProp.FindPropertyRelative("Question").stringValue = "";

                            // Choices (4개 고정)
                            var choicesProp = ftProp.FindPropertyRelative("Choices");
                            choicesProp.ClearArray();
                            if (ft.choices != null)
                            {
                                for (int k = 0; k < ft.choices.Length; k++)
                                {
                                    choicesProp.InsertArrayElementAtIndex(k);
                                    choicesProp.GetArrayElementAtIndex(k).stringValue
                                        = ft.choices[k] ?? "";
                                }
                            }

                            // CorrectIndex
                            ftProp.FindPropertyRelative("CorrectIndex").intValue
                                = ft.correctIndex;

                            // ResultDialogues
                            var resultsProp = ftProp.FindPropertyRelative("ResultDialogues");
                            resultsProp.ClearArray();
                            if (ft.resultDialogues != null)
                            {
                                for (int r = 0; r < ft.resultDialogues.Length; r++)
                                {
                                    var rd = ft.resultDialogues[r];
                                    resultsProp.InsertArrayElementAtIndex(r);
                                    var rdProp = resultsProp.GetArrayElementAtIndex(r);

                                    rdProp.FindPropertyRelative("ChoiceIndex").intValue
                                        = rd.choiceIndex;
                                    rdProp.FindPropertyRelative("IsSuccess").boolValue
                                        = rd.isSuccess;
                                    WriteLines(rdProp.FindPropertyRelative("Lines"), rd.lines);
                                }
                            }
                        }
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();

                int charCount = data.characters?.Length ?? 0;
                Debug.Log($"[ProfileDataImporter] 임포트 완료 — " +
                          $"StageId: {data.stageId}, 캐릭터 수: {charCount}");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"StageId: {data.stageId}\n캐릭터 수: {charCount}\n\n" +
                    "⚠ CharacterIcon은 Inspector에서 직접 연결하세요.",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileDataImporter] 임포트 실패 — {e.Message}");
                EditorUtility.DisplayDialog("임포트 실패", e.Message, "확인");
            }
        }

        /// <summary>FinalTalkLine 배열을 SerializedProperty 리스트에 씁니다.</summary>
        private static void WriteLines(SerializedProperty listProp,
                                       ProfileJsonLine[] lines)
        {
            listProp.ClearArray();
            if (lines == null) return;

            for (int i = 0; i < lines.Length; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                var lineProp = listProp.GetArrayElementAtIndex(i);
                lineProp.FindPropertyRelative("SpeakerId").intValue = lines[i].speakerId;
                lineProp.FindPropertyRelative("Text").stringValue = lines[i].text ?? "";
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
                Debug.Log($"[ProfileDataImporter] 폴더 생성 — {CampaignFolderPath}");
            }
        }
    }

    // ── JSON 데이터 구조 ──────────────────────────────────────────────────

    [Serializable]
    internal class ProfileJsonRoot
    {
        public string stageId;
        public ProfileJsonCharacter[] characters;
    }

    [Serializable]
    internal class ProfileJsonCharacter
    {
        public int characterId;
        public string characterFullName;
        public string characterRole;
        public ProfileJsonFinalTalk finalTalk;
    }

    [Serializable]
    internal class ProfileJsonFinalTalk
    {
        public ProfileJsonLine[] introLines;
        public string[] choices;
        public int correctIndex;
        public ProfileJsonResultDialogue[] resultDialogues;
    }

    [Serializable]
    internal class ProfileJsonResultDialogue
    {
        public int choiceIndex;
        public bool isSuccess;
        public ProfileJsonLine[] lines;
    }

    [Serializable]
    internal class ProfileJsonLine
    {
        public int speakerId;
        public string text;
    }
}
#endif