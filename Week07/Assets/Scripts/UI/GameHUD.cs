using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 진행 중 표시되는 메인 HUD를 관리합니다.
///
/// Canvas 구조 (씬에 직접 구성):
///   Canvas
///   └── TopBar
///       ├── LoopTurnText  (TMP) ← LoopTurnText 연결
///       └── PhaseText     (TMP) ← PhaseText 연결
///
/// 결과 로그와 턴 확인은 DialogueManager가 담당합니다.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("루프 · 턴 정보")]
    [SerializeField] private TMP_Text _loopTurnText;
    [SerializeField] private TMP_Text _phaseText;

    [Header("시간대 이미지 (아침 · 점심 · 저녁 순서로 연결)")]
    [SerializeField] private Image[] _dayPhaseImages = new Image[3];

    [Header("턴 넘기기 텍스트")]
    [SerializeField] private TMP_Text _nextTurnText;

    [Header("행동 단계 버튼")]
    [SerializeField] private Button _endTurnButton;
    [SerializeField] private Button _deductionButton;

    private TurnStateMachine _turnSM;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null)
        {
            Debug.LogError("[GameHUD] GameFlowController를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _turnSM = gfc.GetTurnSM();
        SubscribeEvents();
        SetupButtons(gfc);

        bool isPlayerAction = gfc.CurrentLoopState == LoopStateType.RunningTurn
                           && gfc.CurrentTurnState  == TurnStateType.PlayerAction;
        SetDeductionButton(isPlayerAction);
        SetEndTurnButton(isPlayerAction);
    }

    private void OnDestroy()
    {
        if (_turnSM == null) return;
        _turnSM.OnPlayerActionStarted -= HandlePlayerActionStarted;
        _turnSM.OnTurnEndEntered      -= HandleTurnEndEntered;
    }

    private void Update()
    {
        RefreshLoopTurnText();
        RefreshPhaseText();
        RefreshDayPhaseImages();
        RefreshNextTurnText();
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────────────

    private void OnEndTurnClicked()    => GameFlowController.Instance?.ForceEndTurn();
    private void OnDeductionClicked()  => GameFlowController.Instance?.EnterFinalDecision();

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    private void HandlePlayerActionStarted()
    {
        SetDeductionButton(true);
        SetEndTurnButton(true);
    }

    private void HandleTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
    {
        // 다이어로그 재생 중에는 행동 버튼 비활성화
        SetDeductionButton(false);
        SetEndTurnButton(false);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        if (_turnSM == null) return;
        _turnSM.OnPlayerActionStarted += HandlePlayerActionStarted;
        _turnSM.OnTurnEndEntered      += HandleTurnEndEntered;
    }

    private void SetupButtons(GameFlowController gfc)
    {
        if (_endTurnButton != null)
            _endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (_deductionButton != null)
            _deductionButton.onClick.AddListener(OnDeductionClicked);
    }

    private void RefreshLoopTurnText()
    {
        if (_loopTurnText == null) return;
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;
        int daysLeft = LoopStateMachine.MaxLoops - gfc.LoopCount + 1;
        _loopTurnText.text = $"마감일까지 {daysLeft}일";
    }

    private void RefreshPhaseText()
    {
        if (_phaseText == null) return;
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;
        _phaseText.text = GetPhaseLabel(gfc.CurrentLoopState, gfc.TurnCount);
    }

    private void RefreshDayPhaseImages()
    {
        if (_dayPhaseImages == null || _dayPhaseImages.Length == 0) return;
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;
        int activeIndex = gfc.TurnCount - 1;
        for (int i = 0; i < _dayPhaseImages.Length; i++)
        {
            if (_dayPhaseImages[i] != null)
                _dayPhaseImages[i].gameObject.SetActive(i == activeIndex);
        }
    }

    private static string GetPhaseLabel(LoopStateType loop, int turnCount = 0)
    {
        return loop switch
        {
            LoopStateType.RunningTurn           => turnCount switch
            {
                1 => "아침",
                2 => "점심",
                3 => "저녁",
                _ => ""
            },
            LoopStateType.AwaitingFinalDecision => "최종 결정 대기",
            LoopStateType.FinalDecision         => "최종 결정",
            LoopStateType.GameEnd               => "게임 종료",
            _                                   => ""
        };
    }

    private void RefreshNextTurnText()
    {
        if (_nextTurnText == null) return;
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;
        _nextTurnText.text = gfc.TurnCount switch
        {
            1 => "점심으로 가기",
            2 => "저녁으로 가기",
            3 => "퇴고하기",
            _ => ""
        };
    }

    private void SetEndTurnButton(bool interactable)
    {
        if (_endTurnButton != null)
            _endTurnButton.interactable = interactable;
    }

    private void SetDeductionButton(bool interactable)
    {
        if (_deductionButton != null)
            _deductionButton.interactable = interactable;
    }
}
