using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 배치된 ZonePoint를 관리하며 구역 ID로 위치를 조회합니다.
/// CharacterSpawner, PlayerTurnInputHandler에서 참조합니다.
///
/// 설정 방법:
///   Inspector의 Zones 배열에 씬의 ZonePoint를 순서 무관하게 등록하세요.
/// </summary>
public class ZoneLayout : MonoBehaviour
{
    [SerializeField] private ZonePoint[] _zones;

    // 구역별 슬롯 배정: zoneId → 슬롯 목록 (값 -1 = 빈 슬롯)
    private readonly Dictionary<int, List<int>> _slotMap = new();

    /// <summary>구역 ID로 ZonePoint를 반환합니다. 없으면 null.</summary>
    public ZonePoint GetZonePoint(int zoneId)
    {
        foreach (var zone in _zones)
        {
            if (zone != null && zone.ZoneId == zoneId)
                return zone;
        }
        return null;
    }

    // ── 슬롯 배정 관리 ───────────────────────────────────────────────────────

    /// <summary>
    /// 초기 슬롯 배정을 설정합니다. 게임 시작 또는 루프 리셋 시 호출하세요.
    /// charZoneMap: characterId → zoneId
    /// </summary>
    public void InitSlots(Dictionary<int, int> charZoneMap)
    {
        _slotMap.Clear();
        var byZone = new Dictionary<int, List<int>>();
        foreach (var kv in charZoneMap)
        {
            if (!byZone.TryGetValue(kv.Value, out var list))
                byZone[kv.Value] = list = new List<int>();
            list.Add(kv.Key);
        }
        foreach (var kv in byZone)
        {
            var chars = kv.Value;
            chars.Sort();
            _slotMap[kv.Key] = new List<int>(chars);
        }
    }

    /// <summary>
    /// 캐릭터를 fromZoneId에서 toZoneId로 슬롯 이동합니다.
    /// 기존 슬롯은 -1(빈 칸)으로 남기고, dropWorldPos에 가장 가까운 빈 슬롯에 배치합니다.
    /// </summary>
    public void MoveToZone(int characterId, int fromZoneId, int toZoneId, Vector3 dropWorldPos = default)
    {
        if (fromZoneId == toZoneId) return;

        // 기존 구역에서 슬롯 비우기 (-1로 표시, 다른 캐릭터는 이동 없음)
        if (_slotMap.TryGetValue(fromZoneId, out var fromSlots))
        {
            int idx = fromSlots.IndexOf(characterId);
            if (idx >= 0) fromSlots[idx] = -1;
        }

        // 목표 구역 슬롯 목록 준비
        if (!_slotMap.TryGetValue(toZoneId, out var toSlots))
            _slotMap[toZoneId] = toSlots = new List<int>();

        int bestIdx  = -1;
        int capacity = toSlots.Count;

        if (dropWorldPos != default && capacity > 0)
        {
            // 빈 슬롯 중 dropWorldPos에 가장 가까운 위치 선택
            float bestDist = float.MaxValue;
            for (int i = 0; i < capacity; i++)
            {
                if (toSlots[i] != -1) continue;
                float dist = Vector3.SqrMagnitude(
                    GetSlotPosition(toZoneId, i, capacity) - dropWorldPos);
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }
        }

        if (bestIdx >= 0)
            toSlots[bestIdx] = characterId;
        else if (toSlots.Contains(-1))
            toSlots[toSlots.IndexOf(-1)] = characterId; // 폴백: 첫 번째 빈 슬롯
        else
            toSlots.Add(characterId);                   // 폴백: 맨 뒤에 추가
    }

    /// <summary>구역 중심 월드 위치를 반환합니다. 없으면 Vector3.zero.</summary>
    public Vector3 GetZonePosition(int zoneId)
    {
        var zone = GetZonePoint(zoneId);
        if (zone == null)
        {
            Debug.LogWarning($"[ZoneLayout] ZoneId {zoneId} 에 해당하는 ZonePoint가 없습니다.");
            return Vector3.zero;
        }
        return zone.Position;
    }

    /// <summary>
    /// 구역 내 slotIndex 번째 슬롯 위치를 반환합니다.
    /// totalInZone은 해당 구역에 배치될 전체 캐릭터 수 (자동 오프셋 계산에 사용).
    /// </summary>
    public Vector3 GetSlotPosition(int zoneId, int slotIndex, int totalInZone)
    {
        var zone = GetZonePoint(zoneId);
        if (zone == null)
        {
            Debug.LogWarning($"[ZoneLayout] ZoneId {zoneId} 에 해당하는 ZonePoint가 없습니다.");
            return Vector3.zero;
        }
        return zone.GetSlotPosition(slotIndex, totalInZone);
    }

