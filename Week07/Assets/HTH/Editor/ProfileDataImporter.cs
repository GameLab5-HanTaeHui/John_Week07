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
    /// ─── 파일 경로 ───────────────────────────────────────────────────────
    ///   StreamingAssets/Campaign/profile_{stageId}.json
    ///   예: StreamingAssets/Campaign/profile_Stage_1_Phase2.json
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   1. StreamingAssets/Campaign/ 폴더에 JSON 파일 배치
    ///   2. ProfileDataSO 에셋 선택
    ///   3. Inspector 하단 → 파일명 입력
    ///   4. "Import From StreamingAssets" 버튼 클릭
    ///
    /// ─── 주의사항 ─────────────────────────────────────────────────────────
    ///   CharacterIcon, CardIllustration (Sprite) 은 JSON으로 설정 불가합니다.
    ///   임포트 후 Inspector에서 직접 연결하세요.
    ///
    /// ─── JSON 형식 ───────────────────────────────────────────────────────
    ///   {
    ///     "stageId": "Stage_1_Phase2",
    ///     "characters": [
    ///       {
    ///         "characterId": 1,
    ///         "requiredFragmentCount": 5,
    ///         "characterFullName": "엔비",
    ///         "characterRole": "(주인공)",
    ///         "profileItems": [
    ///           {
    ///             "question": "가장 두려워하는 것",
    ///             "choices": ["선택지1", "선택지2", "선택지3"],
    ///             "correctChoiceIndex": 0,
    ///             "requiredFragmentId": ""
    ///           }
    ///         ],
    ///         "conceptCard": {
    ///           "catchphrase": "캐치프레이즈",
    ///           "appearance": "외형 설명",
    ///           "narrativeBackground": "서사적 배경",
    ///           "personality": "성격",
    ///           "gimmickRelevance": "기믹 연관성"
    ///         },
    ///         "epilogueLines": ["문단1", "문단2", "..."]
    ///       }
    ///     ]
    ///   }
    /// </summary>
    [CustomEditor(typeof(ProfileDataSO))]
    public class ProfileDataImporter : UnityEditor.Editor
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
                "⚠ CharacterIcon, CardIllustration (Sprite)은\n" +
                "JSON으로 설정 불가합니다.\n" +
                "임포트 후 Inspector에서 직접 연결하세요.",
                MessageType.Warning);

            EditorGUILayout.HelpBox(
                "epilogueLines: 문단 단위 배열\n" +
                "예: [\"나는 늘...\", \"그래서...\"]\n\n" +
                "epilogueText: 단일 문자열 (하위 호환, 자동 분리)",
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

                // characterProfiles 초기화
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
                        charProp.FindPropertyRelative("RequiredFragmentCount").intValue
                            = charData.requiredFragmentCount;
                        charProp.FindPropertyRelative("CharacterFullName").stringValue
                            = charData.characterFullName ?? "";
                        charProp.FindPropertyRelative("CharacterRole").stringValue
                            = charData.characterRole ?? "";

                        // ── EpilogueLines ─────────────────────────────────
                        var epilogueProp = charProp.FindPropertyRelative("EpilogueLines");
                        epilogueProp.ClearArray();

                        if (charData.epilogueLines != null && charData.epilogueLines.Length > 0)
                        {
                            // 신규: epilogueLines 배열 사용
                            for (int j = 0; j < charData.epilogueLines.Length; j++)
                            {
                                epilogueProp.InsertArrayElementAtIndex(j);
                                epilogueProp.GetArrayElementAtIndex(j).stringValue
                                    = charData.epilogueLines[j] ?? "";
                            }
                        }
                        else if (!string.IsNullOrEmpty(charData.epilogueText))
                        {
                            // 하위 호환: epilogueText를 줄바꿈 기준으로 분리
                            var lines = charData.epilogueText.Split(
                                new[] { "\n\n", "\r\n\r\n" },
                                StringSplitOptions.RemoveEmptyEntries);

                            for (int j = 0; j < lines.Length; j++)
                            {
                                epilogueProp.InsertArrayElementAtIndex(j);
                                epilogueProp.GetArrayElementAtIndex(j).stringValue
                                    = lines[j].Trim();
                            }
                        }

                        // ── ProfileItems ──────────────────────────────────
                        var itemsProp = charProp.FindPropertyRelative("ProfileItems");
                        itemsProp.ClearArray();

                        if (charData.profileItems != null)
                        {
                            for (int j = 0; j < charData.profileItems.Length; j++)
                            {
                                var itemData = charData.profileItems[j];
                                itemsProp.InsertArrayElementAtIndex(j);
                                var itemProp = itemsProp.GetArrayElementAtIndex(j);

                                itemProp.FindPropertyRelative("Question").stringValue
                                    = itemData.question ?? "";
                                itemProp.FindPropertyRelative("CorrectChoiceIndex").intValue
                                    = itemData.correctChoiceIndex;
                                itemProp.FindPropertyRelative("RequiredFragmentId").stringValue
                                    = itemData.requiredFragmentId ?? "";

                                // Choices
                                var choicesProp = itemProp.FindPropertyRelative("Choices");
                                choicesProp.ClearArray();

                                if (itemData.choices != null)
                                {
                                    for (int k = 0; k < itemData.choices.Length; k++)
                                    {
                                        choicesProp.InsertArrayElementAtIndex(k);
                                        choicesProp.GetArrayElementAtIndex(k).stringValue
                                            = itemData.choices[k] ?? "";
                                    }
                                }
                            }
                        }

                        // ── ConceptCard ───────────────────────────────────
                        if (charData.conceptCard != null)
                        {
                            var cardProp = charProp.FindPropertyRelative("ConceptCard");
                            cardProp.FindPropertyRelative("Catchphrase").stringValue
                                = charData.conceptCard.catchphrase ?? "";
                            cardProp.FindPropertyRelative("Appearance").stringValue
                                = charData.conceptCard.appearance ?? "";
                            cardProp.FindPropertyRelative("NarrativeBackground").stringValue
                                = charData.conceptCard.narrativeBackground ?? "";
                            cardProp.FindPropertyRelative("Personality").stringValue
                                = charData.conceptCard.personality ?? "";
                            cardProp.FindPropertyRelative("GimmickRelevance").stringValue
                                = charData.conceptCard.gimmickRelevance ?? "";
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
                    "⚠ CharacterIcon, CardIllustration은\nInspector에서 직접 연결하세요.",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileDataImporter] 임포트 실패 — {e.Message}");
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
        public int requiredFragmentCount;
        public string characterFullName;
        public string characterRole;
        public ProfileJsonItem[] profileItems;
        public ProfileJsonConceptCard conceptCard;

        /// <summary>시점 완결문 문단 배열입니다. (신규)</summary>
        public string[] epilogueLines;

        /// <summary>시점 완결문 단일 문자열입니다. (하위 호환)</summary>
        public string epilogueText;
    }

    [Serializable]
    internal class ProfileJsonItem
    {
        public string question;
        public string[] choices;
        public int correctChoiceIndex;
        public string requiredFragmentId;
    }

    [Serializable]
    internal class ProfileJsonConceptCard
    {
        public string catchphrase;
        public string appearance;
        public string narrativeBackground;
        public string personality;
        public string gimmickRelevance;
    }
}
#endif