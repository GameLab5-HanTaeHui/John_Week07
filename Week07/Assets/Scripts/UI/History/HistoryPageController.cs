using HTH;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector에서 직접 할당된 HistoryPagePanel을 5루프 × 3턴 구조로 관리합니다.
/// TurnHistoryRepository.OnRecordCommitted를 구독하여
/// 턴이 완료될 때마다 해당 패널을 SetActive(true)로 활성화합니다.
///
/// ─── 동작 원칙 ────────────────────────────────────────────────────────────
///   • 비활성 패널    : SetActive(false) — 씬에 배치되어 있으나 보이지 않음
///   • 대기 패널      : SetActive(true)  — 오리진 위치에 그대로 있음
///   • 펼쳐진 패널    : 오리진 Y + _expandOffset 위치로 슬라이드업
///
///   새 기록 도착   → SetActive(true), 오리진 위치에서 대기
///   헤더 버튼 클릭 → 현재 패널 오리진으로 복귀, 클릭 패널 위로 슬라이드업
///
/// ─── Inspector 할당 방법 ─────────────────────────────────────────────────
///   _loopPanels 배열 크기를 5로 설정한 뒤,
///   각 LoopPanelRow의 Turns 배열 크기를 3으로 설정하고
///   씬에 미리 배치된 HistoryPagePanel을 순서대로 연결합니다.
///     _loopPanels[0].Turns[0] → Loop0 Turn0 패널
///     _loopPanels[0].Turns[1] → Loop0 Turn1 패널
///     ...
///     _loopPanels[4].Turns[2] → Loop4 Turn2 패널
/// </summary>
[DisallowMultipleComponent]
public class HistoryPageController : MonoBehaviour
{
    public static HistoryPageController Instance { get; private set; }
    // ── 2D 배열 Inspector 래퍼 ───────────────────────────────────────────────

    /// <summary>
    /// Unity Inspector는 2차원 배열을 직접 지원하지 않으므로
    /// 루프 1개 분량의 턴 패널 묶음을 래핑합니다.
    /// </summary>
    [Serializable]
    public class LoopPanelRow
    {
        [Tooltip("이 루프에 해당하는 턴 패널 (Turn0, Turn1, Turn2 순서)")]
        public HistoryPagePanel[] Turns = new HistoryPagePanel[3];
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("패널 할당 — [루프 인덱스][턴 인덱스]")]
    [Tooltip("크기를 5로 고정하고 각 Row의 Turns를 3개씩 씬 오브젝트로 연결하세요.")]
    [SerializeField] private LoopPanelRow[] _loopPanels = new LoopPanelRow[5];

    [Header("Backdrop — 패널 뒤 전체화면 투명 버튼")]
    [Tooltip("Image(alpha=0) + Button 컴포넌트. 패널이 펼쳐질 때만 SetActive(true).")]
    [SerializeField] private Button _backdropButton;

    [Header("순환 재기록 (캠페인 전용)")]
    [Tooltip("true  — 5루프 초과 시 패널을 초기화하고 처음부터 재기록합니다.\n"
             + "false — 5루프 초과 시 경고만 출력합니다. (기본 동작)")]
    [SerializeField] private bool _cycleOnOverflow = false;

    [Header("애니메이션")]
    [Tooltip("버튼 클릭 시 오리진 Y에서 위로 올라가는 거리 (px)")]
    [SerializeField] private float _expandOffset = 450f;
    [Tooltip("최초 등장 시 오리진 아래에서 시작하는 거리 (px)")]
    [SerializeField] private float _spawnBelowOffset = 200f;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>어떤 패널의 헤더(포스트잇)가 클릭되었을 때 발생합니다. TutorialManager에서 구독합니다.</summary>
    public event Action OnAnyPanelHeaderClicked;

    /// <summary> 패널이 닫힐 때 발생하는 이벤트
    public event Action OnAnyPanelCollapsed;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    /// <summary>[loopIndex][turnIndex] 형태의 런타임 2D 참조 — Awake에서 구성됩니다.</summary>
    private HistoryPagePanel[][] _panels;

    /// <summary>현재 펼쳐진 패널 (없으면 null)</summary>
    private HistoryPagePanel _expandedPanel;

