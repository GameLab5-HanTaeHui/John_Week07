using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 CharacterSpawner입니다.
    ///
    /// ─── 위치 이동 규칙 (절대 원칙) ──────────────────────────────────────────
    ///   캐릭터 위치는 다음 두 경우에만 변경됩니다.
    ///   1. SpawnAll        — 게임 최초 시작 시 초기 배치
    ///   2. SyncViewsToGameState (강제퇴고 전용) — GFC.HandleLoopReset에서만 호출
    ///
    ///   그 외 모든 상황(일반 퇴고, 부활, 상태 갱신)에서는
    ///   위치를 절대 변경하지 않습니다.
    ///
    /// ─── 메서드별 역할 ───────────────────────────────────────────────────────
    ///   SpawnAll                — 최초 스폰 + InitSlots + 위치 스냅
    ///   SyncViewsToGameState    — 강제퇴고 시 전원 초기 위치로 스냅 (위치 변경 O)
    ///   ResetSlots              — 슬롯 맵만 재초기화 (위치 변경 X)
    ///   RefreshAllViews         — Init(새 상태) + RefreshView만 수행 (위치 변경 X)
    ///   ApplyZoneRulesToGameState — Zone 규칙 적용 (위치 변경 X)
    /// </summary>
    public class CampaignCharacterSpawner : MonoBehaviour
    {
        [SerializeField] private CharacterRegistry _characterRegistry;
        [SerializeField] private CampaignZoneLayout _zoneLayout;

        // ── 공개 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// ★ 강제퇴고 전용 — 전원을 GameState Zone 기준 위치로 스냅합니다.
        /// 일반 퇴고/부활에서는 절대 호출하지 마세요.
        /// 반드시 ResetSlots() 이후에 호출하세요.
        /// </summary>
        public void SyncViewsToGameState(GameState gameState, Dictionary<int, CharacterView> views)
        {
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            var positions = _zoneLayout.ComputeSlotPositions(charZoneMap);
            var rotations = _zoneLayout.ComputeSlotRotations(charZoneMap);

            foreach (var kv in views)
            {
                var state = gameState.GetCharacterState(kv.Key);
                if (state != null) kv.Value.Init(state);

                if (positions.TryGetValue(kv.Key, out var pos)) kv.Value.SnapToPosition(pos);
                if (rotations.TryGetValue(kv.Key, out var rot)) kv.Value.SnapToRotation(rot);

                kv.Value.RefreshView();
            }
        }

        /// <summary>
        /// 슬롯 맵만 재초기화합니다. 위치는 변경하지 않습니다.
        /// 강제퇴고 시 SyncViewsToGameState() 직전에 호출합니다.
        /// </summary>
        public void ResetSlots(GameState gameState, Dictionary<int, CharacterView> views)
        {
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            _zoneLayout.InitSlots(charZoneMap);
        }

        /// <summary>
        /// 일반 퇴고/부활 시 호출합니다.
        /// 모든 View를 새 GameState 기준으로 Init() + RefreshView()합니다.
        /// ★ 위치는 절대 변경하지 않습니다.
        /// </summary>
        public void RefreshAllViews(GameState gameState, Dictionary<int, CharacterView> views)
        {
            foreach (var kv in views)
            {
                var state = gameState.GetCharacterState(kv.Key);
                if (state != null) kv.Value.Init(state);
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

            // ★ SpawnAll에서만 InitSlots + 위치 스냅
            var charZoneMap = BuildCharZoneMap(gameState, views.Keys);
            _zoneLayout.InitSlots(charZoneMap);
            var positions = _zoneLayout.ComputeSlotPositions(charZoneMap);
            var rotations = _zoneLayout.ComputeSlotRotations(charZoneMap);

            foreach (var kv in views)
            {
                if (positions.TryGetValue(kv.Key, out var pos)) kv.Value.SnapToPosition(pos);
                if (rotations.TryGetValue(kv.Key, out var rot)) kv.Value.SnapToRotation(rot);
                kv.Value.RefreshView();
            }

            return views;
        }

        /// <summary>
        /// 능력 무효화 구역을 항상 비활성(false)으로 강제 적용합니다.
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

        private static Dictionary<int, int> BuildCharZoneMap(
            GameState gameState, IEnumerable<int> charIds)
        {
            var map = new Dictionary<int, int>();
            foreach (var charId in charIds)
            {
                var state = gameState.GetCharacterState(charId);
                if (state != null) map[charId] = state.CurrentZone;
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