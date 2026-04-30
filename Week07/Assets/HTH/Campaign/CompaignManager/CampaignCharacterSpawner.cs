using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 CharacterSpawner입니다.
    /// 기본모드 CharacterSpawner와 완전히 분리됩니다.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────────
    ///   ApplyZoneRulesToGameState — 능력 무효화 구역을 항상 비활성(false)으로 강제.
    ///     Phase2에서는 Zone의 DisableAbilities 설정을 무시합니다.
    ///   DisableAllAbilityZones() 메서드 유지 (CampaignModeManager 연계).
    ///
    /// Inspector 필수 연결:
    ///   CharacterRegistry → 7개 캐릭터 데이터
    ///   ZoneLayout        → 씬의 구역 위치 마커
    /// </summary>
    public class CampaignCharacterSpawner : MonoBehaviour
    {
        [SerializeField] private CharacterRegistry _characterRegistry;
        [SerializeField] private CampaignZoneLayout _zoneLayout;

        /// <summary>모든 CharacterView를 GameState의 Zone 슬롯 위치로 스냅합니다.</summary>
        public void SyncViewsToGameState(GameState gameState, Dictionary<int, CharacterView> views)
        {
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            _zoneLayout.InitSlots(charZoneMap);
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
        /// ★ Phase2 전용 — 능력 무효화 구역을 항상 비활성(false)으로 강제 적용합니다.
        /// ZonePoint.DisableAbilities 설정을 무시합니다.
        /// </summary>
        public void ApplyZoneRulesToGameState(GameState gameState)
        {
            var disabled = new bool[GameState.ZoneCount];   // 전부 false
            var effects = new ZoneEffectConfig[GameState.ZoneCount];

            for (int i = 0; i < GameState.ZoneCount; i++)
            {
                var zone = _zoneLayout?.GetZonePoint(i);
                effects[i] = zone?.ZoneEffect;
                // disabled[i] = false → Phase2에서는 모든 구역 능력 활성
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