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
    /// ─── 씬 구성 가이드 ──────────────────────────────────────────────────
    ///   Canvas (World Space 또는 Screen Space)
    ///   └── FinalTalkUI (이 컴포넌트)
    ///       ├── TransitionPanel          ← 검은 화면 전환용. CanvasGroup 필요. 기본 비활성.
    ///       │   └── Image (검정 꽉 채우기)
    ///       ├── Panel                    ← 최종 대화 전체 루트. 기본 비활성.
    ///       │   ├── BackgroundImage      ← 캐릭터별 배경 이미지. 검정→흰색 전환 대상.
    ///       │   ├── CharacterNameText    ← 대화 상대 이름 표시용 TMP
    ///       │   ├── ChoicePanel          ← 선택지 버튼 4개 + 제출 버튼 묶음. 기본 비활성.
    ///       │   │   ├── ChoiceButton0~3  ← 선택지 버튼 4개 (각각 TMP 자식 포함)
    ///       │   │   └── SubmitButton     ← "전한다" 확정 버튼
    ///       │   └── DialoguePlayer       ← 인게임 대화 패널 포함
    ///       └── EndingImage              ← 엔딩 스프라이트 표시용. 기본 비활성.
    ///
    /// ─── 진행 흐름 ───────────────────────────────────────────────────────
    ///   ProfileInquirySelectPanel → Show(characterId) 호출
    ///     1. TransitionPanel 페이드 인  (검은 화면)
    ///     2. BackgroundImage 교체 → 검정→흰색 색상 전환  (장소 이동 연출)
    ///     3. TransitionPanel 페이드 아웃  (배경 화면 등장)
    ///     4. 도입 대화 출력  (DialoguePlayer 인게임 모드)
    ///     5. ChoicePanel 활성화 → 플레이어 선택 → SubmitButton 활성
    ///     6. SubmitButton 클릭 → ChoicePanel 비활성화
    ///     7. 결과 대화 출력  (DialoguePlayer 인게임 모드)
    ///     8. 세이브 저장 → 6명 완료 여부 확인
    ///        ├ 미완료 → TransitionPanel 페이드 인/아웃 → 선택 패널 복귀
    ///        └ 완료   → 엔딩 시퀀스 진행 → 로비 이동
    ///
    /// ─── 엔딩 시퀀스 (6명 완료 시) ───────────────────────────────────────
    ///   TransitionPanel 페이드 인 (검은 화면)
    ///   → EndingImage 스프라이트 교체 (성공/실패)
    ///   → TransitionPanel 페이드 아웃 (엔딩 이미지 등장)
    ///   → 엔딩 독백 출력  (DialoguePlayer 엔딩 모드, 타이핑 효과)
    ///   → TransitionPanel 페이드 인 → 클릭 대기 → 로비 씬 이동
    ///
    /// ─── Inspector 연결 체크리스트 ───────────────────────────────────────
    ///   [데이터]
    ///   □ Profile Data         → ProfileDataSO 에셋
    ///
    ///   [패널]
    ///   □ Panel                → 최종 대화 루트 GameObject (기본 비활성)
    ///   □ Transition Panel     → 검은 화면 패널 GameObject (CanvasGroup 필요, 기본 비활성)
    ///
    ///   [배경]
    ///   □ Background Image     → Panel 자식 Image 컴포넌트 (배경 스프라이트 + 색상 전환용)
    ///   □ Background Sprites   → 인덱스=CharacterId 배열. [0]=공통, [2]=메이 배경, ...
    ///                            CharacterId가 없으면 [0] 사용. 비워두면 배경 전환 없음.
    ///
    ///   [캐릭터 정보]
    ///   □ Character Name Text  → 대화 상대 이름 TMP_Text
    ///
    ///   [선택지]
    ///   □ Choice Panel         → 선택지 버튼들과 제출 버튼의 부모 GameObject (기본 비활성)
    ///   □ Choice Buttons[4]    → 선택지 버튼 0~3번 (Button 컴포넌트)
    ///   □ Choice Texts[4]      → 선택지 버튼 내부 TMP_Text 0~3번 (버튼과 동일 순서)
    ///   □ Submit Button        → "전한다" 확정 버튼 (선택지 고르기 전 비활성)
    ///
    ///   [대화 출력]
    ///   □ Dialogue Player      → DialoguePlayer 컴포넌트 (도입/결과/엔딩 독백 모두 담당)
    ///
    ///   [연결]
    ///   □ Select Panel         → ProfileInquirySelectPanel (대화 완료 후 복귀용)
    ///
    ///   [엔딩]
    ///   □ Ending Image         → 엔딩 이미지 표시용 Image 컴포넌트 (기본 비활성)
    ///   □ Success Ending Sprite→ 전원 성공 시 표시할 Sprite
    ///   □ Failure Ending Sprite→ 1명 이상 실패 시 표시할 Sprite
    ///   □ Success Ending Lines → 성공 엔딩 독백 FinalTalkLine 목록
    ///                            SpeakerId=1(엔비) 내레이션 위주로 입력
    ///   □ Failure Ending Lines → 실패 엔딩 독백 FinalTalkLine 목록
    ///
    ///   [씬 전환]
    ///   □ Lobby Scene Name     → 로비 씬 이름 (기본 "LobbyScene")
    ///   □ Fade Duration        → 화면 전환 페이드 시간 (기본 0.5초)
    ///   □ Notice Lock Duration → 엔딩 클릭 차단 시간 (기본 2초)
    ///
    ///   [설정]
    ///   □ Total Characters     → 최종 대화 완료 기준 인원수 (기본 6)
    /// </summary>
    [DisallowMultipleComponent]
    public class FinalTalkUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 이름/최종 대화 데이터 ScriptableObject입니다.\n" +
                 "HTH/Campaign/ProfileData 경로의 에셋을 연결하세요.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("패널")]
        [Tooltip("최종 대화 전체 루트 GameObject입니다.\n" +
                 "Awake에서 자동으로 비활성화됩니다.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("화면 전환용 검은 패널입니다.\n" +
                 "★ CanvasGroup 컴포넌트가 반드시 있어야 합니다.\n" +
                 "★ 전체 화면을 덮는 검정 Image를 자식으로 배치하세요.\n" +
                 "Awake에서 자동으로 비활성화됩니다.")]
        [SerializeField] private GameObject _transitionPanel;

        [Header("배경")]
        [Tooltip("캐릭터별 배경 이미지를 표시할 Image 컴포넌트입니다.\n" +
                 "검정(Color.black)에서 흰색(Color.white)으로 색상 전환하며 장소 이동 연출을 표현합니다.\n" +
                 "Panel의 자식으로 배치하세요.")]
        [SerializeField] private Image _backgroundImage;

        [Tooltip("캐릭터별 배경 스프라이트 목록입니다.\n" +
                 "인덱스 = CharacterId\n" +
                 "  [0] 공통 (CharacterId에 해당하는 스프라이트가 없을 때 사용)\n" +
                 "  [1] 엔비 — 미사용 (비워두기)\n" +
                 "  [2] 메이 배경\n" +
                 "  [3] 루이스 배경\n" +
                 "  ... 순으로 입력\n" +
                 "비워두면 배경 전환 연출 없이 진행됩니다.")]
        [SerializeField] private List<Sprite> _backgroundSprites = new();

        [Header("캐릭터 정보")]
        [Tooltip("대화 상대 이름을 표시하는 TMP_Text입니다.\n" +
                 "ProfileDataSO.CharacterFullName에서 자동으로 채워집니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Header("선택지")]
        [Tooltip("선택지 버튼 4개와 제출 버튼을 묶은 부모 GameObject입니다.\n" +
                 "도입 대화가 끝난 후 활성화됩니다.\n" +
                 "Awake에서 자동으로 비활성화됩니다.")]
        [SerializeField] private GameObject _choicePanel;

        [Tooltip("선택지 버튼 4개입니다. 인덱스 0~3 순서로 연결하세요.\n" +
                 "ProfileDataSO.FinalTalkData.Choices 데이터와 인덱스가 일치해야 합니다.")]
        [SerializeField] private Button[] _choiceButtons = new Button[4];

        [Tooltip("선택지 버튼 내부 TMP_Text 4개입니다.\n" +
                 "Choice Buttons와 동일한 0~3 순서로 연결하세요.")]
        [SerializeField] private TMP_Text[] _choiceTexts = new TMP_Text[4];

        [Tooltip("선택지를 고른 뒤 최종 확정하는 버튼입니다.\n" +
                 "선택지를 하나라도 누르기 전까지 비활성(interactable=false) 상태입니다.")]
        [SerializeField] private Button _submitButton;

        [Header("대화 출력")]
        [Tooltip("도입 대화, 결과 대화, 엔딩 독백을 모두 담당하는 DialoguePlayer입니다.\n" +
                 "  도입/결과 대화 → 인게임 모드 (dialoguePanel 사용)\n" +
                 "  엔딩 독백      → 엔딩 모드 (endingPanel + 타이핑 효과 사용)\n" +
                 "Profile Data 슬롯에 ProfileDataSO를 연결해야 이름이 표시됩니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Header("연결")]
        [Tooltip("최종 대화 완료 또는 취소 후 복귀할 캐릭터 선택 패널입니다.\n" +
                 "대화 완료 시 자동으로 Show()가 호출됩니다.")]
        [SerializeField] private ProfileInquirySelectPanel _selectPanel;

        [Header("엔딩")]
        [Tooltip("엔딩 전용 패널 GameObject입니다.\n" +
                 "TransitionPanel과 별개로 엔딩 이미지+타이틀을 담는 컨테이너입니다.\n" +
                 "★ 씬 구성:\n" +
                 "  EndingPanel (이 슬롯)\n" +
                 "  ├── EndingBgImage  ← Ending Bg Image 슬롯에 연결\n" +
                 "  └── TitleText      ← Title Text 슬롯에 연결\n" +
                 "Awake에서 자동으로 비활성화됩니다.")]
        [SerializeField] private GameObject _endingPanel;

        [Tooltip("엔딩 배경 스프라이트를 표시할 Image 컴포넌트입니다.\n" +
                 "EndingPanel의 자식 Image에 연결하세요.\n" +
                 "성공/실패에 따라 스프라이트가 교체됩니다.")]
        [SerializeField] private Image _endingBgImage;

        [Tooltip("전원 성공 시 표시할 엔딩 스프라이트입니다.")]
        [SerializeField] private Sprite _successEndingSprite;

        [Tooltip("1명 이상 실패 시 표시할 엔딩 스프라이트입니다.")]
        [SerializeField] private Sprite _failureEndingSprite;

        [Tooltip("엔딩 최종 결과 문구를 표시할 TMP_Text입니다.\n" +
                 "EndingPanel의 자식으로 배치하세요.\n" +
                 "독백 대화가 모두 끝난 후 활성화됩니다.")]
        [SerializeField] private TMP_Text _titleText;

        [Tooltip("전원 성공 시 Title Text에 표시할 문구입니다.")]
        [SerializeField]
        [TextArea(1, 2)]
        private string _successTitleMessage = "용병단이 와해 되었습니다.";

        [Tooltip("1명 이상 실패 시 Title Text에 표시할 문구입니다.")]
        [SerializeField]
        [TextArea(1, 2)]
        private string _failureTitleMessage = "임무에 실패하였습니다.";

        [Tooltip("성공 엔딩 나레이션+독백 FinalTalkLine 목록입니다.\n" +
                 "SpeakerId = 0 → 이름/이미지 없는 나레이션 텍스트\n" +
                 "SpeakerId = 1 → 엔비 독백 (퍼스널컬러 표시)\n" +
                 "인게임 모드 (dialoguePanel)로 재생됩니다.")]
        [SerializeField] private List<FinalTalkLine> _successEndingLines = new();

        [Tooltip("실패 엔딩 나레이션+캐릭터 대화 FinalTalkLine 목록입니다.\n" +
                 "SpeakerId = 0 → 이름/이미지 없는 나레이션 텍스트\n" +
                 "SpeakerId = 1~7 → 각 캐릭터 대화 (이름+퍼스널컬러 표시)\n" +
                 "인게임 모드 (dialoguePanel)로 재생됩니다.")]
        [SerializeField] private List<FinalTalkLine> _failureEndingLines = new();

        [Header("씬 전환")]
        [Tooltip("엔딩 완료 후 이동할 로비 씬 이름입니다.\n" +
                 "Build Settings에 등록된 씬 이름과 정확히 일치해야 합니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Tooltip("화면 전환(페이드 인/아웃) 시간입니다. (초 단위, 기본 0.5)\n" +
                 "배경 검정→흰색 전환에도 동일하게 적용됩니다.")]
        [SerializeField] private float _fadeDuration = 0.5f;

        [Tooltip("엔딩 시퀀스 마지막에 클릭을 차단할 시간입니다. (초 단위, 기본 2)\n" +
                 "이 시간이 지난 후 마우스 클릭으로 로비로 이동합니다.")]
        [SerializeField] private float _noticeLockDuration = 2f;

        [Header("설정")]
        [Tooltip("최종 대화 완료 기준 인원수입니다. (기본 6)\n" +
                 "이 수에 도달하면 엔딩 시퀀스가 자동 시작됩니다.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;

        /// <summary>현재 선택한 선택지 인덱스. -1이면 미선택.</summary>
        private int _pendingChoiceIndex = -1;
        private CanvasGroup _transitionCg;

        /// <summary>최종 대화 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_choicePanel != null) _choicePanel.SetActive(false);
            if (_endingPanel != null) _endingPanel.SetActive(false);
            if (_titleText != null) _titleText.gameObject.SetActive(false);

            if (_transitionPanel != null)
            {
                _transitionPanel.SetActive(false);
                _transitionCg = _transitionPanel.GetComponent<CanvasGroup>();
                if (_transitionCg == null)
                    _transitionCg = _transitionPanel.AddComponent<CanvasGroup>();
            }

            _submitButton?.onClick.AddListener(OnSubmitClicked);
        }

        private void OnDestroy()
        {
            _submitButton?.onClick.RemoveListener(OnSubmitClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 최종 대화를 시작합니다.
        /// ProfileInquirySelectPanel에서 캐릭터 버튼 클릭 후 호출합니다.
        /// </summary>
        public void Show(int characterId)
        {
            if (_profileData == null) { Debug.LogError("[FinalTalkUI] ProfileDataSO 미연결"); return; }

            var profile = _profileData.FindProfile(characterId);
            if (profile == null) { Debug.LogWarning($"[FinalTalkUI] #{characterId} 프로파일 없음"); return; }

            _currentCharacterId = characterId;
            _currentProfile = profile;
            _pendingChoiceIndex = -1;
            IsOpen = true;

            StartCoroutine(EnterSequence(profile));
        }

        /// <summary>패널을 즉시 닫습니다. 복귀 연출 없이 강제 닫기용.</summary>
        public void Hide()
        {
            _panel?.SetActive(false);
            _choicePanel?.SetActive(false);
            IsOpen = false;
        }

        // ── 진입 연출 ────────────────────────────────────────────────────

        private IEnumerator EnterSequence(CharacterProfileData profile)
        {
            // 1. 검은 화면 페이드 인
            yield return FadeTransition(0f, 1f);

            // 2. 배경 스프라이트 교체 (검정 색상으로 시작)
            SetBackgroundSprite(_currentCharacterId);

            // 3. 패널 활성화 (검은 화면 뒤에서 선택지/이름 미리 준비)
            _panel?.SetActive(true);
            if (_submitButton != null)
            {
                _submitButton.gameObject.SetActive(false);
                _submitButton.interactable = false;
            }
            SetupCharacterName(_currentCharacterId);
            SetupChoiceButtons(profile.FinalTalk);

            // 4. 배경 검정 → 흰색 전환 (장소 이동 연출)
            yield return FadeBackgroundToWhite();

            // 5. 검은 화면 페이드 아웃 (배경 등장)
            yield return FadeTransition(1f, 0f);

            // 6. 도입 대화 출력 — 인게임 모드 (FinalTalkLine → FragmentDialogueLine 변환)
            var data = profile.FinalTalk;
            if (data != null && data.IntroLines.Count > 0 && _dialoguePlayer != null)
            {
                bool done = false;
                _dialoguePlayer.Play(ToFragmentLines(data.IntroLines), onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            // 7. 선택지 패널 활성화
            _choicePanel?.SetActive(true);
            if (_submitButton != null)
            {
                _submitButton.gameObject.SetActive(true);
                _submitButton.interactable = false; // 선택지 미선택 상태
            }
        }

        // ── 배경 관리 ────────────────────────────────────────────────────

        private void SetBackgroundSprite(int characterId)
        {
            if (_backgroundImage == null) return;

            Sprite sprite = null;
            if (characterId >= 0 && characterId < _backgroundSprites.Count)
                sprite = _backgroundSprites[characterId];
            if (sprite == null && _backgroundSprites.Count > 0)
                sprite = _backgroundSprites[0];

            if (sprite != null)
            {
                _backgroundImage.sprite = sprite;
                _backgroundImage.color = Color.black; // 검정에서 시작
                _backgroundImage.enabled = true;
            }
        }

        private IEnumerator FadeBackgroundToWhite()
        {
            if (_backgroundImage == null) yield break;
            yield return _backgroundImage.DOColor(Color.white, _fadeDuration)
                                         .SetEase(Ease.OutQuad)
                                         .WaitForCompletion();
        }

        // ── 전환 패널 ────────────────────────────────────────────────────

        /// <summary>
        /// TransitionPanel(검은 화면)을 from → to 알파로 페이드합니다.
        /// from=0,to=1 : 페이드 인 (화면이 어두워짐)
        /// from=1,to=0 : 페이드 아웃 (화면이 밝아짐)
        /// </summary>
        private IEnumerator FadeTransition(float from, float to)
        {
            if (_transitionCg == null) yield break;
            _transitionPanel.SetActive(true);
            _transitionCg.alpha = from;
            _transitionCg.blocksRaycasts = to > 0f;
            yield return _transitionCg.DOFade(to, _fadeDuration)
                                      .SetEase(to > 0f ? Ease.InQuad : Ease.OutQuad)
                                      .WaitForCompletion();
            if (to <= 0f) _transitionPanel.SetActive(false);
        }

        // ── UI 초기화 ────────────────────────────────────────────────────

        private void SetupCharacterName(int characterId)
        {
            if (_characterNameText == null) return;
            var p = _profileData?.FindProfile(characterId);
            _characterNameText.text = !string.IsNullOrEmpty(p?.CharacterFullName)
                ? p.CharacterFullName : $"#{characterId}";
        }

        private void SetupChoiceButtons(FinalTalkData data)
        {
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;
                bool hasChoice = data != null
                                 && i < data.Choices.Count
                                 && !string.IsNullOrEmpty(data.Choices[i]);
                _choiceButtons[i].gameObject.SetActive(hasChoice);
                _choiceButtons[i].interactable = hasChoice;
                if (_choiceTexts[i] != null)
                    _choiceTexts[i].text = hasChoice ? data.Choices[i] : "";
                int captured = i;
                _choiceButtons[i].onClick.RemoveAllListeners();
                _choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(captured));
            }
        }

        // ── 선택지 처리 ──────────────────────────────────────────────────

        private void OnChoiceClicked(int choiceIndex)
        {
            _pendingChoiceIndex = choiceIndex;

            // 선택된 버튼 강조 (선택 = 불투명, 미선택 = 반투명)
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                if (_choiceButtons[i] == null) continue;
                var cg = _choiceButtons[i].GetComponent<CanvasGroup>()
                         ?? _choiceButtons[i].gameObject.AddComponent<CanvasGroup>();
                cg.alpha = (i == choiceIndex) ? 1f : 0.5f;
            }

            if (_submitButton != null)
                _submitButton.interactable = true;
        }

        private void OnSubmitClicked()
        {
            if (_pendingChoiceIndex < 0) return;
            ConfirmChoice(_pendingChoiceIndex);
        }

        private void ConfirmChoice(int choiceIndex)
        {
            var data = _currentProfile?.FinalTalk;
            bool isSuccess = data != null && choiceIndex == data.CorrectIndex;

            Debug.Log($"[FinalTalkUI] 제출 확정 — #{_currentCharacterId} " +
                      $"선택지={choiceIndex} 성공={isSuccess}");

            // C13 — final_talk_result
            GameLogger.Instance?.LogEvent("final_talk_result", new Dictionary<string, object>
            {
                { "character_id",  _currentCharacterId                  },
                { "choice_index",  choiceIndex                          },
                { "is_correct",    isSuccess                            },
                { "elapsed_sec",   GameLogger.Instance?.SessionElapsedSec ?? 0 },
            });

            _choicePanel?.SetActive(false);
            StartCoroutine(PlayResultAndSave(data, choiceIndex, isSuccess));
        }

        // ── 결과 처리 ────────────────────────────────────────────────────

        private IEnumerator PlayResultAndSave(FinalTalkData data, int choiceIndex, bool isSuccess)
        {
            // 결과 대화 출력 — 인게임 모드
            FinalTalkResultDialogue resultDialogue = null;
            if (data?.ResultDialogues != null)
                resultDialogue = data.ResultDialogues.Find(r => r.ChoiceIndex == choiceIndex);

            if (resultDialogue != null && resultDialogue.Lines.Count > 0 && _dialoguePlayer != null)
            {
                bool done = false;
                _dialoguePlayer.Play(ToFragmentLines(resultDialogue.Lines), onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            SaveFinalTalkResult(choiceIndex, isSuccess);

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            int completedCount = saveData?.finalTalkRecords.FindAll(r => r.completed).Count ?? 0;

            if (completedCount >= _totalCharacters)
            {
                // 6명 완료 → 엔딩 시퀀스
                yield return StartCoroutine(PlayEndingSequence(saveData));
            }
            else
            {
                _selectPanel.FinalTalkAndButtonUpdate();
                // 미완료 → 검은 화면으로 복귀 후 선택 패널 다시 열기
                yield return FadeTransition(0f, 1f);
                Hide();
                _selectPanel?.Show();
                yield return FadeTransition(1f, 0f);
            }
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

        // ── 엔딩 시퀀스 ──────────────────────────────────────────────────

        private IEnumerator PlayEndingSequence(CampaignSaveData saveData)
        {
            int completedCount = saveData?.finalTalkRecords?.FindAll(r => r.completed && r.success).Count ?? 0;
            bool allSuccess = completedCount >= _totalCharacters;

            // C18 — final_talk_accuracy
            GameLogger.Instance?.LogEvent("final_talk_accuracy", new Dictionary<string, object>
            {
                { "correct_count", completedCount                                             },
                { "total",         _totalCharacters                                           },
                { "success_rate",  (_totalCharacters > 0
                    ? (float)completedCount / _totalCharacters : 0f).ToString("F2")           },
                { "all_success",   allSuccess                                                  },
            });

            // C19 — ending_reached
            var gfc19 = CampaignGameFlowController.Instance;
            GameLogger.Instance?.LogEvent("ending_reached", new Dictionary<string, object>
            {
                { "result",          allSuccess ? "success" : "failure"          },
                { "total_play_sec",  GameLogger.Instance?.SessionElapsedSec ?? 0 },
                { "loop_count",      gfc19?.LoopCount ?? 0                       },
            });

            // TitleText 초기화
            if (_titleText != null)
            {
                _titleText.text = "";
                _titleText.gameObject.SetActive(false);
            }

            // 1. 검은 화면 (TransitionPanel 페이드 인)
            yield return FadeTransition(0f, 1f);
            Hide();
            _selectPanel?.Hide();

            // 2. EndingPanel 활성화 + 배경 스프라이트 교체
            if (_endingBgImage != null)
                _endingBgImage.sprite = allSuccess ? _successEndingSprite : _failureEndingSprite;
            if (_endingPanel != null)
                _endingPanel.SetActive(true);

            // 3. 검은 화면 해제 (엔딩 패널 등장)
            yield return FadeTransition(1f, 0f);

            // 4. 나레이션/독백/캐릭터 대화 출력 — 인게임 모드
            // SpeakerId=0 → 이름/이미지 없는 나레이션
            // SpeakerId=1 → 엔비 독백
            // SpeakerId=2~7 → 각 캐릭터 대화 (실패 엔딩)
            var endingLines = allSuccess ? _successEndingLines : _failureEndingLines;
            if (endingLines != null && endingLines.Count > 0 && _dialoguePlayer != null)
            {
                bool done = false;
                _dialoguePlayer.Play(ToFragmentLines(endingLines), onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            // 5. TitleText 표시 (독백 완료 후)
            if (_titleText != null)
            {
                _titleText.text = allSuccess ? _successTitleMessage : _failureTitleMessage;
                _titleText.gameObject.SetActive(true);
            }

            // 6. 대기 후 검은 화면 → 클릭 → 로비
            yield return new WaitForSeconds(_noticeLockDuration);
            yield return FadeTransition(0f, 1f);
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

            LoadLobbyScene();
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        /// <summary>
        /// FinalTalkLine을 FragmentDialogueLine으로 변환합니다.
        /// 도입/결과 대화를 DialoguePlayer 인게임 모드로 재생하기 위해 사용합니다.
        /// SpeakerId(int) → TextId(string) 변환만 수행하며 Text는 그대로 유지됩니다.
        /// </summary>
        private static List<FragmentDialogueLine> ToFragmentLines(List<FinalTalkLine> source)
        {
            var result = new List<FragmentDialogueLine>(source.Count);
            foreach (var line in source)
            {
                if (string.IsNullOrEmpty(line.Text)) continue;
                result.Add(new FragmentDialogueLine
                {
                    TextId = line.SpeakerId.ToString(),
                    Text = line.Text
                });
            }
            return result;
        }

        private void LoadLobbyScene()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null) CampaignSaveManager.Instance.Save(saveData);
            SceneManager.LoadScene(_lobbySceneName);
        }
    }
}