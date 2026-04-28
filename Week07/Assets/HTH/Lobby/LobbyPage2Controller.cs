using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비 2페이지 (도감)를 관리합니다.
    ///
    /// ─── 기본 동작 ───────────────────────────────────────────────────────
    ///   왼쪽: CharacterIconButton 7개
    ///   오른쪽: 선택된 캐릭터의 시점 완결문 + 이미지 조각 표시
    ///
    /// ─── 히든 엔딩 조건 ──────────────────────────────────────────────────
    ///   7개 에필로그 전부 해금된 상태에서
    ///   버튼을 _secretOrder 순서대로 클릭하면
    ///   → 이미지 조각들이 합쳐지는 연출
    ///   → 완성된 사진 클릭 → 히든 엔딩 다이얼로그
    ///
    ///   실패 조건 (둘 중 하나)
    ///   A. 틀린 버튼 클릭 시 즉시 초기화
    ///   B. _secretTimeLimit 초 내에 완성 못 하면 초기화
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Icon Button Prefab    → CharacterIconButton 프리팹
    ///   Icon Grid             → GridLayoutGroup 부모
    ///   Profile Data          → ProfileDataSO 에셋
    ///   Reward Save Data      → RewardSaveData 에셋
    ///   Character Name Text   → 오른쪽 캐릭터 이름 TMP
    ///   Epilogue Text         → 오른쪽 시점 완결문 TMP
    ///   Default Message       → 미선택 안내 GameObject
    ///   Prev Page Button      → 1페이지 이동 버튼
    ///   Fragment Images[7]    → 캐릭터별 이미지 조각 Image (CharacterId 1~7 순서)
    ///   Complete Image        → 완성된 사진 Image (기본 비활성)
    ///   Complete Image Button → 완성 사진 클릭 버튼
    ///   Hidden Ending Panel   → 검은 화면 패널
    ///   Hidden Ending Player  → 히든 엔딩 대사 재생 DialoguePlayer
    ///   Hidden Ending Lines   → 히든 엔딩 대사 목록
    ///   Secret Order[7]       → 올바른 클릭 순서 (기본: 1,2,3,4,5,6,7)
    ///   Secret Time Limit     → 순서 입력 제한 시간 (초, 0이면 무제한)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage2Controller : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("캐릭터 버튼 생성")]
        [SerializeField] private CharacterIconButton _iconButtonPrefab;
        [SerializeField] private Transform _iconGrid;

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("에필로그 카드")]
        [Tooltip("EpilogueCardPanel 7개를 씬에 미리 배치하고 연결합니다.\n"
                 + "CharacterId 1~7 순서로 연결하세요.")]
        [SerializeField] private EpilogueCardPanel[] _epilogueCards = new EpilogueCardPanel[7];

        [Header("페이지 이동")]
        [SerializeField] private Button _prevPageButton;

        [Header("이미지 조각 (CharacterId 1~7 순서)")]
        [Tooltip("캐릭터별 이미지 조각 Image 7개입니다.\n" +
                 "Element 0 = CharacterId 1 순서로 연결합니다.")]
        [SerializeField] private Image[] _fragmentImages = new Image[7];

        [Header("완성 사진")]
        [Tooltip("7조각 완성 시 표시할 전체 이미지입니다. (기본 비활성)")]
        [SerializeField] private GameObject _completeImageRoot;

        [Tooltip("완성 사진 클릭 버튼입니다.")]
        [SerializeField] private Button _completeImageButton;

        [Header("히든 엔딩")]
        [Tooltip("히든 엔딩 검은 화면 패널입니다.")]
        [SerializeField] private GameObject _hiddenEndingPanel;

        [Tooltip("히든 엔딩 대사를 재생할 DialoguePlayer입니다.")]
        [SerializeField] private DialoguePlayer _hiddenEndingPlayer;

        [Tooltip("히든 엔딩 대사 목록입니다.")]
        [SerializeField] private List<string> _hiddenEndingLines = new();

        [Header("히든 엔딩 퍼즐 설정")]
        [Tooltip("올바른 클릭 순서입니다.\n기본: 1,2,3,4,5,6,7\nInspector에서 자유롭게 변경 가능합니다.")]
        [SerializeField] private int[] _secretOrder = { 1, 2, 3, 4, 5, 6, 7 };

        [Tooltip("순서 입력 제한 시간(초)입니다.\n0이면 시간 제한 없음.")]
        [SerializeField] private float _secretTimeLimit = 30f;

        [Tooltip("조각 합쳐지는 연출 시간(초)입니다.")]
        [SerializeField] private float _assembleDuration = 1.5f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterIconButton[] _buttons;
        private int _selectedCharacterId = -1;

        // 히든 엔딩 퍼즐
        private int _secretProgress;    // 현재 몇 번째 순서까지 맞췄는지
        private bool _puzzleActive;      // 퍼즐 진행 중 여부
        private bool _puzzleComplete;    // 완성 여부
        private Coroutine _timeoutCoroutine;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            BuildGrid();
            _prevPageButton?.onClick.AddListener(OnPrevPageClicked);
            _completeImageButton?.onClick.AddListener(OnCompleteImageClicked);

            if (_completeImageRoot != null) _completeImageRoot.SetActive(false);
            if (_hiddenEndingPanel != null) _hiddenEndingPanel.SetActive(false);

            // 모든 조각 이미지를 black으로 초기화
            ResetFragmentColors();

            // 에필로그 카드 초기화
            InitEpilogueCards();
        }

        private void OnEnable()
        {
            if (_buttons == null) return;

            RefreshButtonStates();

            // 해금 상태가 바뀌었을 수 있으므로 카드 텍스트도 갱신
            RefreshEpilogueCards();
        }

        // ── Private — 그리드 생성 ─────────────────────────────────────────

        private void BuildGrid()
        {
            if (_iconButtonPrefab == null || _iconGrid == null) return;

            _buttons = new CharacterIconButton[7];

            for (int i = 0; i < 7; i++)
            {
                int capturedId = i + 1;

                var profile = _profileData?.FindProfile(capturedId);
                Sprite icon = profile?.CharacterIcon;
                string name = profile?.CharacterFullName ?? $"#{capturedId}";
                bool unlocked = _rewardSaveData != null &&
                                  _rewardSaveData.IsEpilogueUnlocked(capturedId);

                var btn = Instantiate(_iconButtonPrefab, _iconGrid);
                btn.Setup(
                    characterId: capturedId,
                    icon: icon,
                    collectedName: name,
                    onClicked: () => OnPortraitClicked(capturedId)
                );

                var button = btn.GetComponent<Button>();
                if (button != null)
                    button.interactable = unlocked;

                _buttons[i] = btn;
            }
        }

        private void RefreshButtonStates()
        {
            if (_buttons == null) return;

            bool allUnlocked = true;
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;

                int capturedId = i + 1;
                bool unlocked = _rewardSaveData != null &&
                                  _rewardSaveData.IsEpilogueUnlocked(capturedId);

                var button = _buttons[i].GetComponent<Button>();
                if (button != null)
                    button.interactable = unlocked;

                if (!unlocked) allUnlocked = false;
            }

            bool wasActive = _puzzleActive;

            // 7개 전부 해금 시 퍼즐 모드 활성화
            _puzzleActive = allUnlocked && !_puzzleComplete;

            // 퍼즐 모드 새로 진입 시
            // 일반 모드에서 white였던 이미지를 black으로 초기화
            if (!wasActive && _puzzleActive)
            {
                ResetFragmentColors();
                _selectedCharacterId = -1;
            }
        }

        // ── Private — 버튼 클릭 처리 ─────────────────────────────────────

        private void OnPortraitClicked(int characterId)
        {
            // 퍼즐 완성 후에는 일반 에필로그 열람만 허용
            if (_puzzleComplete)
            {
                SelectCharacter(characterId);
                return;
            }

            // 퍼즐 모드 처리
            if (_puzzleActive)
            {
                HandlePuzzleInput(characterId);
                return;
            }

            // 일반 동작: 시점 완결문 표시
            SelectCharacter(characterId);
        }

        private void SelectCharacter(int characterId)
        {
            // 이전 선택 이미지 → black
            SetFragmentColor(_selectedCharacterId, Color.black);

            _selectedCharacterId = characterId;

            if (_buttons != null)
                for (int i = 0; i < _buttons.Length; i++)
                    _buttons[i]?.SetSelected(i + 1 == characterId);

            // 새로 선택한 이미지 → white
            SetFragmentColor(characterId, Color.white);

            // 카드 방식으로 에필로그 표시 (카드 클릭으로 확대)
            // SelectCharacter는 이미지 컬러만 처리, 카드 확대는 사용자 클릭으로
        }

        // ── Private — 히든 엔딩 퍼즐 ─────────────────────────────────────

        /// <summary>
        /// 퍼즐 입력을 처리합니다.
        /// 올바른 순서면 진행, 틀리면 즉시 초기화합니다.
        ///
        /// 첫 입력 시작 규칙
        ///   _secretProgress == 0 이면 퍼즐의 첫 번째 입력입니다.
        ///   이 시점에 일반 모드에서 활성화된 이미지/카드 상태를 전부 초기화합니다.
        ///   첫 번째 정답(_secretOrder[0])의 이미지와 카드만 활성화해 퍼즐 시작을 명확히 합니다.
        /// </summary>
        private void HandlePuzzleInput(int characterId)
        {
            if (_secretOrder == null || _secretOrder.Length == 0) return;

            int expected = _secretOrder[_secretProgress];

            if (characterId == expected)
            {
                // ── 첫 입력 시 이전 상태 정리 ────────────────────────────
                if (_secretProgress == 0)
                {
                    // 일반 모드에서 활성화된 이미지 전부 black으로 초기화
                    ResetFragmentColors();

                    // 일반 모드 선택 카드 상태 초기화
                    _selectedCharacterId = -1;
                    if (_buttons != null)
                        foreach (var btn in _buttons)
                            btn?.SetSelected(false);

                    // 에필로그 카드도 전부 닫기
                    if (_epilogueCards != null)
                        foreach (var card in _epilogueCards)
                            card?.CollapseInstant();
                }

                // 정답 — 이미지 조각 white 활성화
                ShowFragmentImage(characterId);
                _secretProgress++;

                // 타임아웃 코루틴 재시작
                if (_secretTimeLimit > 0f)
                {
                    if (_timeoutCoroutine != null)
                        StopCoroutine(_timeoutCoroutine);
                    _timeoutCoroutine = StartCoroutine(TimeoutCoroutine());
                }

                if (_secretProgress >= _secretOrder.Length)
                {
                    // 완성!
                    if (_timeoutCoroutine != null)
                    {
                        StopCoroutine(_timeoutCoroutine);
                        _timeoutCoroutine = null;
                    }
                    StartCoroutine(AssembleCompleteImage());
                }
                else
                {
                    // 선택 상태 표시
                    if (_buttons != null)
                        for (int i = 0; i < _buttons.Length; i++)
                            _buttons[i]?.SetSelected(i + 1 == characterId);
                }
            }
            else
            {
                // 오답 → 즉시 초기화
                ResetPuzzle();
                // 일반 동작으로 시점 완결문 표시
                SelectCharacter(characterId);
            }
        }

        /// <summary>캐릭터 이미지 조각을 white로 활성화합니다.</summary>
        private void ShowFragmentImage(int characterId)
        {
            int index = characterId - 1;
            if (_fragmentImages == null || index >= _fragmentImages.Length) return;
            if (_fragmentImages[index] == null) return;

            _fragmentImages[index].color = Color.white;
            _fragmentImages[index].transform
                .DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);
        }

        /// <summary>퍼즐을 초기화합니다. 모든 이미지 조각을 black으로 되돌립니다.</summary>
        private void ResetPuzzle()
        {
            _secretProgress = 0;

            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }

            // 이미지 조각 전부 black으로 초기화
            ResetFragmentColors();

            // 버튼 선택 해제
            if (_buttons != null)
                foreach (var btn in _buttons)
                    btn?.SetSelected(false);
        }

        /// <summary>제한 시간 초과 시 퍼즐을 초기화합니다.</summary>
        private IEnumerator TimeoutCoroutine()
        {
            yield return new WaitForSeconds(_secretTimeLimit);
            ResetPuzzle();
            _timeoutCoroutine = null;
        }

        /// <summary>
        /// 7조각이 완성됐을 때 합쳐지는 연출 후 완성 사진을 표시합니다.
        /// </summary>
        /// <summary>
        /// 7조각이 완성됐을 때 반짝이며 완성 사진으로 전환되는 연출입니다.
        ///
        /// 연출 순서
        ///   1. 전체 조각 동시 반짝임 3회 (White ↔ 따뜻한 White 펄스)
        ///   2. 전체 조각 Black으로 페이드아웃
        ///   3. CompleteImage 팝업 (DOScale OutBack)
        /// </summary>
        private IEnumerator AssembleCompleteImage()
        {
            _puzzleComplete = true;
            _puzzleActive = false;

            // 버튼 선택 해제
            if (_buttons != null)
                foreach (var btn in _buttons)
                    btn?.SetSelected(false);

            // ── 1단계: 반짝임 연출 ───────────────────────────────────────
            if (_fragmentImages != null)
            {
                // 전체 White 보장
                foreach (var img in _fragmentImages)
                    if (img != null) img.color = Color.white;

                float flashDur = 0.1f;
                Color peakColor = new Color(1f, 1f, 0.7f, 1f); // 따뜻한 노란빛

                for (int i = 0; i < 4; i++)
                {
                    // White → PeakColor
                    foreach (var img in _fragmentImages)
                        img?.DOColor(peakColor, flashDur).SetEase(Ease.OutQuad);

                    yield return new WaitForSeconds(flashDur);

                    // PeakColor → White
                    foreach (var img in _fragmentImages)
                        img?.DOColor(Color.white, flashDur).SetEase(Ease.InQuad);

                    yield return new WaitForSeconds(flashDur);
                }

                // 마지막 강한 섬광 후 홀드
                foreach (var img in _fragmentImages)
                    img?.DOColor(peakColor, flashDur * 0.5f).SetEase(Ease.OutFlash);

                yield return new WaitForSeconds(0.2f);

                // ── 2단계: 전체 Black 페이드아웃 ─────────────────────────
                float fadeDur = _assembleDuration * 0.35f;
                foreach (var img in _fragmentImages)
                    img?.DOColor(Color.black, fadeDur).SetEase(Ease.InQuad);

                yield return new WaitForSeconds(fadeDur);
            }

            // ── 3단계: CompleteImage 팝업 ────────────────────────────────
            if (_completeImageRoot != null)
            {
                _completeImageRoot.SetActive(true);
                _completeImageRoot.transform.localScale = Vector3.zero;
                _completeImageRoot.transform
                    .DOScale(Vector3.one, _assembleDuration * 0.5f)
                    .SetEase(Ease.OutBack);

                yield return new WaitForSeconds(_assembleDuration * 0.5f);
            }
        }

        /// <summary>완성 사진 클릭 시 히든 엔딩을 재생합니다.</summary>
        private void OnCompleteImageClicked()
        {
            if (!_puzzleComplete) return;
            StartCoroutine(PlayHiddenEnding());
        }

        /// <summary>히든 엔딩 다이얼로그를 재생합니다.</summary>
        private IEnumerator PlayHiddenEnding()
        {
            // 검은 화면 페이드 인
            if (_hiddenEndingPanel != null)
            {
                _hiddenEndingPanel.SetActive(true);
                var cg = _hiddenEndingPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = _hiddenEndingPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                yield return cg.DOFade(1f, 0.5f).WaitForCompletion();
            }

            // 검은 화면 페이드 아웃
            if (_hiddenEndingPanel != null)
            {
                var cg = _hiddenEndingPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                    yield return cg.DOFade(0f, 0.5f).WaitForCompletion();
                _hiddenEndingPanel.SetActive(false);
            }
        }

        // ── Private — 이미지 컬러 유틸 ───────────────────────────────────────

        /// <summary>
        /// 모든 조각 이미지를 black으로 초기화합니다.
        /// Start() 및 ResetPuzzle()에서 호출합니다.
        /// </summary>
        private void ResetFragmentColors()
        {
            if (_fragmentImages == null) return;
            foreach (var img in _fragmentImages)
                if (img != null) img.color = Color.black;
        }

        /// <summary>
        /// 특정 캐릭터의 조각 이미지 색상을 변경합니다.
        /// characterId ≤ 0 이면 무시합니다.
        /// </summary>
        private void SetFragmentColor(int characterId, Color color)
        {
            if (characterId <= 0) return;
            int index = characterId - 1;
            if (_fragmentImages == null || index >= _fragmentImages.Length) return;
            if (_fragmentImages[index] == null) return;

            // 퍼즐 진행 중 white인 조각은 건드리지 않음
            // (히든 엔딩 퍼즐에서 맞춘 조각이 흰 상태 유지)
            if (_puzzleActive && _fragmentImages[index].color == Color.white
                && color == Color.black)
                return;

            _fragmentImages[index].color = color;
        }

        // ── Private — 시점 완결문 표시 ────────────────────────────────────

        // ── Private — 에필로그 카드 초기화 ──────────────────────────────────

        /// <summary>
        /// 씬에 배치된 EpilogueCardPanel 7개를 초기화합니다.
        /// 해금된 캐릭터는 활성화, 미해금은 비활성화합니다.
        /// 카드를 클릭하면 확대되며 시점 완결문이 표시됩니다.
        /// </summary>
        private void InitEpilogueCards()
        {
            if (_epilogueCards == null) return;

            for (int i = 0; i < _epilogueCards.Length; i++)
            {
                var card = _epilogueCards[i];
                if (card == null) continue;

                int capturedId = i + 1;
                bool unlocked = _rewardSaveData != null &&
                                 _rewardSaveData.IsEpilogueUnlocked(capturedId);

                // 항상 표시
                card.gameObject.SetActive(true);

                // 해금된 카드만 텍스트 주입
                // 미해금 카드는 빈 텍스트 → IPointerClickHandler가 있어도
                // 미해금 시 Expand() 내부에서 텍스트가 비어 있어 의미 없음
                if (unlocked)
                {
                    var profile = _profileData?.FindProfile(capturedId);
                    card.Setup(profile?.EpilogueText ?? string.Empty);
                }
                // 미해금은 Setup 호출 안 함 → CollapsedText / ExpandedText 빈 상태 유지

                // 다른 카드 열릴 때 이 카드 닫기 (중복 등록 방지)
                card.OnExpanded -= OnCardExpanded;
                card.OnExpanded += OnCardExpanded;
            }
        }

        /// <summary>
        /// 이미 생성된 카드의 텍스트와 활성 상태를 갱신합니다.
        /// OnEnable() 및 헬퍼 RefreshPage() 호출 시 사용합니다.
        /// </summary>
        private void RefreshEpilogueCards()
        {
            if (_epilogueCards == null) return;

            for (int i = 0; i < _epilogueCards.Length; i++)
            {
                var card = _epilogueCards[i];
                if (card == null) continue;

                int capturedId = i + 1;
                bool unlocked = _rewardSaveData != null &&
                                  _rewardSaveData.IsEpilogueUnlocked(capturedId);

                if (unlocked)
                {
                    var profile = _profileData?.FindProfile(capturedId);
                    string text = profile?.EpilogueText ?? string.Empty;

                    // 텍스트가 비어있지 않을 때만 갱신
                    if (!string.IsNullOrEmpty(text))
                        card.Setup(text);
                }
            }
        }

        /// <summary>카드 하나가 열리면 나머지를 모두 닫습니다.</summary>
        private void OnCardExpanded(EpilogueCardPanel opened)
        {
            if (_epilogueCards == null) return;
            foreach (var card in _epilogueCards)
            {
                if (card == null || card == opened) continue;
                if (card.IsExpanded) card.Collapse();
            }
        }

        // ── Private — 페이지 이동 ─────────────────────────────────────────

        private void OnPrevPageClicked()
        {
            _selectedCharacterId = -1;
            ResetPuzzle();

            if (_buttons != null)
                foreach (var btn in _buttons)
                    btn?.SetSelected(false);

            var bookAnimator = FindObjectOfType<TitleBookAnimator>();
            bookAnimator?.TurnPageBack();
        }
    }
}