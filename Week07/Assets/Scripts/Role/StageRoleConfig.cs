using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별로 등장할 직업 목록을 정의하는 ScriptableObject입니다.
/// 스테이지마다 에셋을 하나씩 생성하세요. (예: StageRoleConfig_Stage1 ...)
/// 직업 추가/제거는 Inspector의 Roles 리스트에서 직접 조작합니다.
/// </summary>
[CreateAssetMenu(fileName = "StageRoleConfig_", menuName = "MafiaGame/Role/StageRoleConfig")]
public class StageRoleConfig : ScriptableObject
{
    [Tooltip("저장 파일과 대조에 사용할 고유 스테이지 ID (예: Stage_1). 변경 시 기존 세이브가 무효화됩니다.")]
    [SerializeField] private string _stageId;

    [SerializeField] private List<RoleData> roles = new List<RoleData>();

    [Tooltip("이 스테이지의 루프 강제 종료 조건. null이면 루프 조건 없음.")]
    [SerializeField] private LoopConditionConfig _loopCondition;

    /// <summary>고유 스테이지 식별자. 세이브 파일 검증에 사용합니다.</summary>
    public string StageId => _stageId;

    /// <summary>스테이지별 루프 강제 종료 조건. null이면 루프가 자동 종료되지 않습니다.</summary>
    public LoopConditionConfig LoopCondition => _loopCondition;

    /// <summary>등록된 직업 목록 (읽기 전용)</summary>
    public IReadOnlyList<RoleData> Roles => roles;

    /// <summary>특정 직업 타입이 이 스테이지에 포함되어 있는지 확인합니다.</summary>
    public bool ContainsRole(RoleType type)
    {
        return roles.Exists(r => r != null && r.RoleType == type);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CheckDuplicateRoles();
    }

    // 같은 RoleType이 중복 등록되면 에디터 콘솔에 경고를 출력합니다.
    private void CheckDuplicateRoles()
    {
        var seen = new HashSet<RoleType>();
        foreach (var role in roles)
        {
            if (role == null) continue;
            if (!seen.Add(role.RoleType))
            {
                Debug.LogWarning($"[StageRoleConfig] '{name}' 에 {role.RoleType} 직업이 중복 등록되어 있습니다.");
            }
        }
    }
#endif
}
