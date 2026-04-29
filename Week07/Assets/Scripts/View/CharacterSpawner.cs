using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시작 시 CharacterRegistry의 프리팹을 인스턴스화하고
/// ZoneLayout을 참조해 초기 구역 위치에 배치합니다.
/// 기본모드 전용입니다. 캠페인 모드는 CampaignCharacterSpawner를 사용하세요.
///
/// Inspector 필수 연결:
///   CharacterRegistry → 7개 캐릭터 데이터
///   ZoneLayout        → 씬의 구역 위치 마커
/// </summary>
public class CharacterSpawner : MonoBehaviour
{
    [SerializeField] private CharacterRegistry _characterRegistry;
    [SerializeField] private ZoneLayout _zoneLayout;

    /// <summary>
    /// 모든 CharacterView를 GameState의 현재 Zone 슬롯 위치로 스냅합니다.
    /// 루프 시작 시 뷰를 초기 배치 위치로 복원할 때 호출합니다.
    /// </summary>
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

    /// <summary>
    /// 모든 캐릭터 프리팹을 스폰하고 초기 구역 슬롯 위치에 배치합니다.
    /// </summary>
    public Dictionary<int, CharacterView> SpawnAll(GameState gameState)
    {
        var views = new Dictionary<int, CharacterView>();

        foreach (var data in _characterRegistry.Characters)
        {
            if (data.Prefab == null)
            {
                Debug.LogWarning($"[CharacterSpawner] '{data.CharacterName}'의 Prefab이 연결되지 않았습니다.");
                continue;
            }

            var characterState = gameState.GetCharacterState(data.CharacterId);
            if (characterState == null)
            {
                Debug.LogWarning($"[CharacterSpawner] CharacterId={data.CharacterId}에 해당하는 CharacterState가 없습니다.");
                continue;
            }

            var instance = Instantiate(data.Prefab, Vector3.zero, Quaternion.identity);
            instance.name = $"Character_{data.CharacterName}";

            var view = instance.GetComponent<CharacterView>();
            if (view == null)
            {
                Debug.LogError($"[CharacterSpawner] '{data.CharacterName}' 프리팹에 CharacterView가 없습니다.");
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
    /// ZoneLayout의 능력 비활성화 규칙을 GameState에 적용합니다.
    /// SpawnAll 후와 루프 리셋(HandleLoopReset) 시 호출하세요.
    /// </summary>
    public void ApplyZoneRulesToGameState(GameState gameState)
    {
        var disabled = new bool[GameState.ZoneCount];
        var effects = new ZoneEffectConfig[GameState.ZoneCount];

        for (int i = 0; i < GameState.ZoneCount; i++)
        {
            var zone = _zoneLayout?.GetZonePoint(i);
            disabled[i] = zone != null && zone.DisableAbilities;
            effects[i] = zone?.ZoneEffect;
        }

        gameState.InitZoneRules(disabled);
        gameState.InitZoneEffects(effects);
    }

    // ── Private ──────────────────────────────────────────────────────────────

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
            Debug.LogWarning("[CharacterSpawner] CharacterRegistry가 연결되지 않았습니다.");
        if (_zoneLayout == null)
            Debug.LogWarning("[CharacterSpawner] ZoneLayout이 연결되지 않았습니다.");
    }
#endif
}