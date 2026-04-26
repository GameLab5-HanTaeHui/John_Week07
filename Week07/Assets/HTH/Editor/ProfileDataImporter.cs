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
    ///         "requiredFragmentCount": 3,
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
    ///         "epilogueText": "시점 완결문"
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
                "⚠ CharacterIcon, CardIllustration (Sprite)은\n" +
                "JSON으로 설정 불가합니다.\n" +
                "임포트 후 Inspector에서 직접 연결하세요.",
                MessageType.Warning);

            EditorGUILayout.HelpBox(
                "JSON 형식:\n" +
                "{\n" +
                "  \"stageId\": \"Stage_1_Phase2\",\n" +
                "  \"characters\": [\n" +
                "    {\n" +
                "      \"characterId\": 1,\n" +
                "      \"requiredFragmentCount\": 3,\n" +
                "      \"characterFullName\": \"엔비\",\n" +
                "      \"characterRole\": \"(주인공)\",\n" +
                "      \"profileItems\": [...],\n" +
                "      \"conceptCard\": {...},\n" +
                "      \"epilogueText\": \"...\"\n" +
                "    }\n" +
                "  ]\n" +
                "}",
                MessageType.Info);
        }

        /// <summary>
        /// StreamingAssets/Campaign/{fileName}.json을 읽어
        /// ProfileDataSO에 데이터를 입력합니다.
        /// </summary>
        private void ImportFromStreamingAssets(string fileName)
        {
            // 폴더 없으면 생성
            EnsureCampaignFolderExists();

            string path = Path.Combine(CampaignFolderPath, $"{fileName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"[ProfileDataImporter] 파일 없음 — {path}");
                EditorUtility.DisplayDialog(
                    "파일 없음",
                    $"파일을 찾을 수 없습니다.\n{path}",
                    "확인");
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

                // stageId 설정
                serialized.FindProperty("_stageId").stringValue = data.stageId ?? "";

                // characterProfiles 리스트 초기화
                var profilesProp = serialized.FindProperty("_characterProfiles");
                profilesProp.ClearArray();

                if (data.characters != null)
                {
                    for (int i = 0; i < data.characters.Length; i++)
                    {
                        var charData = data.characters[i];
                        profilesProp.InsertArrayElementAtIndex(i);
                        var charProp = profilesProp.GetArrayElementAtIndex(i);

                        // 기본 필드 설정
                        charProp.FindPropertyRelative("CharacterId").intValue
                            = charData.characterId;
                        charProp.FindPropertyRelative("RequiredFragmentCount").intValue
                            = charData.requiredFragmentCount;
                        charProp.FindPropertyRelative("CharacterFullName").stringValue
                            = charData.characterFullName ?? "";
                        charProp.FindPropertyRelative("CharacterRole").stringValue
                            = charData.characterRole ?? "";
                        charProp.FindPropertyRelative("EpilogueText").stringValue
                            = charData.epilogueText ?? "";

                        // ProfileItems 설정
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

                                // Choices 설정
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

                        // ConceptCard 설정
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

                Debug.Log($"[ProfileDataImporter] 임포트 완료 — " +
                          $"StageId: {data.stageId}, " +
                          $"캐릭터 수: {data.characters?.Length ?? 0}, " +
                          $"파일: {path}");

                EditorUtility.DisplayDialog(
                    "임포트 완료",
                    $"StageId: {data.stageId}\n캐릭터 수: {data.characters?.Length ?? 0}\n\n" +
                    "⚠ CharacterIcon, CardIllustration은\nInspector에서 직접 연결하세요.",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileDataImporter] 임포트 실패 — {e.Message}");
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
                Debug.Log($"[ProfileDataImporter] 폴더 생성 — {CampaignFolderPath}");
            }
        }
    }

    // ── JSON 데이터 구조 ──────────────────────────────────────────────────

    [Serializable]
    internal class ProfileJsonRoot
    {
        /// <summary>스테이지 ID입니다.</summary>
        public string stageId;

        /// <summary>캐릭터별 프로파일 데이터 배열입니다.</summary>
        public ProfileJsonCharacter[] characters;
    }

    [Serializable]
    internal class ProfileJsonCharacter
    {
        /// <summary>캐릭터 ID입니다. (1~7)</summary>
        public int characterId;

        /// <summary>프로파일 추리 가능 최소 대화 조각 수입니다.</summary>
        public int requiredFragmentCount;

        /// <summary>캐릭터 이름입니다.</summary>
        public string characterFullName;

        /// <summary>캐릭터 역할입니다.</summary>
        public string characterRole;

        /// <summary>프로파일 항목 배열입니다.</summary>
        public ProfileJsonItem[] profileItems;

        /// <summary>컨셉 카드 데이터입니다.</summary>
        public ProfileJsonConceptCard conceptCard;

        /// <summary>시점 완결문입니다.</summary>
        public string epilogueText;
    }

    [Serializable]
    internal class ProfileJsonItem
    {
        /// <summary>프로파일 질문입니다.</summary>
        public string question;

        /// <summary>선택지 배열입니다.</summary>
        public string[] choices;

        /// <summary>정답 선택지 인덱스입니다. (0-based)</summary>
        public int correctChoiceIndex;

        /// <summary>추리 가능 조건 FragmentId입니다.</summary>
        public string requiredFragmentId;
    }

    [Serializable]
    internal class ProfileJsonConceptCard
    {
        /// <summary>캐치프레이즈입니다.</summary>
        public string catchphrase;

        /// <summary>외형적 특징입니다.</summary>
        public string appearance;

        /// <summary>서사적 배경입니다.</summary>
        public string narrativeBackground;

        /// <summary>성격 및 행동 원리입니다.</summary>
        public string personality;

        /// <summary>기믹 연관성입니다.</summary>
        public string gimmickRelevance;
    }
}
#endif