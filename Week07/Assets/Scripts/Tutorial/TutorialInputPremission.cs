using System;

/// <summary>
/// 튜토리얼 각 단계별 허용 상호작용 권한 (비트 플래그)
///
/// ─── 사용법 ──────────────────────────────────────────────────────────────
///   단일 권한 : SetInputPermission(TutorialInputPermission.CharacterMove)
///   복합 권한 : SetInputPermission(CharacterCardToggle | HintPostItToggle)
///   확인      : IsInputAllowed(TutorialInputPermission.HintPin)
/// </summary>
[Flags]
public enum TutorialInputPermission
{
    None = 0,

    // ── 대화 ────────────────────────────────────────────────────────────
    /// <summary>화면 클릭으로 대화 넘기기</summary>
    DialogueAdvance = 1 << 0,

    // ── 월드 조작 ────────────────────────────────────────────────────────
    /// <summary>캐릭터 드래그 이동</summary>
    CharacterMove = 1 << 1,
    /// <summary>깃털펜(시간 진행) 클릭</summary>
    AdvanceTurn = 1 << 2,

    // ── 패널 ────────────────────────────────────────────────────────────
    /// <summary>역할 패널(에드먼드 메모장) 열기/닫기</summary>
    RoleDocToggle = 1 << 3,
    /// <summary>인물 카드(다이어리) 열기/닫기</summary>
    CharacterCardToggle = 1 << 4,
    /// <summary>힌트 포스트잇 열기/닫기</summary>
    HintPostItToggle = 1 << 5,
    /// <summary>힌트 포스트잇 핀 고정 (★ 신규)</summary>
    HintPin = 1 << 6,

    // ── 최종 추리 ────────────────────────────────────────────────────────
    /// <summary>최종 추리 책 클릭 (방 진입)</summary>
    FinalDecisionEnter = 1 << 7,
    /// <summary>최종 추리 인물 선택</summary>
    FinalCharacterSelect = 1 << 8,
    /// <summary>최종 추리 선택지 클릭</summary>
    FinalDecisionSelect = 1 << 9,

    All = ~0
}