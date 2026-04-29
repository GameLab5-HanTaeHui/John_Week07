using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 ZoneLayout입니다.
    /// 기본모드는 ZoneLayout(HTH namespace)을 사용하세요.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────
    ///   IsAbilityDisabled() 제거 — 캠페인에 능력 무효화 구역 없음
    ///   OnValidate() GameState.ZoneCount 검증 제거 — Zone 수 제약 없음
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Zones                → ZonePoint 배열 (순서 무관)
    /// </summary>
    public class CampaignZoneLayout : MonoBehaviour
    {
        [SerializeField] private ZonePoint[] _zones;

        private readonly Dictionary<int, List<int>> _slotMap = new();

        // ── 구역 조회 ─────────────────────────────────────────────────────

        /// <summary>구역 ID로 ZonePoint를 반환합니다. 없으면 null.</summary>
        public ZonePoint GetZonePoint(int zoneId)
        {
            foreach (var zone in _zones)
                if (zone != null && zone.ZoneId == zoneId)
                    return zone;
            return null;
        }

        /// <summary>구역 중심 월드 위치를 반환합니다. 없으면 Vector3.zero.</summary>
        public Vector3 GetZonePosition(int zoneId)
        {
            var zone = GetZonePoint(zoneId);
            if (zone == null)
            {
                Debug.LogWarning($"[CampaignZoneLayout] ZoneId {zoneId} 에 해당하는 ZonePoint가 없습니다.");
                return Vector3.zero;
            }
            return zone.Position;
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

            if (_slotMap.TryGetValue(fromZoneId, out var fromSlots))
            {
                int idx = fromSlots.IndexOf(characterId);
                if (idx >= 0) fromSlots[idx] = -1;
            }

            if (!_slotMap.TryGetValue(toZoneId, out var toSlots))
                _slotMap[toZoneId] = toSlots = new List<int>();

            int bestIdx = -1;
            int capacity = toSlots.Count;

            if (dropWorldPos != default && capacity > 0)
            {
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
                toSlots[toSlots.IndexOf(-1)] = characterId;
            else
                toSlots.Add(characterId);
        }

        // ── 슬롯 위치/회전 계산 ──────────────────────────────────────────────

        /// <summary>
        /// 구역 내 slotIndex 번째 슬롯 위치를 반환합니다.
        /// totalInZone은 해당 구역에 배치될 전체 캐릭터 수입니다.
        /// </summary>
        public Vector3 GetSlotPosition(int zoneId, int slotIndex, int totalInZone)
        {
            var zone = GetZonePoint(zoneId);
            if (zone == null)
            {
                Debug.LogWarning($"[CampaignZoneLayout] ZoneId {zoneId} 에 해당하는 ZonePoint가 없습니다.");
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
        /// charZoneMap(characterId → zoneId)을 받아 각 캐릭터의 슬롯 위치를 계산합니다.
        /// 반환: characterId → 월드 위치
        /// </summary>
        public Dictionary<int, Vector3> ComputeSlotPositions(Dictionary<int, int> charZoneMap)
        {
            var result = new Dictionary<int, Vector3>(charZoneMap.Count);

            var byZone = new Dictionary<int, HashSet<int>>();
            foreach (var kv in charZoneMap)
            {
                if (!byZone.TryGetValue(kv.Value, out var set))
                    byZone[kv.Value] = set = new HashSet<int>();
                set.Add(kv.Key);
            }

            foreach (var kv in byZone)
            {
                int zoneId = kv.Key;
                var activeChars = kv.Value;

                if (!_slotMap.TryGetValue(zoneId, out var slots))
                {
                    var sorted = new List<int>(activeChars);
                    sorted.Sort();
                    int total = sorted.Count;
                    for (int i = 0; i < total; i++)
                        result[sorted[i]] = GetSlotPosition(zoneId, i, total);
                    continue;
                }

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

        /// <summary>
        /// charZoneMap(characterId → zoneId)을 받아 각 캐릭터의 슬롯 회전을 계산합니다.
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
                int zoneId = kv.Key;
                var activeChars = kv.Value;

                if (!_slotMap.TryGetValue(zoneId, out var slots))
                {
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
    }
}