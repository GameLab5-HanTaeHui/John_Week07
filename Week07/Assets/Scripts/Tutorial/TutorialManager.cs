using DG.Tweening;
using HTH.Campaign;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 흐름 전체를 관리하는 싱글턴입니다.
///
/// ─── SO 기반 설계 ────────────────────────────────────────────────────────
///   모든 Phase 설정(텍스트/화자/권한/화살표)은 TutorialPhaseTableSO에 있습니다.
///   TutorialManager는 SO를 읽어서 실행만 합니다. switch-case 없음.
///
/// ─── Phase 처리 흐름 ─────────────────────────────────────────────────────
///   EnterPhase(phase)
///     → SO에서 TutorialPhaseConfig 읽기
///     → Dialogues[]가 있으면 첫 줄부터 출력 시작
///     → 마지막 줄 클릭 후 → PhaseType 분기
///         Dialogue → NextPhase 자동 진입
///         Action   → ActionPermission 부여 + 화살표
///     → 조작 완료(Notify*) → NextPhaseOnAction 진입
///
/// ─── 화자 ────────────────────────────────────────────────────────────────
///   TutorialPhaseTableSO.SpeakerSprites / SpeakerNames 배열
///   None(0)=나레이션 / Envy(1)=엔비 / May(2)=메이
///
/// ─── 외부 코드 수정 목록 ─────────────────────────────────────────────────
///   PlayerTurnInputHandler  : IsCharacterDragAllowed / IsZoneDropAllowed 체크
///   HoldToAdvanceTurn       : IsInputAllowed(AdvanceTurn) 체크
///   HoldToEnterFinalDecision: IsInputAllowed(FinalDecisionEnter) 체크
///   DrawerPanel             : OnShown / OnHidden 이벤트
///   TutorialHintPanel       : NotifyHintPostItOpened / NotifyHintPostItClosed
///   Tuto_PinnedHintPanel    : NotifyHintPinned
/// </summary>
[DisallowMultipleComponent]
public class TutorialManager : SingletonMonobehaviour<TutorialManager>
{
    // ── Inspector — 데이터 ──────────────────────────────────────────────────

    [Header("데이터")]
    [Tooltip("Phase별 설정 SO. 모든 텍스트/화자/권한/화살표가 여기 있습니다.")]
    [SerializeField] private TutorialPhaseTableSO _phaseTable;

    [Tooltip("이벤트 안내(퇴고 등) 텍스트 SO")]
    [SerializeField] private TutorialGuideData _guideData;

    [SerializeField] private RewardSaveData _rewardSaveData;

    // ── Inspector — UI ──────────────────────────────────────────────────────

    [Header("UI 매니저")]
    [SerializeField] private TutorialUIManager _uiManager;

    [Header("대화창")]
    [Tooltip("대화창 루트 GameObject")]
    [SerializeField] private GameObject _DialogueBox;

    [Tooltip("화자 이름 TMP")]
    [SerializeField] private TMP_Text _dialogueName;

    [Tooltip("화자 이미지 Image")]
    [SerializeField] private Image _dialogueSpeakerImage;

    [Header("기타 UI")]
    [Tooltip("화살표 Transform")]
    [SerializeField] private Transform _TutoArrow;

    [Tooltip("최종 추리 캐릭터 선택 버튼")]
    [SerializeField] private Button _iconButton;

    [Tooltip("최종 추리 확정 버튼")]
    [SerializeField] private Button _finalSelectButton;

    [Tooltip("페이드 오버레이")]
    [SerializeField] private Image _fadeOverlay;

    [Tooltip("엔딩 타이틀 TMP")]
    [SerializeField] private TMP_Text _endingtitle;

    [Tooltip("캠페인 모드 안내 TMP")]
    [SerializeField] private TMP_Text _campaignModeText;

    // ── Inspector — 튜토리얼 고정 설정 ─────────────────────────────────────

