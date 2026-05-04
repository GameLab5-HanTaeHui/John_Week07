/// <summary>
/// 튜토리얼 Phase 전체 목록입니다.
///
/// ─── 병합 원칙 ────────────────────────────────────────────────────────────
///   · 연속 대화(같은 흐름)   → 하나의 Phase에 Dialogues[] 배열로 묶음
///   · 조작 전 안내 대화     → Action Phase의 Dialogues[]에 포함
///   · 조작 완료 이벤트      → Notify* 메서드 → NextPhaseOnAction 진입
///
/// ─── 시나리오 20단계 → Phase 매핑 ─────────────────────────────────────────
///   1.  엔비 인트로 자기소개               → Dialog_EnvyIntro
///   2.  캐릭터 이동 설명 + 이동            → Action_MoveEnvy
///   3.  턴 종료 설명 + 깃털펜 클릭         → Action_TurnEnd_Quill
///   4.  메이와 대화 + 대화조각 알림         → Dialog_TalkWithMay
///   5.  게임 패널 기능 도입부 (엔비 독백)   → Dialog_PanelIntro
///   6.  에드먼드 메모장 열기 + 설명 + 닫기  → Action_OpenEdmundNote
///                                           Dialog_EdmundNarration
///                                           Action_CloseEdmundNote
///   7.  엔비 상황 설명 (다이어리 도입)      → Dialog_EnvyStrategy
///   8.  다이어리 열기                       → Action_OpenEnvyDiary
///   9.  정보 카드 / 대화 조각 설명          → Dialog_DiaryNarration
///   10. 진실/거짓 마우스 클릭 설명          → Dialog_SlotClickGuide
///   11. 힌트 포스트잇 열기                  → Action_OpenHintPostIt
///   12. 고정핀 설명 + 고정/해제             → Dialog_PinGuide
///                                           Action_PinHint
///   13. 힌트 포스트잇 닫기                  → Action_CloseHintPostIt
///   14. 다이어리 카드 닫기                  → Action_CloseEnvyDiary
///   15. 엔비 상황 설명 (최종 도입)          → Dialog_FinalIntro
///   16. 최종 대화 책 클릭                   → Action_EnterFinalDecision1
///   17. 엔비 아이콘 버튼 클릭               → Action_EnterFinalDecision2
///   18. 최종 질문지 설명 + 선택             → Dialog_FinalSelectionGuide
///                                           Action_SelectFinalOption
///   19. 엔비 결과 독백                      → Dialog_FinalResult
///   20. 마무리 독백 + 종료                  → Dialog_EnvyOutro
///                                           Tutorial_End
/// </summary>
public enum TutorialPhase
{
    Inactive,
    WaitIntro,

    // ── 1. 엔비 인트로 자기소개 ─────────────────────────────────────────
    /// <summary>
    /// [엔비 8줄] 자기소개 독백.
    /// Phase2~9 텍스트.
    /// NextPhase → Action_MoveEnvy
    /// </summary>
    Dialog_EnvyIntro,

    // ── 2. 캐릭터 이동 ───────────────────────────────────────────────────
    /// <summary>
    /// [Action] 엔비 드래그 이동.
    /// Dialogues[0~1]: 엔비 상황 설명 (Phase10~11)
    /// Dialogues[2]:   가이드 (Phase12)
    /// ActionPermission: CharacterMove
    /// NextPhaseOnAction → Action_TurnEnd_Quill
    /// </summary>
    Action_MoveEnvy,

    // ── 3. 턴 종료 ───────────────────────────────────────────────────────
    /// <summary>
    /// [Action] 깃털펜 클릭.
    /// Dialogues[0]: 가이드 (Phase13)
    /// ActionPermission: AdvanceTurn
    /// NextPhaseOnAction → Dialog_TalkWithMay
    /// </summary>
    Action_TurnEnd_Quill,

    // ── 4. 메이와 대화 + 대화 조각 알림 ─────────────────────────────────
    /// <summary>
    /// [엔비↔메이 12줄 + 나레이션 2줄] 대화 릴레이 + 조각 획득 알림.
    /// Dialogues[0~11]: 엔비↔메이 대화 (Phase14~25)
    /// Dialogues[12]:   나레이션 — 조각 획득 알림 (Phase26)
    /// Dialogues[13]:   나레이션 — 단서 시스템 설명 (Phase27)
    /// NextPhase → Dialog_PanelIntro
    /// </summary>
    Dialog_TalkWithMay,

