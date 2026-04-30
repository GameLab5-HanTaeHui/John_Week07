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
    ///   카드 10개 + 슬롯 5개를 씬에 미리 배치합니다.
    ///   Show() 시 수집된 조각 데이터만 주입합니다.
    ///
    /// ─── 카드 데이터 출처 ────────────────────────────────────────────────
    ///   FragmentCollector.HasFragment("P01_01") 로 수집 여부 확인
    ///   → ProfileClueDataSO.GetCluesByCharacter(id) 로 진실/거짓 텍스트 조회
    ///   → 수집된 것만 카드에 주입 (미수집 카드는 비활성화)
    ///
    ///   진실(CLUE) 카드 5개: clue.ClueText  → IsClue = true
    ///   거짓(HINT) 카드 5개: clue.HintText  → IsClue = false
    ///   10개 카드 랜덤 섞어서 배치
    ///
    /// ─── 정답 판정 ───────────────────────────────────────────────────────
    ///   슬롯 5개 모두 채워지면 제출 버튼 활성화
    ///   각 슬롯의 IsCorrect (CurrentCard.IsClue == true) 가 전부 true 면 정답
    ///   오답 → 검은 화면 + 소설체 문구 → 패널 닫고 인게임 복귀
    ///   정답 → 패널 닫고 에필로그 다이얼로그 재생
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data           → ProfileDataSO 에셋
    ///   Profile Clue Data      → ProfileClueDataSO 에셋
    ///   Fragment Collector     → FragmentCollector
    ///   Reward Save Data       → RewardSaveData 에셋
    ///   Panel                  → 전체 패널 GameObject
    ///   Character Name Text    → 캐릭터 이름 TMP
    ///   Cards[10]              → 씬에 배치된 카드 10개
    ///   Slots[5]               → 씬에 배치된 슬롯 5개 (Step 0~4)
    ///   Submit Button          → 제출 버튼
    ///   Close Button           → 닫기 버튼
    ///   Wrong Answer Panel     → 오답 검은 화면 패널 (CanvasGroup 필요)
    ///   Wrong Answer Text      → 소설체 문구 TMP
    ///   Wrong Answer Lines     → 문구 목록
    ///   Fade Duration          → 페이드 시간 (초)
    ///   Display Time           → 문구 표시 시간 (초)
    ///   Epilogue Player        → 에필로그 DialoguePlayer
    ///   Select Panel           → ProfileInquirySelectPanel (닫기용)
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

        [Header("카드 (씬에 미리 배치, 총 10개)")]
        [Tooltip("진실 5개 + 거짓 5개 = 총 10개 카드를 연결합니다.\n" +
                 "Show() 시 수집된 조각 텍스트가 주입되고 랜덤 배치됩니다.")]
        [SerializeField] private ProfileAnswerCard[] _cards = new ProfileAnswerCard[10];

        [Header("슬롯 (씬에 미리 배치, 총 5개)")]
        [Tooltip("Slot_0~Slot_4 순서로 5개 연결합니다.\n" +
                 "각 슬롯의 Inspector에서 Step Index를 0~4로 설정하세요.")]
        [SerializeField] private ProfileAnswerSlot[] _slots = new ProfileAnswerSlot[5];

        [Header("버튼")]
        [Tooltip("모든 슬롯이 채워지면 활성화됩니다.")]
        [SerializeField] private Button _submitButton;

        [Tooltip("추리를 포기하고 패널을 닫습니다.")]
        [SerializeField] private Button _closeButton;

        [Header("오답 처리")]
        [Tooltip("오답 시 표시할 검은 화면 패널입니다. CanvasGroup 컴포넌트 필요.")]
        [SerializeField] private GameObject _wrongAnswerPanel;

        [Tooltip("소설체 오답 문구 TMP입니다.")]
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
        [Tooltip("오답/닫기 시 함께 닫을 ProfileInquirySelectPanel입니다.")]
        [SerializeField] private ProfileInquirySelectPanel _selectPanel;

        [Header("씬 전환")]
        [Tooltip("에필로그 완료 후 이동할 씬 이름입니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Tooltip("알림 표시 후 클릭 차단 시간 (초)")]
        [SerializeField] private float _noticeLockDuration = 2f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;
        private float _currentDisplayTime;

        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_wrongAnswerPanel != null) _wrongAnswerPanel.SetActive(false);

            // Submit 버튼 초기 비활성 + 숨김
            // 슬롯 5개가 전부 채워지기 전까지 표시하지 않습니다.
            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            // 슬롯은 씬에 직접 배치되어 있으므로
            // _panel 비활성 여부와 무관하게 이벤트 등록이 가능합니다.
            foreach (var slot in _slots)
                if (slot != null)
                    slot.OnSlotChanged += _ => RefreshSubmitButton();

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

            if (_panel != null) _panel.SetActive(true);
            IsOpen = true;

            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            SetupCharacterInfo(characterId);
            ResetAll();
            SetupSlotsAndCards(characterId, profile);

            // Layout 완료 후 홈 위치 재기록
            StartCoroutine(RecordHomesNextFrame());

            Debug.Log($"[ProfileInquiryUI] 열림 — #{characterId}");
        }

        /// <summary>
        /// Close 버튼 클릭 시 호출됩니다.
        ///
        /// ─── 동작 분기 ───────────────────────────────────────────────────
        ///   정답란(슬롯)에 카드가 하나라도 있으면:
        ///     → 슬롯의 카드를 전부 원래 위치로 돌려보냅니다. (ResetCards)
        ///   정답란이 전부 비어있으면:
        ///     → 추리 패널을 닫습니다. (Hide + SelectPanel.Hide)
        /// </summary>
        private void OnCloseClicked()
        {
            bool anySlotFilled = false;
            foreach (var slot in _slots)
            {
                if (slot != null && slot.HasCard)
                {
                    anySlotFilled = true;
                    break;
                }
            }

            if (anySlotFilled)
            {
                // 슬롯에 카드가 있으면 전부 되돌리기
                ResetCards();
            }
            else
            {
                // 슬롯이 전부 비어있으면 패널 닫기
                Hide();
                _selectPanel?.Hide();
            }
        }

        /// <summary>
        /// 슬롯에 꽂힌 카드를 모두 제자리로 돌려보내고
        /// Submit 버튼을 비활성화합니다.
        /// </summary>
        public void ResetCards()
        {
            foreach (var slot in _slots)
                slot?.ResetSlot();

            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }
        }

        /// <summary>패널을 닫고 모든 카드를 홈으로 즉시 복귀시킵니다.</summary>
        public void Hide()
        {
            foreach (var card in _cards)
                card?.ReturnHomeInstant();

            foreach (var slot in _slots)
                slot?.ResetSlot();

            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
        }

        // ── Private — 데이터 주입 ─────────────────────────────────────────

        private void SetupCharacterInfo(int characterId)
        {
            if (_characterNameText == null) return;

            // 1순위: 수집된 이름 (CharacterRecordPanelManager)
            string name = CharacterRecordPanelManager.Instance?
                .GetCollectedName(characterId);

            // 2순위: ProfileDataSO 이름
            if (string.IsNullOrEmpty(name))
            {
                var profile = _profileData?.FindProfile(characterId);
                name = profile?.CharacterFullName;
            }

            // 3순위: 번호 표시 (폴백)
            _characterNameText.text = string.IsNullOrEmpty(name)
                ? $"#{characterId}" : name;
        }

        /// <summary>
        /// 수집된 조각을 기반으로 슬롯 질문과 카드 텍스트를 주입합니다.
        ///
        /// 과정:
        ///   1. ProfileClueDataSO에서 캐릭터의 조각 5개 조회
        ///   2. 각 조각 수집 여부를 FragmentCollector.HasFragment()로 확인
        ///   3. 수집된 조각의 ClueText(진실)/HintText(거짓)를 카드에 주입
        ///   4. 카드 10개를 랜덤 섞어서 배치
        ///   5. 슬롯에 Step 질문 주입
        /// </summary>
        private void SetupSlotsAndCards(int characterId, CharacterProfileData profile)
        {
            var clues = _profileClueData.GetCluesByCharacter(characterId);
            if (clues == null || clues.Count == 0)
            {
                Debug.LogWarning($"[ProfileInquiryUI] #{characterId} ProfileClue 없음");
                return;
            }

            // 카드 데이터 구성: (stepIndex, isClue, text)
            var cardDatas = new List<(int step, bool isClue, string text)>();

            int stepCount = Mathf.Min(
                Mathf.Min(clues.Count, profile.ProfileItems.Count),
                _slots.Length);

            for (int i = 0; i < stepCount; i++)
            {
                var clue = clues[i];
                bool collected = _fragmentCollector?.HasFragment(clue.ProfileClueId) ?? false;

                // 수집된 조각만 카드에 추가
                if (collected)
                {
                    cardDatas.Add((i, true, clue.ClueText));   // 진실
                    cardDatas.Add((i, false, clue.HintText));   // 거짓
                }

                // 슬롯 질문 주입
                if (i < _slots.Length && _slots[i] != null)
                    _slots[i].SetupQuestion(profile.ProfileItems[i].Question);
            }

            // 카드 데이터 랜덤 섞기
            for (int i = cardDatas.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cardDatas[i], cardDatas[j]) = (cardDatas[j], cardDatas[i]);
            }

            // 카드에 데이터 주입 (카드 수보다 데이터가 적으면 나머지 비활성화)
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;

                if (i < cardDatas.Count)
                {
                    var (step, isClue, text) = cardDatas[i];
                    _cards[i].SetupData(step, isClue, text, _slots);
                    _cards[i].gameObject.SetActive(true);
                }
                else
                {
                    // 미수집으로 채울 카드 없으면 비활성화
                    _cards[i].gameObject.SetActive(false);
                }
            }
        }

        private void ResetAll()
        {
            // 카드 위치는 변경하지 않습니다.
            // 씬에 배치된 위치가 홈 위치입니다.
            // RecordHomesNextFrame()에서 홈 위치를 새로 기록합니다.
            foreach (var card in _cards)
                card?.gameObject.SetActive(true);

            foreach (var slot in _slots)
                slot?.ResetSlot();
        }

        private IEnumerator RecordHomesNextFrame()
        {
            yield return null;
            yield return null;

            foreach (var card in _cards)
                if (card != null && card.gameObject.activeSelf)
                    card.RecordHome();
        }

        private void RefreshSubmitButton()
        {
            if (_submitButton == null) return;

            bool allFilled = true;
            foreach (var slot in _slots)
            {
                if (slot == null || !slot.HasCard)
                {
                    allFilled = false;
                    break;
                }
            }

            // 5개 전부 채워지면 버튼 표시 + 활성화
            // 하나라도 비어있으면 버튼 숨김
            _submitButton.gameObject.SetActive(allFilled);
            _submitButton.interactable = allFilled;
        }

        // ── Private — 제출 및 판정 ────────────────────────────────────────

        private void OnSubmitClicked()
        {
            if (_currentProfile == null) return;

            // 모든 슬롯에 진실 조각이 드롭됐는지 확인
            bool allCorrect = true;
            foreach (var slot in _slots)
            {
                if (slot == null || !slot.IsCorrect)
                {
                    allCorrect = false;
                    break;
                }
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

            // 검은 화면 FadeIn + 정답 문구 표시
            yield return StartCoroutine(FadeInResultPanel(_correctAnswerLine,
                                                          _correctAnswerDisplayTime));

            // ★ 추가 — 클릭 유도 문구 표시 후 클릭 대기
            if (_wrongAnswerText != null)
                _wrongAnswerText.text = _correctAnswerLine + "\n\n[ 클릭하여 계속 ]";

            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            Hide();
            _selectPanel?.Hide();

            bool isFirstUnlock = _rewardSaveData != null
                && _rewardSaveData.GetUnlockedEpilogueCount() == 1;

            StartCoroutine(PlayEpilogueAndNotify(isFirstUnlock));
        }

        private IEnumerator PlayEpilogueAndNotify(bool isFirstUnlock)
        {
            if (_epilogueDialoguePlayer == null) yield break;
            if (_currentProfile?.EpilogueLines == null
                || _currentProfile.EpilogueLines.Count == 0) yield break;

            var lines = new List<DialogueLine>();


            if (lines.Count == 0) yield break;

            // 에필로그 완료까지 대기
            bool done = false;
            _epilogueDialoguePlayer.Play(lines, onComplete: () => done = true);
            yield return new WaitUntil(() => done);

            // 첫 해금 시 알림 표시 후 로비 이동
            // 이후 해금 시 바로 로비 이동
            if (isFirstUnlock)
                yield return StartCoroutine(ShowUnlockNoticeAndGoLobby());
            else
                LoadLobbyScene();
        }

        /// <summary>
        /// <summary>
        /// "도감이 열렸습니다" 알림을 표시하고 2초간 클릭을 차단합니다.
        /// 기존 _wrongAnswerPanel / _wrongAnswerText를 재활용합니다.
        /// 검은 화면은 FadeInResultPanel()으로 이미 켜진 상태입니다.
        /// 2초 후 클릭하면 로비 씬으로 이동합니다.
        /// </summary>
        private IEnumerator ShowUnlockNoticeAndGoLobby()
        {
            // 검은 화면은 이미 떠 있음 — 텍스트만 교체
            if (_wrongAnswerText != null)
                _wrongAnswerText.text =
                    "도감이 열렸습니다.\n" +
                    "로비에서 해금된 캐릭터의 기록을 확인할 수 있습니다.";

            // CanvasGroup blocksRaycasts 보장
            if (_wrongAnswerPanel != null)
            {
                var cg = _wrongAnswerPanel.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;
            }

            // 2초 클릭 차단
            yield return new WaitForSeconds(_noticeLockDuration);

            // 클릭 유도 문구 추가
            if (_wrongAnswerText != null)
                _wrongAnswerText.text += "\n\n[ 클릭하여 계속 ]";

            // 클릭 대기
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            // 씬 전환 (검은 화면은 씬 전환으로 자동 정리됨)
            LoadLobbyScene();
        }

        private void LoadLobbyScene()
        {
            // ★ 로비 이동 전 현재 세이브 데이터 강제 저장
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
                CampaignSaveManager.Instance.Save(saveData);


            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── Private — 오답 처리 ───────────────────────────────────────────

        private IEnumerator HandleWrongAnswer()
        {
            // Submit 버튼 비활성화 (중복 클릭 방지)
            if (_submitButton != null)
            {
                _submitButton.interactable = false;
                _submitButton.gameObject.SetActive(false);
            }

            // 오답 문구 랜덤 선택
            string wrongLine = _wrongAnswerLines.Count > 0
                ? _wrongAnswerLines[Random.Range(0, _wrongAnswerLines.Count)]
                : string.Empty;

            // 검은 화면 FadeIn + 문구 표시 + FadeOut (오답 표시 시간 사용)
            _currentDisplayTime = _wrongAnswerDisplayTime;
            yield return StartCoroutine(ShowResultPanel(wrongLine));

            // 패널 닫기 → 인게임 복귀
            Hide();
            _selectPanel?.Hide();
        }

        // ── Private — 공통 결과 패널 ─────────────────────────────────────

        /// <summary>
        /// 검은 화면 FadeIn + 문구 표시 + FadeOut을 모두 처리합니다.
        /// 오답 처리에 사용합니다.
        /// </summary>
        private IEnumerator ShowResultPanel(string message)
        {
            yield return StartCoroutine(FadeInResultPanel(message, _currentDisplayTime));
            yield return StartCoroutine(FadeOutResultPanel());
        }

        /// <summary>
        /// 검은 화면 FadeIn + 문구 표시 + 대기만 처리합니다.
        /// 정답 처리에 사용합니다. (FadeOut은 하지 않아 검은 화면 유지)
        /// </summary>
        private IEnumerator FadeInResultPanel(string message, float displayTime)
        {
            if (_wrongAnswerPanel == null) yield break;

            _wrongAnswerPanel.SetActive(true);
            var cg = _wrongAnswerPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _wrongAnswerPanel.AddComponent<CanvasGroup>();

            if (_wrongAnswerText != null)
                _wrongAnswerText.text = string.Empty;

            // 페이드 인
            cg.alpha = 0f;
            cg.blocksRaycasts = true;
            yield return cg.DOFade(1f, _wrongAnswerFadeDuration)
                           .SetEase(Ease.OutQuad)
                           .WaitForCompletion();

            // 문구 표시
            if (_wrongAnswerText != null)
                _wrongAnswerText.text = message;

            // 대기
            yield return new WaitForSeconds(displayTime);
        }

        /// <summary>
        /// 검은 화면 FadeOut + 패널 비활성화를 처리합니다.
        /// </summary>
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
    }
}