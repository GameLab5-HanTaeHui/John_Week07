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
    /// ─── 수정 이력 ───────────────────────────────────────────────────────
    ///   DialoguePlayer → EndingDialoguePlayer로 교체.
    ///   도입/결과 대화가 FinalTalkLine 기반이므로 EndingDialoguePlayer가 담당.
    ///   ProfileDataSO는 엔딩 전용 데이터이므로 유지.
    ///   ConvertToDialogueLines() 제거 — 변환 없이 직접 Play().
    ///
    /// ─── SO 역할 분리 ────────────────────────────────────────────────────
    ///   FragmentDataSO   → 캠페인 인게임 대화 조각 (DialoguePlayer 사용)
    ///   ProfileDataSO    → 엔딩 최종 대화 (EndingDialoguePlayer 사용) ← 이 파일
    ///
    /// ─── 진행 흐름 ───────────────────────────────────────────────────────
    ///   Show(characterId)
    ///     → 도입 대사 출력 (EndingDialoguePlayer)
    ///     → 선택지 4개 표시
    ///     → 플레이어 선택 → 확인 팝업
    ///     → 결과 대화 출력 (EndingDialoguePlayer)
    ///       └ 미구현이면 공란 처리 (즉시 통과)
    ///     → 성공/실패 세이브 저장 → Hide()
    ///     → 6명 완료 시 엔딩 판정 → 로비
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data           → ProfileDataSO 에셋
    ///   Panel                  → 전체 패널 GameObject
    ///   Character Name Text    → 캐릭터 이름 TMP
    ///   Choice Buttons[4]      → 선택지 버튼 4개
    ///   Choice Texts[4]        → 선택지 버튼 내 TMP 4개
    ///   Confirm Panel          → 확인 팝업 패널
    ///   Confirm Text           → 팝업 문구 TMP
    ///   Confirm Button         → "전한다" 버튼
    ///   Cancel Button          → "다시 생각한다" 버튼
    ///   Result Panel           → 결과(검은 화면) 패널 (CanvasGroup 필요)
    ///   Result Text            → 결과 문구 TMP
    ///   Ending Dialogue Player → 도입/결과 대화 출력용 EndingDialoguePlayer
    ///   Select Panel           → FinalTalkSelectPanel (닫기용)
    ///   Total Characters       → 전체 대상 캐릭터 수 (기본 6)
    ///   Lobby Scene Name       → 로비 씬 이름
    ///   Fade Duration          → 페이드 시간
    ///   Notice Lock Duration   → 알림 클릭 차단 시간
    ///   Success Ending Text    → 성공 엔딩 문구
    ///   Failure Ending Text    → 실패 엔딩 문구
    /// </summary>
    [DisallowMultipleComponent]
    public class FinalTalkUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("엔딩 전용 캐릭터 프로파일 데이터입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("UI 루트")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 정보")]
        [SerializeField] private TMP_Text _characterNameText;

        [Header("선택지 버튼 (4개)")]
        [SerializeField] private Button[] _choiceButtons = new Button[4];
        [SerializeField] private TMP_Text[] _choiceTexts = new TMP_Text[4];

        [Header("확인 팝업")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField]
        [TextArea(2, 3)]
        private string _confirmMessage = "이 대화는 되돌릴 수 없습니다.\n정말 이 말을 전하시겠습니까?";

        [Header("결과 패널")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _resultHoldTime = 2f;

        [Header("대화 출력")]
        [Tooltip("도입 대사 및 결과 대화를 출력할 EndingDialoguePlayer입니다.\n" +
                 "FinalTalkLine 기반 대사를 출력합니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Header("연결")]
        [SerializeField] private MonoBehaviour _selectPanel;

        [Header("엔딩 문구")]
        [SerializeField]
        [TextArea(2, 4)]
        private string _successEndingText =
            "용병단은 무너졌다.\n엔비는 그 자리에 서서, 자신이 심은 균열을 바라보았다.";

        [SerializeField]
        [TextArea(2, 4)]
        private string _failureEndingText =
            "어딘가에서 실수가 있었다.\n그 사람은 끝내 무너지지 않았다.";

        [Header("씬 전환")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";
        [SerializeField] private float _noticeLockDuration = 2f;

        [Header("설정")]
        [Tooltip("전체 최종 대화 대상 캐릭터 수입니다. 기본 6.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;
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
                Debug.LogWarning($"[FinalTalkUI] #{characterId} FinalTalkData 미구현 — 공란 처리");

            _currentCharacterId = characterId;
            _currentProfile = profile;
            _pendingChoiceIndex = -1;

            _panel?.SetActive(true);
            IsOpen = true;

            SetupCharacterInfo(characterId);
            SetupChoiceButtons(profile.FinalTalk);
            StartCoroutine(PlayIntroAndShowChoices(profile.FinalTalk));
        }

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

            // 수집된 이름 우선 → ProfileDataSO.CharacterFullName → #ID
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(characterId);
            if (string.IsNullOrEmpty(name))
                name = _profileData?.FindProfile(characterId)?.CharacterFullName;
            _characterNameText.text = !string.IsNullOrEmpty(name) ? name : $"#{characterId}";
        }

        private void SetupChoiceButtons(FinalTalkData data)
        {
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;

                bool hasChoice = data != null
                                 && i < data.Choices.Count
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
            if (data != null && data.IntroLines.Count > 0 && _dialoguePlayer != null)
            {
                bool done = false;
                _dialoguePlayer.Play(data.IntroLines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            // 도입 완료 후 선택지 표시
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;
                bool hasChoice = data != null
                                 && i < data.Choices.Count
                                 && !string.IsNullOrEmpty(data.Choices[i]);
                _choiceButtons[i].gameObject.SetActive(hasChoice);
            }
        }

        // ── Private — 선택지 처리 ─────────────────────────────────────────

        private void OnChoiceClicked(int choiceIndex)
        {
            _pendingChoiceIndex = choiceIndex;

            if (_confirmPanel != null)
            {
                if (_confirmText != null) _confirmText.text = _confirmMessage;
                _confirmPanel.SetActive(true);
            }
            else
                ConfirmChoice();
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

            foreach (var btn in _choiceButtons)
                if (btn != null) btn.gameObject.SetActive(false);

            StartCoroutine(PlayResultAndSave(data, _pendingChoiceIndex, isSuccess));
        }

        // ── Private — 결과 처리 ───────────────────────────────────────────

        private IEnumerator PlayResultAndSave(FinalTalkData data, int choiceIndex, bool isSuccess)
        {
            FinalTalkResultDialogue resultDialogue = null;
            if (data?.ResultDialogues != null)
                resultDialogue = data.ResultDialogues.Find(r => r.ChoiceIndex == choiceIndex);

            // 결과 대화 출력 — 미구현이면 공란 처리
            if (resultDialogue != null && resultDialogue.Lines.Count > 0
                && _dialoguePlayer != null)
            {
                bool done = false;
                _dialoguePlayer.Play(resultDialogue.Lines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
            else
            {
                Debug.Log($"[FinalTalkUI] #{_currentCharacterId} 선택지{choiceIndex} 결과 대화 미구현");
            }

            SaveFinalTalkResult(choiceIndex, isSuccess);
            Hide();

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
        }

        // ── Private — 엔딩 ───────────────────────────────────────────────

        private IEnumerator ShowEndingAndGoLobby(CampaignSaveData saveData)
        {
            bool allSuccess = saveData != null
                && saveData.finalTalkRecords
                   .FindAll(r => r.completed && r.success).Count >= _totalCharacters;

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

                if (_resultText != null)
                    _resultText.text = endingText + "\n\n[ 클릭하여 계속 ]";
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
    }
}