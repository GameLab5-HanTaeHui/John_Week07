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
    /// 최종 대화 시스템 UI입니다.
    ///
    /// ─── 기획 개요 ───────────────────────────────────────────────────────
    ///   플레이어(엔비)가 용병단 구성원 6명과 단둘이 나누는 1회성 대화.
    ///   엔비는 거짓말을 하지 않는다. 단, 진실을 가장 무너질 사람에게 들려준다.
    ///   한 번 선택하면 되돌릴 수 없다.
    ///
    /// ─── 진행 흐름 ───────────────────────────────────────────────────────
    ///   Show(characterId)
    ///     → 도입 대사 출력 (DialoguePlayer)
    ///     → 선택지 4개 표시
    ///     → 플레이어 선택 → 확인 팝업
    ///     → 결과 대화 출력 (DialoguePlayer)
    ///       ├ 미구현 시 공란 처리 (즉시 완료)
    ///     → 성공/실패 세이브 저장
    ///     → Hide()
    ///
    /// ─── 엔딩 판정 ───────────────────────────────────────────────────────
    ///   6명 모두 완료 시 자동 판정.
    ///   전원 success → 성공 엔딩 문구 → 로비
    ///   1명 이상 실패 → 실패 엔딩 문구 → 로비
    ///   엔딩 대사는 EpilogueLines 확장 예정이므로 빈 줄 처리 지원.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data          → ProfileDataSO 에셋
    ///   Panel                 → 전체 패널 GameObject
    ///   Character Name Text   → 캐릭터 이름 TMP
    ///   Choice Buttons[4]     → 선택지 버튼 4개
    ///   Choice Texts[4]       → 선택지 버튼 내 TMP 4개
    ///   Confirm Panel         → 확인 팝업 패널
    ///   Confirm Text          → 팝업 문구 TMP
    ///   Confirm Button        → "전한다" 버튼
    ///   Cancel Button         → "다시 생각한다" 버튼
    ///   Result Panel          → 결과(검은 화면) 패널 (CanvasGroup 필요)
    ///   Result Text           → 결과 문구 TMP
    ///   Dialogue Player       → 도입/결과 대화 출력용 DialoguePlayer
    ///   Select Panel          → FinalTalkSelectPanel (닫기용)
    ///   Total Characters      → 전체 대상 캐릭터 수 (기본 6)
    ///   Lobby Scene Name      → 로비 씬 이름
    ///   Fade Duration         → 페이드 시간
    ///   Notice Lock Duration  → 알림 클릭 차단 시간
    ///   Success Ending Lines  → 성공 엔딩 문구 목록 (확장 예정)
    ///   Failure Ending Lines  → 실패 엔딩 문구 목록 (확장 예정)
    /// </summary>
    [DisallowMultipleComponent]
    public class FinalTalkUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 프로파일 데이터입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("UI 루트")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 정보")]
        [SerializeField] private TMP_Text _characterNameText;

        [Header("선택지 버튼 (4개)")]
        [SerializeField] private Button[] _choiceButtons = new Button[4];
        [SerializeField] private TMP_Text[] _choiceTexts = new TMP_Text[4];

        [Header("확인 팝업")]
        [Tooltip("선택지 확정 전 표시되는 팝업입니다.")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField]
        [TextArea(2, 3)]
        private string _confirmMessage =
            "이 대화는 되돌릴 수 없습니다.\n정말 이 말을 전하시겠습니까?";

        [Header("결과 패널")]
        [Tooltip("결과 대화 출력 후 표시할 검은 화면 패널입니다. CanvasGroup 필요.")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _resultHoldTime = 2f;

        [Header("대화 출력")]
        [Tooltip("도입 대사 및 결과 대화를 출력할 DialoguePlayer입니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Header("연결")]
        [SerializeField] private MonoBehaviour _selectPanel;

        [Header("엔딩 문구 (확장 예정)")]
        [Tooltip("6명 전원 성공 시 표시될 엔딩 문구입니다.\n추후 DialogueLine 목록으로 교체 예정.")]
        [SerializeField]
        [TextArea(2, 4)]
        private string _successEndingText =
            "용병단은 무너졌다.\n엔비는 그 자리에 서서, 자신이 심은 균열을 바라보았다.";

        [Tooltip("1명 이상 실패 시 표시될 엔딩 문구입니다.\n추후 DialogueLine 목록으로 교체 예정.")]
        [SerializeField]
        [TextArea(2, 4)]
        private string _failureEndingText =
            "어딘가에서 실수가 있었다.\n그 사람은 끝내 무너지지 않았다.";

        [Header("씬 전환")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";
        [SerializeField] private float _noticeLockDuration = 2f;

        [Header("추리 설정")]
        [Tooltip("전체 최종 대화 대상 캐릭터 수입니다. 기본 6.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;

        /// <summary>현재 선택한 선택지 인덱스 (확인 전 임시 보관)</summary>
        private int _pendingChoiceIndex = -1;

        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);

            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        private void OnDestroy()
        {
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>특정 캐릭터의 최종 대화를 시작합니다.</summary>
        public void Show(int characterId)
        {
            if (_profileData == null)
            {
                Debug.LogError("[FinalTalkUI] ProfileDataSO 미연결");
                return;
            }

            var profile = _profileData.FindProfile(characterId);
            if (profile == null)
            {
                Debug.LogWarning($"[FinalTalkUI] #{characterId} 프로파일 없음");
                return;
            }

            if (profile.FinalTalk == null)
            {
                Debug.LogWarning($"[FinalTalkUI] #{characterId} FinalTalkData 미구현 — 공란 처리");
            }

            _currentCharacterId = characterId;
            _currentProfile = profile;
            _pendingChoiceIndex = -1;

            _panel?.SetActive(true);
            IsOpen = true;

            SetupCharacterInfo(characterId);
            SetupChoiceButtons(profile.FinalTalk);

            StartCoroutine(PlayIntroAndShowChoices(profile.FinalTalk));
        }

        /// <summary>패널을 닫고 상태를 초기화합니다.</summary>
        public void Hide()
        {
            _panel?.SetActive(false);
            _confirmPanel?.SetActive(false);
            IsOpen = false;
        }

        // ── Private — 초기화 ─────────────────────────────────────────────

        private void SetupCharacterInfo(int characterId)
        {
            if (_characterNameText == null) return;
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(characterId);
            if (string.IsNullOrEmpty(name))
            {
                var p = _profileData?.FindProfile(characterId);
                name = p?.CharacterFullName;
            }
            _characterNameText.text = !string.IsNullOrEmpty(name) ? name : $"#{characterId}";
        }

        private void SetupChoiceButtons(FinalTalkData data)
        {
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;

                bool hasChoice = data != null && i < data.Choices.Count
                                 && !string.IsNullOrEmpty(data.Choices[i]);
                _choiceButtons[i].gameObject.SetActive(false); // 도입 대사 중 숨김
                _choiceButtons[i].interactable = hasChoice;

                if (_choiceTexts[i] != null)
                    _choiceTexts[i].text = hasChoice ? data.Choices[i] : "";

                int captured = i;
                _choiceButtons[i].onClick.RemoveAllListeners();
                _choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(captured));
            }
        }

        // ── Private — 도입 대사 ───────────────────────────────────────────

        private IEnumerator PlayIntroAndShowChoices(FinalTalkData data)
        {
            // 도입 대사가 있으면 DialoguePlayer로 출력
            if (data != null && data.IntroLines.Count > 0 && _dialoguePlayer != null)
            {
                var lines = ConvertToDialogueLines(data.IntroLines);
                bool done = false;
                _dialoguePlayer.Play(lines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            // 선택지 버튼 표시
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;
                bool hasChoice = data != null && i < data.Choices.Count
                                 && !string.IsNullOrEmpty(data.Choices[i]);
                _choiceButtons[i].gameObject.SetActive(hasChoice);
            }
        }

        // ── Private — 선택지 클릭 ────────────────────────────────────────

        private void OnChoiceClicked(int choiceIndex)
        {
            _pendingChoiceIndex = choiceIndex;

            // 확인 팝업 표시
            if (_confirmPanel != null)
            {
                if (_confirmText != null)
                    _confirmText.text = _confirmMessage;
                _confirmPanel.SetActive(true);
            }
            else
            {
                // 팝업 없으면 즉시 확정
                ConfirmChoice();
            }
        }

        private void OnConfirmClicked()
        {
            _confirmPanel?.SetActive(false);
            ConfirmChoice();
        }

        private void OnCancelClicked()
        {
            _pendingChoiceIndex = -1;
            _confirmPanel?.SetActive(false);
        }

        private void ConfirmChoice()
        {
            if (_pendingChoiceIndex < 0) return;

            var data = _currentProfile?.FinalTalk;
            bool isSuccess = data != null && _pendingChoiceIndex == data.CorrectIndex;

            Debug.Log($"[FinalTalkUI] 선택 확정 — #{_currentCharacterId} " +
                      $"선택지={_pendingChoiceIndex} 성공={isSuccess}");

            // 선택지 버튼 숨김
            foreach (var btn in _choiceButtons)
                if (btn != null) btn.gameObject.SetActive(false);

            StartCoroutine(PlayResultAndSave(data, _pendingChoiceIndex, isSuccess));
        }

        // ── Private — 결과 처리 ───────────────────────────────────────────

        private IEnumerator PlayResultAndSave(FinalTalkData data, int choiceIndex, bool isSuccess)
        {
            // 결과 대화 찾기
            FinalTalkResultDialogue resultDialogue = null;
            if (data?.ResultDialogues != null)
                resultDialogue = data.ResultDialogues.Find(r => r.ChoiceIndex == choiceIndex);

            // 결과 대화 출력 (미구현이면 공란 처리 — 즉시 통과)
            if (resultDialogue != null && resultDialogue.Lines.Count > 0
                && _dialoguePlayer != null)
            {
                var lines = ConvertToDialogueLines(resultDialogue.Lines);
                bool done = false;
                _dialoguePlayer.Play(lines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
            else
            {
                Debug.Log($"[FinalTalkUI] #{_currentCharacterId} 결과 대화 미구현 — 공란 처리");
            }

            // 세이브 저장
            SaveFinalTalkResult(choiceIndex, isSuccess);

            Hide();

            // 6명 모두 완료됐는지 확인
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            int completedCount = saveData?.finalTalkRecords.FindAll(r => r.completed).Count ?? 0;

            if (completedCount >= _totalCharacters)
                StartCoroutine(ShowEndingAndGoLobby(saveData));
        }

        private void SaveFinalTalkResult(int choiceIndex, bool isSuccess)
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            var record = saveData.finalTalkRecords.Find(r => r.characterId == _currentCharacterId);
            if (record == null)
            {
                record = new FinalTalkRecord { characterId = _currentCharacterId };
                saveData.finalTalkRecords.Add(record);
            }

            record.completed = true;
            record.success = isSuccess;
            record.selectedChoiceIndex = choiceIndex;
            record.resultSeen = true;

            CampaignSaveManager.Instance.Save(saveData);
            Debug.Log($"[FinalTalkUI] 세이브 완료 — #{_currentCharacterId} success={isSuccess}");
        }

        // ── Private — 엔딩 판정 ───────────────────────────────────────────

        /// <summary>
        /// 6명 완료 시 성공/실패 엔딩 문구를 표시하고 로비로 이동합니다.
        /// 엔딩 대사는 추후 DialogueLine 목록으로 교체 예정입니다.
        /// </summary>
        private IEnumerator ShowEndingAndGoLobby(CampaignSaveData saveData)
        {
            bool allSuccess = saveData != null
                && saveData.finalTalkRecords.FindAll(r => r.completed && r.success).Count
                   >= _totalCharacters;

            string endingText = allSuccess ? _successEndingText : _failureEndingText;

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(true);
                var cg = _resultPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = _resultPanel.AddComponent<CanvasGroup>();

                if (_resultText != null) _resultText.text = string.Empty;
                cg.alpha = 0f;
                cg.blocksRaycasts = true;

                yield return cg.DOFade(1f, _fadeDuration)
                               .SetEase(Ease.OutQuad)
                               .WaitForCompletion();

                if (_resultText != null) _resultText.text = endingText;
                yield return new WaitForSeconds(_noticeLockDuration);

                if (_resultText != null) _resultText.text = endingText + "\n\n[ 클릭하여 계속 ]";
                yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            }

            LoadLobbyScene();
        }

        private void LoadLobbyScene()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
                CampaignSaveManager.Instance.Save(saveData);

            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── Private — 유틸 ───────────────────────────────────────────────

        private List<DialogueLine> ConvertToDialogueLines(List<FinalTalkLine> source)
        {
            var result = new List<DialogueLine>(source.Count);
            foreach (var line in source)
            {
                if (string.IsNullOrEmpty(line.Text)) continue;
                result.Add(new DialogueLine
                {
                    TextId = line.SpeakerId.ToString(),
                    Text = line.Text
                });
            }
            return result;
        }
    }
}