    /// <summary>이번 세션에서 히스토리 패널을 열람한 총 횟수</summary>
    private int _historyOpenCount;
    /// <summary>_cycleOnOverflow=true 시 5루프 단위 기준점.</summary>
    private int _displayLoopOffset;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        BuildPanelArray();
        InitAllPanels();
    }

    private void Start()
    {
        TurnHistoryRepository.Instance.OnRecordCommitted += HandleRecordCommitted;

        // 디스크에서 복구된 기존 기록 반영 (게임 재시작 시)
        foreach (var record in TurnHistoryRepository.Instance.GetAllRecords())
            HandleRecordCommitted(record);

    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        // Backdrop: 시작 시 비활성, 클릭 시 펼쳐진 패널 닫기
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
    /// <summary>
    /// 패널 바깥 투명 버튼(_backdropButton) 클릭 시 호출됩니다.
    ///
    /// ─── 동작 조건 ────────────────────────────────────────────────────────
    ///   FinalDecision 상태  → 차단 (버튼으로만 닫기)
    ///   튜토리얼 중         → 차단 (튜토리얼 흐름 우선)
    ///   일반 인게임         → CollapseExpanded() 호출
    /// </summary>
    private void OnBackdropClicked()
    {
        if (GameFlowController.Instance?.CurrentLoopState == LoopStateType.FinalDecision)
            return;

        // 튜토리얼 씬 전체에서 backdrop 클릭으로 닫기 차단
        if (TutorialManager.IsActive) return;

        CollapseExpanded();
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────

    private void HandleRecordCommitted(TurnRecord record)
    {
        if (_cycleOnOverflow)
        {
            HandleRecordWithCycle(record);
            return;
        }

        // ── 기본 동작 ──────────────────────────────────────────────────────
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

    /// <summary>
    /// _cycleOnOverflow = true일 때 호출됩니다.
    /// 5루프 초과 시 패널을 초기화하고 처음부터 재기록합니다.
    /// </summary>
    private void HandleRecordWithCycle(TurnRecord record)
    {
        int displayLoop = record.LoopIndex % _panels.Length;

        int expectedOffset = (record.LoopIndex / _panels.Length) * _panels.Length;
        if (expectedOffset != _displayLoopOffset)
        {
            _displayLoopOffset = expectedOffset;
            ResetAllPanels();
            Debug.Log($"[HistoryPageController] {record.LoopIndex + 1}루프 — 패널 초기화 후 재기록");
        }

        if (!IsValidIndex(displayLoop, record.TurnIndex))
        {
            Debug.LogWarning($"[HistoryPageController] 범위 초과 — displayLoop:{displayLoop} T:{record.TurnIndex}");
            return;
        }

        var panel = _panels[displayLoop][record.TurnIndex];
        if (panel == null) return;

        panel.gameObject.SetActive(true);
        panel.Activate(record);
        panel.SpawnIn(_spawnBelowOffset);
    }

    /// <summary>모든 패널을 비활성화하고 Init()으로 상태를 초기화합니다.</summary>
    private void ResetAllPanels()
    {
        CollapseExpanded();

        for (int loop = 0; loop < _panels.Length; loop++)
            for (int turn = 0; turn < _panels[loop].Length; turn++)
            {
                var panel = _panels[loop][turn];
                if (panel == null) continue;
                panel.gameObject.SetActive(false);
                panel.Init(loop, turn);
            }
    }

    private void HandlePanelHeaderClicked(HistoryPagePanel clicked)
    {
        if (clicked == _expandedPanel)
        {
            CollapseExpanded();
            return;
        }

        // ★ [HTH추가] 히스토리 열람 로그 (지표 #12)
        _historyOpenCount++;

        // ★ 캠페인(_cycleOnOverflow=true)이면 CampaignGameFlowController, 아니면 GameFlowController
        int currentDay = 0;
        string currentTimeOfDay = "";
        if (_cycleOnOverflow)
        {
            var cgfc = HTH.Campaign.CampaignGameFlowController.Instance;
            currentDay = cgfc?.CurrentDay ?? 0;
            currentTimeOfDay = cgfc?.CurrentTimeOfDay ?? "";
        }
        else
        {
            var gfc = GameFlowController.Instance;
            currentDay = gfc?.CurrentDay ?? 0;
            currentTimeOfDay = gfc?.CurrentTimeOfDay ?? "";
        }

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
            { "current_day",              currentDay },
            { "current_time_of_day",      currentTimeOfDay },
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

        // PanelManager에 닫기 콜백 등록
        PanelManager.Instance?.RegisterPanel(CollapseExpanded);
    }

    /// <summary>
    /// 현재 펼쳐진 패널을 닫습니다.
    /// Backdrop 클릭 시 호출됩니다.
    /// </summary>
    public void CollapseExpanded()
    {
        if (_expandedPanel == null) return;
        _expandedPanel.Collapse();
        _expandedPanel = null;
        SetBackdropActive(false);

        // PanelManager 등록 해제
        PanelManager.Instance?.UnregisterPanel(CollapseExpanded);

        OnAnyPanelCollapsed?.Invoke();
    }

    private void SetBackdropActive(bool active)
    {
        if (_backdropButton != null)
            _backdropButton.gameObject.SetActive(active);
    }

    /// <summary>특정 TurnRecord에 해당하는 패널을 찾아 펼칩니다. HistoryManager에서 호출합니다.</summary>
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

    // ── Editor 방어 ───────────────────────────────────────────────────────────

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
            {
                if (row.Turns[turn] == null)
                    Debug.LogWarning($"[HistoryPageController] 패널 미연결 — L{loop} T{turn}");
            }
        }
    }
#endif
}