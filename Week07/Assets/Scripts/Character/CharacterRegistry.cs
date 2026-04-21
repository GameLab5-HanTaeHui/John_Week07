using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임에 등장하는 모든 캐릭터 데이터를 보관하는 ScriptableObject입니다.
/// CharacterData_A ~ CharacterData_G 에셋을 이 레지스트리에 등록하세요.
/// GameSetupState에서 이 레지스트리를 참조해 CharacterState를 생성합니다.
/// </summary>
[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "MafiaGame/Character/CharacterRegistry")]
public class CharacterRegistry : ScriptableObject
{
    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    public IReadOnlyList<CharacterData> Characters => characters;

    /// <summary>고정 ID로 CharacterData를 조회합니다. 없으면 null을 반환합니다.</summary>
    public CharacterData GetById(int characterId)
    {
        return characters.Find(c => c != null && c.CharacterId == characterId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CheckDuplicateIds();
    }

    private void CheckDuplicateIds()
    {
        var seen = new HashSet<int>();
        foreach (var data in characters)
        {
            if (data == null) continue;
            if (!seen.Add(data.CharacterId))
                Debug.LogWarning($"[CharacterRegistry] '{name}' 에 CharacterId {data.CharacterId} 가 중복 등록되어 있습니다.");
        }
    }
#endif
}