    [Header("고정 설정")]
    [Tooltip("튜토리얼에서 이동 가능한 캐릭터 ID (엔비)")]
    [SerializeField] private int _restrictedCharacterId = 4;

    [Tooltip("튜토리얼에서 허용되는 목표 구역 ID")]
    [SerializeField] private int _restrictedTargetZoneId = 2;

    [SerializeField] private string _lobbySceneName = "LobbyScene";

    // ── Inspector — DrawerPanel ─────────────────────────────────────────────

    [Header("DrawerPanel CanvasGroup")]
    [SerializeField] private CanvasGroup _roleDocGroup;
    [SerializeField] private CanvasGroup _memoBookGroup;

    [Header("DrawerPanel (이벤트 구독)")]
    [SerializeField] private DrawerPanel _roleDocDrawer;
    [SerializeField] private DrawerPanel _memoBookDrawer;

    // ── Inspector — 하이라이트 ──────────────────────────────────────────────

    [Header("하이라이트 Transform")]
    [SerializeField] private Transform _restrictedZoneTransform;
    [SerializeField] private Transform _quillPenTransform;
    [SerializeField] private Transform _finalDecisionBookTransform;
    [SerializeField] private Transform _heroCharacterTransform;

    [Header("기타 참조")]
    [SerializeField] private NotepadToggleManager _notepadToggleManager;

    // ── 내부 상태 ────────────────────────────────────────────────────────────

    private TutorialPhase _currentPhase = TutorialPhase.Inactive;
    private TutorialInputPermission _allowedInputs = TutorialInputPermission.None;

    private int _currentDialogueIndex;
    private bool _isPlayingDialogue;
    private bool _canExitToLobby;

    private bool _shownNormalLoopGuide;
    private bool _shownForceLoopGuide;
    private bool _shownDeadlineGuide;

    private PlayerActionState _playerAction;
    private TurnStateMachine _turnSM;

    // ── 공개 상태 ────────────────────────────────────────────────────────────

    public static bool IsActive
        => Instance != null && Instance._currentPhase != TutorialPhase.Inactive;

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

        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

        // 엔비 캐릭터 Transform 자동 탐색
        if (_heroCharacterTransform == null)
        {
            var views = GameFlowController.Instance?.CharacterViews;
            if (views != null && views.TryGetValue(_restrictedCharacterId, out var view))
                _heroCharacterTransform = view.transform;
        }

