using UnityEngine;

/// <summary>
/// 캐릭터 행 프리팹의 각 파트를 연결합니다.
/// 행 프리팹 루트에 이 컴포넌트를 추가하고 Inspector에서 연결하세요.
///
/// 프리팹 구조 (HorizontalLayoutGroup):
///   Row
///   ├── NamePart    (ShakeLetters) ← 캐릭터 이름
///   ├── ParticlePart(ShakeLetters) ← "은/는"
///   ├── Slot        (RoleSlot + Image, 투명 가능)
///   └── SuffixPart  (ShakeLetters) ← "이다."
/// </summary>
public class CharacterRowItem : MonoBehaviour
{
    [SerializeField] public ShakeLetters namePart;
    [SerializeField] public ShakeLetters particlePart;
    [SerializeField] public RoleSlot     slot;
    [SerializeField] public ShakeLetters suffixPart;
}
