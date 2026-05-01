using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 개별 캐릭터의 프로파일 추리 UI입니다.
    ///
    /// ─── 핵심 구조 ───────────────────────────────────────────────────────
    ///   질문 6개를 한 번에 하나씩 순서대로 제시합니다.
    ///   각 질문마다 4개의 객관식 선택지 버튼이 표시됩니다.
    ///   선택지 중 하나를 클릭하면 해당 질문의 답이 기록됩니다.
    ///
    /// ─── 버튼 동작 ───────────────────────────────────────────────────────
    ///   Close(뒤로가기):
    ///     현재 질문에서 뒤로 → 이전 질문으로 이동
    ///     첫 번째 질문에서 뒤로 → 패널 닫기
    ///     TMP 문구: "더 생각하기"
    ///
    ///   Submit(인물 추리 하기):
    ///     6개 질문을 모두 답한 뒤 활성화
    ///     기존 정답 판정 로직 유지
    ///     TMP 문구: "인물 추리 하기"
    ///
    /// ─── 정답 판정 ───────────────────────────────────────────────────────
    ///   각 질문의 정답 선택지(IsClue == true)를 모두 선택하면 정답
    ///   오답 → 검은 화면 + 소설체 문구 → 패널 닫고 인게임 복귀
    ///   정답 → 에필로그 재생
    ///   6명 전부 정답 → 최종 엔딩 문구 + 로비 이동
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data           → ProfileDataSO 에셋
    ///   Profile Clue Data      → ProfileClueDataSO 에셋
    ///   Fragment Collector     → FragmentCollector
    ///   Reward Save Data       → RewardSaveData 에셋
    ///   Panel                  → 전체 패널 GameObject
    ///   Character Name Text    → 캐릭터 이름 TMP
    ///   Question Text          → 질문 내용 TMP
    ///   Step Indicator Text    → "3 / 6" 형식 진행도 TMP
    ///   Choice Buttons[4]      → 선택지 버튼 4개
    ///   Choice Texts[4]        → 선택지 버튼 내 TMP 4개
    ///   Submit Button          → 인물 추리 하기 버튼
    ///   Close Button           → 더 생각하기(뒤로가기) 버튼
    ///   Wrong Answer Panel     → 오답 검은 화면 패널 (CanvasGroup 필요)
    ///   Wrong Answer Text      → 소설체 문구 TMP
    ///   Wrong Answer Lines     → 오답 문구 목록
    ///   Correct Answer Line    → 정답 문구
    ///   Ending Line            → 6명 전부 정답 시 최종 엔딩 문구
    ///   Fade Duration          → 페이드 시간 (초)
    ///   Display Time           → 오답 문구 표시 시간 (초)
    ///   Epilogue Player        → 에필로그 DialoguePlayer
    ///   Select Panel           → ProfileInquirySelectPanel (닫기용)
    ///   Total Characters       → 전체 추리 대상 캐릭터 수 (기본 6)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquiryUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 프로파일 데이터입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Tooltip("진실(CLUE)/거짓(HINT) 조각 텍스트 데이터입니다.")]
        [SerializeField] private ProfileClueDataSO _profileClueData;

        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("보상 해금 기록 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("UI 루트")]
        [Tooltip("전체 패널 GameObject입니다. (기본 비활성)")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 정보")]
        [Tooltip("캐릭터 이름을 표시하는 TMP입니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Header("질문 UI")]
        [Tooltip("현재 질문 내용을 표시하는 TMP입니다.")]
        [SerializeField] private TMP_Text _questionText;

        [Tooltip("진행도를 표시하는 TMP입니다. 예: '3 / 6'")]
        [SerializeField] private TMP_Text _stepIndicatorText;

        [Header("선택지 버튼 (4개)")]
        [Tooltip("선택지 버튼 4개를 순서대로 연결합니다.")]
        [SerializeField] private Button[] _choiceButtons = new Button[4];

        [Tooltip("선택지 버튼 내 TMP 4개를 순서대로 연결합니다.")]
        [SerializeField] private TMP_Text[] _choiceTexts = new TMP_Text[4];

        [Header("버튼")]
        [Tooltip("6개 질문을 모두 답하면 활성화됩니다.\nTMP 문구: '인물 추리 하기'")]
        [SerializeField] private Button _submitButton;

        [Tooltip("뒤로가기 버튼입니다.\n현재 질문 취소 또는 패널 닫기.\nTMP 문구: '더 생각하기'")]
        [SerializeField] private Button _closeButton;

        [Header("오답 처리")]
        [Tooltip("오답/정답 시 표시할 검은 화면 패널입니다. CanvasGroup 컴포넌트 필요.")]
        [SerializeField] private GameObject _wrongAnswerPanel;

        [Tooltip("소설체 문구 TMP입니다.")]
        [SerializeField] private TMP_Text _wrongAnswerText;

        [Tooltip("오답 시 랜덤으로 표시될 소설체 문구 목록입니다.")]
        [SerializeField]
        private List<string> _wrongAnswerLines = new()
        {
            "이건… 내가 원하던 모습이 아니야.",
            "아직 보이지 않는 것들이 있어. 더 들어야 해.",
            "틀렸어. 이 사람을 나는 아직 모르는 거야.",
            "뭔가 어긋나 있어. 다시 처음부터 생각해야 해.",
            "나는 그들의 말을 듣고 있었지만, 진심은 다른 곳에 있었어."
        };

        [Tooltip("정답 시 표시될 소설체 문구입니다.")]
        [SerializeField]
        [TextArea(2, 4)]
        private string _correctAnswerLine =
            "이제야 보인다.\n이것이 그 사람의 진짜 모습이었다.";

        [Tooltip("6명 전부 정답 시 표시될 최종 엔딩 문구입니다.")]
        [SerializeField]
        [TextArea(2, 4)]
        private string _endingLine =
            "모든 것이 밝혀졌다.\n이야기는 이제 끝을 향해 나아간다.";

        [Tooltip("검은 화면 페이드 인/아웃 시간 (초)")]
        [SerializeField] private float _wrongAnswerFadeDuration = 0.5f;

        [Tooltip("오답 문구 표시 시간 (초)")]
        [SerializeField] private float _wrongAnswerDisplayTime = 3f;

        [Tooltip("정답 문구 표시 후 에필로그 시작까지 대기 시간 (초)")]
        [SerializeField] private float _correctAnswerDisplayTime = 2.5f;

        [Header("에필로그")]
        [Tooltip("정답 시 에필로그를 재생할 DialoguePlayer입니다.")]
        [SerializeField] private DialoguePlayer _epilogueDialoguePlayer;

        [Header("연결")]
        [Tooltip("닫기 시 함께 닫을 ProfileInquirySelectPanel입니다.")]
        [SerializeField] private ProfileInquirySelectPanel _selectPanel;

        [Header("씬 전환")]
        [Tooltip("에필로그 완료 후 이동할 씬 이름입니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Tooltip("알림 표시 후 클릭 차단 시간 (초)")]
        [SerializeField] private float _noticeLockDuration = 2f;

        [Header("추리 설정")]
        [Tooltip("전체 추리 대상 캐릭터 수입니다. 기본 6.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;

        /// <summary>현재 표시 중인 질문 인덱스 (0-based)</summary>
        private int _currentStepIndex;

        /// <summary>각 질문에 대한 선택 결과. 인덱스 = stepIndex, 값 = 선택된 선택지의 isClue</summary>
        private bool?[] _answers;

        /// <summary>각 질문의 선택지 데이터. 인덱스 = stepIndex</summary>
        private List<(string text, bool isClue)>[] _choiceData;

        /// <summary>총 질문 수 (수집된 조각 수 기준)</summary>
        private int _totalSteps;

        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_wrongAnswerPanel != null) _wrongAnswerPanel.SetActive(false);

            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            _submitButton?.onClick.AddListener(OnSubmitClicked);
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            _submitButton?.onClick.RemoveListener(OnSubmitClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>특정 캐릭터의 프로파일 추리 패널을 엽니다.</summary>
        public void Show(int characterId)
        {
            if (_profileData == null || _profileClueData == null)
            {
                Debug.LogError("[ProfileInquiryUI] ProfileDataSO 또는 ProfileClueDataSO 미연결");
                return;
            }

            var profile = _profileData.FindProfile(characterId);
            if (profile == null)
            {
                Debug.LogWarning($"[ProfileInquiryUI] #{characterId} 프로파일 없음");
                return;
            }

            _currentCharacterId = characterId;
            _currentProfile = profile;

            if (_panel == null)
            {
                Debug.LogError("[ProfileInquiryUI] Panel이 연결되지 않았습니다.");
                return;
            }

            _panel.SetActive(true);
            IsOpen = true;

            SetupCharacterInfo(characterId);
            SetupChoiceData(characterId, profile);
            GoToStep(0);

            Debug.Log($"[ProfileInquiryUI] 열림 — #{characterId}, 총 {_totalSteps}단계");
        }

        /// <summary>패널을 닫고 상태를 초기화합니다.</summary>
        public void Hide()
        {
            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
        }

        // ── Private — 데이터 구성 ─────────────────────────────────────────

        private void SetupCharacterInfo(int characterId)
        {
            if (_characterNameText == null) return;
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(characterId);
            _characterNameText.text = !string.IsNullOrEmpty(name) ? name : $"#{characterId}";
        }

        /// <summary>
        /// 수집된 조각을 기반으로 각 질문의 선택지 데이터를 구성합니다.
        /// 각 질문마다 진실 1개 + 거짓 3개 = 4개 선택지를 랜덤 배치합니다.
        /// 진실 선택지가 부족할 경우 빈 텍스트로 채웁니다.
        /// </summary>
        private void SetupChoiceData(int characterId, CharacterProfileData profile)
        {
            var clues = _profileClueData.GetCluesByCharacter(characterId);
            if (clues == null || clues.Count == 0)
            {
                Debug.LogWarning($"[ProfileInquiryUI] #{characterId} ProfileClue 없음");
                _totalSteps = 0;
                return;
            }

            int stepCount = Mathf.Min(
                Mathf.Min(clues.Count, profile.ProfileItems.Count),
                _totalCharacters);

            _totalSteps = stepCount;
            _answers = new bool?[stepCount];
            _choiceData = new List<(string text, bool isClue)>[stepCount];

            for (int i = 0; i < stepCount; i++)
            {
                var clue = clues[i];
                bool collected = _fragmentCollector?.HasFragment(clue.ProfileClueId) ?? false;

                // 선택지 구성: 정답(ClueText) 1개 + 오답들(HintText 계열) 3개
                // 수집되지 않은 조각은 정답/오답 모두 빈 텍스트로 처리
                var choices = new List<(string text, bool isClue)>();

                if (collected)
                {
                    choices.Add((clue.ClueText, true));   // 정답

                    // 다른 조각들의 HintText를 오답으로 활용
                    var wrongPool = new List<string>();
                    for (int j = 0; j < clues.Count; j++)
                    {
                        if (j == i) continue;
                        bool otherCollected = _fragmentCollector?.HasFragment(clues[j].ProfileClueId) ?? false;
                        if (otherCollected && !string.IsNullOrEmpty(clues[j].HintText))
                            wrongPool.Add(clues[j].HintText);
                    }

                    // 오답이 부족하면 현재 조각의 HintText도 추가
                    if (wrongPool.Count == 0 && !string.IsNullOrEmpty(clue.HintText))
                        wrongPool.Add(clue.HintText);

                    // 오답 3개 채우기
                    Shuffle(wrongPool);
                    for (int w = 0; w < 3; w++)
                        choices.Add(w < wrongPool.Count ? (wrongPool[w], false) : ("...", false));
                }
                else
                {
                    // 미수집 — 빈 선택지로 채움
                    choices.Add(("?", false));
                    choices.Add(("?", false));
                    choices.Add(("?", false));
                    choices.Add(("?", false));
                }

                Shuffle(choices);
                _choiceData[i] = choices;
            }
        }

        // ── Private — 질문 표시 ───────────────────────────────────────────

        /// <summary>지정한 스텝으로 이동해 질문과 선택지를 표시합니다.</summary>
        private void GoToStep(int stepIndex)
        {
            _currentStepIndex = stepIndex;

            // 진행도 표시
            if (_stepIndicatorText != null)
                _stepIndicatorText.text = $"{stepIndex + 1} / {_totalSteps}";

            // 질문 텍스트
            if (_questionText != null && _currentProfile != null
                && stepIndex < _currentProfile.ProfileItems.Count)
                _questionText.text = _currentProfile.ProfileItems[stepIndex].Question;

            // 선택지 버튼 갱신
            var choices = (_choiceData != null && stepIndex < _choiceData.Length)
                ? _choiceData[stepIndex]
                : null;

            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;

                bool hasChoice = choices != null && i < choices.Count;
                _choiceButtons[i].gameObject.SetActive(hasChoice);

                if (!hasChoice) continue;

                var choice = choices[i];
                if (_choiceTexts[i] != null)
                    _choiceTexts[i].text = choice.text;

                // 이미 이 스텝에 답이 있으면 선택된 상태 강조 (선택지 재방문 시)
                // 버튼은 항상 클릭 가능 — 재선택으로 답 변경 허용
                int capturedIndex = i;
                _choiceButtons[i].onClick.RemoveAllListeners();
                _choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(capturedIndex));
            }

            // Submit 버튼 갱신
            RefreshSubmitButton();
        }

        // ── Private — 선택지 클릭 ────────────────────────────────────────

        private void OnChoiceSelected(int choiceIndex)
        {
            if (_choiceData == null || _currentStepIndex >= _choiceData.Length) return;

            var choices = _choiceData[_currentStepIndex];
            if (choiceIndex >= choices.Count) return;

            bool isClue = choices[choiceIndex].isClue;
            _answers[_currentStepIndex] = isClue;

            Debug.Log($"[ProfileInquiryUI] Step{_currentStepIndex} 선택 — " +
                      $"'{choices[choiceIndex].text}' isClue={isClue}");

            // 다음 질문이 있으면 자동으로 넘어감
            int nextStep = _currentStepIndex + 1;
            if (nextStep < _totalSteps)
                GoToStep(nextStep);
            else
                RefreshSubmitButton(); // 마지막 질문이면 Submit 활성화 검사
        }

        // ── Private — Close(뒤로가기) ────────────────────────────────────

        private void OnCloseClicked()
        {
            if (_currentStepIndex > 0)
            {
                // 현재 질문의 답 취소 후 이전 질문으로
                _answers[_currentStepIndex] = null;
                GoToStep(_currentStepIndex - 1);
            }
            else
            {
                // 첫 번째 질문에서 뒤로 → 패널 닫기
                Hide();
                _selectPanel?.Hide();
            }
        }

        // ── Private — Submit 버튼 상태 ───────────────────────────────────

        private void RefreshSubmitButton()
        {
            if (_submitButton == null) return;

            // 모든 질문에 답이 있어야 활성화
            bool allAnswered = _answers != null;
            if (allAnswered)
            {
                for (int i = 0; i < _totalSteps; i++)
                {
                    if (_answers[i] == null) { allAnswered = false; break; }
                }
            }

            // 마지막 질문 도달 시에만 Submit 표시
            bool isLastStep = _currentStepIndex == _totalSteps - 1;

            _submitButton.gameObject.SetActive(isLastStep && allAnswered);
            _submitButton.interactable = isLastStep && allAnswered;
        }

        // ── Private — 제출 및 판정 ────────────────────────────────────────

        private void OnSubmitClicked()
        {
            if (_currentProfile == null) return;

            bool allCorrect = true;
            for (int i = 0; i < _totalSteps; i++)
            {
                if (_answers[i] != true) { allCorrect = false; break; }
            }

            Debug.Log($"[ProfileInquiryUI] 제출 — #{_currentCharacterId} 전부정답={allCorrect}");

            if (allCorrect)
                StartCoroutine(HandleCorrectAnswer());
            else
                StartCoroutine(HandleWrongAnswer());
        }

        // ── Private — 정답 처리 ───────────────────────────────────────────

        private IEnumerator HandleCorrectAnswer()
        {
            string charName = CharacterRecordPanelManager.Instance?
                .GetCollectedName(_currentCharacterId);

            _fragmentCollector?.UnlockConceptCard(_currentCharacterId);
            _fragmentCollector?.UnlockEpilogue(_currentCharacterId);
            _rewardSaveData?.SaveConceptCardUnlock(_currentCharacterId, charName);
            _rewardSaveData?.SaveEpilogueUnlock(_currentCharacterId, charName);

            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            // 검은 화면 FadeIn + 정답 문구
            yield return StartCoroutine(FadeInResultPanel(_correctAnswerLine,
                                                          _correctAnswerDisplayTime));

            if (_wrongAnswerText != null)
                _wrongAnswerText.text = _correctAnswerLine + "\n\n[ 클릭하여 계속 ]";

            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            Hide();
            _selectPanel?.Hide();

            // 6명 전부 정답인지 확인
            int unlockedCount = _rewardSaveData != null
                ? _rewardSaveData.GetUnlockedEpilogueCount()
                : 0;

            bool isAllCleared = unlockedCount >= _totalCharacters;

            if (isAllCleared)
                StartCoroutine(HandleAllCharactersCleared());
            else
                StartCoroutine(PlayEpilogueAndNotify(unlockedCount == 1));
        }

        /// <summary>6명 전부 정답 시 최종 엔딩 문구 표시 후 로비 이동</summary>
        private IEnumerator HandleAllCharactersCleared()
        {
            // 검은 화면이 이미 켜진 상태 — 텍스트만 교체
            if (_wrongAnswerText != null)
                _wrongAnswerText.text = _endingLine;

            yield return new WaitForSeconds(_noticeLockDuration);

            if (_wrongAnswerText != null)
                _wrongAnswerText.text = _endingLine + "\n\n[ 클릭하여 계속 ]";

            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            LoadLobbyScene();
        }

        private IEnumerator PlayEpilogueAndNotify(bool isFirstUnlock)
        {
            if (_epilogueDialoguePlayer == null) yield break;
            if (_currentProfile?.EpilogueLines == null
                || _currentProfile.EpilogueLines.Count == 0) yield break;

            // EpilogueLines는 List<string> — DialogueLine으로 변환
            // TextId를 캐릭터 ID 문자열로, Text를 대사 내용으로 설정합니다.
            var lines = new List<DialogueLine>();
            foreach (var text in _currentProfile.EpilogueLines)
            {
                if (string.IsNullOrEmpty(text)) continue;
                lines.Add(new DialogueLine
                {
                    TextId = _currentCharacterId.ToString(),
                    Text = text
                });
            }
            if (lines.Count == 0) yield break;

            bool done = false;
            _epilogueDialoguePlayer.Play(lines, onComplete: () => done = true);
            yield return new WaitUntil(() => done);

            if (isFirstUnlock)
                yield return StartCoroutine(ShowUnlockNoticeAndGoLobby());
            else
                LoadLobbyScene();
        }

        private IEnumerator ShowUnlockNoticeAndGoLobby()
        {
            if (_wrongAnswerText != null)
                _wrongAnswerText.text =
                    "도감이 열렸습니다.\n" +
                    "로비에서 해금된 캐릭터의 기록을 확인할 수 있습니다.";

            if (_wrongAnswerPanel != null)
            {
                var cg = _wrongAnswerPanel.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;
            }

            yield return new WaitForSeconds(_noticeLockDuration);

            if (_wrongAnswerText != null)
                _wrongAnswerText.text += "\n\n[ 클릭하여 계속 ]";

            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            LoadLobbyScene();
        }

        private void LoadLobbyScene()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
                CampaignSaveManager.Instance.Save(saveData);

            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── Private — 오답 처리 ───────────────────────────────────────────

        private IEnumerator HandleWrongAnswer()
        {
            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            string wrongLine = _wrongAnswerLines.Count > 0
                ? _wrongAnswerLines[Random.Range(0, _wrongAnswerLines.Count)]
                : string.Empty;

            yield return StartCoroutine(ShowResultPanel(wrongLine));

            Hide();
            _selectPanel?.Hide();
        }

        // ── Private — 공통 결과 패널 ─────────────────────────────────────

        private IEnumerator ShowResultPanel(string message)
        {
            yield return StartCoroutine(FadeInResultPanel(message, _wrongAnswerDisplayTime));
            yield return StartCoroutine(FadeOutResultPanel());
        }

        private IEnumerator FadeInResultPanel(string message, float displayTime)
        {
            if (_wrongAnswerPanel == null) yield break;

            _wrongAnswerPanel.SetActive(true);
            var cg = _wrongAnswerPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _wrongAnswerPanel.AddComponent<CanvasGroup>();

            if (_wrongAnswerText != null)
                _wrongAnswerText.text = string.Empty;

            cg.alpha = 0f;
            cg.blocksRaycasts = true;

            yield return cg.DOFade(1f, _wrongAnswerFadeDuration)
                           .SetEase(Ease.OutQuad)
                           .WaitForCompletion();

            if (_wrongAnswerText != null)
                _wrongAnswerText.text = message;

            yield return new WaitForSeconds(displayTime);
        }

        private IEnumerator FadeOutResultPanel()
        {
            if (_wrongAnswerPanel == null) yield break;

            var cg = _wrongAnswerPanel.GetComponent<CanvasGroup>();
            if (cg == null) yield break;

            yield return cg.DOFade(0f, _wrongAnswerFadeDuration)
                           .SetEase(Ease.InQuad)
                           .WaitForCompletion();

            _wrongAnswerPanel.SetActive(false);
        }

        // ── Private — 유틸 ───────────────────────────────────────────────

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}