using System;
using System.Collections.Generic;

/// <summary>
/// 턴 기록을 인메모리로 관리하는 저장소입니다.
/// 디스크 저장 없이 런타임 히스토리 UI 전용으로 동작합니다.
///
/// 접근 방법:
///   TurnHistoryRepository.Instance.GetAllRecords()
/// </summary>
public class TurnHistoryRepository
{
    // ── Singleton ────────────────────────────────────────────────────────────

    public static TurnHistoryRepository Instance { get; } = new TurnHistoryRepository();

    private TurnHistoryRepository() { }

    // ── 인메모리 저장소 ──────────────────────────────────────────────────────

    private readonly List<TurnRecord> _records = new List<TurnRecord>();

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 새 TurnRecord가 커밋될 때 발생합니다.
    /// HistoryPageController 등 UI 레이어가 구독하여 패널을 활성화합니다.
    /// </summary>
    public event Action<TurnRecord> OnRecordCommitted;

    // ── 쓰기 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 턴 기록을 추가합니다.
    /// RoleActivationState에서 ConfirmDeaths() 직후 호출합니다.
    /// </summary>
    public void Commit(TurnRecord record, bool wasLoopCondition, List<CharacterPositionSnapshot> finalStates)
    {
        _records.Add(record);
        OnRecordCommitted?.Invoke(record);
    }

    /// <summary>
    /// 인메모리 기록을 전부 지웁니다.
    /// 새 게임 시작 시 호출하세요.
    /// </summary>
    public void ClearAll()
    {
        _records.Clear();
    }

    // ── 읽기 API ─────────────────────────────────────────────────────────────

    /// <summary>전체 턴 기록을 반환합니다. 히스토리 UI에서 직접 접근합니다.</summary>
    public IReadOnlyList<TurnRecord> GetAllRecords() => _records;

    /// <summary>특정 루프의 턴 기록만 필터링해 반환합니다.</summary>
    public IReadOnlyList<TurnRecord> GetRecordsByLoop(int loopIndex)
    {
        var result = new List<TurnRecord>();
        foreach (var r in _records)
        {
            if (r.LoopIndex == loopIndex)
                result.Add(r);
        }
        return result;
    }

    /// <summary>특정 루프·턴의 기록을 반환합니다. 없으면 null.</summary>
    public TurnRecord GetRecord(int loopIndex, int turnIndex)
    {
        foreach (var r in _records)
        {
            if (r.LoopIndex == loopIndex && r.TurnIndex == turnIndex)
                return r;
        }
        return null;
    }
}