        SubscribeGameEvents();
        EnterPhase(TutorialPhase.WaitIntro);
    }

    private void Update()
    {
        if (_canExitToLobby && Input.GetMouseButtonDown(0))
        {
            _canExitToLobby = false;
            SceneManager.LoadScene(_lobbySceneName);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeGameEvents();
    }

    // ── 외부 진입점 ──────────────────────────────────────────────────────────

    /// <summary>DialogueManager 또는 씬 진입 시 튜토리얼을 시작합니다.</summary>
    public void StartTutorial() => EnterPhase(TutorialPhase.Dialog_EnvyIntro);

    // ── 핵심: Phase 전환 ─────────────────────────────────────────────────────

    private void EnterPhase(TutorialPhase phase)
    {
        if (phase == TutorialPhase.Inactive) return;

        _currentPhase = phase;
        _uiManager?.ClearAll();
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

        Debug.Log($"[TutorialManager] Phase: {phase}");

        // ── System 전용 처리 ──────────────────────────────────────────────
        if (phase == TutorialPhase.WaitIntro)
        {
            SetInputPermission(TutorialInputPermission.None);
            if (_DialogueBox != null) _DialogueBox.SetActive(false);
            return;
        }
        if (phase == TutorialPhase.Tutorial_End)
        {
            HandleTutorialComplete();
            return;
        }

        // ── SO에서 설정 읽기 ──────────────────────────────────────────────
        var cfg = _phaseTable?.Get(phase);
        if (cfg == null)
        {
            Debug.LogWarning($"[TutorialManager] PhaseTable에 {phase} 없음");
            return;
        }

        // 부가 설정 적용
        if (cfg.EnableRoleDocGroup) SetDrawerInteractable(_roleDocGroup, true);
        if (cfg.EnableMemoBookGroup) SetDrawerInteractable(_memoBookGroup, true);

        // PreDelay가 있으면 대기 후 시작
        if (cfg.PreDelay > 0f)
            StartCoroutine(PreDelayThenStart(cfg));
        else
            StartPhaseContent(cfg);
    }

    private IEnumerator PreDelayThenStart(TutorialPhaseConfig cfg)
    {
        SetInputPermission(TutorialInputPermission.None);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        yield return new WaitForSeconds(cfg.PreDelay);
        StartPhaseContent(cfg);
    }

    // ── 대화 출력 시작 ────────────────────────────────────────────────────────

    private void StartPhaseContent(TutorialPhaseConfig cfg)
    {
        if (cfg.Dialogues != null && cfg.Dialogues.Count > 0)
        {
            // 대화가 있으면 첫 줄부터 출력
            _currentDialogueIndex = 0;
            _isPlayingDialogue = true;
            ShowDialogueLine(cfg);
        }
        else
        {
            // 대화 없으면 바로 Phase 동작 실행
            _isPlayingDialogue = false;
            ExecutePhaseBehavior(cfg);
        }
    }

    private void ShowDialogueLine(TutorialPhaseConfig cfg)
    {
        if (_DialogueBox != null) _DialogueBox.SetActive(true);
        SetInputPermission(TutorialInputPermission.DialogueAdvance);
        _uiManager?.SetClickAdvance(true);

        var line = cfg.Dialogues[_currentDialogueIndex];
        ApplySpeaker(line.Speaker);
        _uiManager?.ShowGuide(line.Text, _phaseTable?.GetSprite(line.Speaker));
    }

    // ── 클릭으로 대화 넘기기 ─────────────────────────────────────────────────

    private void HandleGuideAdvanced()
    {
        if (!IsInputAllowed(TutorialInputPermission.DialogueAdvance)) return;

        var cfg = _phaseTable?.Get(_currentPhase);
        if (cfg == null) return;

        if (_isPlayingDialogue)
        {
            if (_currentDialogueIndex < cfg.Dialogues.Count - 1)
            {
                // 다음 줄 출력
                _currentDialogueIndex++;
                ShowDialogueLine(cfg);
            }
            else
            {
                // 마지막 줄 — Phase 동작 실행
                _isPlayingDialogue = false;
                ExecutePhaseBehavior(cfg);
            }
        }
    }

    // ── Phase 동작 분기 ───────────────────────────────────────────────────────

    private void ExecutePhaseBehavior(TutorialPhaseConfig cfg)
    {
        switch (cfg.PhaseType)
        {
            case TutorialPhaseType.Dialogue:
                // 대화 종료 → NextPhase 자동 진입
                var next = cfg.NextPhase != TutorialPhase.Inactive
                    ? cfg.NextPhase
                    : _phaseTable.GetNext(_currentPhase);
                EnterPhase(next);
                break;

            case TutorialPhaseType.Action:
                // 대화창 닫고 조작 권한 부여
                if (_DialogueBox != null) _DialogueBox.SetActive(false);
                SetInputPermission(cfg.ActionPermission);
                _uiManager?.SetClickAdvance(false);
                ApplyArrow(cfg);
                ApplyPhaseHighlight(cfg);
                break;

            case TutorialPhaseType.System:
                Debug.Log($"[TutorialManager] System Phase: {cfg.Phase}");
                break;
        }
    }

    // ── Notify* : 조작 완료 → NextPhaseOnAction 진입 ─────────────────────────

    /// <summary>캐릭터 이동 완료 (HandleActionConfirmed 내부 처리)</summary>
    private void HandleActionConfirmed(int characterId, int targetZoneId)
    {
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return;
        if (characterId != _restrictedCharacterId || targetZoneId != _restrictedTargetZoneId) return;
        AdvanceFromAction();
    }

    /// <summary>깃털펜 클릭 → 턴 종료 이벤트 (1초 딜레이 후 다음 Phase)</summary>
    private void HandleTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
    {
        if (!IsInputAllowed(TutorialInputPermission.AdvanceTurn)) return;

        // ★ 이 시점은 검은 화면이 시작되는 순간 — 입력 차단만 수행
        // 실제 대화 진입은 화면이 밝아진 후 HandlePlayerActionStarted에서 처리
        SetInputPermission(TutorialInputPermission.None);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);
        _uiManager?.SetClickAdvance(false);

        // 데드라인 안내
        var gfc = GameFlowController.Instance;
        if (gfc != null && gfc.LoopCount >= LoopStateMachine.MaxLoops - 1 && !_shownDeadlineGuide)
        {
            _shownDeadlineGuide = true;
            ShowEventGuide(TutorialEventType.Deadline);
        }
    }

    /// <summary>역할 패널 열림</summary>
    private void HandleRoleDocShown()
    {
        if (!IsInputAllowed(TutorialInputPermission.RoleDocToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>역할 패널 닫힘</summary>
    private void HandleRoleDocHidden()
    {
        if (!IsInputAllowed(TutorialInputPermission.RoleDocToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>다이어리 열림</summary>
    public void NotifyCharacterCardOpened()
    {
        if (!IsInputAllowed(TutorialInputPermission.CharacterCardToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>다이어리 닫힘</summary>
    public void NotifyCharacterCardClosed()
    {
        if (!IsInputAllowed(TutorialInputPermission.CharacterCardToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>힌트 포스트잇 열림</summary>
    public void NotifyHintPostItOpened()
    {
        if (!IsInputAllowed(TutorialInputPermission.HintPostItToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>힌트 포스트잇 닫힘</summary>
    public void NotifyHintPostItClosed()
    {
        if (!IsInputAllowed(TutorialInputPermission.HintPostItToggle)) return;
        AdvanceFromAction();
    }

    /// <summary>힌트 고정핀 고정 완료 (★ 신규)</summary>
    public void NotifyHintPinned()
    {
        if (!IsInputAllowed(TutorialInputPermission.HintPin)) return;
        AdvanceFromAction();
    }

    /// <summary>최종 추리 책 클릭 완료</summary>
    public void HandleFinalDecisionEntered()
    {
        if (!IsInputAllowed(TutorialInputPermission.FinalDecisionEnter)) return;
        AdvanceFromAction();
    }

    /// <summary>최종 추리 인물 선택 완료</summary>
    public void NotifyFinalCharacterSelected()
    {
        if (!IsInputAllowed(TutorialInputPermission.FinalCharacterSelect)) return;
        AdvanceFromAction();
    }

    /// <summary>최종 추리 선택지 클릭 (화살표 이동만, 제출은 NotifyFinalSubmit)</summary>
    public void NotifyFinalDecisionSelected(int optionIndex)
    {
        if (!IsInputAllowed(TutorialInputPermission.FinalDecisionSelect)) return;
        if (optionIndex == 1)
            SetArrowPos(350f, -250f, 0f, 0f, 0f, -180f);
    }

    /// <summary>최종 추리 확정 제출</summary>
    public void NotifyFinalSubmit()
    {
        if (!IsInputAllowed(TutorialInputPermission.FinalDecisionSelect)) return;
        StartCoroutine(FinalTransitionCoroutine());
    }

    // ── PlayerAction 시작 ─────────────────────────────────────────────────────

    private void HandlePlayerActionStarted()
    {
        // ★ 이 시점이 검은 화면이 끝나고 화면이 밝아진 순간
        // Action_TurnEnd_Quill 단계에서만 → SO의 NextPhaseOnAction으로 대화 진입
        if (_currentPhase == TutorialPhase.Action_TurnEnd_Quill)
        {
            var cfg = _phaseTable?.Get(_currentPhase);
            var nextPhase = cfg?.NextPhaseOnAction ?? TutorialPhase.Inactive;
            if (nextPhase != TutorialPhase.Inactive)
                StartCoroutine(DelayedEnterPhase(nextPhase, 1.0f));
            return;
        }

        // 엔비 이동 단계에서만 캐릭터 하이라이트
        if (_currentPhase == TutorialPhase.Action_MoveEnvy && _heroCharacterTransform != null)
            _uiManager?.SetWorldHighlight(_heroCharacterTransform);
    }

    // ── 이벤트 안내 (퇴고 등) ─────────────────────────────────────────────────

    private void HandleLoopReset()
    {
        if (_shownNormalLoopGuide) return;
        _shownNormalLoopGuide = true;
        ShowEventGuide(TutorialEventType.NormalLoop);
    }

    private void HandleLoopConditionTriggered()
    {
        if (_shownForceLoopGuide) return;
        _shownForceLoopGuide = true;
        _shownNormalLoopGuide = true;
        ShowEventGuide(TutorialEventType.ForceLoop);
    }

    private void HandleGameEnded(bool isWin)
    {
        if (isWin) { TutorialProgressRepository.Instance.MarkCleared(); return; }
        ShowEventGuide(TutorialEventType.GameFail);
        StartCoroutine(DelayedLoadScene(_lobbySceneName, 2f));
    }

    // ── 튜토리얼 종료 ────────────────────────────────────────────────────────

    private void HandleTutorialComplete()
    {
        SetInputPermission(TutorialInputPermission.None);
        _uiManager?.ClearAll();
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

        if (_fadeOverlay != null)
        {
            _fadeOverlay.gameObject.SetActive(true);
            _fadeOverlay.color = new Color(0, 0, 0, 1f);
        }
        if (_endingtitle != null)
        {
            _endingtitle.text = "튜토리얼 완료";
            _endingtitle.gameObject.SetActive(true);
        }
        if (_campaignModeText != null)
        {
            _campaignModeText.text = "캠페인 모드가 열렸습니다";
            _campaignModeText.gameObject.SetActive(true);
        }

        TutorialProgressRepository.Instance?.MarkCleared();
        if (_rewardSaveData != null) _rewardSaveData.SaveTutorialClear();
        TutorialSaveHelper.GrantTutorialReward();

        // ★ 튜토리얼 완료 시 중간 업로드 (세션 유지, StopStageLogging 없음)
        UploadCurrentProgress(isWin: true);

        _canExitToLobby = true;
        _currentPhase = TutorialPhase.Inactive;
    }

    /// <summary>
    /// 세션을 끊지 않고 현재까지 기록된 데이터를 Discord로 업로드합니다.
    /// 튜토리얼 완료 / 캐릭터 최종 대화 완료 등 중간 체크포인트에 사용합니다.
    /// </summary>
    private void UploadCurrentProgress(bool isWin)
    {
        var logger = GameLogger.Instance;
        if (logger == null || LogUploader.Instance == null) return;

        string fileName = logger.BuildUploadFileName();
        string stageId = logger.CurrentStageId;
        byte[] bytes = logger.ExtractCurrentSessionBytes(); // StopStageLogging 없이 추출
        LogUploader.Instance.UploadSessionBytes(bytes, fileName, isWin, stageId);
    }

    // ── 이벤트 구독 ──────────────────────────────────────────────────────────

    private void SubscribeGameEvents()
    {
        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnLoopReset += HandleLoopReset;
            gfc.OnFinalDecisionEntered += HandleFinalDecisionEntered;
            gfc.OnGameEnded += HandleGameEnded;

            _turnSM = gfc.GetTurnSM();
            _playerAction = gfc.GetPlayerActionState();
        }

        if (_turnSM != null)
        {
            _turnSM.OnTurnEndEntered += HandleTurnEndEntered;
            _turnSM.OnPlayerActionStarted += HandlePlayerActionStarted;
            _turnSM.OnLoopConditionTriggered += HandleLoopConditionTriggered;
        }
        if (_playerAction != null)
            _playerAction.OnActionConfirmed += HandleActionConfirmed;

        if (_roleDocDrawer != null) { _roleDocDrawer.OnShown += HandleRoleDocShown; _roleDocDrawer.OnHidden += HandleRoleDocHidden; }
        if (_memoBookDrawer != null) { _memoBookDrawer.OnShown += NotifyCharacterCardOpened; _memoBookDrawer.OnHidden += NotifyCharacterCardClosed; }
        if (_uiManager != null) _uiManager.OnGuideAdvanced += HandleGuideAdvanced;
        if (_iconButton != null) _iconButton.onClick.AddListener(NotifyFinalCharacterSelected);
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

        if (_roleDocDrawer != null) { _roleDocDrawer.OnShown -= HandleRoleDocShown; _roleDocDrawer.OnHidden -= HandleRoleDocHidden; }
        if (_memoBookDrawer != null) { _memoBookDrawer.OnShown -= NotifyCharacterCardOpened; _memoBookDrawer.OnHidden -= NotifyCharacterCardClosed; }
        if (_uiManager != null) _uiManager.OnGuideAdvanced -= HandleGuideAdvanced;
        if (_iconButton != null) _iconButton.onClick.RemoveListener(NotifyFinalCharacterSelected);
    }

    // ── 입력 허가 API ─────────────────────────────────────────────────────────

    /// <summary>현재 단계에서 해당 권한이 허용됐는지 확인합니다.</summary>
    public bool IsInputAllowed(TutorialInputPermission permission)
    {
        if (!IsActive) return true;
        return (_allowedInputs & permission) != 0;
    }

    /// <summary>캐릭터 드래그 가능 여부</summary>
    public bool IsCharacterDragAllowed(int characterId)
    {
        if (!IsActive) return true;
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;
        if (_currentPhase == TutorialPhase.Action_MoveEnvy)
            return characterId == _restrictedCharacterId;
        return true;
    }

    /// <summary>캐릭터 드롭(구역 배치) 가능 여부</summary>
    public bool IsZoneDropAllowed(int characterId, int zoneId)
    {
        if (!IsActive) return true;
        if (!IsInputAllowed(TutorialInputPermission.CharacterMove)) return false;
        if (_currentPhase == TutorialPhase.Action_MoveEnvy)
            return characterId == _restrictedCharacterId && zoneId == _restrictedTargetZoneId;
        return true;
    }

    // ── Private 유틸 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 Phase의 NextPhaseOnAction으로 진입합니다.
    /// 모든 Notify* 메서드가 이 메서드를 통해 다음 Phase로 이동합니다.
    /// </summary>
    private void AdvanceFromAction()
    {
        var cfg = _phaseTable?.Get(_currentPhase);
        if (cfg?.NextPhaseOnAction != TutorialPhase.Inactive)
            EnterPhase(cfg.NextPhaseOnAction);
    }

    private void SetInputPermission(TutorialInputPermission permission)
        => _allowedInputs = permission;

    private void ApplySpeaker(TutorialSpeaker speaker)
    {
        if (_phaseTable == null) return;
        if (_dialogueName != null)
            _dialogueName.text = _phaseTable.GetName(speaker);
        if (_dialogueSpeakerImage != null)
        {
            var sprite = _phaseTable.GetSprite(speaker);
            _dialogueSpeakerImage.sprite = sprite;
            _dialogueSpeakerImage.enabled = sprite != null;
        }
    }

    private void ApplyArrow(TutorialPhaseConfig cfg)
    {
        if (_TutoArrow == null) return;
        if (!cfg.ShowArrow) { _TutoArrow.gameObject.SetActive(false); return; }

        _TutoArrow.gameObject.SetActive(true);
        _TutoArrow.localPosition = new Vector3(cfg.ArrowPosition.x, cfg.ArrowPosition.y, 0f);
        _TutoArrow.localRotation = Quaternion.Euler(0f, 0f, cfg.ArrowRotationZ);
        _uiManager?.SetSecondaryBounce(_TutoArrow);
    }

    private void SetArrowPos(float pX, float pY, float pZ, float rX, float rY, float rZ)
    {
        if (_TutoArrow == null) return;
        _TutoArrow.gameObject.SetActive(true);
        _TutoArrow.localPosition = new Vector3(pX, pY, pZ);
        _TutoArrow.localRotation = Quaternion.Euler(rX, rY, rZ);
        _uiManager?.SetSecondaryBounce(_TutoArrow);
    }

    private void ApplyPhaseHighlight(TutorialPhaseConfig cfg)
    {
        var perm = cfg.ActionPermission;
        if ((perm & TutorialInputPermission.CharacterMove) != 0)
        {
            if (_heroCharacterTransform != null) _uiManager?.SetBounceOnly(_heroCharacterTransform, loop: true);
            if (_restrictedZoneTransform != null) _uiManager?.SetSecondaryBounce(_restrictedZoneTransform);
        }
        if ((perm & TutorialInputPermission.AdvanceTurn) != 0)
            if (_quillPenTransform != null) _uiManager?.SetWorldHighlight(_quillPenTransform);
        if ((perm & TutorialInputPermission.FinalDecisionEnter) != 0)
            if (_finalDecisionBookTransform != null) _uiManager?.SetWorldHighlight(_finalDecisionBookTransform);
        if ((perm & TutorialInputPermission.FinalCharacterSelect) != 0)
            if (_iconButton != null) _iconButton.gameObject.SetActive(true);
        if ((perm & TutorialInputPermission.FinalDecisionSelect) != 0)
            if (_finalSelectButton != null) _finalSelectButton.interactable = true;
    }

    private void ShowEventGuide(TutorialEventType eventType)
    {
        if (_guideData == null || _uiManager == null) return;
        if (_guideData.TryGetEventGuide(eventType, out string text, out Sprite sprite))
            if (!string.IsNullOrEmpty(text)) _uiManager.ShowGuide(text, sprite);
    }

    private static void SetDrawerInteractable(CanvasGroup group, bool interactable)
    {
        if (group == null) return;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }

    private void InitDrawerLocks()
    {
        SetDrawerInteractable(_roleDocGroup, false);
        SetDrawerInteractable(_memoBookGroup, false);
    }

    // ── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator DelayedEnterPhase(TutorialPhase nextPhase, float delay)
    {
        SetInputPermission(TutorialInputPermission.None);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        _uiManager?.SetClickAdvance(false);
        yield return new WaitForSeconds(delay);
        EnterPhase(nextPhase);
    }

    private IEnumerator FinalTransitionCoroutine()
    {
        SetInputPermission(TutorialInputPermission.None);
        _uiManager?.SetClickAdvance(false);
        if (_DialogueBox != null) _DialogueBox.SetActive(false);
        if (_TutoArrow != null) _TutoArrow.gameObject.SetActive(false);

        if (_fadeOverlay != null)
        {
            _fadeOverlay.gameObject.SetActive(true);
            Color c = _fadeOverlay.color; c.a = 0f; _fadeOverlay.color = c;
            _fadeOverlay.DOFade(1f, 1.5f).SetEase(Ease.InQuad);
        }
        yield return new WaitForSeconds(1.5f);
        EnterPhase(TutorialPhase.Dialog_FinalResult);
    }

    private IEnumerator DelayedLoadScene(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}