    // ── 5. 게임 패널 기능 도입부 ────────────────────────────────────────
    /// <summary>
    /// [엔비 1줄] 패널 기능 설명 도입 독백.
    /// Dialogues[0]: 엔비 (Phase28)
    /// NextPhase → Action_OpenEdmundNote
    /// </summary>
    Dialog_PanelIntro,

    // ── 6. 에드먼드 메모장 ───────────────────────────────────────────────
    /// <summary>
    /// [Action] 메모장 열기.
    /// Dialogues[0]: 가이드 (Phase29)
    /// ActionPermission: RoleDocToggle
    /// EnableRoleDocGroup: true
    /// NextPhaseOnAction → Dialog_EdmundNarration
    /// ShowArrow: true, ArrowPosition: (-680, 270), ArrowRotationZ: -180
    /// </summary>
    Action_OpenEdmundNote,

    /// <summary>
    /// [나레이션 3줄] 메모장 내용 설명.
    /// Dialogues[0]: Phase30
    /// Dialogues[1]: Phase31
    /// Dialogues[2]: Phase32
    /// NextPhase → Action_CloseEdmundNote
    /// </summary>
    Dialog_EdmundNarration,

    /// <summary>
    /// [Action] 메모장 닫기.
    /// Dialogues[0]: 가이드 (Phase33)
    /// ActionPermission: RoleDocToggle
    /// NextPhaseOnAction → Dialog_EnvyStrategy
    /// ShowArrow: true, ArrowPosition: (-220, 270), ArrowRotationZ: -180
    /// </summary>
    Action_CloseEdmundNote,

    // ── 7. 엔비 상황 설명 (다이어리 도입) ──────────────────────────────
    /// <summary>
    /// [엔비 3줄] 다이어리 도입 전 상황 설명.
    /// Dialogues[0]: Phase34
    /// Dialogues[1]: Phase35
    /// Dialogues[2]: Phase36
    /// NextPhase → Action_OpenEnvyDiary
    /// </summary>
    Dialog_EnvyStrategy,

    // ── 8. 다이어리 열기 ─────────────────────────────────────────────────
    /// <summary>
    /// [Action] 다이어리 열기.
    /// Dialogues[0]: 가이드 (Phase37)
    /// ActionPermission: CharacterCardToggle
    /// EnableMemoBookGroup: true
    /// NextPhaseOnAction → Dialog_DiaryNarration
    /// ShowArrow: true, ArrowPosition: (-465, -225), ArrowRotationZ: -90
    /// </summary>
    Action_OpenEnvyDiary,

    // ── 9. 정보 카드 / 대화 조각 설명 ───────────────────────────────────
    /// <summary>
    /// [나레이션 3줄] 정보 카드 및 대화 조각 설명.
    /// Dialogues[0]: Phase38
    /// Dialogues[1]: Phase39
    /// Dialogues[2]: Phase40 (가운데 힌트 메모지 언급)
    /// NextPhase → Dialog_SlotClickGuide
    /// </summary>
    Dialog_DiaryNarration,

    // ── 10. 진실/거짓 슬롯 클릭 설명 ────────────────────────────────────
    /// <summary>
    /// [나레이션 1줄] CyclicColorText 클릭으로 진실/거짓 표시 설명.
    /// Dialogues[0]: "대화 조각 텍스트를 클릭하면 색상이 바뀝니다..."
    /// NextPhase → Action_OpenHintPostIt
    /// </summary>
    Dialog_SlotClickGuide,

    // ── 11. 힌트 포스트잇 열기 ───────────────────────────────────────────
    /// <summary>
    /// [Action] 힌트 포스트잇 열기.
    /// Dialogues[0]: 가이드 (Phase40 두 번째 / Phase41)
    /// ActionPermission: HintPostItToggle
    /// NextPhaseOnAction → Dialog_PinGuide
    /// ShowArrow: true, ArrowPosition: (95, 180), ArrowRotationZ: -180
    /// </summary>
    Action_OpenHintPostIt,

    // ── 12. 고정핀 설명 + 핀 고정/해제 ─────────────────────────────────
    /// <summary>
    /// [나레이션+엔비 3줄] 힌트 설명 + 고정핀 방법 안내.
    /// Dialogues[0]: Phase42 — 힌트 조건 설명
    /// Dialogues[1]: Phase43 — 엔비 독백
    /// Dialogues[2]: Phase44 — 엔비 독백
    /// Dialogues[3]: 가이드  — "힌트 텍스트를 클릭하면 고정핀에 등록됩니다..."
    /// NextPhase → Action_PinHint
    /// </summary>
    Dialog_PinGuide,

