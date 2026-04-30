using HTH;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector에서 직접 할당된 HistoryPagePanel을 5루프 × 3턴 구조로 관리합니다.
/// TurnHistoryRepository.OnRecordCommitted를 구독하여
/// 턴이 완료될 때마다 해당 패널을 SetActive(true)로 활성화합니다.
/// 기본모드 전용입니다.
///
/// ─── Inspector 할당 방법 ─────────────────────────────────────────────────
///   _loopPanels 배열 크기를 5로 설정한 뒤,
///   각 LoopPanelRow의 Turns 배열 크기를 3으로 설정하고
///   씬에 미리 배치된 HistoryPagePanel을 순서대로 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public class HistoryPageController : MonoBehaviour
{
    [Serializable]
    public class LoopPanelRow
    {
        [Tooltip("이 루프에 해당하는 턴 패널 (Turn0, Turn1, Turn2 순서)")]
        public HistoryPagePanel[] Turns = new HistoryPagePanel[3];
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("패널 할당 — [루프 인덱스][턴 인덱스]")]
    [SerializeField] private LoopPanelRow[] _loopPanels = new LoopPanelRow[5];

    [Header("Backdrop")]
    [SerializeField] private Button _backdropButton;

    [Header("애니메이션")]
    [SerializeField] private float _expandOffset = 450f;
    [SerializeField] private float _spawnBelowOffset = 200f;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    public event Action OnAnyPanelHeaderClicked;
    public event Action OnAnyPanelCollapsed;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private HistoryPagePanel[][] _panels;
    private HistoryPagePanel _expandedPanel;
    private int _historyOpenCount;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildPanelArray();
        InitAllPanels();
    }

    private void Start()
    {
        TurnHistoryRepository.Instance.OnRecordCommitted += HandleRecordCommitted;

        foreach (var record in TurnHistoryRepository.Instance.GetAllRecords())
            HandleRecordCommitted(record);
    }

    private void OnDestroy()
    {
        TurnHistoryRepository.Instance.OnRecordCommitted -= HandleRecordCommitted;
    }

    // ── 초기화 ────────────────────────────────────────────────────────────────

    private void BuildPanelArray()
    {
        _panels = new HistoryPagePanel[_loopPanels.Length][];
        for (int loop = 0; loop < _loopPanels.Length; loop++)
        {
            var turns = _loopPanels[loop].Turns;
            _panels[loop] = new HistoryPagePanel[turns.Length];
            for (int turn = 0; turn < turns.Length; turn++)
                _panels[loop][turn] = turns[turn];
        }
    }

    private void InitAllPanels()
    {
        if (_backdropButton != null)
        {
            _backdropButton.gameObject.SetActive(false);
            _backdropButton.onClick.AddListener(OnBackdropClicked);
        }

        for (int loop = 0; loop < _panels.Length; loop++)
        {
            for (int turn = 0; turn < _panels[loop].Length; turn++)
            {
                var panel = _panels[loop][turn];
                if (panel == null)
                {
                    Debug.LogWarning($"[HistoryPageController] 패널 미연결 — L{loop} T{turn}");
                    continue;
                }
                panel.Init(loop, turn);
                panel.OnHeaderClicked += HandlePanelHeaderClicked;
                panel.gameObject.SetActive(false);
            }
        }
    }

    private void OnBackdropClicked()
    {
        if (GameFlowController.Instance?.CurrentLoopState == LoopStateType.FinalDecision)
            return;
        if (TutorialManager.IsActive) return;
        CollapseExpanded();
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    private void HandleRecordCommitted(TurnRecord record)
    {
        if (!IsValidIndex(record.LoopIndex, record.TurnIndex))
        {
            Debug.LogWarning($"[HistoryPageController] 범위 초과 — L{record.LoopIndex} T{record.TurnIndex}");
            return;
        }

        var panel = _panels[record.LoopIndex][record.TurnIndex];
        if (panel.IsActivated) return;

        panel.gameObject.SetActive(true);
        panel.Activate(record);
        panel.SpawnIn(_spawnBelowOffset);
    }

    private void HandlePanelHeaderClicked(HistoryPagePanel clicked)
    {
        if (clicked == _expandedPanel)
        {
            CollapseExpanded();
            return;
        }

        _historyOpenCount++;
        var gfc = GameFlowController.Instance;

        string historyTimeOfDay = clicked.TurnIndex switch
        {
            0 => "morning",
            1 => "lunch",
            2 => "evening",
            _ => "unknown"
        };

        GameLogger.Instance?.LogEvent("history_open", new Dictionary<string, object>
        {
            { "target_loop",              clicked.LoopIndex + 1 },
            { "target_turn",              clicked.TurnIndex + 1 },
            { "target_day",               clicked.LoopIndex + 1 },
            { "target_time_of_day",       historyTimeOfDay },
            { "current_day",              gfc?.CurrentDay ?? 0 },
            { "current_time_of_day",      gfc?.CurrentTimeOfDay ?? "" },
            { "click_count_this_session", _historyOpenCount },
        });

        OnAnyPanelHeaderClicked?.Invoke();
        ExpandPanel(clicked, instant: false);
    }

    // ── 핵심 전환 로직 ────────────────────────────────────────────────────────

    private void ExpandPanel(HistoryPagePanel target, bool instant)
    {
        if (_expandedPanel != null && _expandedPanel != target)
            _expandedPanel.Collapse(instant);

        _expandedPanel = target;
        target.Expand(_expandOffset, instant);
        SetBackdropActive(true);

        PanelManager.Instance?.RegisterPanel(CollapseExpanded);
    }

    public void CollapseExpanded()
    {
        if (_expandedPanel == null) return;
        _expandedPanel.Collapse();
        _expandedPanel = null;
        SetBackdropActive(false);

        PanelManager.Instance?.UnregisterPanel(CollapseExpanded);
        OnAnyPanelCollapsed?.Invoke();
    }

    private void SetBackdropActive(bool active)
    {
        if (_backdropButton != null)
            _backdropButton.gameObject.SetActive(active);
    }

    public void ExpandByRecord(TurnRecord record)
    {
        if (!IsValidIndex(record.LoopIndex, record.TurnIndex)) return;
        var panel = _panels[record.LoopIndex][record.TurnIndex];
        if (!panel.IsActivated) return;
        ExpandPanel(panel, instant: false);
    }

    // ── 유틸리티 ──────────────────────────────────────────────────────────────

    private bool IsValidIndex(int loopIndex, int turnIndex)
        => loopIndex >= 0 && loopIndex < _panels.Length
        && turnIndex >= 0 && turnIndex < _panels[loopIndex].Length;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_loopPanels == null || _loopPanels.Length == 0)
        {
            Debug.LogWarning("[HistoryPageController] _loopPanels가 비어 있습니다.");
            return;
        }

        for (int loop = 0; loop < _loopPanels.Length; loop++)
        {
            var row = _loopPanels[loop];
            if (row == null || row.Turns == null) continue;
            for (int turn = 0; turn < row.Turns.Length; turn++)
                if (row.Turns[turn] == null)
                    Debug.LogWarning($"[HistoryPageController] 패널 미연결 — L{loop} T{turn}");
        }
    }
#endif
}