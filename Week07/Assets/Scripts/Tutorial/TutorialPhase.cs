using System;

/// <summary>
/// 튜토리얼 순서형 단계(Phase)를 정의합니다.
/// TutorialManager가 이 순서대로 진행합니다.
/// </summary>

public enum TutorialPhase
{
    Inactive,
    WaitIntro,

    // ── 1. 도입: 엔비의 독백 ──
    Dialog_EnvyIntro_1,         // [엔비] "안녕. 나는 엔비..."
    Dialog_EnvyIntro_2,         // [엔비] "물론 진짜 목적은..."
    Dialog_EnvyIntro_3,         // [엔비] "누군가 내게 의뢰..."
    Dialog_EnvyIntro_4,         // [엔비] "겉으로 보기엔 멀쩡..."
    Dialog_EnvyIntro_5,         // [엔비] "하지만 들리는 소문..."
    Dialog_EnvyIntro_6,         // [엔비] "그렇다면 내가 할일..."
    Dialog_EnvyIntro_7,         // [엔비] "그리고 그상처를..."
    Dialog_EnvyIntro_8,         // [엔비] "그럼 시작해볼까..."

    // ── 2. 첫 이동 및 시간 진행 ──
    Dialog_MoveEnvy1,         // [엔비] "사람들이 흩어져..."
    Dialog_MoveEnvy2,         // [엔비] "우선 혼자 있는 사람..."

    Action_MoveEnvy,            // [Action] 엔비를 구역으로 드래그
    Action_TurnEnd_Quill,       // [Action] 깃털펜 클릭 (시간 진행)

    // ── 3. 첫 대화: 메이 ──
    Dialog_EnvyTalk1,           // [엔비] "메이님 안녕..."
    Dialog_MayTalk_1,           // [메이] "신입? 무슨일..."

    Dialog_EnvyTalk2,           // [엔비] "별견 아니..."
    Dialog_MayTalk_2,           // [메이] "분위기?"

    Dialog_EnvyTalk3,           // [엔비] "다들 오래 함께한 것..."
    Dialog_MayTalk_3,           // [메이] "원래는 아니였지..."

    Dialog_EnvyTalk4,           // [엔비] "무슨일 있었..."
    Dialog_MayTalk_4,           // [메이] "있었지 잡을 수..."

    Dialog_EnvyTalk5,           // [엔비] "드래곤이요?..."
    Dialog_MayTalk_5,           // [메이] "거의 끝났었어.."

    Dialog_EnvyTalk6,           // [엔비] "그런데 어쩌다가..."
    Dialog_MayTalk_6,           // [메이] "그건 아직 네가..."

    Dialog_PieceTuto,           // [Narration] "조각을 획득 하였습니다. 메이는 드래곤을 놓친..."
    Dialog_System_Clue,         // [Narration] "대화를 통해 사건과 인물에 대한 단서..."

    // ── 4. 에드먼드의 메모장 (역할 패널) ──
    Dialog_EnvyRules1,             // [엔비] "이상한 규칙이 있어..."
    Action_OpenEdmundNote,         // [Action] "왼쪽 에드먼드의 메모장을 열어..."
    Dialog_EdmundNarration1,        // [Narration] "각 역할 기능과 사건 서술 순서..."
    Dialog_EdmundNarration2,        // [Narration] "역할 기능은 인물의 행동 패턴과..."
    Dialog_EdmundNarration3,        // [Narration] "사건 서술 순서는 사건이 어떤 순서로 진행..."
    Action_CloseEdmundNote,        // [Action] "패널을 다시 닫아주세요."

    // ── 5. 캐릭터 파일 (엔비의 다이어리) ──
    Dialog_EnvyStrategy1,          // [엔비] "이제 사람을 봐야해..."
    Dialog_EnvyStrategy2,          // [엔비] "누가 무엇을 두려워 하는지..."
    Dialog_EnvyStrategy3,          // [엔비] "그걸 알아야 이 용병단을 흔들 수 있어..."
    Action_OpenEnvyDiary,          // [Action] "엔비의 다이어리에서 캐릭터 정보를 확인..."
    Dialog_DiaryNarration1,         // [Narration] "정보 카드에서는 이름, 역할, 수집된 대화 조각을 확인..."
    Dialog_DiaryNarration2,         // [Narration] "대화 조각은 캐릭터들과의 대화 후 엔비가 수집한 단서..."
    Dialog_DiaryNarration3,         // [Narration] "가운데 메모지는 다른 대화 조각의 힌트..."
    Action_OpenHintPostIt,         // [Action] "메모지를 눌러 보세요."
    Dialog_DiaryNarration5,         // [Narration] "힌트는 대화조각을 얻는 조건을..."
    Dialog_EnvyContext1,            // [엔비] "어떤 말은 혼자 있을때만..."
    Dialog_EnvyContext2,            // [엔비] "사람은 항상 같은 말을 하지..."
    Action_CloseEnvyDiary,         // [Action] "힌트 메모지와 캐릭터 정보카드를 닫아주세요."

    // ── 6. 최종 대화 (최종 집필) ──
    Dialog_FinalIntro1,             // [엔비] "내 최종 목표는 수집한 정보를 이용해..."
    Action_EnterFinalDecision1,     // [Action] "우측 책을 눌러 최종 추리를 시작해주세요."
    Action_EnterFinalDecision2,     // [Action] "엔비를 선택해주세요."
    Dialog_EnterFinalDecision1,     // [Narration] "최종 대화 대화에서는 수집한 대화조각..."
    Dialog_EnterFinalDecision2,     // [Narration] "같은 진실이라도 어떤부분을 어디까지..."
    Dialog_FinalIntro2,             // [엔비] "용병단 사람들이 나에게 말을 거는..."
    Dialog_EnterFinalDecision3,     // [Narration] "해당 인물의 성격과 사건을 고려하여, 상대방을 가장..."
    Action_SelectFinalOption,       // [Action] "질문 내용을 선택해주세요."
    Dialog_FinalResult,             // [엔비] ".. 내가 스파이라는걸 들킨다면 실패야..."

    // ── 7. 종료 및 실전 진입 ──
    Dialog_EnvyOutro1,           // "이제 기본적인 건 알겠어. 본격적으로 시작해보자."
    Dialog_EnvyOutro2,           // "사건을 모으고, 사람을 이해하고, 말을 어떻게 전달할지..."
    Dialog_EnvyOutro3,           // "이 용병단은 아직 서로를 동료라고 믿고 있어. 함께 해온 시간..."
    Dialog_EnvyOutro4,           // "필요한 건 완전한 거짓말이 아니야..."
    Dialog_EnvyOutro5,           // "그럼 이제 본격적으로 시작해보자..."
    Tutorial_End                // 튜토리얼 종료 처리
}
