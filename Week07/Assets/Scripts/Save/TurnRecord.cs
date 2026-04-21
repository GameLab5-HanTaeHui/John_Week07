using System;
using System.Collections.Generic;

/// <summary>
/// 이동 전 또는 이동 후 캐릭터 한 명의 위치·생사 스냅샷입니다.
/// </summary>
[Serializable]
public class CharacterPositionSnapshot
{
    /// <summary>캐릭터 고정 ID</summary>
    public int  CharacterId;
    /// <summary>해당 시점의 구역 ID</summary>
    public int  ZoneId;
    /// <summary>해당 시점의 생사 여부</summary>
    public bool IsAlive;
}

/// <summary>
/// 이번 턴에 발생한 사망 1건을 기록합니다.
/// 과거 보기 UI에서 아이콘 표시에 사용합니다.
/// </summary>
[Serializable]
public class TurnDeathRecord
{
    /// <summary>사망한 캐릭터 고정 ID</summary>
    public int      CharacterId;
    /// <summary>사망 원인 직업 (아이콘 표시용)</summary>
    public RoleType CauseRole;
    /// <summary>능력을 발동한 캐릭터 고정 ID</summary>
    public int      SourceCharacterId;
}

/// <summary>
/// 턴 1회 분량의 완전한 스냅샷입니다.
///
/// 용도:
///   레벨 복구  — Seed + BeforeAction/AfterAction으로 특정 루프·턴 직전 상태 재현
///   과거 보기  — Deaths 목록만 사용해 각 턴 사망자를 아이콘으로 표시
/// </summary>
[Serializable]
public class TurnRecord
{
    /// <summary>0-based 루프 인덱스</summary>
    public int LoopIndex;
    /// <summary>0-based 턴 인덱스 (루프 내)</summary>
    public int TurnIndex;
    /// <summary>이 루프를 결정한 시드값 — 레벨 복구의 기준점</summary>
    public int Seed;

    /// <summary>플레이어 행동 전 위치 (PreviousZone 기준)</summary>
    public List<CharacterPositionSnapshot> BeforeAction = new List<CharacterPositionSnapshot>();
    /// <summary>플레이어 행동 후 위치 (CurrentZone 기준, 능력 발동 전)</summary>
    public List<CharacterPositionSnapshot> AfterAction  = new List<CharacterPositionSnapshot>();
    /// <summary>이번 턴 확정된 사망 목록</summary>
    public List<TurnDeathRecord>           Deaths       = new List<TurnDeathRecord>();
    /// <summary>특수 루프 조건(살인자 사망 / 3명 이상 사망)으로 루프가 리셋된 턴이면 true</summary>
    public bool IsLoopConditionTurn;
}
