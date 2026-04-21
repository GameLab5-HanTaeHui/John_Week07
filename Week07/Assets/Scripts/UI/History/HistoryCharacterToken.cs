using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 히스토리 뷰에서 캐릭터 한 명을 나타내는 미니어처 토큰 컴포넌트입니다.
///
/// 프리팹 구조:
///   HistoryCharacterToken  (이 컴포넌트 + RectTransform)
///   ├── MovedIcon           (Image) ← _movedIcon 연결  (이동 시 표시)
///   ├── StaticIcon          (Image) ← _staticIcon 연결 (정지 시 표시)
///   ├── DiedThisTurnMark    (GameObject) ← _diedThisTurnMark 연결 (이번 턴 사망)
///   └── AlreadyDeadMark     (GameObject) ← _alreadyDeadMark 연결 (이전 턴 사망)
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class HistoryCharacterToken : MonoBehaviour
{
    [SerializeField] private Image      _movedIcon;
    [SerializeField] private Image      _staticIcon;
    [SerializeField] private GameObject _diedThisTurnMark;
    [SerializeField] private GameObject _alreadyDeadMark;

    /// <summary>다른 컴포넌트에서 직접 위치를 조작할 때 사용합니다.</summary>
    public RectTransform Rect { get; private set; }

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
    }

    /// <summary>이동 여부, 이번 턴 사망 여부, 이전 턴 사망 여부로 토큰을 초기화합니다.</summary>
    public void Setup(bool moved, bool diedThisTurn, bool alreadyDead)
    {
        if (_movedIcon        != null) _movedIcon.gameObject.SetActive(moved);
        if (_staticIcon       != null) _staticIcon.gameObject.SetActive(!moved);
        if (_diedThisTurnMark != null) _diedThisTurnMark.SetActive(diedThisTurn);
        if (_alreadyDeadMark  != null) _alreadyDeadMark.SetActive(alreadyDead);
    }
}
