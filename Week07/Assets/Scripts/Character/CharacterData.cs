using UnityEngine;

/// <summary>
/// 캐릭터 1명의 정적 데이터를 정의하는 ScriptableObject입니다.
/// 캐릭터마다 에셋을 1개 생성하세요. (CharacterData_A ~ CharacterData_G)
///
/// characterId는 프리팹과 1:1로 대응하는 고정값입니다.
/// 한 번 설정 후 절대 변경하지 마세요. (RoleAssignmentTable 키로 사용됨)
/// </summary>
[CreateAssetMenu(fileName = "CharacterData_", menuName = "MafiaGame/Character/CharacterData")]
public class CharacterData : ScriptableObject
{
    [SerializeField] private int        characterId;
    [SerializeField] private string     characterName;
    [SerializeField] private Sprite     portrait;
    [SerializeField] private GameObject prefab;

    /// <summary>프리팹과 1:1 대응하는 고정 ID (0~6). 변경 금지.</summary>
    public int        CharacterId   => characterId;
    public string     CharacterName => characterName;
    public Sprite     Portrait      => portrait;
    public GameObject Prefab        => prefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (characterId < 0 || characterId > 6)
            Debug.LogWarning($"[CharacterData] '{name}' characterId는 0~6 범위여야 합니다. 현재값: {characterId}");

        if (string.IsNullOrWhiteSpace(characterName))
            Debug.LogWarning($"[CharacterData] '{name}' characterName이 비어 있습니다.");
    }
#endif
}
