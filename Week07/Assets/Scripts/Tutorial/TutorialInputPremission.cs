using System;

/// <summary>
/// 각 단계별로 허용할 상호작용 권한 (비트 플래그)
/// 달성하지 못한 상호작용을 철저히 막기 위해 세분화합니다.
/// </summary>
[Flags]
public enum TutorialInputPermission
{
    None = 0,
    DialogueAdvance = 1 << 0, // 대화 패널 넘기기 (클릭)

    // 조작 권한
    CharacterMove = 1 << 1, // 캐릭터 드래그 앤 드롭
    AdvanceTurn = 1 << 2, // 턴 종료 깃털펜 사용
    FinalDecision = 1 << 3, // 최종 집필 책 클릭

    // 패널 열기/닫기 권한
    RoleDocToggle = 1 << 4, // 왼쪽 역할 패널 열기/닫기
    HistoryToggle = 1 << 5, // 기록 파일 열기/닫기
    MemoToggle = 1 << 6, // 메모 수첩 열기/닫기
    MemoWrite = 1 << 7, // 메모 격자칸 작성

    // 버튼 상호작용 권한
    HistorySwapButton = 1 << 8, // 교체 버튼
    CharacterCardToggle = 1 << 9, // 인물 카드 열기/닫기
    HintPostItToggle = 1 << 10, // 힌트 포스트잇 열기/닫기

    All = ~0
}