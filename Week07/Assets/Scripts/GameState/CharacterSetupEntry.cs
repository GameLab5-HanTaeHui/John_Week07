using System;

/// <summary>
/// 캐릭터 1명의 초기 배치 정보를 담는 구조체입니다.
/// StageSetupConfig에서 배열로 관리하며, 시드 인코딩/디코딩의 기본 단위입니다.
/// </summary>
[Serializable]
public struct CharacterSetupEntry
{
    /// <summary>CharacterRegistry의 고정 ID (0~6)</summary>
    public int characterId;

    /// <summary>StageRoleConfig.Roles 리스트의 인덱스 (0~roleCount-1)</summary>
    public int roleIndex;

    /// <summary>초기 배치 구역 (0~ZoneCount-1)</summary>
    public int zoneId;
}
