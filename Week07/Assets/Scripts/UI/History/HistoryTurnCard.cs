using UnityEngine;

/// <summary>
/// 히스토리 뷰에서 턴 한 회를 표시하는 카드 컴포넌트입니다.
///
/// 프리팹 구조:
///   TurnCard (이 컴포넌트 + RectTransform)
///   ├── ZoneAnchor_0  (HistoryZoneAnchor, ZoneId = 0)  ← 구역 0의 미니 위치
///   ├── ZoneAnchor_1  (HistoryZoneAnchor, ZoneId = 1)
///   ├── ZoneAnchor_2  (HistoryZoneAnchor, ZoneId = 2)
///   ├── ZoneAnchor_3  (HistoryZoneAnchor, ZoneId = 3)
///   └── TokenContainer (RectTransform) ← _tokenContainer 연결, 토큰의 부모
///
/// ZoneAnchor는 TokenContainer 밖에 두어야 InverseTransformPoint 변환이 정확합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class HistoryTurnCard : MonoBehaviour
{
    [SerializeField] private RectTransform _tokenContainer;

    /// <summary>토큰을 자식으로 붙일 컨테이너 RectTransform입니다.</summary>
    public RectTransform TokenContainer => _tokenContainer;

    private HistoryZoneAnchor[] _anchors;

    private void Awake()
    {
        _anchors = GetComponentsInChildren<HistoryZoneAnchor>(includeInactive: true);
    }

    // ── 외부 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 주어진 zoneId 앵커의 위치를 TokenContainer 기준 로컬 좌표로 반환합니다.
    /// HistoryManager가 토큰의 anchoredPosition을 설정할 때 사용합니다.
    /// </summary>
    public Vector2 GetZoneLocalPosition(int zoneId)
    {
        foreach (var anchor in _anchors)
        {
            if (anchor.ZoneId != zoneId) continue;

            // 앵커의 월드 위치 → TokenContainer의 로컬 위치 변환
            Vector3 local = _tokenContainer.InverseTransformPoint(anchor.transform.position);
            return new Vector2(local.x, local.y);
        }

        Debug.LogWarning($"[HistoryTurnCard] ZoneId {zoneId} 앵커를 찾을 수 없습니다. Vector2.zero 반환.");
        return Vector2.zero;
    }
}
