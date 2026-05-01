using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 CharacterSpawner입니다.
    /// 기본모드 CharacterSpawner와 완전히 분리됩니다.
    ///
    /// ─── 슬롯 초기화 규칙 ────────────────────────────────────────────────────
    ///   InitSlots 호출은 반드시 아래 두 시점에만 수행합니다.
    ///   1. SpawnAll        — 게임 최초 시작 시
    ///   2. ResetSlots      — 퇴고/강제퇴고(루프 리셋) 시 HandleLoopReset에서 호출
    ///   SyncViewsToGameState는 슬롯 맵을 건드리지 않고 위치만 반영합니다.
    ///
    /// ─── 캠페인 부활 규칙 ────────────────────────────────────────────────────
    ///   ReviveDeadOnly — 사망자만 초기 Zone 위치로 스냅, 생존자 위치 유지
    ///   HandleLoopReset에서 SyncViewsToGameState 대신 이 메서드를 호출합니다.
    ///   사망자 목록(deadCharacterIds)은 GFC가 GameState 갱신 전에 캡처해서 전달합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────────
    ///   CharacterRegistry → 캐릭터 데이터 에셋
    ///   ZoneLayout        → 씬의 CampaignZoneLayout 컴포넌트
    /// </summary>
    public class CampaignCharacterSpawner : MonoBehaviour
    {
        [SerializeField] private CharacterRegistry _characterRegistry;
        [SerializeField] private CampaignZoneLayout _zoneLayout;

        // ── 공개 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 모든 CharacterView를 GameState의 Zone 슬롯 위치로 스냅합니다.
        /// ★ 슬롯 맵을 초기화하지 않습니다 — 기존 슬롯 배치를 유지합니다.
        ///    루프 리셋 시에는 이 함수 전에 ResetSlots()를 먼저 호출하세요.
        /// </summary>
        public void SyncViewsToGameState(GameState gameState, Dictionary<int, CharacterView> views)
        {
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            var positions = _zoneLayout.ComputeSlotPositions(charZoneMap);
            var rotations = _zoneLayout.ComputeSlotRotations(charZoneMap);

            foreach (var kv in views)
            {
                var state = gameState.GetCharacterState(kv.Key);
                if (state != null)
                    kv.Value.Init(state);

                if (positions.TryGetValue(kv.Key, out var pos))
                    kv.Value.SnapToPosition(pos);
                if (rotations.TryGetValue(kv.Key, out var rot))
                    kv.Value.SnapToRotation(rot);

                kv.Value.RefreshView();
            }
        }

        /// <summary>
        /// 퇴고/강제퇴고(루프 리셋) 시 슬롯 맵을 GameState 기준으로 재초기화합니다.
        /// HandleLoopReset → ResetSlots → ReviveDeadOnly 순서로 호출하세요.
        /// </summary>
        public void ResetSlots(GameState gameState, Dictionary<int, CharacterView> views)
        {
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            _zoneLayout.InitSlots(charZoneMap);
        }

        /// <summary>
        /// 캠페인 루프 리셋 시 사망자만 초기 Zone 위치로 스냅합니다.
        /// ★ 생존 캐릭터는 위치를 이동하지 않습니다.
        ///
        /// deadCharacterIds: GFC가 OnLoopReset 이벤트 발행 직전(GameState 갱신 전)에
        ///   캡처한 사망자 ID 집합입니다. GameState는 이미 부활 완료 상태로 전달됩니다.
        ///
        /// 처리:
        ///   사망자 → Init(부활 상태로 갱신) + 슬롯 위치 스냅 + RefreshView
        ///   생존자 → Init(상태 갱신) + RefreshView, 위치 이동 없음
        /// </summary>
        public void ReviveDeadOnly(
            GameState gameState,
            Dictionary<int, CharacterView> views,
            HashSet<int> deadCharacterIds)
        {
            // 사망자 슬롯 위치 계산 (GameState 기준 — 이미 부활 완료 상태)
            var revivedZoneMap = new Dictionary<int, int>();
            if (deadCharacterIds != null)
            {
                foreach (int id in deadCharacterIds)
                {
                    if (!views.ContainsKey(id)) continue;
                    var s = gameState.GetCharacterState(id);
                    if (s != null)
                        revivedZoneMap[id] = s.CurrentZone;
                }
            }

            var revivedPositions = revivedZoneMap.Count > 0
                ? _zoneLayout.ComputeSlotPositions(revivedZoneMap)
                : null;
            var revivedRotations = revivedZoneMap.Count > 0
                ? _zoneLayout.ComputeSlotRotations(revivedZoneMap)
                : null;

            foreach (var kv in views)
            {
                var state = gameState.GetCharacterState(kv.Key);
                if (state == null) continue;

                // 상태 갱신 — 생사 여부 최신화
                kv.Value.Init(state);

                bool isDead = deadCharacterIds != null && deadCharacterIds.Contains(kv.Key);
                if (isDead)
                {
                    // ★ 사망자 — 부활 위치로 스냅
                    if (revivedPositions != null && revivedPositions.TryGetValue(kv.Key, out var pos))
                        kv.Value.SnapToPosition(pos);
                    if (revivedRotations != null && revivedRotations.TryGetValue(kv.Key, out var rot))
                        kv.Value.SnapToRotation(rot);

                    Debug.Log($"[CampaignCharacterSpawner] 부활 스냅 — #{kv.Key} Zone={state.CurrentZone}");
                }
                // ★ 생존자 — 위치 이동 없음

                kv.Value.RefreshView();
            }
        }

        /// <summary>모든 캐릭터 프리팹을 스폰하고 초기 구역 슬롯 위치에 배치합니다.</summary>
        public Dictionary<int, CharacterView> SpawnAll(GameState gameState)
        {
            var views = new Dictionary<int, CharacterView>();

            foreach (var data in _characterRegistry.Characters)
            {
                if (data.Prefab == null)
                {
                    Debug.LogWarning($"[CampaignCharacterSpawner] '{data.CharacterName}'의 Prefab 미연결.");
                    continue;
                }

                var characterState = gameState.GetCharacterState(data.CharacterId);
                if (characterState == null)
                {
                    Debug.LogWarning($"[CampaignCharacterSpawner] CharacterId={data.CharacterId} CharacterState 없음.");
                    continue;
                }

                var instance = Instantiate(data.Prefab, Vector3.zero, Quaternion.identity);
                instance.name = $"Character_{data.CharacterName}";

                var view = instance.GetComponent<CharacterView>();
                if (view == null)
                {
                    Debug.LogError($"[CampaignCharacterSpawner] '{data.CharacterName}' 프리팹에 CharacterView 없음.");
                    Destroy(instance);
                    continue;
                }

                view.Init(characterState);
                views[data.CharacterId] = view;
            }

            // ★ SpawnAll에서만 InitSlots 호출 — 이후 SyncViews는 슬롯 맵 유지
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            _zoneLayout.InitSlots(charZoneMap);
            var positions = _zoneLayout.ComputeSlotPositions(charZoneMap);
            var rotations = _zoneLayout.ComputeSlotRotations(charZoneMap);

            foreach (var kv in views)
            {
                if (positions.TryGetValue(kv.Key, out var pos))
                    kv.Value.SnapToPosition(pos);
                if (rotations.TryGetValue(kv.Key, out var rot))
                    kv.Value.SnapToRotation(rot);
                kv.Value.RefreshView();
            }

            return views;
        }

        /// <summary>
        /// 능력 무효화 구역을 항상 비활성(false)으로 강제 적용합니다.
        /// ZonePoint.DisableAbilities 설정을 무시합니다.
        /// </summary>
        public void ApplyZoneRulesToGameState(GameState gameState)
        {
            var disabled = new bool[GameState.ZoneCount];
            var effects = new ZoneEffectConfig[GameState.ZoneCount];

            for (int i = 0; i < GameState.ZoneCount; i++)
            {
                var zone = _zoneLayout?.GetZonePoint(i);
                effects[i] = zone?.ZoneEffect;
            }

            gameState.InitZoneRules(disabled);
            gameState.InitZoneEffects(effects);
        }

        /// <summary>
        /// ZonePoint의 DisableAbilities를 런타임에 모두 해제합니다.
        /// CampaignModeManager 진입 시 호출합니다.
        /// </summary>
        public void DisableAllAbilityZones(bool disable)
        {
            if (_zoneLayout == null) return;
            for (int i = 0; i < GameState.ZoneCount; i++)
            {
                var zone = _zoneLayout.GetZonePoint(i);
                if (zone == null) continue;
                if (zone.DisableAbilities)
                    zone.SetDisableAbilities(false);
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private static Dictionary<int, int> BuildCharZoneMap(GameState gameState, IEnumerable<int> charIds)
        {
            var map = new Dictionary<int, int>();
            foreach (var charId in charIds)
            {
                var state = gameState.GetCharacterState(charId);
                if (state != null)
                    map[charId] = state.CurrentZone;
            }
            return map;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_characterRegistry == null)
                Debug.LogWarning("[CampaignCharacterSpawner] CharacterRegistry 미연결.");
            if (_zoneLayout == null)
                Debug.LogWarning("[CampaignCharacterSpawner] ZoneLayout 미연결.");
        }
#endif
    }
}