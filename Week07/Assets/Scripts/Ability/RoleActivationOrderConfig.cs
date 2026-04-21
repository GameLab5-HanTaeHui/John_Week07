using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직업 능력 발동 순서를 정의하는 ScriptableObject입니다.
/// Inspector의 Execution Order 리스트에서 위→아래 순서로 능력이 발동됩니다.
///
/// 기본 발동 순서:
///   1. 광신도  (이동 시 이동 전 구역 사망 판정)
///   2. 변수    (살인자와 같은 구역 시 살인자 사망 판정)
///   3. 살인자  (같은 구역 사망 판정)
///   4. 주인공  (면역 - 자신의 사망 마크 제거)
///   5. 대리자  (살인자/광신도 사망 마크 치환)
///   6. 친구A   (자신 사망 시 친구B 연쇄 사망)
///
/// 새 직업 추가 시 이 리스트에 RoleData를 드래그해 원하는 위치에 삽입하세요.
/// </summary>
[CreateAssetMenu(fileName = "RoleActivationOrderConfig", menuName = "MafiaGame/Ability/RoleActivationOrderConfig")]
public class RoleActivationOrderConfig : ScriptableObject
{
    [SerializeField] private List<RoleData> executionOrder = new List<RoleData>();

    /// <summary>능력 발동 순서 목록 (읽기 전용). 인덱스 0이 가장 먼저 실행됩니다.</summary>
    public IReadOnlyList<RoleData> ExecutionOrder => executionOrder;

#if UNITY_EDITOR
    private void OnValidate()
    {
        CheckDuplicateRoles();
    }

    private void CheckDuplicateRoles()
    {
        var seen = new System.Collections.Generic.HashSet<RoleType>();
        foreach (var role in executionOrder)
        {
            if (role == null) continue;
            if (!seen.Add(role.RoleType))
                UnityEngine.Debug.LogWarning($"[RoleActivationOrderConfig] '{name}' 에 {role.RoleType} 직업이 중복 등록되어 있습니다.");
        }
    }
#endif
}
