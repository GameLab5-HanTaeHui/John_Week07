using System;
using System.Collections.Generic;

/// <summary>
/// 디스크에 저장되는 단일 슬롯 세이브 파일의 루트 구조입니다.
/// TurnHistoryRepository가 JsonUtility로 직렬화합니다.
/// </summary>
[Serializable]
public class GameSaveData
{
    /// <summary>이 세이브가 속한 스테이지 ID. 다른 스테이지로 잘못 로드되는 것을 방지합니다.</summary>
    public string StageId;

    /// <summary>세이브 당시 활성화된 씬 이름. 로비에서 복귀 시 이 씬으로 로드합니다.</summary>
    public string SceneName;

    /// <summary>게임 전체 턴 기록 (루프 리셋을 포함한 모든 루프·턴)</summary>
    public List<TurnRecord> Turns = new List<TurnRecord>();

    /// <summary>마지막 저장 시각 (Unix 밀리초, UTC)</summary>
    public long SavedAt;

    // ── 재개 지점 ──────────────────────────────────────────────────────────

    /// <summary>마지막으로 완료된 루프 인덱스. -1이면 재개 데이터 없음.</summary>
    public int  ResumeLoopIndex       = -1;
    /// <summary>해당 루프에서 마지막으로 완료된 턴 인덱스.</summary>
    public int  ResumeTurnIndex       = -1;
    /// <summary>ResumeLoopIndex에 사용된 시드.</summary>
    public int  ResumeSeed;
    /// <summary>루프 종료 조건(살인자 사망·3인 사망)으로 턴이 끝났으면 true.</summary>
    public bool WasLoopConditionTurn;
    /// <summary>사망 확정 후 최종 캐릭터 위치·생사 스냅샷.</summary>
    public List<CharacterPositionSnapshot> ResumeCharacterStates = new List<CharacterPositionSnapshot>();

    // ── 메모장 ─────────────────────────────────────────────────────────────

    /// <summary>메모장 토글 스위치 인덱스 목록. 배열 순서 = NotepadToggleManager._toggles 순서.</summary>
    public List<int> NotepadToggleStates = new List<int>();
}
