using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 개별 인물 기록장 패널입니다.
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   앞면 (FrontFace): 인물 기록장
    ///     - 캐릭터 이름/번호
    ///     - 진실 문장 조각 5개 (TMP_Text 배열)
    ///     - 거짓 문장 조각 5개 (TMP_Text 배열)
    ///     - 컨셉 카드 보기 버튼
    ///
    ///   뒷면 (BackFace): 컨셉 카드
    ///     - 컨셉 카드 내용
    ///     - 돌아가기 버튼
    ///
    /// ─── 슬라이드 연출 ───────────────────────────────────────────────────
    ///   토글 버튼 클릭
    ///     닫힘 → 현재 위치에서 위로 슬라이드업 (열림)
    ///     열림 → 원래 위치로 슬라이드다운 (닫힘)
    ///   뒷면 상태에서 토글 버튼 클릭
    ///     → 앞면으로 180도 회전 후 슬라이드다운
    ///
    /// ─── 카드 뒤집기 연출 ────────────────────────────────────────────────
    ///   DOTween ScaleX 방식
    ///     앞면 → ScaleX 1→0 (앞면 숨김) → 뒷면 활성화 → ScaleX 0→1
    ///     뒷면 → ScaleX 1→0 (뒷면 숨김) → 앞면 활성화 → ScaleX 0→1
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data            → ProfileDataSO 에셋
    ///   Hint Data               → FragmentHintDataSO 에셋
    ///   Dialogue Data           → CampaignDialogueSO 에셋
    ///   Fragment Collector      → _CampaignSystem/FragmentCollector
    ///   Flip Root               → 앞뒤가 함께 붙어있는 회전 대상 RectTransform
    ///   Front Face              → 앞면 GameObject
    ///   Back Face               → 뒷면 GameObject
    ///   Character Id Text       → 앞면/CharacterIdText
    ///   Character Name Text     → 앞면/CharacterNameText
    ///   True Fragment Slots[5]  → 진실 문장 조각 TMP_Text 5개
    ///   False Fragment Slots[5] → 거짓 문장 조각 TMP_Text 5개
    ///   True Fragment Ids[5]    → 진실 조각 FragmentId 5개 (Inspector 입력)
    ///   False Fragment Ids[5]   → 거짓 조각 FragmentId 5개 (Inspector 입력)
    ///   Flip To Back Button     → 컨셉 카드 보기 버튼
    ///   Flip To Front Button    → 돌아가기 버튼
    ///   Concept Card Text       → 뒷면 컨셉 카드 내용 TMP_Text
    ///   Open Offset             → 슬라이드업 거리 (px)
    ///   Flip Duration           → 뒤집기 한쪽 소요 시간 (초)
    ///   Anim Duration           → 슬라이드 애니메이션 시간 (초)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CharacterRecordPanel : MonoBehaviour, IPointerClickHandler
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("대화 조각 전체 데이터입니다.\n" +
                 "ClueText/HintText/HintDescription/조건/대사 모두 포함됩니다.")]
        [SerializeField] private FragmentDataSO _fragmentData;

        [Tooltip("캐릭터 이름/역할 표시용 ProfileDataSO 에셋입니다.\n" +
                 "엔딩 전용 데이터(FinalTalk)는 사용하지 않습니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("컴포넌트 참조")]
        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("카드 뒤집기 오브젝트")]
        [Tooltip("앞면과 뒷면을 모두 포함하는 회전 대상 RectTransform입니다.\n" +
                 "이 오브젝트의 ScaleX를 0→1로 조작해 뒤집기 연출을 구현합니다.")]
        [SerializeField] private RectTransform _flipRoot;

        [Tooltip("앞면 GameObject입니다. (인물 기록장)")]
        [SerializeField] private GameObject _frontFace;

        [Tooltip("뒷면 GameObject입니다. (컨셉 카드)")]
        [SerializeField] private GameObject _backFace;

        [Header("앞면 — 헤더")]
        [Tooltip("'#1' 형식으로 캐릭터 번호를 표시합니다.")]
        [SerializeField] private TMP_Text _characterIdText;

        [Tooltip("캐릭터 이름을 표시합니다. 미수집 시 글리치 처리됩니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Tooltip("'획득한 대화조각 (0/10)' 형식으로 수집 현황을 표시합니다.\n" +
                 "진실 5개 + 거짓 5개 = 총 10개 기준입니다.")]
        [SerializeField] private TMP_Text _fragmentCountText;

        [Header("앞면 — 문장 조각 슬롯")]
        [Tooltip("혼합 모드 여부입니다.\n" +
                 "false: 진실(왼쪽 5개) / 거짓(오른쪽 5개) 분리 배치\n" +
                 "true:  진실+거짓 섞어서 랜덤 배치 + 랜덤 위치 해금")]
        [SerializeField] private bool _mixMode = false;

        [Tooltip("분리 모드(mixMode=false)의 진실 슬롯 5개입니다. (왼쪽)")]
        [SerializeField] private TMP_Text[] _trueFragmentSlots = new TMP_Text[5];

        [Tooltip("분리 모드(mixMode=false)의 거짓 슬롯 5개입니다. (오른쪽)")]
        [SerializeField] private TMP_Text[] _falseFragmentSlots = new TMP_Text[5];

        [Tooltip("혼합 모드(mixMode=true)의 통합 슬롯 10개입니다.\n" +
                 "진실+거짓이 섞여 랜덤 배치됩니다.")]
        [SerializeField] private TMP_Text[] _mixedFragmentSlots = new TMP_Text[10];

        [Tooltip("진실 조각 FragmentId 목록입니다.\n" +
                 "ProfileClueDataSO 연결 시 자동 채워집니다. (P01_01 형식)\n" +
                 "수동 입력 불필요.")]
        [SerializeField] private string[] _trueFragmentIds = new string[5];

        [Tooltip("거짓 조각 FragmentId 목록입니다.\n" +
             "ProfileClueDataSO 연결 시 자동 채워집니다. (P01_01 형식)\n" +
             "수동 입력 불필요.")]
        [SerializeField] private string[] _falseFragmentIds = new string[5];

        [Header("힌트 패널")]
        [Tooltip("힌트 패널 컴포넌트입니다.")]
        [SerializeField] private HintPanel _hintPanel;

        [Header("앞면 — 버튼")]
        [Tooltip("컨셉 카드 보기 버튼입니다. 클릭 시 뒷면으로 뒤집힙니다.")]
        [SerializeField] private Button _flipToBackButton;

        [Tooltip("돌아가기 버튼입니다. 클릭 시 앞면으로 뒤집힙니다.")]
        [SerializeField] private Button _flipToFrontButton;

        [Header("슬롯 색상")]
        [Tooltip("수집된 조각 텍스트 색상")]
        [SerializeField] private Color _collectedColor = new Color(0.1f, 0.1f, 0.1f);

        [Tooltip("미수집 힌트 텍스트 색상")]
        [SerializeField] private Color _hintColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("글리치 설정")]

        [Tooltip("글리치 대체 문자 목록입니다.")]
        [SerializeField] private string _glitchChars = "█▓▒░?#@&*";

        [Tooltip("미수집 슬롯에 표시할 글리치 텍스트 고정 길이입니다.")]
        [SerializeField] private int _glitchLength = 16;

        [Header("슬라이드 애니메이션")]
        [Tooltip("버튼 클릭 시 패널이 위로 올라가는 거리 (px)")]
        [SerializeField] private float _openOffset = 400f;

        [Tooltip("슬라이드 애니메이션 시간 (초)")]
        [SerializeField] private float _animDuration = 0.35f;

        [Tooltip("슬라이드업 Ease")]
        [SerializeField] private Ease _expandEase = Ease.OutCubic;

        [Tooltip("슬라이드다운 Ease")]
        [SerializeField] private Ease _collapseEase = Ease.InCubic;

        [Header("카드 뒤집기 애니메이션")]
        [Tooltip("뒤집기 한쪽(ScaleX 1→0 또는 0→1) 소요 시간 (초)")]
        [SerializeField] private float _flipDuration = 0.2f;

        [Tooltip("뒤집기 Ease")]
        [SerializeField] private Ease _flipEase = Ease.InOutQuad;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        /// <summary>패널의 RectTransform 컴포넌트입니다.</summary>
        private RectTransform _rect;

        /// <summary>현재 Y 회전값을 추적해 앞/뒤 렌더링 순서를 제어합니다.</summary>
        private bool _isTrackingFlip;

        /// <summary>닫힌 상태의 Y 위치입니다. (씬에 배치된 원래 위치)</summary>
        private float _closedY;

        /// <summary>열린 상태의 Y 위치입니다. (_closedY + _openOffset)</summary>
        private float _openedY;

        /// <summary>현재 진행 중인 슬라이드 트윈입니다.</summary>
        private Tweener _slideTween;

        /// <summary>현재 진행 중인 뒤집기 트윈입니다.</summary>
        private Tweener _flipTween;

        /// <summary>현재 표시 중인 캐릭터 ID입니다.</summary>
        private int _currentCharacterId = -1;

        /// <summary>현재 표시 중인 캐릭터 프로파일 데이터입니다.</summary>
        private CharacterProfileData _currentProfile;

        /// <summary>현재 캐릭터의 FragmentEntry 5개입니다.</summary>
        private List<FragmentEntry> _currentClues;

        /// <summary>
        /// 혼합 모드 슬롯 배치입니다.
        /// 최초 1회만 셔플되고 이후 Open() 시 재사용됩니다.
        /// </summary>
        private (string profileClueId, bool isClue)[] _slotAssignments;

        /// <summary>슬롯 배치가 이미 초기화됐는지 여부입니다.</summary>
        private bool _slotsInitialized;

        /// <summary>현재 뒷면(컨셉 카드)이 표시 중인지 여부입니다.</summary>
        private bool _isBackFaceShowing;

        /// <summary>현재 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 뒤집기+슬라이드다운 애니메이션이 진행 중인지 여부입니다.
        /// Manager에서 다음 패널 Open 타이밍 대기에 사용합니다.
        /// </summary>
        public bool IsClosing { get; private set; }

        /// <summary>슬롯 배치가 초기화됐는지 여부입니다.</summary>
        public bool IsSlotsInitialized => _slotsInitialized;

        // ── Unity ────────────────────────────────────────────────────────

        /// <summary>
        /// 초기화합니다.
        /// 닫힌/열린 Y 위치를 계산하고 버튼 리스너를 등록합니다.
        /// </summary>
        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _closedY = _rect.anchoredPosition.y;
            _openedY = _closedY + _openOffset;

            // 뒷면은 처음에 비활성화
            if (_backFace != null) _backFace.SetActive(false);
            if (_frontFace != null) _frontFace.SetActive(true);

            // 컨셉 카드 보기 버튼은 조각 10개 수집 전까지 비활성화
            if (_flipToBackButton != null)
                _flipToBackButton.gameObject.SetActive(false);

            _flipToBackButton?.onClick.AddListener(FlipToBack);
            _flipToFrontButton?.onClick.AddListener(FlipToFront);
        }

        /// <summary>트윈과 버튼 리스너를 정리합니다.</summary>
        // ── 포인터 이벤트 ─────────────────────────────────────────────────

        /// <summary>
        /// CharacterRecordPanel 클릭 시 HintPanel이 열려있으면 닫습니다.
        /// HintPanel이 닫힌 상태에서는 아무 동작 없음.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_hintPanel != null && _hintPanel.IsExpanded)
                _hintPanel.CollapseExternal();
        }

        private void OnDestroy()
        {
            _slideTween?.Kill();
            _flipTween?.Kill();
            _flipToBackButton?.onClick.RemoveListener(FlipToBack);
            _flipToFrontButton?.onClick.RemoveListener(FlipToFront);
            UnsubscribeCyclicColorEvents();
        }
        /// <summary>
        /// 매 프레임 FlipRoot의 Y 회전값을 감지해
        /// 앞면/뒷면 Hierarchy 순서를 조정합니다.
        /// </summary>
        private void Update()
        {
            if (!_isTrackingFlip || _flipRoot == null) return;
            if (_frontFace == null || _backFace == null) return;

            float yRot = _flipRoot.localEulerAngles.y;

            if (yRot > 90f && yRot < 270f)
            {
                // 뒷면이 앞에 와야 하는 구간
                _backFace.transform.SetAsLastSibling();
            }
            else
            {
                // 앞면이 앞에 와야 하는 구간
                _frontFace.transform.SetAsLastSibling();
            }
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 기록장을 슬라이드업으로 엽니다.
        /// 슬롯 초기화는 Phase2 진입 시 InitializeSlots()에서 이미 완료됩니다.
        /// </summary>
        public void Open(int characterId)
        {
            if (!_slotsInitialized)
            {
                Debug.LogWarning($"[CharacterRecordPanel] 슬롯 미초기화 — CharacterId={characterId}");
                return;
            }

            // ★ 뒤집힌 상태: Manager가 WaitUntil(!IsClosing)으로 대기 후 Open 호출하므로
            //   여기서는 단순히 OpenInternal만 수행
            OpenInternal(characterId);
        }

        /// <summary>
        /// Open()의 실제 초기화 로직입니다.
        /// </summary>
        private void OpenInternal(int characterId)
        {
            _currentCharacterId = characterId;
            _currentProfile = _profileData?.FindProfile(characterId);

            ShowFrontFaceInstant();
            RefreshAll();
            SubscribeCyclicColorEvents(); // ★ 열릴 때 이벤트 구독

            _hintPanel?.SetCharacter(characterId);

            SlideTo(_openedY, _expandEase);
            IsOpen = true;
            IsClosing = false;
        }

        /// <summary>
        /// Phase2 진입 시 1회 호출됩니다.
        /// ProfileClueDataSO에서 조각을 로드하고 슬롯 배치를 결정합니다.
        /// Open()과 완전히 분리되어 있으며 열람 여부와 무관하게 동작합니다.
        /// </summary>
        public void InitializeSlots(int characterId)
        {
            if (_slotsInitialized) return;
            if (_fragmentData == null)
            {
                Debug.LogWarning("[CharacterRecordPanel] FragmentDataSO 미연결");
                return;
            }

            _currentClues = _fragmentData.GetByCharacter(characterId);
            if (_currentClues == null || _currentClues.Count == 0)
            {
                Debug.LogWarning($"[CharacterRecordPanel] CharacterId={characterId} FragmentEntry 없음");
                return;
            }

            Debug.Log($"[CharacterRecordPanel] CharacterId={characterId} " +
                      $"FragmentEntry {_currentClues.Count}개 로드");

            // 캐릭터 ID 저장 (Open() 전에도 RefreshIfCurrent 동작 보장)
            _currentCharacterId = characterId;

            if (!_mixMode)
            {
                // 분리 모드: FragmentId 배열 채우기
                for (int i = 0; i < 5; i++)
                {
                    string pid = i < _currentClues.Count
                        ? _currentClues[i].ProfileClueId : "";
                    if (i < _trueFragmentIds.Length) _trueFragmentIds[i] = pid;
                    if (i < _falseFragmentIds.Length) _falseFragmentIds[i] = pid;
                }
            }
            else
            {
                // 혼합 모드: 1회 셔플
                InitMixedSlots();
            }

            _slotsInitialized = true;
            Debug.Log($"[CharacterRecordPanel] 슬롯 초기화 완료 — CharacterId={characterId}");
        }

        /// <summary>
        /// 패널을 닫습니다.
        /// 뒷면 상태라면 먼저 앞면으로 회전 후 슬라이드다운합니다.
        /// CharacterRecordPanelManager 또는 토글 버튼에서 호출합니다.
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            IsClosing = true;
            UnsubscribeCyclicColorEvents(); // ★ 닫힐 때 이벤트 해제

            if (_isBackFaceShowing)
            {
                FlipToFrontThenSlideDown();
            }
            else
            {
                // 앞면 → 슬라이드다운 완료 시 IsClosing 해제
                _slideTween?.Kill();
                _slideTween = _rect
                    .DOAnchorPosY(_closedY, _animDuration)
                    .SetEase(_collapseEase)
                    .OnComplete(() => IsClosing = false);
            }
        }

        /// <summary>
        /// 조각 수집 또는 이름 공개 시 현재 열린 패널을 즉시 갱신합니다.
        /// CharacterRecordPanelManager에서 호출합니다.
        /// </summary>
        public void RefreshIfCurrent(int characterId)
        {
            if (!IsOpen) return;
            if (_currentCharacterId != characterId) return;
            if (_currentClues == null) return;

            RefreshHeader();
            RefreshFragmentCount();
            RefreshFragmentSlots();
        }

        // ── Private — 전체 갱신 ───────────────────────────────────────────

        /// <summary>헤더, 문장 조각 슬롯, 컨셉 카드를 모두 갱신합니다.</summary>
        private void RefreshAll()
        {
            RefreshHeader();
            RefreshFragmentSlots();
            RefreshFragmentCount();
        }

        // ── Private — 헤더 ────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 번호와 이름을 갱신합니다.
        /// 이름은 ProfileDataSO에서 가져오며, 수집된 이름이 있으면 우선 표시합니다.
        /// </summary>
        private void RefreshHeader()
        {
            if (_characterIdText != null)
                _characterIdText.text = $"#{_currentCharacterId}";

            if (_characterNameText == null) return;

            // 이름/역할은 ProfileDataSO에서 고정 표시 — 수집 개념 없음
            string name = _currentProfile?.CharacterFullName ?? $"#{_currentCharacterId}";
            string role = _currentProfile?.CharacterRole ?? "";

            _characterNameText.text = string.IsNullOrEmpty(role)
                ? name
                : $"{name} {role}";
        }

        // ── Private — 문장 조각 슬롯 ─────────────────────────────────────

        /// <summary>
        /// 획득한 대화조각 카운트를 갱신합니다.
        /// ProfileClueDataSO의 ProfileClueId(P01_01 형식)로 수집 여부를 확인합니다.
        /// CLUE/HINT 동일한 FragmentId를 사용하므로 수집 1개 = 슬롯 2개(진실+거짓) 동시 해금.
        /// 형식: "획득한 대화조각 (X/10)"
        /// </summary>
        private void RefreshFragmentCount()
        {
            if (_fragmentCountText == null) return;

            int total = _mixMode
                ? _mixedFragmentSlots.Length
                : _trueFragmentSlots.Length + _falseFragmentSlots.Length;

            int collected = 0;

            if (_currentClues != null)
            {
                foreach (var clue in _currentClues)
                {
                    if (clue == null || string.IsNullOrEmpty(clue.ProfileClueId)) continue;
                    bool isCollected = _fragmentCollector?.HasFragment(clue.ProfileClueId) ?? false;
                    // 수집 1개 → 진실+거짓 2슬롯 해금
                    if (isCollected) collected += 2;
                }
            }

            _fragmentCountText.text = $"획득한 대화조각 ({collected}/{total})";

            if (_flipToBackButton != null)
                _flipToBackButton.gameObject.SetActive(collected >= total);
        }

        /// <summary>
        /// 모드에 따라 문장 조각 슬롯을 갱신합니다.
        /// 분리 모드: 진실(왼쪽)/거짓(오른쪽) 5개씩
        /// 혼합 모드: 10개 슬롯에 랜덤 배치
        /// </summary>
        private void RefreshFragmentSlots()
        {
            if (_mixMode)
                RefreshMixedSlots();
            else
                RefreshSeparatedSlots();
        }

        /// <summary>분리 모드 — 진실(왼쪽) / 거짓(오른쪽) 5개씩 갱신합니다.</summary>
        private void RefreshSeparatedSlots()
        {
            for (int i = 0; i < _trueFragmentSlots.Length; i++)
            {
                var clue = (_currentClues != null && i < _currentClues.Count)
                    ? _currentClues[i] : null;
                string pid = clue?.ProfileClueId ?? "";
                string text = clue?.ClueText ?? "";
                RefreshSlot(_trueFragmentSlots[i], pid, text);

                // ★ CyclicColorText 상태 복원 — 수집된 슬롯만
                bool collected = !string.IsNullOrEmpty(pid) &&
                                 (_fragmentCollector?.HasFragment(pid) ?? false);
                if (collected)
                    RestoreCyclicState(_trueFragmentSlots[i], $"true_{i}");
            }

            for (int i = 0; i < _falseFragmentSlots.Length; i++)
            {
                var clue = (_currentClues != null && i < _currentClues.Count)
                    ? _currentClues[i] : null;
                string pid = clue?.ProfileClueId ?? "";
                string text = clue?.HintText ?? "";
                RefreshSlot(_falseFragmentSlots[i], pid, text);

                // ★ CyclicColorText 상태 복원 — 수집된 슬롯만
                bool collected = !string.IsNullOrEmpty(pid) &&
                                 (_fragmentCollector?.HasFragment(pid) ?? false);
                if (collected)
                    RestoreCyclicState(_falseFragmentSlots[i], $"false_{i}");
            }
        }

        /// <summary>
        /// 슬롯 TMP에 붙은 CyclicColorText의 상태를 SaveData에서 복원합니다.
        /// RefreshSlot()이 색상을 덮어쓰기 전에 CyclicColorText가 관리하도록 합니다.
        /// </summary>
        private void RestoreCyclicState(TMP_Text slot, string slotKey)
        {
            if (slot == null) return;
            var cyclic = slot.GetComponent<CyclicColorText>();
            if (cyclic == null) return;

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            var entry = saveData.slotColorStates?.Find(
                e => e.characterId == _currentCharacterId && e.slotKey == slotKey);
            int savedState = entry?.state ?? 0;
            cyclic.SetState(savedState);
        }

        /// <summary>혼합 모드 — 섞인 슬롯 10개를 갱신합니다.</summary>
        private void RefreshMixedSlots()
        {
            if (_slotAssignments == null) return;

            for (int i = 0; i < _mixedFragmentSlots.Length; i++)
            {
                if (i >= _slotAssignments.Length) break;

                var (pid, isClue) = _slotAssignments[i];
                var clue = _currentClues?.Find(c => c.ProfileClueId == pid);
                string text = isClue
                    ? (clue?.ClueText ?? "")
                    : (clue?.HintText ?? "");

                RefreshSlot(_mixedFragmentSlots[i], pid, text);
            }
        }

        /// <summary>
        /// 슬롯 1개를 갱신합니다.
        /// FragmentId 없음 → 글리치 더미 텍스트
        /// 미수집 → 힌트를 글리치 처리
        /// 수집됨 → 실제 대사
        /// </summary>
        private void RefreshSlot(TMP_Text slot, string fragmentId, string hintText)
        {
            if (slot == null) return;

            slot.enabled = true;

            // FragmentId가 없는 슬롯 → 글리치 더미 텍스트로 채움
            if (string.IsNullOrEmpty(fragmentId))
            {
                slot.text = ApplyGlitch("???????????????????");
                slot.color = _hintColor;
                return;
            }

            bool isCollected = _fragmentCollector?.HasFragment(fragmentId) ?? false;

            if (isCollected)
            {
                // 수집됨 → hintText 파라미터가 실제 CLUE/HINT 텍스트이므로 그대로 표시
                slot.text = hintText;
                // ★ CyclicColorText가 붙어있으면 색상은 CyclicColorText가 관리
                // 직접 color를 덮어쓰면 CyclicColorText 상태(초록/빨강)가 초기화됨
                var cyclic = slot.GetComponent<CyclicColorText>();
                if (cyclic == null)
                    slot.color = _collectedColor;
                // CyclicColorText 상태 복원은 RefreshSeparatedSlots에서 별도 처리
            }
            else
            {
                // 미수집 → 힌트를 글리치 처리해서 표시
                slot.text = ApplyGlitch(hintText);
                slot.color = _hintColor;
            }
        }

        // ── CyclicColorText 이벤트 구독/저장 ────────────────────────────────

        private void SubscribeCyclicColorEvents()
        {
            foreach (var slot in _trueFragmentSlots)
                SubscribeSlot(slot, true);
            foreach (var slot in _falseFragmentSlots)
                SubscribeSlot(slot, false);
        }

        private void UnsubscribeCyclicColorEvents()
        {
            foreach (var slot in _trueFragmentSlots)
                UnsubscribeSlot(slot);
            foreach (var slot in _falseFragmentSlots)
                UnsubscribeSlot(slot);
        }

        private void SubscribeSlot(TMP_Text slot, bool isTrue)
        {
            if (slot == null) return;
            var cyclic = slot.GetComponent<CyclicColorText>();
            if (cyclic == null) return;
            cyclic.OnStateChanged -= OnCyclicStateChanged;
            cyclic.OnStateChanged += OnCyclicStateChanged;
        }

        private void UnsubscribeSlot(TMP_Text slot)
        {
            if (slot == null) return;
            var cyclic = slot.GetComponent<CyclicColorText>();
            if (cyclic == null) return;
            cyclic.OnStateChanged -= OnCyclicStateChanged;
        }

        private void OnCyclicStateChanged(CyclicColorText sender, int state)
        {
            // 어느 슬롯인지 찾아서 slotKey 결정
            for (int i = 0; i < _trueFragmentSlots.Length; i++)
            {
                if (_trueFragmentSlots[i] == null) continue;
                var cyclic = _trueFragmentSlots[i].GetComponent<CyclicColorText>();
                if (cyclic == sender) { SaveSlotState($"true_{i}", state); return; }
            }
            for (int i = 0; i < _falseFragmentSlots.Length; i++)
            {
                if (_falseFragmentSlots[i] == null) continue;
                var cyclic = _falseFragmentSlots[i].GetComponent<CyclicColorText>();
                if (cyclic == sender) { SaveSlotState($"false_{i}", state); return; }
            }
        }

        private void SaveSlotState(string slotKey, int state)
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            var entry = saveData.slotColorStates?.Find(
                e => e.characterId == _currentCharacterId && e.slotKey == slotKey);
            if (entry != null)
            {
                entry.state = state;
            }
            else
            {
                saveData.slotColorStates ??= new System.Collections.Generic.List<SlotColorStateEntry>();
                saveData.slotColorStates.Add(new SlotColorStateEntry
                { characterId = _currentCharacterId, slotKey = slotKey, state = state });
            }
            CampaignSaveManager.Instance.Save(saveData);
        }

        // ── Private — 카드 뒤집기 ─────────────────────────────────────────

        /// <summary>
        /// 앞면 → 뒷면으로 뒤집습니다.
        /// 컨셉 카드 보기 버튼 클릭 시 호출됩니다.
        /// </summary>
        private void FlipToBack()
        {
            if (_isBackFaceShowing) return;
            Flip(toBack: true);
        }

        /// <summary>
        /// 뒷면 → 앞면으로 뒤집습니다.
        /// 돌아가기 버튼 클릭 시 호출됩니다.
        /// </summary>
        private void FlipToFront()
        {
            if (!_isBackFaceShowing) return;
            Flip(toBack: false);
        }

        /// <summary>
        /// 뒷면 → 앞면으로 회전 후 슬라이드다운합니다.
        /// 뒷면 상태에서 토글 버튼 클릭 시 호출됩니다.
        /// </summary>
        private void FlipToFrontThenSlideDown()
        {
            if (_flipRoot == null)
            {
                SlideTo(_closedY, _collapseEase);
                return;
            }

            _flipTween?.Kill();
            _isTrackingFlip = true;

            // 앞면/뒷면 모두 활성화
            if (_frontFace != null) _frontFace.SetActive(true);
            if (_backFace != null) _backFace.SetActive(true);

            // 현재 180°에서 180° 더 회전 → 360°(=0°)
            float currentY = _flipRoot.localEulerAngles.y;

            _flipTween = _flipRoot
                .DOLocalRotate(new Vector3(0f, currentY + 180f, 0f), _flipDuration * 2f)
                .SetEase(_flipEase)
                .OnComplete(() =>
                {
                    if (_backFace != null) _backFace.SetActive(false);
                    _flipRoot.localEulerAngles = Vector3.zero;
                    _isBackFaceShowing = false;
                    _isTrackingFlip = false;

                    SlideTo(_closedY, _collapseEase);
                    // 슬라이드다운 완료 후 IsClosing 해제
                    _slideTween?.OnComplete(() => IsClosing = false);
                });
        }

        /// <summary>
        /// Y축 회전으로 카드를 뒤집습니다.
        /// Update()에서 회전값을 감지해 앞/뒤 Hierarchy 순서를 자동 조정합니다.
        /// </summary>
        /// <param name="toBack">true = 앞→뒤 / false = 뒤→앞</param>
        private void Flip(bool toBack)
        {
            if (_flipRoot == null) return;

            _flipTween?.Kill();
            _isTrackingFlip = true;

            // 앞→뒤: 현재 0° → 180°
            // 뒤→앞: 현재 180° → 360°(=0°)
            float currentY = _flipRoot.localEulerAngles.y;
            float targetY = toBack ? currentY + 180f : currentY + 180f;

            // 뒤집기 시작 전 상태 설정
            _isBackFaceShowing = toBack;

            // 앞면/뒷면 모두 활성화 상태로 유지 (Update에서 순서로 제어)
            if (_frontFace != null) _frontFace.SetActive(true);
            if (_backFace != null) _backFace.SetActive(true);

            _flipTween = _flipRoot
               .DOLocalRotate(new Vector3(0f, targetY, 0f), _flipDuration * 2f)
               .SetEase(_flipEase)
               .OnComplete(() =>
               {
                   if (toBack)
                   {
                       // 앞→뒤 완료: 뒷면 상태 유지 (180° 고정)
                       if (_frontFace != null) _frontFace.SetActive(false);
                       _flipRoot.localEulerAngles = new Vector3(0f, 180f, 0f);
                       _isBackFaceShowing = true;
                   }
                   else
                   {
                       // 뒤→앞 완료: 앞면 상태 유지 (0° 고정)
                       if (_backFace != null) _backFace.SetActive(false);
                       _flipRoot.localEulerAngles = new Vector3(0f, 0f, 0f);
                       _isBackFaceShowing = false;
                   }

                   _isTrackingFlip = false;
               });
        }

        /// <summary>
        /// 애니메이션 없이 즉시 앞면 상태로 초기화합니다.
        /// Open() 호출 시 초기화에 사용합니다.
        /// </summary>
        private void ShowFrontFaceInstant()
        {
            _isBackFaceShowing = false;
            _isTrackingFlip = false;

            if (_frontFace != null) _frontFace.SetActive(true);
            if (_backFace != null) _backFace.SetActive(false);

            // 회전값과 ScaleX 초기화
            if (_flipRoot != null)
                _flipRoot.localEulerAngles = Vector3.zero;
        }

        // ── Private — 슬라이드 애니메이션 ────────────────────────────────

        /// <summary>
        /// 지정한 Y 위치로 슬라이드합니다.
        /// </summary>
        /// <param name="targetY">목표 Y 위치</param>
        /// <param name="ease">Ease 타입</param>
        private void SlideTo(float targetY, Ease ease)
        {
            _slideTween?.Kill();
            _slideTween = _rect
                .DOAnchorPosY(targetY, _animDuration)
                .SetEase(ease);
        }

        // ── Private — 글리치 처리 ─────────────────────────────────────────

        /// <summary>
        /// 혼합 모드에서 진실(5개)+거짓(5개) 총 10개 항목을
        /// 랜덤 셔플해 _slotAssignments에 배치합니다.
        /// Open() 시 1회 호출됩니다.
        /// </summary>
        private void InitMixedSlots()
        {
            // 10개 항목 생성 (CLUE 5개 + HINT 5개)
            var items = new List<(string profileClueId, bool isClue)>();

            if (_currentClues != null)
            {
                foreach (var clue in _currentClues)
                {
                    items.Add((clue.ProfileClueId, true));   // CLUE (진실)
                    items.Add((clue.ProfileClueId, false));  // HINT (거짓)
                }
            }

            // Fisher-Yates 셔플
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }

            _slotAssignments = items.ToArray();
        }

        /// <summary>
        /// 고정 길이의 100% 글리치 텍스트를 생성합니다.
        /// 원본 텍스트와 무관하게 _glitchLength 길이로 생성해
        /// 원문 유추를 방지합니다.
        /// </summary>
        private string ApplyGlitch(string _ = null)
        {
            if (string.IsNullOrEmpty(_glitchChars)) return "????????????????";

            var sb = new System.Text.StringBuilder(_glitchLength);
            for (int i = 0; i < _glitchLength; i++)
                sb.Append(_glitchChars[UnityEngine.Random.Range(0, _glitchChars.Length)]);

            return sb.ToString();
        }
    }
}