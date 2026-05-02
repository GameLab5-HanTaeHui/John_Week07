using System;

/// <summary>
/// 각 단계별로 허용할 상호작용 권한 (비트 플래그)
/// 달성하지 못한 상호작용을 철저히 막기 위해 세분화합니다.
/// </summary>
[Flags]
public enum TutorialInputPermission
{
    None = 0,

    // 1. 대화 진행 (모든 Dialog_... 단계)
    DialogueAdvance = 1 << 0,

    // 2. 월드 조작
    CharacterMove = 1 << 1,   // 엔비 캐릭터 드래그
    AdvanceTurn = 1 << 2,     // 깃털펜(시간 진행) 클릭[cite: 3]

    // 3. 에드먼드의 메모장 (역할 패널)
    RoleDocToggle = 1 << 3,   // 패널 열기 및 닫기 공통[cite: 3]

    // 4. 엔비의 다이어리 (인물 카드)
    CharacterCardToggle = 1 << 4, // 다이어리 열기 및 닫기 공통[cite: 3]
    HintPostItToggle = 1 << 5,    // 가운데 힌트 포스트잇 클릭[cite: 3]

    // 5. 최종 추리 (최종 집필) - 단계별 권한 분리
    FinalDecisionEnter = 1 << 6,  // 우측 책 클릭 (방 진입)[cite: 3]
    FinalCharacterSelect = 1 << 7, // 방 내부에서 인물(엔비) 아이콘 선택[cite: 3]
    FinalDecisionSelect = 1 << 8,  // 대화 선택지(1, 2번 등) 클릭[cite: 3]

    All = ~0
}