    /// <summary>구역 내 slotIndex 번째 슬롯 회전을 반환합니다. 없으면 Quaternion.identity.</summary>
    public Quaternion GetSlotRotation(int zoneId, int slotIndex)
    {
        var zone = GetZonePoint(zoneId);
        if (zone == null) return Quaternion.identity;
        return zone.GetSlotRotation(slotIndex);
    }

    /// <summary>
    /// charZoneMap(characterId → zoneId)을 받아 각 캐릭터의 슬롯 회전을 계산합니다.
    /// _slotMap의 고정 슬롯 인덱스를 사용하므로 다른 캐릭터가 이동해도 회전이 바뀌지 않습니다.
    /// 반환: characterId → 월드 회전
    /// </summary>
    public Dictionary<int, Quaternion> ComputeSlotRotations(Dictionary<int, int> charZoneMap)
    {
        var result = new Dictionary<int, Quaternion>(charZoneMap.Count);

        var byZone = new Dictionary<int, HashSet<int>>();
        foreach (var kv in charZoneMap)
        {
            if (!byZone.TryGetValue(kv.Value, out var set))
                byZone[kv.Value] = set = new HashSet<int>();
            set.Add(kv.Key);
        }

        foreach (var kv in byZone)
        {
            int zoneId      = kv.Key;
            var activeChars = kv.Value;

            if (!_slotMap.TryGetValue(zoneId, out var slots))
            {
                // _slotMap 미초기화 폴백: 정렬 순서로 처리
                var sorted = new List<int>(activeChars);
                sorted.Sort();
                for (int i = 0; i < sorted.Count; i++)
                    result[sorted[i]] = GetSlotRotation(zoneId, i);
                continue;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                int charId = slots[i];
                if (charId == -1 || !activeChars.Contains(charId)) continue;
                result[charId] = GetSlotRotation(zoneId, i);
            }
        }

        return result;
    }

    /// <summary>해당 구역에서 능력이 비활성화되어 있으면 true를 반환합니다.</summary>
    public bool IsAbilityDisabled(int zoneId)
    {
        var zone = GetZonePoint(zoneId);
        return zone != null && zone.DisableAbilities;
    }

    /// <summary>
    /// charZoneMap(characterId → zoneId)을 받아 각 캐릭터의 슬롯 위치를 계산합니다.
    /// _slotMap의 고정 슬롯 인덱스를 사용하므로 다른 캐릭터가 이동해도 위치가 바뀌지 않습니다.
    /// 반환: characterId → 월드 위치
    /// </summary>
    public Dictionary<int, Vector3> ComputeSlotPositions(Dictionary<int, int> charZoneMap)
    {
        var result = new Dictionary<int, Vector3>(charZoneMap.Count);

        // 구역별 활성 캐릭터 집합
        var byZone = new Dictionary<int, HashSet<int>>();
        foreach (var kv in charZoneMap)
        {
            if (!byZone.TryGetValue(kv.Value, out var set))
                byZone[kv.Value] = set = new HashSet<int>();
            set.Add(kv.Key);
        }

        foreach (var kv in byZone)
        {
            int zoneId      = kv.Key;
            var activeChars = kv.Value;

            if (!_slotMap.TryGetValue(zoneId, out var slots))
            {
                // _slotMap 미초기화 폴백: 정렬 순서로 처리
                var sorted = new List<int>(activeChars);
                sorted.Sort();
                int total = sorted.Count;
                for (int i = 0; i < total; i++)
                    result[sorted[i]] = GetSlotPosition(zoneId, i, total);
                continue;
            }

            // 슬롯 용량(빈 슬롯 포함)을 기준으로 간격을 고정하여 위치 이동 없음
            int capacity = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                int charId = slots[i];
                if (charId == -1 || !activeChars.Contains(charId)) continue;
                result[charId] = GetSlotPosition(zoneId, i, capacity);
            }
        }

        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_zones == null || _zones.Length != GameState.ZoneCount)
            Debug.LogWarning($"[ZoneLayout] Zones 배열에 ZonePoint가 {GameState.ZoneCount}개 등록되어야 합니다. 현재: {_zones?.Length ?? 0}개");
    }
#endif
}
