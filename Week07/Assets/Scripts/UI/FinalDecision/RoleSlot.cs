using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 역할 카드가 드롭될 수 있는 빈칸 슬롯입니다.
/// Inspector에서 _characterId를 이 슬롯에 표시된 캐릭터의 CharacterId와 일치시켜 설정하세요.
/// Image 컴포넌트 필수 (Raycast Target = true).
/// </summary>
[RequireComponent(typeof(Image))]
public class RoleSlot : MonoBehaviour, IDropHandler
{
    [Tooltip("이 슬롯에 표시된 캐릭터의 CharacterId (CharacterData.characterId와 동일한 값으로 설정)")]
    [SerializeField] private int _characterId;

    public int      CharacterId  => _characterId;
    public RoleCard AssignedCard { get; private set; }

    private void Awake()
    {
        GetComponent<Image>().raycastTarget = true;
    }

    public void Init() { /* CharacterId는 Inspector의 _characterId에서 읽음 */ }

    public void OnDrop(PointerEventData eventData)
    {
        var card = eventData.pointerDrag?.GetComponent<RoleCard>();
        if (card == null) return;

        if (AssignedCard != null && AssignedCard != card)
            AssignedCard.ReturnHome();

        AssignedCard = card;
        card.SnapToSlot(this);

        var actual = GameFlowController.Instance != null
            ? GameFlowController.Instance.GetActualRole(_characterId)
            : default;
        Debug.Log($"[RoleSlot] CharId={_characterId}  배정={card.RoleType}  정답={actual}  → {(card.RoleType == actual ? "O" : "X")}");
    }

    public void Release(RoleCard card)
    {
        if (AssignedCard == card)
            AssignedCard = null;
    }

    public void ForceRelease()
    {
        AssignedCard = null;
    }
}
