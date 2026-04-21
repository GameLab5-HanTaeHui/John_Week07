/// <summary>
/// 루프 레벨 상태 목록입니다.
/// GameSetup → LoopStart → RunningTurn → LoopEnd → (반복 or AwaitingFinalDecision) → FinalDecision → WinState or LoseState
/// </summary>
public enum LoopStateType
{
    GameSetup,              // 역할 배정 + 초기 배치 (게임 시작 1회)
    LoopStart,              // 루프 시작 (2루프부터 초기 배치 복원)
    RunningTurn,            // 턴 실행 중 (TurnStateMachine에 위임)
    LoopEnd,                // 3턴 완료 후 루프 정리
    AwaitingFinalDecision,  // 루프 소진 후 대기 — 이동·턴 불가, HoldToEnterFinalDecision 필수
    FinalDecision,          // 최종 결정 단계 (역할 추리 제출)
    WinState,               // 승리 — 다이얼로그 종료 후 진입
    LoseState,              // 패배 — 다이얼로그 종료 후 진입
    GameEnd                 // (레거시) 결과 표시 및 게임 종료
}