    /// <summary>
    /// [Action] 힌트 고정핀 고정 후 해제.
    /// Dialogues[0]: 가이드 — "힌트를 클릭해 고정하고, 다시 클릭해 해제해보세요."
    /// ActionPermission: HintPin | HintPostItToggle
    /// NextPhaseOnAction → Action_CloseHintPostIt
    /// </summary>
    Action_PinHint,

    // ── 13. 힌트 포스트잇 닫기 ───────────────────────────────────────────
    /// <summary>
    /// [Action] 힌트 포스트잇 닫기.
    /// Dialogues[0]: 가이드 (Phase45 전반부)
    /// ActionPermission: HintPostItToggle
    /// NextPhaseOnAction → Action_CloseEnvyDiary
    /// ShowArrow: true, ArrowPosition: (350, 150), ArrowRotationZ: -150
    /// </summary>
    Action_CloseHintPostIt,

    // ── 14. 다이어리 카드 닫기 ───────────────────────────────────────────
    /// <summary>
    /// [Action] 다이어리 캐릭터 카드 닫기.
    /// Dialogues[0]: 가이드 (Phase45 후반부)
    /// ActionPermission: CharacterCardToggle
    /// NextPhaseOnAction → Dialog_FinalIntro
    /// ShowArrow: true, ArrowPosition: (-310, 400), ArrowRotationZ: -210
    /// </summary>
    Action_CloseEnvyDiary,

    // ── 15. 엔비 상황 설명 (최종 도입) ──────────────────────────────────
    /// <summary>
    /// [엔비 1줄] 최종 대화 도입 상황 설명.
    /// Dialogues[0]: Phase46
    /// NextPhase → Action_EnterFinalDecision1
    /// </summary>
    Dialog_FinalIntro,

    // ── 16. 최종 대화 책 클릭 ────────────────────────────────────────────
    /// <summary>
    /// [Action] 최종 추리 책 클릭.
    /// Dialogues[0]: 가이드 (Phase47)
    /// ActionPermission: FinalDecisionEnter
    /// NextPhaseOnAction → Action_EnterFinalDecision2
    /// ShowArrow: true, ArrowPosition: (550, 130), ArrowRotationZ: -15
    /// </summary>
    Action_EnterFinalDecision1,

    // ── 17. 엔비 아이콘 버튼 클릭 ────────────────────────────────────────
    /// <summary>
    /// [Action] 엔비 아이콘 선택.
    /// Dialogues[0]: 가이드 (Phase48)
    /// ActionPermission: FinalCharacterSelect
    /// NextPhaseOnAction → Dialog_FinalSelectionGuide
    /// ShowArrow: true, ArrowPosition: (-300, 150), ArrowRotationZ: 0
    /// </summary>
    Action_EnterFinalDecision2,

    // ── 18. 최종 질문지 설명 + 선택 ──────────────────────────────────────
    /// <summary>
    /// [나레이션+엔비 4줄] 최종 대화 선택 방법 설명.
    /// Dialogues[0]: Phase49
    /// Dialogues[1]: Phase50
    /// Dialogues[2]: Phase51 — 엔비
    /// Dialogues[3]: Phase52 — 나레이션
    /// NextPhase → Action_SelectFinalOption
    /// </summary>
    Dialog_FinalSelectionGuide,

    /// <summary>
    /// [Action] 질문지 선택 + 제출.
    /// Dialogues[0]: 가이드 (Phase53)
    /// ActionPermission: FinalDecisionSelect
    /// NextPhaseOnAction → (FinalTransitionCoroutine → Dialog_FinalResult)
    /// ShowArrow: true, ArrowPosition: (750, 200), ArrowRotationZ: -180
    /// </summary>
    Action_SelectFinalOption,

    // ── 19. 엔비 결과 독백 ───────────────────────────────────────────────
    /// <summary>
    /// [나레이션+엔비 2줄] 최종 결과 후 독백.
    /// Dialogues[0]: Phase54 — 나레이션 (엔비의 목표)
    /// Dialogues[1]: Phase55 — 엔비
    /// NextPhase → Dialog_EnvyOutro
    /// </summary>
    Dialog_FinalResult,

    // ── 20. 마무리 독백 + 종료 ───────────────────────────────────────────
    /// <summary>
    /// [엔비 5줄] 마무리 독백.
    /// Dialogues[0]: Phase56
    /// Dialogues[1]: Phase57
    /// Dialogues[2]: Phase58
    /// Dialogues[3]: Phase59
    /// Dialogues[4]: Phase60
    /// NextPhase → Tutorial_End
    /// </summary>
    Dialog_EnvyOutro,

    /// <summary>튜토리얼 종료 처리 (HandleTutorialComplete 호출)</summary>
    Tutorial_End
}