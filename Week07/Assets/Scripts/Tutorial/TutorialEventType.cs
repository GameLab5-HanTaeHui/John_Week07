/// <summary>
/// 게임 흐름 이벤트로 트리거되는 튜토리얼 안내 종류입니다.
/// Phase 순서에 무관하게 해당 상황이 처음 발생할 때 1회 표시됩니다.
/// </summary>
public enum TutorialEventType
{
    NormalLoop,    // 일반 퇴고 (3턴 모두 소진)
    ForceLoop,     // 강제 퇴고 (루프 조건 발동)
    Deadline,      // 마감일 도달
    FinalDecision, // 최종 집필 진입
    GameFail,      // 최종 집필 실패
}
