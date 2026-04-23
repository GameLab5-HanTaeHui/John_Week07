using System;

/// <summary>
/// 튜토리얼 진행 중 허용되는 입력 종류를 정의하는 플래그 열거형입니다.
/// TutorialManager.IsInputAllowed()로 각 입력 처리 지점에서 체크합니다.
/// </summary>
[Flags]
public enum TutorialInputPermission
{
    None               = 0,
    CharacterMove      = 1 << 0,  // 캐릭터 드래그 이동 (PlayerTurnInputHandler)
    AdvanceTurn        = 1 << 1,  // 턴 넘기기 홀드 (HoldToAdvanceTurn)
    EnterFinalDecision = 1 << 2,  // 최종 결정 진입 홀드 (HoldToEnterFinalDecision)
    RoleDocUI          = 1 << 3,  // 역할 기능 DrawerPanel
    MemoOpen           = 1 << 4,  // 메모 수첩 DrawerPanel 열기/닫기
    EventRecord        = 1 << 5,  // 사건 기록 포스트잇 클릭
    MemoWrite          = 1 << 6,  // 메모 수첩 O/△/X 토글 (SequentialImageToggle)
    All                = ~0
}
