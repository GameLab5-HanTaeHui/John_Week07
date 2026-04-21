using UnityEngine;

/// <summary>
/// HistoryTurnCard 프리팹 안에서 구역 한 칸의 위치를 표시하는 마커 컴포넌트입니다.
/// 빈 RectTransform에 부착하고 ZoneId를 게임의 구역 번호와 맞춰 설정하세요.
///
/// 프리팹 구조 예시:
///   TurnCard
///   ├── ZoneAnchor_0  (HistoryZoneAnchor, ZoneId = 0)
///   ├── ZoneAnchor_1  (HistoryZoneAnchor, ZoneId = 1)
///   ├── ZoneAnchor_2  (HistoryZoneAnchor, ZoneId = 2)
///   ├── ZoneAnchor_3  (HistoryZoneAnchor, ZoneId = 3)
///   └── TokenContainer
/// </summary>
[DisallowMultipleComponent]
public class HistoryZoneAnchor : MonoBehaviour
{
    [SerializeField] private int _zoneId;

    public int ZoneId => _zoneId;
}
