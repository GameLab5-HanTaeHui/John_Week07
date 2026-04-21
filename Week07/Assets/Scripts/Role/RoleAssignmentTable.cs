using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임에서 캐릭터 고정 ID와 직업 데이터를 매핑하는 테이블입니다.
/// 캐릭터 고정 ID는 각 캐릭터 프리팹에 직접 부여된 정수값입니다. (0~N, 씬/런타임에 무관하게 불변)
/// 직접 new로 생성하며, 생명주기는 이 테이블을 소유한 Manager가 책임집니다.
/// </summary>
public class RoleAssignmentTable
{
    private readonly Dictionary<int, RoleData> _table = new Dictionary<int, RoleData>();

    /// <summary>
    /// 캐릭터에 직업을 배정합니다. 이미 배정된 경우 덮어씁니다.
    /// </summary>
    /// <param name="characterId">캐릭터 프리팹에 부여된 고정 ID</param>
    /// <param name="roleData">배정할 직업 데이터</param>
    public void Assign(int characterId, RoleData roleData)
    {
        if (roleData == null)
        {
            Debug.LogWarning($"[RoleAssignmentTable] characterId {characterId} 에 null RoleData를 배정하려 했습니다.");
            return;
        }
        _table[characterId] = roleData;
    }

    /// <summary>특정 캐릭터의 직업 배정을 해제합니다.</summary>
    public void Remove(int characterId)
    {
        _table.Remove(characterId);
    }

    /// <summary>
    /// 캐릭터 ID로 직업 데이터를 조회합니다.
    /// </summary>
    /// <returns>배정된 직업이 있으면 true, roleData에 값을 반환합니다.</returns>
    public bool TryGetRole(int characterId, out RoleData roleData)
    {
        return _table.TryGetValue(characterId, out roleData);
    }

    /// <summary>현재 배정된 전체 테이블을 읽기 전용으로 반환합니다.</summary>
    public IReadOnlyDictionary<int, RoleData> GetAll()
    {
        return _table;
    }

    /// <summary>테이블 전체를 초기화합니다. 스테이지 전환 시 호출하세요.</summary>
    public void Clear()
    {
        _table.Clear();
    }

    /// <summary>현재 배정된 캐릭터 수를 반환합니다.</summary>
    public int Count => _table.Count;
}
