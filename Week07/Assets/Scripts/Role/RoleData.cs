using UnityEngine;

/// <summary>
/// 직업 한 종류의 데이터를 정의하는 ScriptableObject입니다.
/// 직업별로 에셋을 하나씩 생성하세요. (예: RoleData_Murderer, RoleData_Protagonist ...)
///
/// AbilityConfig 연결 방법:
///   능력이 있는 직업 → 해당 AbilityConfig 에셋을 AbilityConfig 필드에 연결
///   능력이 없는 직업 (친구B 등) → AbilityConfig 필드를 비워두세요 (null 허용)
/// </summary>
[CreateAssetMenu(fileName = "RoleData_", menuName = "MafiaGame/Role/RoleData")]
public class RoleData : ScriptableObject
{
    [SerializeField] private RoleType     roleType;
    [SerializeField] private string       roleName;
    [SerializeField] private string       description;
    [SerializeField] private Sprite       roleIcon;
    [SerializeField] private AbilityConfig abilityConfig;

    public RoleType      RoleType      => roleType;
    public string        RoleName      => roleName;
    public string        Description   => description;
    public Sprite        RoleIcon      => roleIcon;

    /// <summary>
    /// 직업 능력 설정. 능력이 없는 직업은 null입니다.
    /// RoleAbilityProcessor에서 null 여부를 확인 후 Execute()를 호출합니다.
    /// </summary>
    public AbilityConfig AbilityConfig => abilityConfig;
}
