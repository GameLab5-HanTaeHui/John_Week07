using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼 흐름 전체를 관리하는 싱글턴입니다.
///
/// ─── 역할 분리 ────────────────────────────────────────────────────────────
///   TutorialManager  : 단계 진행 로직, 이벤트 구독, 입력 허가 판단
///   TutorialUIManager: 가이드 텍스트 패널·하이라이트 등 시각 요소
///
/// ─── 기존 코드 수정 목록 (최소화) ─────────────────────────────────────────
///   PlayerTurnInputHandler  : 캐릭터 픽업/드롭 시 IsCharacterDragAllowed / IsZoneDropAllowed 체크
///   HoldToAdvanceTurn       : BeginHold() 에서 IsInputAllowed(AdvanceTurn) 체크
///   HoldToEnterFinalDecision: BeginHold() 에서 IsInputAllowed(EnterFinalDecision) 체크
///   HistoryPageController   : OnAnyPanelHeaderClicked 이벤트 추가
///   GameEndState            : Enter() 에서 _loopSM.FireGameEnded() 호출
///   LoopStateMachine        : OnGameEnded 이벤트 + FireGameEnded() 추가
///   GameFlowController      : OnGameEnded 패스스루 이벤트 추가
///
/// ─── Inspector 설정 가이드 ────────────────────────────────────────────────
///   1. TutorialGuideData 에셋에 모든 안내 텍스트를 채워넣으세요.
///   2. 각 DrawerPanel 루트에 CanvasGroup 컴포넌트를 추가한 뒤 _roleDocGroup 등에 연결하세요.
///   3. NotepadToggleManager를 _notepadToggleManager 에 연결하세요.
///   4. _lobbySceneName 에 로비 씬 이름을 입력하세요.
/// </summary>
[DisallowMultipleComponent]
public class TutorialManager : SingletonMonobehaviour<TutorialManager>
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("데이터")]
    [SerializeField] private TutorialGuideData _guideData;

    [Header("UI 매니저 참조")]
    [SerializeField] private TutorialUIManager _uiManager;

    [Header("튜토리얼 고정 설정값")]
    [Tooltip("튜토리얼에서 이동 가능한 캐릭터 ID (살인자 캐릭터)")]
    [SerializeField] private int _restrictedCharacterId = 4;
    [Tooltip("튜토리얼에서 허용되는 목표 구역 ID (하단 구역)")]
    [SerializeField] private int _restrictedTargetZoneId = 2;

    [Header("씬 이름")]
    [Tooltip("튜토리얼 실패 시 돌아갈 로비 씬 이름")]
    [SerializeField] private string _lobbySceneName = "LobbyScene";

    [Header("DrawerPanel CanvasGroup 참조 (각 패널 루트에 CanvasGroup 추가 필요)")]
    [SerializeField] private CanvasGroup _roleDocGroup;
    [SerializeField] private CanvasGroup _memoBookGroup;

    [Header("DrawerPanel 참조 (OnShown 구독용)")]
    [SerializeField] private DrawerPanel _roleDocDrawer;
    [SerializeField] private DrawerPanel _memoBookDrawer;

    [Header("History / 메모 참조")]
    [SerializeField] private HistoryPageController _historyController;
    [SerializeField] private NotepadToggleManager   _notepadToggleManager;

    [Header("하이라이트 대상 Transform 참조")]
    [Tooltip("이동 목표 구역 오브젝트 Transform")]
    [SerializeField] private Transform _restrictedZoneTransform;
    [Tooltip("깃털펜(HoldToAdvanceTurn) Transform")]
    [SerializeField] private Transform _quillPenTransform;
    [Tooltip("책(HoldToEnterFinalDecision) Transform")]
    [SerializeField] private Transform _finalDecisionBookTransform;
    [Tooltip("살인자 캐릭터 Transform (게임 시작 후 런타임에 자동 할당 가능)")]
    [SerializeField] private Transform _murdererCharacterTransform;

    [Header("하이라이트 대상 RectTransform 참조 (UI 요소)")]
    [SerializeField] private RectTransform _roleDocHighlightRect;
    [SerializeField] private RectTransform _memoBookHighlightRect;
    [SerializeField] private RectTransform _eventRecordHighlightRect;
    [SerializeField] private RectTransform _dateUIHighlightRect;
    [Tooltip("강제 퇴고 조건 UI RectTransform")]
    [SerializeField] private RectTransform _forceLoopConditionRect;
    [Header("화살표")]
    [SerializeField] private Transform _TutoArrow;

    // ── 정적 상태 (기존 코드에서 TutorialManager.IsActive 로 체크) ──────────

    /// <summary>TutorialManager 인스턴스가 존재하고 활성화된 경우 true.</summary>
    public static bool IsActive => Instance != null && Instance._currentPhase != TutorialPhase.Inactive;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private TutorialPhase         _currentPhase    = TutorialPhase.Inactive;
    private TutorialInputPermission _allowedInputs = TutorialInputPermission.None;

    // 이벤트형 안내 — 최초 1회만 표시
    private bool _shownNormalLoopGuide;
    private bool _shownForceLoopGuide;
    private bool _shownDeadlineGuide;
    private bool _shownFinalDecisionGuide;

    // 이벤트 구독 해제용 캐시
    private PlayerActionState _playerAction;
    private TurnStateMachine  _turnSM;

    // ── Unity ────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        InitDrawerLocks();
    }

    private void Start()
    {
        TutorialProgressRepository.Instance.TryLoad();
        TutorialProgressRepository.Instance.MarkStarted();

        if (_notepadToggleManager == null)
            _notepadToggleManager = FindObjectOfType<NotepadToggleManager>();

        _TutoArrow.gameObject.SetActive(false);

        // Inspector 미연결 시 CharacterViews에서 자동 탐색
        if (_murdererCharacterTransform == null)
        {
            var views = GameFlowController.Instance?.CharacterViews;
            if (views != null && views.TryGetValue(_restrictedCharacterId, out var view))
                _murdererCharacterTransform = view.transform;
        }

        SubscribeGameEvents();
        EnterPhase(TutorialPhase.Initial);
    }

    private void OnDestroy()
    {
        UnsubscribeGameEvents();
        if (_notepadToggleManager != null)
            _notepadToggleManager.OnAnyToggleChanged -= HandleMemoWriteToggled;
    }

    // ── 이벤트 구독 ──────────────────────────────────────────────────────────

    private void SubscribeGameEvents()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null)
        {
            Debug.LogError("[TutorialManager] GameFlowController를 찾을 수 없습니다.");
            return;
        }

        gfc.OnLoopReset            += HandleLoopReset;
        gfc.OnFinalDecisionEntered += HandleFinalDecisionEntered;
        gfc.OnGameEnded            += HandleGameEnded;

        _turnSM = gfc.GetTurnSM();
        if (_turnSM != null)
        {
            _turnSM.OnTurnEndEntered       += HandleTurnEndEntered;
            _turnSM.OnPlayerActionStarted  += HandlePlayerActionStarted;
            _turnSM.OnLoopConditionTriggered += HandleLoopConditionTriggered;
        }

        _playerAction = gfc.GetPlayerActionState();
        if (_playerAction != null)
            _playerAction.OnActionConfirmed += HandleActionConfirmed;

        if (_roleDocDrawer != null)
        {
            _roleDocDrawer.OnShown += HandleRoleDocShown;
            _roleDocDrawer.OnHidden += HandleRoleDocHidden;
        }
        if (_memoBookDrawer != null)
        {
            _memoBookDrawer.OnShown += HandleMemoBookShown;
            _memoBookDrawer.OnHidden += HandleMemoBookClose;
        }

        if (_historyController != null)
        {
            _historyController.OnAnyPanelHeaderClicked += HandleEventRecordClicked;
            _historyController.OnAnyPanelCollapsed += HandleEventRecordClosed;
        }

        if (_uiManager != null)
            _uiManager.OnGuideAdvanced += HandleGuideAdvanced;
    }

    private void UnsubscribeGameEvents()
    {
        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnLoopReset            -= HandleLoopReset;
            gfc.OnFinalDecisionEntered -= HandleFinalDecisionEntered;
            gfc.OnGameEnded            -= HandleGameEnded;
        }

        if (_turnSM != null)
        {
            _turnSM.OnTurnEndEntered         -= HandleTurnEndEntered;
            _turnSM.OnPlayerActionStarted    -= HandlePlayerActionStarted;
            _turnSM.OnLoopConditionTriggered -= HandleLoopConditionTriggered;
        }

        if (_playerAction != null)
            _playerAction.OnActionConfirmed -= HandleActionConfirmed;

        if (_roleDocDrawer != null)
        {
            _roleDocDrawer.OnShown -= HandleRoleDocShown;
            _roleDocDrawer.OnHidden -= HandleRoleDocHidden;
        }
        if (_memoBookDrawer != null)
        {
            _memoBookDrawer.OnShown -= HandleMemoBookShown;
            _memoBookDrawer.OnHidden -= HandleMemoBookClose;
        }

        if (_historyController != null)
        {
            _historyController.OnAnyPanelHeaderClicked -= HandleEventRecordClicked;
            _historyController.OnAnyPanelCollapsed -= HandleEventRecordClosed;
        }

        if (_uiManager != null)
            _uiManager.OnGuideAdvanced -= HandleGuideAdvanced;
    }

    // ── 순서형 단계 전환 ──────────────────────────────────────────────────────

    private void EnterPhase(TutorialPhase phase)
    {
        _currentPhase = phase;
        _uiManager?.ClearAll();

        switch (phase)
        {
            case TutorialPhase.Initial:
                Debug.Log("1");
                SetInputPermission(TutorialInputPermission.None);
                if (_murdererCharacterTransform != null)
                    _uiManager?.SetBounceOnly(_murdererCharacterTransform);
                ShowPhaseGuide(phase);
                StartCoroutine(TransitionAfterDelay(TutorialPhase.RestrictedMove, 0.5f));
                break;

            case TutorialPhase.RestrictedMove: // 캐릭터 옮기기
                Debug.Log("캐릭터 옮기기");
                SetInputPermission(TutorialInputPermission.CharacterMove);
                _uiManager?.SetClickAdvance(false);
                if (_murdererCharacterTransform != null)
                    _uiManager?.SetBounceOnly(_murdererCharacterTransform, loop: true);
                if (_restrictedZoneTransform != null)
                    _uiManager?.SetSecondaryBounce(_restrictedZoneTransform);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.QuillPenUnlocked:// 깃털 다음날
                Debug.Log("깃털");

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(440f, 200f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }
                SetInputPermission(TutorialInputPermission.AdvanceTurn);
                _uiManager?.ClearWorldHighlight();
                if (_quillPenTransform != null)
                    _uiManager?.SetWorldHighlight(_quillPenTransform);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.WaitingForTurnEnd:
                Debug.Log("4");
                SetInputPermission(TutorialInputPermission.None);
                _uiManager?.ClearAll();
                break;

            case TutorialPhase.ResultDisplaying:
                Debug.Log("5");
                SetInputPermission(TutorialInputPermission.None);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.RoleDocGuide: // 왼쪽 역할 패널
                Debug.Log("왼쪽 역할");

                _TutoArrow.localPosition = new Vector3(-680f, 220f, 0f);
                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
                _uiManager?.SetSecondaryBounce(_TutoArrow);

                SetInputPermission(TutorialInputPermission.RoleDocUI);
                SetDrawerInteractable(_roleDocGroup, true);
                _uiManager?.SetClickAdvance(false);
                if (_roleDocHighlightRect != null)
                    _uiManager?.SetUIHighlight(_roleDocHighlightRect);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.RoleDocCloseGuide: // 왼쪽 역할 패널 닫기
                Debug.Log("왼쪽 역할 닫기");
                _TutoArrow.gameObject.SetActive(false);
                SetInputPermission(TutorialInputPermission.RoleDocUI);
                _uiManager?.SetClickAdvance(false);
                if (_roleDocHighlightRect != null) _uiManager?.SetUIHighlight(_roleDocHighlightRect);
                ShowPhaseGuide(phase);
                break;

            // ── 순서: MemoBook → MemoWrite → EventRecord ──────────────────────

            case TutorialPhase.MemoBookGuide: // 오른쪽 하단 메모
                Debug.Log("오른쪽 하단 메모");
                if(!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(580f, -300f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 270f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                SetInputPermission(TutorialInputPermission.RoleDocUI
                                 | TutorialInputPermission.MemoOpen);
                SetDrawerInteractable(_memoBookGroup, true);
                _uiManager?.SetClickAdvance(false);
                _uiManager?.ClearUIHighlight();
                if (_memoBookHighlightRect != null)
                    _uiManager?.SetUIHighlight(_memoBookHighlightRect);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.MemoWriteGuide: // 오른쪽 하단 메모 작성
                Debug.Log("메모 기록");
                SetInputPermission(TutorialInputPermission.RoleDocUI
                                 | TutorialInputPermission.MemoOpen
                                 | TutorialInputPermission.MemoWrite);
                _uiManager?.SetClickAdvance(false);
                _uiManager?.ClearUIHighlight();
                ShowPhaseGuide(phase);
                if (_notepadToggleManager != null)
                    _notepadToggleManager.OnAnyToggleChanged += HandleMemoWriteToggled;
                break;

            case TutorialPhase.MemoBookCloseGuide: // 오른쪽 하단 메모 닫기
                Debug.Log("메모북 닫기");
                _TutoArrow.gameObject.SetActive(false);

                SetInputPermission(TutorialInputPermission.RoleDocUI | TutorialInputPermission.MemoOpen);
                _uiManager?.SetClickAdvance(false);
                if (_memoBookHighlightRect != null) _uiManager?.SetUIHighlight(_memoBookHighlightRect);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.EventRecordGuide: // 왼쪽 하단 파일
                Debug.Log("왼쪽 하단 파일");
                if(!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(-725f, -250f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 270);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                SetInputPermission(TutorialInputPermission.RoleDocUI
                                 | TutorialInputPermission.MemoOpen
                                 | TutorialInputPermission.MemoWrite
                                 | TutorialInputPermission.EventRecord);
                _uiManager?.SetClickAdvance(false);
                _uiManager?.ClearUIHighlight();
                if (_eventRecordHighlightRect != null)
                    _uiManager?.SetUIHighlight(_eventRecordHighlightRect);
                ShowPhaseGuide(phase);
                break;

            // 히스토리 닫기
            case TutorialPhase.EventRecordCloseGuide: // 왼쪽 하단 파일 닫기
                Debug.Log("히스토리 닫기");
                _TutoArrow.gameObject.SetActive(false);

                SetInputPermission(TutorialInputPermission.RoleDocUI | TutorialInputPermission.MemoOpen | TutorialInputPermission.EventRecord);
                _uiManager?.SetClickAdvance(false);
                if (_eventRecordHighlightRect != null) _uiManager?.SetUIHighlight(_eventRecordHighlightRect);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.FinalDecisionBookGuide:
                Debug.Log("11");

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(500f, 70f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                SetInputPermission(TutorialInputPermission.RoleDocUI
                                 | TutorialInputPermission.MemoOpen
                                 | TutorialInputPermission.MemoWrite
                                 | TutorialInputPermission.EventRecord
                                 | TutorialInputPermission.EnterFinalDecision);
                _uiManager?.SetClickAdvance(true);
                if (_finalDecisionBookTransform != null)
                    _uiManager?.SetWorldHighlight(_finalDecisionBookTransform);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.DateUIGuide:
                Debug.Log("12");
                _TutoArrow.gameObject.SetActive(false);

                SetInputPermission(TutorialInputPermission.All);
                _uiManager?.SetClickAdvance(true);
                _uiManager?.ClearWorldHighlight();
                if (_dateUIHighlightRect != null)
                    _uiManager?.SetUIHighlight(_dateUIHighlightRect);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.FullyUnlocked:
                Debug.Log("13");
                SetInputPermission(TutorialInputPermission.All);
                _currentPhase = TutorialPhase.Inactive;
                _uiManager?.ClearAll();
                break;
        }
    }

    // ── 이벤트형 안내 ─────────────────────────────────────────────────────────

    /// <summary>일반 퇴고 (3턴 소진) 발생 시</summary>
    private void HandleLoopReset()
    {
        if (_shownNormalLoopGuide) return;
        _shownNormalLoopGuide = true;
        ShowEventGuide(TutorialEventType.NormalLoop);
    }

    /// <summary>강제 퇴고 (루프 조건 발동) 발생 시</summary>
    private void HandleLoopConditionTriggered()
    {
        if (_shownForceLoopGuide) return;
        _shownForceLoopGuide = true;
        // 강제 퇴고는 일반 퇴고 안내보다 우선
        _shownNormalLoopGuide = true;
        ShowEventGuide(TutorialEventType.ForceLoop);
    }

    /// <summary>최종 집필 진입 시</summary>
    private void HandleFinalDecisionEntered()
    {
        if (_shownFinalDecisionGuide) return;
        _shownFinalDecisionGuide = true;
        ShowEventGuide(TutorialEventType.FinalDecision);
    }

    /// <summary>게임 종료 시 (win/loss)</summary>
    private void HandleGameEnded(bool isWin)
    {
        if (isWin)
        {
            TutorialProgressRepository.Instance.MarkCleared();
            return;
        }
        ShowEventGuide(TutorialEventType.GameFail);
        StartCoroutine(LoadRetrySceneAfterDelay(2f));
    }

    // ── 순서형 이벤트 핸들러 ──────────────────────────────────────────────────

    private void HandleActionConfirmed(int characterId, int targetZoneId)
    {
        if (_currentPhase != TutorialPhase.RestrictedMove) return;
        if (characterId != _restrictedCharacterId) return;
        if (targetZoneId != _restrictedTargetZoneId) return;

        EnterPhase(TutorialPhase.QuillPenUnlocked);
    }

    private void HandleTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
    {
        if (_currentPhase == TutorialPhase.QuillPenUnlocked
            || _currentPhase == TutorialPhase.WaitingForTurnEnd)
        {
            EnterPhase(TutorialPhase.ResultDisplaying);
        }

        // 마감일 체크
        var gfc = GameFlowController.Instance;
        if (gfc != null && gfc.LoopCount >= LoopStateMachine.MaxLoops - 1 && !_shownDeadlineGuide)
        {
            _shownDeadlineGuide = true;
            ShowEventGuide(TutorialEventType.Deadline);
        }
    }

    private void HandlePlayerActionStarted()
    {
        // 결과창이 끝나고 다음 PlayerAction 단계 시작 → UI 순차 가이드 시작
        if (_currentPhase == TutorialPhase.ResultDisplaying)
            EnterPhase(TutorialPhase.RoleDocGuide);
    }

    private void HandleRoleDocShown()
    {
        if (_currentPhase != TutorialPhase.RoleDocGuide) return;
        EnterPhase(TutorialPhase.RoleDocCloseGuide);
    }
    private void HandleRoleDocHidden()
    {
        if (_currentPhase != TutorialPhase.RoleDocCloseGuide) return;
        EnterPhase(TutorialPhase.MemoBookGuide); // 닫히면 -> 메모북 열기로 넘어감
    }

    private void HandleMemoBookShown()
    {
        if (_currentPhase != TutorialPhase.MemoBookGuide) return;
        EnterPhase(TutorialPhase.MemoWriteGuide);
    }

    private void HandleMemoWriteToggled()
    {
        if (_currentPhase != TutorialPhase.MemoWriteGuide) return;
        if (_notepadToggleManager != null)
            _notepadToggleManager.OnAnyToggleChanged -= HandleMemoWriteToggled;
        EnterPhase(TutorialPhase.MemoBookCloseGuide);
    }
    private void HandleMemoBookClose()
    {
        if (_currentPhase != TutorialPhase.MemoBookCloseGuide) return;
        EnterPhase(TutorialPhase.EventRecordGuide);
    }

    private void HandleEventRecordClicked()
    {
        if (_currentPhase != TutorialPhase.EventRecordGuide) return;
        EnterPhase(TutorialPhase.EventRecordCloseGuide);
    }
    private void HandleEventRecordClosed()
    {
        if (_currentPhase != TutorialPhase.EventRecordCloseGuide) return;
        EnterPhase(TutorialPhase.FinalDecisionBookGuide);
    }

    /// <summary>
    /// TutorialUIManager의 Advance 버튼을 눌렀을 때 호출됩니다.
    /// 현재 Phase에 따라 다음 Phase로 전환합니다.
    /// </summary>
    private void HandleGuideAdvanced()
    {
        switch (_currentPhase)
        {
            // 텍스트 클릭 시 가이드만 숨김
            // 실제 단계 진입은 책 클릭 → 취소 버튼(NotifyFinalDecisionCancelled)으로 처리
            case TutorialPhase.FinalDecisionBookGuide:
                _uiManager?.HideGuide();
                break;
            case TutorialPhase.DateUIGuide:
                EnterPhase(TutorialPhase.FullyUnlocked);
                break;
            default:
                _uiManager?.HideGuide();
                break;
        }
    }

    // ── 입력 허가 공개 API ────────────────────────────────────────────────────
    /// <summary>
    /// HoldToEnterFinalDecision의 확인 패널에서 취소(좀더 생각해보기) 버튼 클릭 시 호출됩니다.
    /// FinalDecisionBookGuide 단계에서만 유효하며 DateUIGuide로 진입합니다.
    /// </summary>
    public void NotifyFinalDecisionCancelled()
    {
        if (_currentPhase != TutorialPhase.FinalDecisionBookGuide) return;
        EnterPhase(TutorialPhase.DateUIGuide);
    }
    /// <summary>
    /// 튜토리얼 중 최종 추리 진입이 차단된 후
    /// 다시 책 클릭이 가능하도록 SetClickAdvance를 복원합니다.
    /// HoldToEnterFinalDecision의 확인 버튼 차단 시 호출합니다.
    /// </summary>
    public void RestoreClickAdvanceAfterBlock()
    {
        if (_currentPhase != TutorialPhase.FinalDecisionBookGuide) return;

        // 화살표와 바운스 복원
        if (!_TutoArrow.gameObject.activeSelf)
        {
            _TutoArrow.gameObject.SetActive(true);
            _TutoArrow.localPosition = new Vector3(500f, 70f, 0f);
            _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 0f);
            _uiManager?.SetSecondaryBounce(_TutoArrow);
        }

        // 가이드 텍스트 클릭 가능 복원
        _uiManager?.SetClickAdvance(true);
        ShowPhaseGuide(TutorialPhase.FinalDecisionBookGuide);
    }
    /// <summary>
    /// HoldToEnterFinalDecision의 책 오브젝트 클릭 시 호출됩니다.
    /// FinalDecisionBookGuide 단계에서 화살표를 숨깁니다.
    /// ConfirmPanel이 열리는 동안 화살표가 표시되지 않도록 합니다.
    /// </summary>
    public void NotifyFinalDecisionBookClicked()
    {
        if (_currentPhase != TutorialPhase.FinalDecisionBookGuide) return;
        _TutoArrow.gameObject.SetActive(false);
        _uiManager?.ClearSecondaryBounce();

        // 책 클릭 후 ConfirmPanel이 열리는 동안
        // 화면 클릭으로 가이드 텍스트가 사라지지 않도록 차단
        _uiManager?.SetClickAdvance(false);
    }
    /// <summary>지정한 입력 종류가 현재 허용되는지 반환합니다.</summary>
    public bool IsInputAllowed(TutorialInputPermission permission)
    {
        if (!IsActive) return true;
        return (_allowedInputs & permission) != 0;
    }

    /// <summary>
    /// 해당 캐릭터를 드래그할 수 있는지 반환합니다.
    /// CharacterMove 퍼미션이 있어야 하며, RestrictedMove 단계에서는 지정된 캐릭터만 허용됩니다.
    /// </summary>
    public bool IsCharacterDragAllowed(int characterId)
    {
        if (!IsActive) return true;
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;

        if (_currentPhase == TutorialPhase.RestrictedMove)
            return characterId == _restrictedCharacterId;

        return true;
    }

    /// <summary>
    /// 해당 캐릭터를 해당 구역에 드롭할 수 있는지 반환합니다.
    /// RestrictedMove 단계에서는 지정된 목표 구역만 허용됩니다.
    /// </summary>
    public bool IsZoneDropAllowed(int characterId, int zoneId)
    {
        if (!IsActive) return true;
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;

        if (_currentPhase == TutorialPhase.RestrictedMove)
            return characterId == _restrictedCharacterId && zoneId == _restrictedTargetZoneId;

        return true;
    }

    // ── Private 유틸 ──────────────────────────────────────────────────────────

    private void SetInputPermission(TutorialInputPermission permission)
    {
        _allowedInputs = permission;
    }

    private void ShowPhaseGuide(TutorialPhase phase)
    {
        if (_guideData == null || _uiManager == null) return;
        string text = _guideData.GetPhaseText(phase);
        if (!string.IsNullOrEmpty(text))
            _uiManager.ShowGuide(text);
    }

    private void ShowEventGuide(TutorialEventType eventType)
    {
        if (_guideData == null || _uiManager == null) return;
        string text = _guideData.GetEventText(eventType);
        if (!string.IsNullOrEmpty(text))
            _uiManager.ShowGuide(text);
    }

    /// <summary>DrawerPanel 루트의 CanvasGroup 인터랙션을 활성/비활성합니다.</summary>
    private static void SetDrawerInteractable(CanvasGroup group, bool interactable)
    {
        if (group == null) return;
        group.interactable    = interactable;
        group.blocksRaycasts  = interactable;
    }

    private IEnumerator TransitionAfterDelay(TutorialPhase nextPhase, float delay)
    {
        yield return new WaitForSeconds(delay);
        EnterPhase(nextPhase);
    }

    private IEnumerator LoadRetrySceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!string.IsNullOrEmpty(_lobbySceneName))
            SceneManager.LoadScene(_lobbySceneName);
        else
            Debug.LogWarning("[TutorialManager] 로비 씬 이름이 설정되지 않았습니다.");
    }

    // ── DrawerPanel 초기 잠금 설정 ──────────────────────────────────────────

    private void InitDrawerLocks()
    {
        SetDrawerInteractable(_roleDocGroup,        false);
        //SetDrawerInteractable(_narrativeOrderGroup, false);
        SetDrawerInteractable(_memoBookGroup,       false);
    }
}
