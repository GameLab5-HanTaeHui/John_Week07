using UnityEngine;

/// <summary>
/// 스테이지 초기 배치를 시드 기반으로 관리하는 ScriptableObject입니다.
///
/// 배치 설계 워크플로우:
///   1. 메뉴 MafiaGame → Stage Setup Tool 에서 캐릭터·직업·Zone을 지정
///   2. "시드 굽기" 버튼으로 이 에셋의 Seed를 자동 기입
///   3. UseRandomSeed = false, 지정 시드로 항상 같은 배치 재현
///
/// 랜덤 배치:
///   UseRandomSeed = true → 런타임에 자동 생성
/// </summary>
[CreateAssetMenu(fileName = "StageSetupConfig", menuName = "MafiaGame/StageSetupConfig")]
public class StageSetupConfig : ScriptableObject
{
    [Header("에디터 미리보기용 참조")]
    [SerializeField] private CharacterRegistry _characterRegistry;
    [SerializeField] private StageRoleConfig   _stageRoleConfig;

    [Header("시드 설정")]
    [SerializeField] private bool _useRandomSeed = true;
    [SerializeField] private int  _seed;

    [Header("배치 미리보기 (읽기 전용 — 에디터 버튼으로 갱신)")]
    [SerializeField] private CharacterSetupEntry[] _previewSetup;

    // ── 런타임 API ─────────────────────────────────────────────────────────

    public bool UseRandomSeed => _useRandomSeed;
    public int  Seed          => _seed;

    /// <summary>
    /// GameSetupState에서 호출합니다.
    /// UseRandomSeed = true면 유효한 범위의 난수를 생성해 캐시 후 반환합니다.
    /// </summary>
    public int GetOrGenerateSeed(int characterCount, int roleCount)
    {
        if (_useRandomSeed)
            _seed = Random.Range(0, SeedEncoder.GetMaxSeed(characterCount, roleCount, GameState.ZoneCount));
        return _seed;
    }

    // ── 에디터 전용 API ────────────────────────────────────────────────────

    public CharacterRegistry     CharacterRegistry => _characterRegistry;
    public StageRoleConfig       StageRoleConfig   => _stageRoleConfig;
    public CharacterSetupEntry[] PreviewSetup      => _previewSetup;

#if UNITY_EDITOR
    /// <summary>시드 → 미리보기 배열 갱신. 에디터 버튼에서 호출합니다.</summary>
    public void RefreshPreviewFromSeed()
    {
        if (_characterRegistry == null || _stageRoleConfig == null) return;

        int charCount = _characterRegistry.Characters.Count;
        int roleCount = _stageRoleConfig.Roles.Count;
        if (charCount == 0 || roleCount == 0) return;

        SeedEncoder.Decode(_seed, charCount, roleCount, GameState.ZoneCount,
            out var rolePerm, out var zones);

        _previewSetup = new CharacterSetupEntry[charCount];
        var chars = _characterRegistry.Characters;
        for (int i = 0; i < charCount; i++)
        {
            _previewSetup[i] = new CharacterSetupEntry
            {
                characterId = chars[i].CharacterId,
                roleIndex   = rolePerm[i],
                zoneId      = zones[i]
            };
        }
    }

    /// <summary>미리보기 배열 → 시드 인코딩. 에디터 버튼에서 호출합니다.</summary>
    public int EncodeFromPreview()
    {
        if (_previewSetup == null || _previewSetup.Length == 0
            || _characterRegistry == null || _stageRoleConfig == null) return _seed;

        int charCount = _previewSetup.Length;
        var rolePerm  = new int[charCount];
        var zones     = new int[charCount];

        var chars = _characterRegistry.Characters;
        for (int i = 0; i < chars.Count && i < charCount; i++)
        {
            int charId = chars[i].CharacterId;
            foreach (var entry in _previewSetup)
            {
                if (entry.characterId == charId)
                {
                    rolePerm[i] = entry.roleIndex;
                    zones[i]    = entry.zoneId;
                    break;
                }
            }
        }

        _seed = SeedEncoder.Encode(rolePerm, zones,
            _stageRoleConfig.Roles.Count, GameState.ZoneCount);
        return _seed;
    }

    /// <summary>
    /// Stage Setup Tool에서 시드를 직접 기입합니다.
    /// UseRandomSeed를 false로 전환해 지정 시드가 사용되도록 합니다.
    /// </summary>
    public void BakeSeed(int seed)
    {
        _useRandomSeed = false;
        _seed          = seed;
        RefreshPreviewFromSeed();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
