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
    [SerializeField] private Transform _heroCharacterTransform;

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
            _notepadToggleManager = FindFirstObjectByType<NotepadToggleManager>();

        _TutoArrow.gameObject.SetActive(false);

        // Inspector 미연결 시 CharacterViews에서 자동 탐색
        if (_heroCharacterTransform == null)
        {
            var views = GameFlowController.Instance?.CharacterViews;
            if (views != null && views.TryGetValue(_restrictedCharacterId, out var view))
                _heroCharacterTransform = view.transform;
        }

        SubscribeGameEvents();
        EnterPhase(TutorialPhase.WaitIntro);
    }
    // DialogueManager에서 호출할 튜토리얼 시작 API
    public void StartTutorial()
    {
        EnterPhase(TutorialPhase.Dialog_Intro1);
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
            // 검은 화면 대기 단계
            case TutorialPhase.WaitIntro:
                // IsActive는 true가 되지만, 권한이 None이므로 
                // 최종 추리 책, 턴 넘기기, 캐릭터 이동 등 모든 입력이 완벽히 차단됩니다!
                SetInputPermission(TutorialInputPermission.None);
                _uiManager?.SetClickAdvance(false);
                _uiManager?.HideGuide();
                break;
            // ── [클릭으로 대화만 넘기는 단계들 묶음] ──
            case TutorialPhase.Dialog_Intro1:               // 이곳에서 대량 사건이...
            case TutorialPhase.Dialog_Intro2:               // 저쪽 구역에 사람이 있네?...
            case TutorialPhase.Dialog_PostMove:             // 좋아 대화 해봐야지...
            case TutorialPhase.Dialog_Npc1:                 // 너는 누구야?
            case TutorialPhase.Dialog_Hero1:                // 나는 (주인공)이고...
            case TutorialPhase.Dialog_Npc2:                 // 사건을 추리하는 사람이야?
            case TutorialPhase.Dialog_Npc3:                 // 우릴 도와줘!
            case TutorialPhase.Dialog_Npc4:                 // 여태껏 정보들이 있는데...
            case TutorialPhase.Dialog_SystemGuidePanels:    // 왼쪽 사건 기록지, 아래...
            case TutorialPhase.Dialog_Hero2:                // 살인자..? 기록해봐야겠어.
            case TutorialPhase.Dialog_Hero3:                // 사건 기록지..?
            case TutorialPhase.Dialog_Hero4:                // 이건 역할을 기록하는 수첩이구나.
            case TutorialPhase.Dialog_Hero5:                // 이건 상세 정보구나 힌트도...
            case TutorialPhase.Dialog_Hero6:                // 좋아 사건 추리가 끝나면...
                // 오직 대화 텍스트 넘기기(화면 클릭) 권한만 부여
                SetInputPermission(TutorialInputPermission.DialogueAdvance);
                _uiManager?.SetClickAdvance(true);

                if (!_TutoArrow.gameObject.activeSelf)
                    _TutoArrow.gameObject.SetActive(false);

                    ShowPhaseGuide(phase);
                break;

            // ── [플레이어의 실제 조작이 필요한 단계들] ──
            case TutorialPhase.Action_MoveCharacter:        // 캐릭터 이동
                SetInputPermission(TutorialInputPermission.CharacterMove);
                _uiManager?.SetClickAdvance(false); // 클릭 대화 넘기기 차단!
                if (_heroCharacterTransform != null) _uiManager?.SetBounceOnly(_heroCharacterTransform, loop: true);
                if (_restrictedZoneTransform != null) _uiManager?.SetSecondaryBounce(_restrictedZoneTransform);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_TurnEnd:              // 턴 종료 (깃털펜)
                SetInputPermission(TutorialInputPermission.AdvanceTurn);
                _uiManager?.SetClickAdvance(false);
                if (_quillPenTransform != null) _uiManager?.SetWorldHighlight(_quillPenTransform);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(415f, 200f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_OpenRoleDoc:          // 역할 패널 열기
            case TutorialPhase.Action_CloseRoleDoc:         // 역할 패널 닫기
                SetInputPermission(TutorialInputPermission.RoleDocToggle);
                _uiManager?.SetClickAdvance(false);
                SetDrawerInteractable(_roleDocGroup, true);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(680f, 270f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_OpenHistory:          // 사건 기록지 열기
            case TutorialPhase.Action_CloseHistory:         // 사건 기록지 닫기
                SetInputPermission(TutorialInputPermission.HistoryToggle);
                _uiManager?.SetClickAdvance(false);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(-510f, -340f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_OpenMemo:             // 메모 수첩 열기
            case TutorialPhase.Action_CloseMemo:            // 메모 수첩 닫기
                SetInputPermission(TutorialInputPermission.MemoToggle);
                SetDrawerInteractable(_memoBookGroup, true);
                _uiManager?.SetClickAdvance(false);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(690f, -290f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_WriteMemo:            // 메모 격자칸 작성
                // 메모 작성이 가능하도록 메모 토글+작성 권한 동시 부여
                SetInputPermission(TutorialInputPermission.MemoToggle | TutorialInputPermission.MemoWrite);
                _uiManager?.SetClickAdvance(false);
                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_ClickSwapButton:      // 인물 카드 보기 버튼 클릭
                SetInputPermission(TutorialInputPermission.HistorySwapButton);
                _uiManager?.SetClickAdvance(false);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(270f, -350f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_OpenCharacterCard:    // 인물 카드 열기
            case TutorialPhase.Action_CloseCharacterCard:   // 인물 카드 닫기
                SetInputPermission(TutorialInputPermission.CharacterCardToggle);
                _uiManager?.SetClickAdvance(false);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(-60f, -390f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_ClickHintPostIt:      // 힌트 포스트잇 열기
            case TutorialPhase.Action_CloseHintPostIt:      // 힌트 포스트잇 닫기
                SetInputPermission(TutorialInputPermission.HintPostItToggle);
                _uiManager?.SetClickAdvance(false);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(-80f, -245f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;

            case TutorialPhase.Action_FinalDecision:        // 최종 집필 책 클릭
                SetInputPermission(TutorialInputPermission.FinalDecision);
                _uiManager?.SetClickAdvance(false);
                if (_finalDecisionBookTransform != null) _uiManager?.SetWorldHighlight(_finalDecisionBookTransform);

                if (!_TutoArrow.gameObject.activeSelf)
                {
                    _TutoArrow.gameObject.SetActive(true);
                    _TutoArrow.localPosition = new Vector3(550f, 130f, 0f);
                    _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -15f);
                    _uiManager?.SetSecondaryBounce(_TutoArrow);
                }

                ShowPhaseGuide(phase);
                break;
        }
    }

    // ── 이벤트형 안내 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 대화 패널(화면)을 클릭했을 때 다음 대화나 행동으로 넘어가는 로직입니다.
    /// </summary>
    private void HandleGuideAdvanced()
    {
        if (!IsInputAllowed(TutorialInputPermission.DialogueAdvance)) return;

        switch (_currentPhase)
        {
            // 1. 도입부 흐름
            case TutorialPhase.Dialog_Intro1: EnterPhase(TutorialPhase.Dialog_Intro2); break;
            case TutorialPhase.Dialog_Intro2: EnterPhase(TutorialPhase.Action_MoveCharacter); break; // 대화 후 조작 유도

            // 2. 캐릭터 이동 후 대화 -> 턴 종료 유도
            case TutorialPhase.Dialog_PostMove: EnterPhase(TutorialPhase.Action_TurnEnd); break;

            // 3. 턴 종료 후 긴 대화 릴레이
            case TutorialPhase.Dialog_Npc1: EnterPhase(TutorialPhase.Dialog_Hero1); break;
            case TutorialPhase.Dialog_Hero1: EnterPhase(TutorialPhase.Dialog_Npc2); break;
            case TutorialPhase.Dialog_Npc2: EnterPhase(TutorialPhase.Dialog_Npc3); break;
            case TutorialPhase.Dialog_Npc3: EnterPhase(TutorialPhase.Dialog_Npc4); break;
            case TutorialPhase.Dialog_Npc4: EnterPhase(TutorialPhase.Dialog_SystemGuidePanels); break;
            case TutorialPhase.Dialog_SystemGuidePanels: EnterPhase(TutorialPhase.Dialog_Hero2); break;

            // 4. 대화 끝, 역할 패널 조작 유도
            case TutorialPhase.Dialog_Hero2: EnterPhase(TutorialPhase.Action_OpenRoleDoc); break;

            // 5. 역할 패널 닫은 후 대화 -> 사건 기록지 조작 유도
            case TutorialPhase.Dialog_Hero3: EnterPhase(TutorialPhase.Action_OpenHistory); break;

            // 6. 사건 기록지 닫은 후 대화 -> 메모장 조작 유도
            case TutorialPhase.Dialog_Hero4: EnterPhase(TutorialPhase.Action_OpenMemo); break;

            // 7. 인물 카드 연 후 대화 -> 포스트잇 조작 유도
            case TutorialPhase.Dialog_Hero5: EnterPhase(TutorialPhase.Action_ClickHintPostIt); break;

            // 8. 인물 카드 완전히 닫은 후 최종 대화 -> 최종 집필 유도
            case TutorialPhase.Dialog_Hero6: EnterPhase(TutorialPhase.Action_FinalDecision); break;
        }
    }

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

    // ── 순서형 이벤트 핸들러 (UI 상호작용 및 게임 이벤트 감지) ────────────

    // [캐릭터 이동 완료] -> Dialog_PostMove 대화로
    private void HandleActionConfirmed(int characterId, int targetZoneId)
    {
        if (_currentPhase != TutorialPhase.Action_MoveCharacter) return;
        if (characterId != _restrictedCharacterId || targetZoneId != _restrictedTargetZoneId) return;
        EnterPhase(TutorialPhase.Dialog_PostMove);
    }

    // [턴 종료 완료] -> Dialog_Npc1 대화로
    private void HandleTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
    {
        if (_currentPhase == TutorialPhase.Action_TurnEnd)
        {
            EnterPhase(TutorialPhase.Dialog_Npc1);
        }

        var gfc = GameFlowController.Instance;
        if (gfc != null && gfc.LoopCount >= LoopStateMachine.MaxLoops - 1 && !_shownDeadlineGuide)
        {
            _shownDeadlineGuide = true;
            ShowEventGuide(TutorialEventType.Deadline);
        }
    }

    private void HandlePlayerActionStarted() { /* 필요 시 활용 */ }

    // [역할 패널 열림] -> 패널 닫기 안내로
    private void HandleRoleDocShown()
    {
        if (_currentPhase != TutorialPhase.Action_OpenRoleDoc) return;
        EnterPhase(TutorialPhase.Action_CloseRoleDoc);
    }

    // [역할 패널 닫힘] -> Dialog_Hero3 대화로
    private void HandleRoleDocHidden()
    {
        if (_currentPhase != TutorialPhase.Action_CloseRoleDoc) return;
        EnterPhase(TutorialPhase.Dialog_Hero3);
    }

    // [사건 기록지 열림] -> 기록지 닫기 안내로
    private void HandleEventRecordClicked()
    {
        if (_currentPhase != TutorialPhase.Action_OpenHistory) return;
        EnterPhase(TutorialPhase.Action_CloseHistory);
    }

    // [사건 기록지 닫힘] -> Dialog_Hero4 대화로
    private void HandleEventRecordClosed()
    {
        if (_currentPhase != TutorialPhase.Action_CloseHistory) return;
        EnterPhase(TutorialPhase.Dialog_Hero4);
    }

    // [메모장 열림] -> 메모장 작성 안내로
    private void HandleMemoBookShown()
    {
        if (_currentPhase != TutorialPhase.Action_OpenMemo) return;
        EnterPhase(TutorialPhase.Action_WriteMemo);
    }

    // [메모 작성 (토글) 완료] -> 메모장 닫기 안내로
    private void HandleMemoWriteToggled()
    {
        if (_currentPhase != TutorialPhase.Action_WriteMemo) return;
        EnterPhase(TutorialPhase.Action_CloseMemo);
    }

    // [메모장 닫힘] -> 인물 카드 보기 버튼 안내로
    private void HandleMemoBookClose()
    {
        if (_currentPhase != TutorialPhase.Action_CloseMemo) return;
        EnterPhase(TutorialPhase.Action_ClickSwapButton);
    }

    // ── 새로 추가된 이벤트 핸들러 (다른 스크립트에서 호출해주어야 함) ──

    public void NotifySwapButtonClicked()
    {
        if (_currentPhase != TutorialPhase.Action_ClickSwapButton) return;
        EnterPhase(TutorialPhase.Action_OpenCharacterCard);
    }

    public void NotifyCharacterCardOpened()
    {
        if (_currentPhase != TutorialPhase.Action_OpenCharacterCard) return;
        EnterPhase(TutorialPhase.Dialog_Hero5);
    }

    public void NotifyHintPostItOpened()
    {
        if (_currentPhase != TutorialPhase.Action_ClickHintPostIt) return;
        EnterPhase(TutorialPhase.Action_CloseHintPostIt);
    }

    public void NotifyHintPostItClosed()
    {
        if (_currentPhase != TutorialPhase.Action_CloseHintPostIt) return;
        EnterPhase(TutorialPhase.Action_CloseCharacterCard);
    }

    public void NotifyCharacterCardClosed()
    {
        if (_currentPhase != TutorialPhase.Action_CloseCharacterCard) return;
        EnterPhase(TutorialPhase.Dialog_Hero6);
    }

    // ── 입력 허가 및 철벽 방어 API ────────────────────────────────────────────

    /// <summary>
    /// 핵심 방어 로직: 현재 튜토리얼 단계에서 '해당 행동'이 허락되었는지 검사합니다.
    /// 튜토리얼 중이 아닐 때(IsActive == false)만 자유 행동을 허용합니다.
    /// </summary>
    public bool IsInputAllowed(TutorialInputPermission permission)
    {
        // 튜토리얼이 끝났거나 비활성 상태면 프리패스!
        if (!IsActive) return true;

        // 현재 단계(_allowedInputs)에 요청한 권한이 없으면 가차 없이 차단(false)
        return (_allowedInputs & permission) != 0;
    }

    /// <summary>
    /// [캐릭터 조작 방어] 해당 캐릭터를 드래그할 수 있는지 반환합니다.
    /// </summary>
    public bool IsCharacterDragAllowed(int characterId)
    {
        if (!IsActive) return true;

        // 1차 방어: 지금이 캐릭터를 움직일 수 있는 타이밍인가? 아니면 차단.
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;

        // 2차 방어: 캐릭터 이동 타이밍이 맞다면, '살인자' 캐릭터가 맞는가?
        // (Action_MoveCharacter 단계에서는 지정된 캐릭터만 만질 수 있음)
        if (_currentPhase == TutorialPhase.Action_MoveCharacter)
            return characterId == _restrictedCharacterId;

        return true;
    }

    /// <summary>
    /// [캐릭터 드롭 방어] 해당 캐릭터를 목표 구역에 놓을 수 있는지 반환합니다.
    /// </summary>
    public bool IsZoneDropAllowed(int characterId, int zoneId)
    {
        if (!IsActive) return true;

        // 1차 방어: 이동 타이밍이 아니면 차단.
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;

        // 2차 방어: 지정된 캐릭터를, 지정된 타겟 구역(하단 구역)에 놓았는가?
        if (_currentPhase == TutorialPhase.Action_MoveCharacter)
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

        if (_guideData.TryGetPhaseGuide(phase, out string text, out Sprite sprite))
        {
            if (!string.IsNullOrEmpty(text))
                _uiManager.ShowGuide(text, sprite);
        }
    }

    private void ShowEventGuide(TutorialEventType eventType)
    {
        if (_guideData == null || _uiManager == null) return;

        if (_guideData.TryGetEventGuide(eventType, out string text, out Sprite sprite))
        {
            if (!string.IsNullOrEmpty(text))
                _uiManager.ShowGuide(text, sprite);
        }
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
