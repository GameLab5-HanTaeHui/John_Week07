using System;
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

    [Header("애니메이션")]
    [Tooltip("버튼 클릭 시 오리진 Y에서 위로 올라가는 거리 (px)")]
    [SerializeField] private float _expandOffset = 450f;
    [Tooltip("최초 등장 시 오리진 아래에서 시작하는 거리 (px)")]
    [SerializeField] private float _spawnBelowOffset = 200f;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>어떤 패널의 헤더(포스트잇)가 클릭되었을 때 발생합니다. TutorialManager에서 구독합니다.</summary>
    public event Action OnAnyPanelHeaderClicked;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    /// <summary>[loopIndex][turnIndex] 형태의 런타임 2D 참조 — Awake에서 구성됩니다.</summary>
    private HistoryPagePanel[][] _panels;

    /// <summary>현재 펼쳐진 패널 (없으면 null)</summary>
    private HistoryPagePanel _expandedPanel;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
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
            _backdropButton.onClick.AddListener(CollapseExpanded);
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
                panel.Init(loop, turn, _expandOffset);
                panel.OnHeaderClicked      += HandlePanelHeaderClicked;
                panel.OnCollapseRequested  += HandlePanelCollapseRequested;
                panel.gameObject.SetActive(false);
            }
        }
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

        // SetActive(true) → 패널 Awake 실행 (오리진 Y 저장, 버튼 리스너 등록)
        panel.gameObject.SetActive(true);
        panel.Activate(record);
        panel.SpawnIn(_spawnBelowOffset); // 오리진 아래에서 슬라이드업
    }

    private void HandlePanelHeaderClicked(HistoryPagePanel clicked)
    {
        if (clicked == _expandedPanel) return;
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
    }

    /// <summary>
    /// 패널이 드래그로 스스로 내려갈 때 호출됩니다.
    /// 패널 자체는 이미 Collapse()를 호출했으므로 상태만 정리합니다.
    /// </summary>
    private void HandlePanelCollapseRequested(HistoryPagePanel panel)
    {
        if (_expandedPanel != panel) return;
        _expandedPanel = null;
        SetBackdropActive(false);
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
