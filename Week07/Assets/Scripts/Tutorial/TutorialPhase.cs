/// <summary>
/// 튜토리얼 순서형 단계(Phase)를 정의합니다.
/// TutorialManager가 이 순서대로 진행합니다.
/// </summary>
public enum TutorialPhase
{
    Inactive,               // 튜토리얼 비활성

    // ── 1턴: 제한된 이동 가이드 ─────────────────────────────────────────────
    Initial,                // 시작 — 전체 입력 잠금, 살인자+구역 하이라이트
    RestrictedMove,         // 살인자 → 하단 구역 이동만 허용
    QuillPenUnlocked,       // 이동 완료 → 깃털펜(턴 종료) 하이라이트

    // ── 결과창 ───────────────────────────────────────────────────────────────
    WaitingForTurnEnd,      // 턴 처리 중 (입력 없음)
    ResultDisplaying,       // 결과창 표시 중

    // ── 2턴: UI 순차 해금 가이드 ────────────────────────────────────────────
    RoleDocGuide,           // 역할 기능 UI 하이라이트
    NarrativeOrderGuide,    // 사서순 UI 하이라이트
    MemoBookGuide,          // 메모 수첩 하이라이트
    EventRecordGuide,       // 사건 기록 포스트잇 하이라이트
    MemoWriteGuide,         // 메모 O/△/X 기능 해금 안내
    FinalDecisionBookGuide, // 책(최종 집필 진입) 하이라이트 — 정보성
    DateUIGuide,            // 날짜 UI 하이라이트 — 이후 전체 해금

    FullyUnlocked,          // 모든 기능 해금 — 자유 플레이
}
