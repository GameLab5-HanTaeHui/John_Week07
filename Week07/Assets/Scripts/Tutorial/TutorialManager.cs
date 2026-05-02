using DG.Tweening;
using HTH.Campaign;
using System.Collections;
using TMPro;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private RewardSaveData _rewardSaveData;

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
    [Header("대화 상자")]
    [SerializeField] private GameObject _DialogueBox;
    [SerializeField] private TMP_Text _dialogueName;
    [SerializeField] private string _envyName;
    [SerializeField] private string _maiName;
    [SerializeField] private Button _iconButton;
    [SerializeField] private Button _finalSelectButton;
    [SerializeField] private Image _fadeOverlay;
    [SerializeField] private TMP_Text _endingtitle;
    [SerializeField] private TMP_Text _campaignModeText;

    // ── 정적 상태 (기존 코드에서 TutorialManager.IsActive 로 체크) ──────────
    /// <summary>TutorialManager 인스턴스가 존재하고 활성화된 경우 true.</summary>
    public static bool IsActive => Instance != null && Instance._currentPhase != TutorialPhase.Inactive;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private TutorialPhase         _currentPhase    = TutorialPhase.Inactive;

    private TutorialInputPermission _allowedInputs = TutorialInputPermission.None;

    private int _closeDiaryStep = 0;

    // 이벤트형 안내 — 최초 1회만 표시
    private bool _shownNormalLoopGuide;
    private bool _shownForceLoopGuide;
    private bool _shownDeadlineGuide;
    private bool _shownFinalDecisionGuide;

    // 이벤트 구독 해제용 캐시
    private PlayerActionState _playerAction;
    private TurnStateMachine  _turnSM;

    // 액션 단계에서 가이드 대화창을 클릭으로 넘겼는지 체크하는 플래그
    private bool _isActionGuideDismissed = false;
    private bool _canExitToLobby = false;
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
        EnterPhase(TutorialPhase.Dialog_EnvyIntro_1);
    }

    private void Update()
    {
        // 튜토리얼 종료 화면이고 마우스 왼쪽 클릭이 발생했을 때
        if (_canExitToLobby && Input.GetMouseButtonDown(0))
        {
            _canExitToLobby = false; // 중복 클릭 방지
            Debug.Log("[Tutorial] 모든 과정 완료. 로비로 이동합니다.");

            // 설정해둔 로비 씬으로 이동
            SceneManager.LoadScene(_lobbySceneName);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeGameEvents();
    }

    // ── 이벤트 구독 ──────────────────────────────────────────────────────────

    private void SubscribeGameEvents()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;

        // ── [1] 게임 흐름 및 상태 관리 ──
        gfc.OnLoopReset += HandleLoopReset;
        gfc.OnFinalDecisionEntered += HandleFinalDecisionEntered; // 최종 추리 방 진입 감지
        gfc.OnGameEnded += HandleGameEnded;

        _turnSM = gfc.GetTurnSM();
        if (_turnSM != null)
        {
            _turnSM.OnTurnEndEntered += HandleTurnEndEntered;      // 턴 종료(깃털펜) 감지
            _turnSM.OnPlayerActionStarted += HandlePlayerActionStarted;
            _turnSM.OnLoopConditionTriggered += HandleLoopConditionTriggered;
        }

        _playerAction = gfc.GetPlayerActionState();
        if (_playerAction != null)
            _playerAction.OnActionConfirmed += HandleActionConfirmed;      // 캐릭터 이동 감지

        // ── [2] 에드먼드의 메모장 (역할 패널) ──
        if (_roleDocDrawer != null)
        {
            _roleDocDrawer.OnShown += HandleRoleDocShown;   // 열기 완료 감지
            _roleDocDrawer.OnHidden += HandleRoleDocHidden; // 닫기 완료 감지
        }

        // ── [3] 엔비의 다이어리 (인물 카드/메모장) ──
        // 시나리오 5단계 조작을 위해 반드시 필요합니다.
        if (_memoBookDrawer != null)
        {
            _memoBookDrawer.OnShown += NotifyCharacterCardOpened; // 다이어리 열림
            _memoBookDrawer.OnHidden += NotifyCharacterCardClosed; // 다이어리 닫힘
        }

        // ── [4] UI 매니저 및 공통 ──
        if (_uiManager != null)
            _uiManager.OnGuideAdvanced += HandleGuideAdvanced; // 대화 넘기기 클릭 감지

        if (_iconButton != null)
            _iconButton.onClick.AddListener(NotifyFinalCharacterSelected);
    }

    private void UnsubscribeGameEvents()
    {
        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnLoopReset -= HandleLoopReset;
            gfc.OnFinalDecisionEntered -= HandleFinalDecisionEntered;
            gfc.OnGameEnded -= HandleGameEnded;
        }

        if (_turnSM != null)
        {
            _turnSM.OnTurnEndEntered -= HandleTurnEndEntered;
            _turnSM.OnPlayerActionStarted -= HandlePlayerActionStarted;
            _turnSM.OnLoopConditionTriggered -= HandleLoopConditionTriggered;
        }

        if (_playerAction != null)
            _playerAction.OnActionConfirmed -= HandleActionConfirmed;

        if (_roleDocDrawer != null)
        {
            _roleDocDrawer.OnShown -= HandleRoleDocShown;
            _roleDocDrawer.OnHidden -= HandleRoleDocHidden;
        }

        // 엔비의 다이어리 구독 해제 추가
        if (_memoBookDrawer != null)
        {
            _memoBookDrawer.OnShown -= NotifyCharacterCardOpened;
            _memoBookDrawer.OnHidden -= NotifyCharacterCardClosed;
        }

        if (_uiManager != null)
            _uiManager.OnGuideAdvanced -= HandleGuideAdvanced;

        if (_iconButton != null)
            _iconButton.onClick.RemoveListener(NotifyFinalCharacterSelected);
    }

    // ── 순서형 단계 전환 ──────────────────────────────────────────────────────
    private void EnterPhase(TutorialPhase phase)
    {
        _currentPhase = phase;
        _uiManager?.ClearAll();

        // 단계 진입 시 기본적으로 화살표는 비활성화
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);
        Debug.Log($"현재 튜토리얼 진행도: {phase}");
        switch (phase)
        {
            case TutorialPhase.WaitIntro:
                SetInputPermission(TutorialInputPermission.None);
            if (_DialogueBox != null) _DialogueBox.SetActive(false);
                break;

            // ── [대화 및 나레이션 단계 공통 처리] ──
            // 모든 Dialog_... 단계들을 여기에 묶어 대화창을 활성화합니다.
            case TutorialPhase.Dialog_EnvyIntro_1:
            case TutorialPhase.Dialog_EnvyIntro_2:
            case TutorialPhase.Dialog_EnvyIntro_3:
            case TutorialPhase.Dialog_EnvyIntro_4:
            case TutorialPhase.Dialog_EnvyIntro_5:
            case TutorialPhase.Dialog_EnvyIntro_6:
            case TutorialPhase.Dialog_EnvyIntro_7:
            case TutorialPhase.Dialog_EnvyIntro_8:
            case TutorialPhase.Dialog_MoveEnvy1:
            case TutorialPhase.Dialog_MoveEnvy2:
            case TutorialPhase.Dialog_EnvyTalk1:
            case TutorialPhase.Dialog_MayTalk_1:
            case TutorialPhase.Dialog_EnvyTalk2:
            case TutorialPhase.Dialog_MayTalk_2:
            case TutorialPhase.Dialog_EnvyTalk3:
            case TutorialPhase.Dialog_MayTalk_3:
            case TutorialPhase.Dialog_EnvyTalk4:
            case TutorialPhase.Dialog_MayTalk_4:
            case TutorialPhase.Dialog_EnvyTalk5:
            case TutorialPhase.Dialog_MayTalk_5:
            case TutorialPhase.Dialog_EnvyTalk6:
            case TutorialPhase.Dialog_MayTalk_6:
            case TutorialPhase.Dialog_PieceTuto:
            case TutorialPhase.Dialog_System_Clue:
            case TutorialPhase.Dialog_EnvyRules1:
            case TutorialPhase.Dialog_EdmundNarration1:
            case TutorialPhase.Dialog_EdmundNarration2:
            case TutorialPhase.Dialog_EdmundNarration3:
            case TutorialPhase.Dialog_EnvyStrategy1:
            case TutorialPhase.Dialog_EnvyStrategy2:
            case TutorialPhase.Dialog_EnvyStrategy3:
            case TutorialPhase.Dialog_DiaryNarration1:
            case TutorialPhase.Dialog_DiaryNarration2:
            case TutorialPhase.Dialog_DiaryNarration3:
            case TutorialPhase.Dialog_DiaryNarration5:
            case TutorialPhase.Dialog_EnvyContext1:
            case TutorialPhase.Dialog_EnvyContext2:
            case TutorialPhase.Dialog_FinalIntro1:
            case TutorialPhase.Dialog_FinalIntro2:
            case TutorialPhase.Dialog_EnterFinalDecision1:
            case TutorialPhase.Dialog_EnterFinalDecision2:
            case TutorialPhase.Dialog_EnterFinalDecision3:
            case TutorialPhase.Dialog_FinalResult:
            case TutorialPhase.Dialog_EnvyOutro1:
            case TutorialPhase.Dialog_EnvyOutro2:
            case TutorialPhase.Dialog_EnvyOutro3:
            case TutorialPhase.Dialog_EnvyOutro4:
            case TutorialPhase.Dialog_EnvyOutro5:
                if (_DialogueBox != null) _DialogueBox.SetActive(true);
                if (_TutoArrow.gameObject.activeSelf) _TutoArrow.gameObject.SetActive(false);
                SetInputPermission(TutorialInputPermission.DialogueAdvance);
                _uiManager?.SetClickAdvance(true);
                ShowPhaseGuide(phase);
            break;

            // ── [조작 단계: 개별 설정] ──

            // 1. 엔비 이동
            case TutorialPhase.Action_MoveEnvy:
                StartCoroutine(ActionWithGuideCoroutine(phase,TutorialInputPermission.CharacterMove,() =>
                {
                    // 이 괄호 안의 코드는 유저가 대화창을 클릭해 닫은 "직후"에 실행됩니다!
                    if (_heroCharacterTransform != null) _uiManager?.SetBounceOnly(_heroCharacterTransform, loop: true);
                    if (_restrictedZoneTransform != null) _uiManager?.SetSecondaryBounce(_restrictedZoneTransform);
                }
                ));
                break;

            // 2. 깃털펜 클릭
            case TutorialPhase.Action_TurnEnd_Quill:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.AdvanceTurn, () =>
                {
                    if (_quillPenTransform != null) _uiManager?.SetWorldHighlight(_quillPenTransform);
                    TutoArrowPos(420f, 210f, 0f, 0f, 0f, 0f);
                }
                ));
                break;

            // 3. 에드먼드의 메모장 열기
            case TutorialPhase.Action_OpenEdmundNote:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.RoleDocToggle, () =>
                {
                    SetDrawerInteractable(_roleDocGroup, true);
                    TutoArrowPos(-680f, 270f, 0f, 0f, 0f, -180f); // 화살표: 왼쪽 패널 가리키기
                }));
                break;

            // 4. 에드먼드의 메모장 닫기
            case TutorialPhase.Action_CloseEdmundNote:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.RoleDocToggle, () =>
                {
                    TutoArrowPos(-220f, 270f, 0f, 0f, 0f, -180f); // 화살표: 닫기 위해 반대 방향
                }));
                break;

            // 5. 엔비의 다이어리 열기
            case TutorialPhase.Action_OpenEnvyDiary:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.CharacterCardToggle, () =>
                {
                    SetDrawerInteractable(_memoBookGroup, true);
                    TutoArrowPos(-465f, -225f, 0f, 0f, 0f, -90f); // 화살표: 우측 하단 탭
                }));
                break;

            // 6. 힌트 포스트잇 열기
            case TutorialPhase.Action_OpenHintPostIt:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.HintPostItToggle, () =>
                {
                    TutoArrowPos(95f, 180f, 0f, 0f, 0f, -180f); // 화살표: 중앙 메모지
                }));
                break;

            // 7. 다이어리 및 포스트잇 닫기
            case TutorialPhase.Action_CloseEnvyDiary:
                _closeDiaryStep = 0;
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.HintPostItToggle, () =>
                {
                    if(_closeDiaryStep == 0)
                        TutoArrowPos(350f, 150f, 0f, 0f, 0f, -150f); // 화살표: 닫기 버튼/탭
                }));
                break;


            // 8. 최종 추리 진입 (책 클릭)
            case TutorialPhase.Action_EnterFinalDecision1:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.FinalDecisionEnter, () =>
                {
                    if (_finalDecisionBookTransform != null) _uiManager?.SetWorldHighlight(_finalDecisionBookTransform);
                    TutoArrowPos(550f, 130f, 0f, 0f, 0f, -15f); // 화살표: 우측 책
                }));
                break;

            // 9. 엔비 선택 (인물 선택)
            case TutorialPhase.Action_EnterFinalDecision2:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.FinalCharacterSelect, () =>
                {
                    _iconButton.gameObject.SetActive(true);
                    TutoArrowPos(-300f, 150f, 0f, 0f, 0f, 0f); // 화살표: 엔비 아이콘
                }));
                break;

            // 10. 질문 선택
            case TutorialPhase.Action_SelectFinalOption:
                StartCoroutine(ActionWithGuideCoroutine(phase, TutorialInputPermission.FinalDecisionSelect, () =>
                {
                    _finalSelectButton.interactable = true;
                    TutoArrowPos(750f, 200f, 0f, 0f, 0f, -180f); // 화살표: 1번 질문 위치 (필요시 좌표 수정)
                }));
                break;

            case TutorialPhase.Tutorial_End:
                EnterPhase(TutorialPhase.Inactive);
                break;
        }
    }

    // 매개변수 맨 끝에 float preDelay = 0f 를 추가합니다. (기본값 0)
    private IEnumerator ActionWithGuideCoroutine(TutorialPhase phase, TutorialInputPermission actionPerm, System.Action onDismissedHighlight, float preDelay = 0f)
    {
        if (_TutoArrow.gameObject.activeSelf) _TutoArrow.gameObject.SetActive(false);
        // 0. 선행 대기 (검은 화면이 밝아지는 시간 대기)
        if (preDelay > 0f)
        {
            // 대기하는 동안 유저가 아무것도 누를 수 없도록 권한을 압수합니다!
            SetInputPermission(TutorialInputPermission.None);
            if (_DialogueBox != null) _DialogueBox.SetActive(false);

            yield return new WaitForSeconds(preDelay);
        }

        _isActionGuideDismissed = false;

        // 1. 대화창 켜기 및 넘기기 권한 부여
        if (_DialogueBox != null) _DialogueBox.SetActive(true);
        SetInputPermission(TutorialInputPermission.DialogueAdvance);
        _uiManager?.SetClickAdvance(true);
        ShowPhaseGuide(phase);

        // 2. 유저가 화면을 클릭할 때까지 무한 대기
        yield return new WaitUntil(() => _isActionGuideDismissed);

        // 3. 클릭 완료! 대화창을 끄고 진짜 조작 권한으로 교체
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        SetInputPermission(actionPerm);
        _uiManager?.SetClickAdvance(false);

        // 4. 하이라이트 등 연출 콜백
        onDismissedHighlight?.Invoke();
    }
    /// <summary>
    /// 특정 시간 대기 후 대화 상자를 표시하는 코루틴입니다.
    /// </summary>
    private IEnumerator DelayedDialogueCoroutine(TutorialPhase nextPhase, float delay)
    {
        // 1. 대기하는 동안 튜토리얼 입력을 철저히 차단 (시스템 클릭과 겹침 방지)
        SetInputPermission(TutorialInputPermission.None);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        _uiManager?.SetClickAdvance(false);

        // 2. 화면이 밝아진 후 지정된 시간(1초) 대기
        yield return new WaitForSeconds(delay);

        // 3. 대기가 끝나면 안전하게 다음 대화 페이즈로 진입
        EnterPhase(nextPhase);
    }

    // ── 예비 순서형 단계 전환 ──────────────────────────────────────────────────────

    //private void EnterPhase(TutorialPhase phase)
    //{
    //    _currentPhase = phase;
    //    _uiManager?.ClearAll();

    //    // 기본적으로 새로운 단계 진입 시 화살표는 끄고 시작 (필요한 Action 단계에서만 켬)
    //    if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

    //    switch (phase)
    //    {
    //        // 검은 화면 대기 단계
    //        case TutorialPhase.WaitIntro:
    //            // IsActive는 true가 되지만, 권한이 None이므로 
    //            // 최종 추리 책, 턴 넘기기, 캐릭터 이동 등 모든 입력이 완벽히 차단됩니다!
    //            SetInputPermission(TutorialInputPermission.None);
    //            _uiManager?.SetClickAdvance(false);
    //            _uiManager?.HideGuide();
    //            if (_DialogueBox != null) _DialogueBox.SetActive(false);
    //            break;
    //        // ── [클릭으로 대화만 넘기는 단계들 묶음] ──
    //        case TutorialPhase.Dialog_Intro1:               // 이곳에서 대량 사건이...
    //        case TutorialPhase.Dialog_Intro2:               // 저쪽 구역에 사람이 있네?...
    //        case TutorialPhase.Dialog_PostMove:             // 좋아 대화 해봐야지...
    //        case TutorialPhase.Dialog_Npc1:                 // 너는 누구야?
    //        case TutorialPhase.Dialog_Hero1:                // 나는 (주인공)이고...
    //        case TutorialPhase.Dialog_Npc2:                 // 사건을 추리하는 사람이야?
    //        case TutorialPhase.Dialog_Npc3:                 // 우릴 도와줘!
    //        case TutorialPhase.Dialog_Npc4:                 // 여태껏 정보들이 있는데...
    //        case TutorialPhase.Dialog_SystemGuidePanels:    // 왼쪽 사건 기록지, 아래...
    //        case TutorialPhase.Dialog_Hero2:                // 살인자..? 기록해봐야겠어.
    //        case TutorialPhase.Dialog_Hero3:                // 사건 기록지..?
    //        case TutorialPhase.Dialog_Hero4:                // 이건 역할을 기록하는 수첩이구나.
    //        case TutorialPhase.Dialog_Hero5:                // 이건 상세 정보구나 힌트도...
    //        case TutorialPhase.Dialog_Hero6:                // 좋아 사건 추리가 끝나면...
    //            // 오직 대화 텍스트 넘기기(화면 클릭) 권한만 부여
    //            if (_DialogueBox != null && !_DialogueBox.activeSelf)
    //                _DialogueBox.SetActive(true);

    //            SetInputPermission(TutorialInputPermission.DialogueAdvance);
    //            _uiManager?.SetClickAdvance(true);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //                _TutoArrow.gameObject.SetActive(false);

    //                ShowPhaseGuide(phase);
    //            break;

    //        // ── [플레이어의 실제 조작이 필요한 단계들] ──
    //        case TutorialPhase.Action_MoveCharacter:        // 캐릭터 이동
    //            if (_DialogueBox != null) _DialogueBox.SetActive(false);
    //            SetInputPermission(TutorialInputPermission.CharacterMove);
    //            _uiManager?.SetClickAdvance(false); // 클릭 대화 넘기기 차단!
    //            if (_heroCharacterTransform != null) _uiManager?.SetBounceOnly(_heroCharacterTransform, loop: true);
    //            if (_restrictedZoneTransform != null) _uiManager?.SetSecondaryBounce(_restrictedZoneTransform);
    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_TurnEnd:              // 턴 종료 (깃털펜)
    //            if (_DialogueBox != null) _DialogueBox.SetActive(false);
    //            SetInputPermission(TutorialInputPermission.AdvanceTurn);
    //            _uiManager?.SetClickAdvance(false);
    //            if (_quillPenTransform != null) _uiManager?.SetWorldHighlight(_quillPenTransform);

    //            // 화살표 활성화 및 위치 조정
    //            if (_TutoArrow != null)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(415f, 200f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, 0f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_OpenRoleDoc:          // 역할 패널 열기
    //        case TutorialPhase.Action_CloseRoleDoc:         // 역할 패널 닫기
    //            if (_DialogueBox != null) _DialogueBox.SetActive(false);
    //            SetInputPermission(TutorialInputPermission.RoleDocToggle);
    //            _uiManager?.SetClickAdvance(false);
    //            SetDrawerInteractable(_roleDocGroup, true);

    //            if (_TutoArrow != null)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(-680f, 270f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_OpenHistory:          // 사건 기록지 열기
    //        case TutorialPhase.Action_CloseHistory:         // 사건 기록지 닫기
    //            if (_DialogueBox != null) _DialogueBox.SetActive(false);
    //            SetInputPermission(TutorialInputPermission.HistoryToggle);
    //            _uiManager?.SetClickAdvance(false);

    //            if (_TutoArrow != null)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(550f, 130f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -15f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_OpenMemo:             // 메모 수첩 열기
    //        case TutorialPhase.Action_CloseMemo:            // 메모 수첩 닫기
    //            SetInputPermission(TutorialInputPermission.MemoToggle);
    //            SetDrawerInteractable(_memoBookGroup, true);
    //            _uiManager?.SetClickAdvance(false);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(690f, -290f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_WriteMemo:            // 메모 격자칸 작성
    //            // 메모 작성이 가능하도록 메모 토글+작성 권한 동시 부여
    //            SetInputPermission(TutorialInputPermission.MemoToggle | TutorialInputPermission.MemoWrite);
    //            _uiManager?.SetClickAdvance(false);
    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_ClickSwapButton:      // 인물 카드 보기 버튼 클릭
    //            SetInputPermission(TutorialInputPermission.HistorySwapButton);
    //            _uiManager?.SetClickAdvance(false);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(270f, -350f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_OpenCharacterCard:    // 인물 카드 열기
    //        case TutorialPhase.Action_CloseCharacterCard:   // 인물 카드 닫기
    //            SetInputPermission(TutorialInputPermission.CharacterCardToggle);
    //            _uiManager?.SetClickAdvance(false);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(-60f, -390f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_ClickHintPostIt:      // 힌트 포스트잇 열기
    //        case TutorialPhase.Action_CloseHintPostIt:      // 힌트 포스트잇 닫기
    //            SetInputPermission(TutorialInputPermission.HintPostItToggle);
    //            _uiManager?.SetClickAdvance(false);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(-80f, -245f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -180f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;

    //        case TutorialPhase.Action_FinalDecision:        // 최종 집필 책 클릭
    //            SetInputPermission(TutorialInputPermission.FinalDecision);
    //            _uiManager?.SetClickAdvance(false);
    //            if (_finalDecisionBookTransform != null) _uiManager?.SetWorldHighlight(_finalDecisionBookTransform);

    //            if (!_TutoArrow.gameObject.activeSelf)
    //            {
    //                _TutoArrow.gameObject.SetActive(true);
    //                _TutoArrow.localPosition = new Vector3(550f, 130f, 0f);
    //                _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, -15f);
    //                _uiManager?.SetSecondaryBounce(_TutoArrow);
    //            }

    //            ShowPhaseGuide(phase);
    //            break;
    //    }
    //}

    // ── 이벤트형 안내 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 튜토리얼 화살표 위치 설정기 입니다.
    /// </summary>
    private void TutoArrowPos(float pX, float pY, float pZ, float rX, float rY, float rZ)
    {
        _TutoArrow.gameObject.SetActive(true);
        _TutoArrow.localPosition = new Vector3(pX, pY, pZ);
        _TutoArrow.localRotation = Quaternion.Euler(rX, rY, rZ);
        _uiManager?.SetSecondaryBounce(_TutoArrow);

        // 바운스 애니메이션도 새로운 위치에서 다시 재생
        _uiManager?.SetSecondaryBounce(_TutoArrow);
    }

    /// <summary>
    /// 대화 패널(화면)을 클릭했을 때 다음 대화나 행동으로 넘어가는 로직입니다.
    /// </summary>
    private void HandleGuideAdvanced()
    {
        // 대화 넘기기 권한이 없는 상태(조작 단계 등)라면 클릭을 무시합니다.[cite: 3]
        if (!IsInputAllowed(TutorialInputPermission.DialogueAdvance)) return;

        // 만약 Action_ (조작 단계)로 이름이 시작하는데 대화 넘기기 클릭이 들어왔다면?
        if (_currentPhase.ToString().StartsWith("Action_"))
        {
            _isActionGuideDismissed = true; // 코루틴의 대기 상태를 풀어줍니다!
            return; // 여기서 바로 종료. 밑의 스위치문을 타지 않음.
        }

        switch (_currentPhase)
        {
            // ── 1. 도입부: 엔비의 독백 (1~8) ──
            case TutorialPhase.Dialog_EnvyIntro_1: EnterPhase(TutorialPhase.Dialog_EnvyIntro_2); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyIntro_2: EnterPhase(TutorialPhase.Dialog_EnvyIntro_3); break;
            case TutorialPhase.Dialog_EnvyIntro_3: EnterPhase(TutorialPhase.Dialog_EnvyIntro_4); break;
            case TutorialPhase.Dialog_EnvyIntro_4: EnterPhase(TutorialPhase.Dialog_EnvyIntro_5); break;
            case TutorialPhase.Dialog_EnvyIntro_5: EnterPhase(TutorialPhase.Dialog_EnvyIntro_6); break;
            case TutorialPhase.Dialog_EnvyIntro_6: EnterPhase(TutorialPhase.Dialog_EnvyIntro_7); break;
            case TutorialPhase.Dialog_EnvyIntro_7: EnterPhase(TutorialPhase.Dialog_EnvyIntro_8); break;
            case TutorialPhase.Dialog_EnvyIntro_8: EnterPhase(TutorialPhase.Dialog_MoveEnvy1); break;

            // ── 2. 첫 이동 안내 ──
            case TutorialPhase.Dialog_MoveEnvy1: EnterPhase(TutorialPhase.Dialog_MoveEnvy2); break;
            case TutorialPhase.Dialog_MoveEnvy2: EnterPhase(TutorialPhase.Action_MoveEnvy); _dialogueName.text = "튜토리얼 가이드"; break; // 조작 단계로 진입

            // ── 3. 첫 대화: 메이 (엔비와 메이의 대화 릴레이 1~6) ──
            case TutorialPhase.Dialog_EnvyTalk1: EnterPhase(TutorialPhase.Dialog_MayTalk_1); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_1: EnterPhase(TutorialPhase.Dialog_EnvyTalk2); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyTalk2: EnterPhase(TutorialPhase.Dialog_MayTalk_2); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_2: EnterPhase(TutorialPhase.Dialog_EnvyTalk3); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyTalk3: EnterPhase(TutorialPhase.Dialog_MayTalk_3); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_3: EnterPhase(TutorialPhase.Dialog_EnvyTalk4); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyTalk4: EnterPhase(TutorialPhase.Dialog_MayTalk_4); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_4: EnterPhase(TutorialPhase.Dialog_EnvyTalk5); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyTalk5: EnterPhase(TutorialPhase.Dialog_MayTalk_5); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_5: EnterPhase(TutorialPhase.Dialog_EnvyTalk6); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyTalk6: EnterPhase(TutorialPhase.Dialog_MayTalk_6); _dialogueName.text = _maiName; break;
            case TutorialPhase.Dialog_MayTalk_6: EnterPhase(TutorialPhase.Dialog_PieceTuto); _dialogueName.text = ""; break;

            // ── 3-1. 시스템 및 단서 안내 ──
            case TutorialPhase.Dialog_PieceTuto: EnterPhase(TutorialPhase.Dialog_System_Clue); _dialogueName.text = "튜토리얼 가이드"; break;
            case TutorialPhase.Dialog_System_Clue: EnterPhase(TutorialPhase.Dialog_EnvyRules1); _dialogueName.text = _envyName; break;

            // ── 4. 에드먼드의 메모장 안내 ──
            case TutorialPhase.Dialog_EnvyRules1: EnterPhase(TutorialPhase.Action_OpenEdmundNote); _dialogueName.text = "튜토리얼 가이드"; break; // 조작(열기)
            case TutorialPhase.Dialog_EdmundNarration1: EnterPhase(TutorialPhase.Dialog_EdmundNarration2); break;
            case TutorialPhase.Dialog_EdmundNarration2: EnterPhase(TutorialPhase.Dialog_EdmundNarration3); _dialogueName.text = "튜토리얼 가이드"; break;
            case TutorialPhase.Dialog_EdmundNarration3: EnterPhase(TutorialPhase.Action_CloseEdmundNote); break; // 조작(닫기)

            // ── 5. 엔비의 다이어리 안내 ──
            case TutorialPhase.Dialog_EnvyStrategy1: EnterPhase(TutorialPhase.Dialog_EnvyStrategy2); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyStrategy2: EnterPhase(TutorialPhase.Dialog_EnvyStrategy3); break;
            case TutorialPhase.Dialog_EnvyStrategy3: EnterPhase(TutorialPhase.Action_OpenEnvyDiary); _dialogueName.text = "튜토리얼 가이드"; break; // 조작(열기)
            case TutorialPhase.Dialog_DiaryNarration1: EnterPhase(TutorialPhase.Dialog_DiaryNarration2); break;
            case TutorialPhase.Dialog_DiaryNarration2: EnterPhase(TutorialPhase.Dialog_DiaryNarration3); break;
            case TutorialPhase.Dialog_DiaryNarration3: EnterPhase(TutorialPhase.Action_OpenHintPostIt); break; // 조작(힌트)
            case TutorialPhase.Dialog_DiaryNarration5: EnterPhase(TutorialPhase.Dialog_EnvyContext1); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyContext1: EnterPhase(TutorialPhase.Dialog_EnvyContext2); break;
            case TutorialPhase.Dialog_EnvyContext2: EnterPhase(TutorialPhase.Action_CloseEnvyDiary); _dialogueName.text = "튜토리얼 가이드"; break; // 조작(닫기)

            // ── 6. 최종 대화 안내 및 선택 ──
            case TutorialPhase.Dialog_FinalIntro1: EnterPhase(TutorialPhase.Action_EnterFinalDecision1); _dialogueName.text = "튜토리얼 가이드"; break;
            case TutorialPhase.Dialog_EnterFinalDecision1: EnterPhase(TutorialPhase.Dialog_EnterFinalDecision2); _dialogueName.text = "튜토리얼 가이드"; break;
            case TutorialPhase.Dialog_EnterFinalDecision2: EnterPhase(TutorialPhase.Dialog_FinalIntro2); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_FinalIntro2: EnterPhase(TutorialPhase.Dialog_EnterFinalDecision3); _dialogueName.text = "튜토리얼 가이드"; break;
            case TutorialPhase.Dialog_EnterFinalDecision3: EnterPhase(TutorialPhase.Action_SelectFinalOption); break; // 조작(선택)
            case TutorialPhase.Dialog_FinalResult: EnterPhase(TutorialPhase.Dialog_EnvyOutro1); _dialogueName.text = _envyName; break;
            case TutorialPhase.Dialog_EnvyOutro1: EnterPhase(TutorialPhase.Dialog_EnvyOutro2); break;
            case TutorialPhase.Dialog_EnvyOutro2: EnterPhase(TutorialPhase.Dialog_EnvyOutro3); break;
            case TutorialPhase.Dialog_EnvyOutro3: EnterPhase(TutorialPhase.Dialog_EnvyOutro4); break;
            case TutorialPhase.Dialog_EnvyOutro4: EnterPhase(TutorialPhase.Dialog_EnvyOutro5); break;

            // ── 7. 종료 ──
            case TutorialPhase.Dialog_EnvyOutro5:
                // 1. 모든 UI와 대화창 정리
                SetInputPermission(TutorialInputPermission.None);
                _uiManager?.ClearAll();
                if (_DialogueBox != null) _DialogueBox.SetActive(false);
                if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

                // 2. 검은 화면(Fade)을 즉시 완전 불투명하게 설정
                if (_fadeOverlay != null)
                {
                    _fadeOverlay.gameObject.SetActive(true);
                    _fadeOverlay.color = new Color(0, 0, 0, 1f); // 검정색, 알파 1
                }

                // 3. 엔딩 텍스트 설정 및 활성화
                if (_endingtitle != null)
                {
                    _endingtitle.text = "튜토리얼 완료";
                    _endingtitle.gameObject.SetActive(true);
                }
                if (_campaignModeText != null)
                {
                    _campaignModeText.text = "캠패인 모드가 열렸습니다";
                    _campaignModeText.gameObject.SetActive(true);
                }

                // 4. 세이브 데이터에 클리어 기록 쾅!
                if (TutorialProgressRepository.Instance != null)
                {
                    TutorialProgressRepository.Instance.MarkCleared();
                    Debug.Log("[Tutorial] 튜토리얼 클리어 데이터를 저장했습니다.");
                }
                if (_rewardSaveData != null)
                    _rewardSaveData.SaveTutorialClear();

                // 4. 로비로 나갈 수 있는 상태로 전환
                _canExitToLobby = true;
                break;
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

    private void HandlePlayerActionStarted()
    {
        // 시스템 검은 화면 연출이 완벽히 끝나고 화면이 밝아진 시점입니다.
        if (_currentPhase == TutorialPhase.Action_TurnEnd_Quill)
        {
            // 1초 뒤에 Dialog_EnvyTalk1 로 진입!
            StartCoroutine(DelayedDialogueCoroutine(TutorialPhase.Dialog_EnvyTalk1, 1.0f));
            _dialogueName.text = _envyName;
        }

        // 현재 튜토리얼이 '엔비 이동' 단계라면
        if (_currentPhase == TutorialPhase.Action_MoveEnvy)
        {
            // 엔비 주변에 월드 하이라이트 이펙트를 켬
            if (_heroCharacterTransform != null)
                _uiManager?.SetWorldHighlight(_heroCharacterTransform);
        }
    }

    // ── 순서형 이벤트 핸들러 (UI 상호작용 및 게임 이벤트 감지) ────────────

    // 1. [캐릭터 이동 완료] -> 깃털펜 조작 단계로
    private void HandleActionConfirmed(int characterId, int targetZoneId)
    {
        if (_currentPhase != TutorialPhase.Action_MoveEnvy) return;

        // 지정된 캐릭터(엔비)와 구역(Zone)이 맞는지 확인
        if (characterId != _restrictedCharacterId || targetZoneId != _restrictedTargetZoneId) return;

        EnterPhase(TutorialPhase.Action_TurnEnd_Quill);
    }

    // 2. [턴 종료(깃털펜) 완료] -> 엔비의 첫 대사로 연결
    private void HandleTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
    {
        // 턴 종료 연출(시스템 대사)이 시작되면 튜토리얼은 입력을 닫고 숨습니다.
        if (_currentPhase == TutorialPhase.Action_TurnEnd_Quill)
        {
            SetInputPermission(TutorialInputPermission.None);
            if (_DialogueBox != null) _DialogueBox.SetActive(false);
            if(_TutoArrow.gameObject.activeSelf) _TutoArrow.gameObject.SetActive(false);
            _uiManager?.SetClickAdvance(false);
        }

        // 데드라인(마지막 루프) 안내는 시나리오와 별개로 체크 (기존 유지)
        var gfc = GameFlowController.Instance;
        if (gfc != null && gfc.LoopCount >= LoopStateMachine.MaxLoops - 1 && !_shownDeadlineGuide)
        {
            _shownDeadlineGuide = true;
            ShowEventGuide(TutorialEventType.Deadline);
        }
    }

    // 3. [에드먼드의 메모장(역할 패널) 열림] -> 나레이션 시작
    private void HandleRoleDocShown()
    {
        if (_currentPhase != TutorialPhase.Action_OpenEdmundNote) return;
        EnterPhase(TutorialPhase.Dialog_EdmundNarration1);
    }

    // 4. [에드먼드의 메모장(역할 패널) 닫힘] -> 엔비의 전략 대사로
    private void HandleRoleDocHidden()
    {
        if (_currentPhase != TutorialPhase.Action_CloseEdmundNote) return;
        EnterPhase(TutorialPhase.Dialog_EnvyStrategy1);
        _dialogueName.text = _envyName;
    }

    // 5. [엔비의 다이어리(캐릭터 정보) 열림] -> 나레이션 시작
    public void NotifyCharacterCardOpened()
    {
        if (_currentPhase != TutorialPhase.Action_OpenEnvyDiary) return;
        EnterPhase(TutorialPhase.Dialog_DiaryNarration1);
    }

    // 6. [힌트 포스트잇 열림] -> 힌트 안내 나레이션으로
    public void NotifyHintPostItOpened()
    {
        if (_currentPhase != TutorialPhase.Action_OpenHintPostIt) return;
        EnterPhase(TutorialPhase.Dialog_DiaryNarration5);
    }

    // 7-1단계 포스트잇 닫기 완료 시 호출됨
    public void NotifyHintPostItClosed()
    {
        _closeDiaryStep = 1;
        // 2단계: 포스트잇 권한은 뺏고, 다이어리 조작 권한을 부여합니다!
        SetInputPermission(TutorialInputPermission.CharacterCardToggle);

        // 화살표를 다이어리 닫기 버튼 쪽으로 순간이동 시킵니다.
        TutoArrowPos(-310f, 400f, 0f, 0f, 0f, -210f);
    }

    // 7-2. [다이어리 및 포스트잇 닫힘] -> 최종 추리 도입부로
    public void NotifyCharacterCardClosed()
    {
        if (_currentPhase != TutorialPhase.Action_CloseEnvyDiary) return;
        EnterPhase(TutorialPhase.Dialog_FinalIntro1);
        _dialogueName.text = _envyName;
    }

    // 8. [최종 추리 진입 - 책 클릭 완료] -> 방 안에서의 나레이션으로
    public void HandleFinalDecisionEntered()
    {
        if (_currentPhase == TutorialPhase.Action_EnterFinalDecision1)
        {
            EnterPhase(TutorialPhase.Action_EnterFinalDecision2);
        }
    }

    // 9. [최종 추리 - 인물(엔비) 선택 완료] -> 엔비의 독백으로
    public void NotifyFinalCharacterSelected()
    {
        if (_currentPhase != TutorialPhase.Action_EnterFinalDecision2) return;
        EnterPhase(TutorialPhase.Dialog_EnterFinalDecision1);
    }

    // 10. [최종 추리 - 선택지 클릭 완료] -> 결과 대사로
    public void NotifyFinalDecisionSelected(int optionIndex)
    {
        if (_currentPhase != TutorialPhase.Action_SelectFinalOption) return;

        // 시나리오 상 1번 선택지를 고른 상황 (정답)
        if (optionIndex == 1)
        {
            Debug.Log("[Tutorial] 정답 선택 완료! 화살표를 확정 버튼으로 이동합니다.");
            TutoArrowPos(350f, -250f, 0f, 0f, 0f, -180f);
        }
    }

    public void NotifyFinalSubmit()
    {
        if (_currentPhase != TutorialPhase.Action_SelectFinalOption) return;

        // 암전 및 대사 전환 연출 코루틴 시작!
        StartCoroutine(FinalTransitionCoroutine());
    }

    private IEnumerator FinalTransitionCoroutine()
    {
        // 1. 모든 조작 권한 압수 (유저가 다른 걸 못 누르게)
        SetInputPermission(TutorialInputPermission.None);
        _uiManager?.SetClickAdvance(false);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

        // 2. 검은 화면 연출 (Fade to Black)
        if (_fadeOverlay != null)
        {
            _fadeOverlay.gameObject.SetActive(true); // 이미지를 활성화

            // 알파값을 0(투명)에서 시작하게 설정
            Color c = _fadeOverlay.color;
            c.a = 0f;
            _fadeOverlay.color = c;

            // DOTween을 사용하여 1.5초 동안 알파값을 1(불투명 검정)로 변경
            _fadeOverlay.DOFade(1f, 1.5f).SetEase(Ease.InQuad);
        }

        // 2. 검은 화면 연출 실행 (UI 매니저나 페이드 매니저 활용)
        // 만약 게임에 이미 화면을 까맣게 만드는 함수가 있다면 여기에 넣어주세요!
        // 예: FadeManager.Instance.FadeToBlack(1f);

        // 3. 완전히 까매질 때까지 잠시 대기 (예: 1.5초)
        yield return new WaitForSeconds(1.5f);

        // 4. 까만 화면 상태에서 엔비의 독백 대사 진입!
        // 여기서 "...내가 스파이라는 걸 들킨다면 실패야..." 대사가 출력됩니다.
        EnterPhase(TutorialPhase.Dialog_FinalResult);
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
        if (_currentPhase == TutorialPhase.Action_MoveEnvy)
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
        if (_currentPhase == TutorialPhase.Action_MoveEnvy)
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
