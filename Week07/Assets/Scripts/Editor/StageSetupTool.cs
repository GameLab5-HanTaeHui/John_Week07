using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터·직업·Zone을 직접 지정하고 시드로 구워 StageSetupConfig에 기입하는 에디터 툴입니다.
///
/// 열기: 메뉴 MafiaGame → Stage Setup Tool
///
/// 워크플로우:
///   1. CharacterRegistry / StageRoleConfig / 대상 StageSetupConfig 연결
///   2. "배치 초기화" 버튼으로 테이블 생성
///   3. 각 행에서 직업·Zone 지정
///   4. "시드 굽기" 버튼 → 시드를 계산해 StageSetupConfig에 자동 기입
/// </summary>
public class StageSetupTool : EditorWindow
{
    // ── 참조 ─────────────────────────────────────────────────────────────────
    private CharacterRegistry _characterRegistry;
    private StageRoleConfig   _stageRoleConfig;
    private StageSetupConfig  _targetConfig;

    // ── 툴 내부 상태 ──────────────────────────────────────────────────────────
    private struct ToolEntry
    {
        public CharacterData character;
        public int           roleIndex;   // StageRoleConfig.Roles 인덱스
        public int           zoneId;
    }

    private List<ToolEntry> _entries    = new();
    private Vector2         _scrollPos;
    private string          _statusMsg  = "";
    private MessageType     _statusType = MessageType.None;

    // ── 메뉴 ─────────────────────────────────────────────────────────────────

    [MenuItem("MafiaGame/Stage Setup Tool")]
    public static void Open()
    {
        var window = GetWindow<StageSetupTool>("Stage Setup Tool");
        window.minSize = new Vector2(560, 400);
    }

    // ── GUI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        DrawReferences();
        EditorGUILayout.Space(6);

        bool ready = _characterRegistry != null && _stageRoleConfig != null;

        GUI.enabled = ready;
        if (GUILayout.Button("배치 테이블 초기화 (캐릭터 수 기준)", GUILayout.Height(28)))
            InitEntries();
        GUI.enabled = true;

        if (!ready)
        {
            EditorGUILayout.HelpBox("CharacterRegistry와 StageRoleConfig를 먼저 연결하세요.", MessageType.Warning);
            return;
        }

        if (_entries.Count == 0)
        {
            EditorGUILayout.HelpBox("위 버튼을 눌러 테이블을 초기화하세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);
        DrawTable();
        EditorGUILayout.Space(8);
        DrawBakeSection();

        if (!string.IsNullOrEmpty(_statusMsg))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_statusMsg, _statusType);
        }
    }

    // ── 참조 섹션 ─────────────────────────────────────────────────────────────

    private void DrawReferences()
    {
        EditorGUILayout.LabelField("── 참조 설정 ──", EditorStyles.boldLabel);

        var newRegistry = (CharacterRegistry)EditorGUILayout.ObjectField(
            "Character Registry", _characterRegistry, typeof(CharacterRegistry), false);
        if (newRegistry != _characterRegistry)
        {
            _characterRegistry = newRegistry;
            _entries.Clear();
        }

        var newRoleConfig = (StageRoleConfig)EditorGUILayout.ObjectField(
            "Stage Role Config", _stageRoleConfig, typeof(StageRoleConfig), false);
        if (newRoleConfig != _stageRoleConfig)
        {
            _stageRoleConfig = newRoleConfig;
            _entries.Clear();
        }

        _targetConfig = (StageSetupConfig)EditorGUILayout.ObjectField(
            "Target Setup Config", _targetConfig, typeof(StageSetupConfig), false);
    }

    // ── 배치 테이블 ───────────────────────────────────────────────────────────

    private void DrawTable()
    {
        EditorGUILayout.LabelField("── 배치 설정 ──", EditorStyles.boldLabel);

        var roles    = _stageRoleConfig.Roles;
        var roleNames = new string[roles.Count];
        for (int i = 0; i < roles.Count; i++)
            roleNames[i] = roles[i].RoleName;

        // 헤더
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("#",      EditorStyles.boldLabel, GUILayout.Width(24));
        GUILayout.Label("캐릭터", EditorStyles.boldLabel, GUILayout.Width(110));
        GUILayout.Label("직업",   EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("Zone",   EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        DrawLine();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{i + 1}", GUILayout.Width(24));
            GUILayout.Label(e.character != null ? e.character.CharacterName : "—",
                GUILayout.Width(110));

            // 직업 드롭다운
            int newRole = EditorGUILayout.Popup(e.roleIndex, roleNames, GUILayout.Width(160));

            // Zone 슬라이더 (0 ~ ZoneCount-1)
            int newZone = EditorGUILayout.IntField(e.zoneId, GUILayout.Width(50));
            newZone = Mathf.Clamp(newZone, 0, GameState.ZoneCount - 1);

            EditorGUILayout.EndHorizontal();

            if (newRole != e.roleIndex || newZone != e.zoneId)
                _entries[i] = new ToolEntry
                    { character = e.character, roleIndex = newRole, zoneId = newZone };
        }

        EditorGUILayout.EndScrollView();

        // 중복 직업 경고
        CheckDuplicateRoles(roleNames);
    }

    // ── 시드 굽기 ─────────────────────────────────────────────────────────────

    private void DrawBakeSection()
    {
        EditorGUILayout.LabelField("── 시드 굽기 ──", EditorStyles.boldLabel);

        bool hasTarget = _targetConfig != null;

        GUI.enabled = hasTarget;
        if (GUILayout.Button("🔥  시드 굽기 → StageSetupConfig에 기입", GUILayout.Height(32)))
            BakeSeed();
        GUI.enabled = true;

        if (!hasTarget)
            EditorGUILayout.HelpBox("Target Setup Config를 연결해야 시드를 기입할 수 있습니다.", MessageType.Warning);
    }

    private void BakeSeed()
    {
        int charCount = _entries.Count;
        int roleCount = _stageRoleConfig.Roles.Count;

        var rolePerm = new int[charCount];
        var zones    = new int[charCount];

        for (int i = 0; i < charCount; i++)
        {
            rolePerm[i] = _entries[i].roleIndex;
            zones[i]    = _entries[i].zoneId;
        }

        int seed = SeedEncoder.Encode(rolePerm, zones, roleCount, GameState.ZoneCount);

        Undo.RecordObject(_targetConfig, "Bake Stage Setup Seed");
        _targetConfig.BakeSeed(seed);

        _statusMsg  = $"시드 {seed} 기입 완료. (UseRandomSeed = false로 전환됨)";
        _statusType = MessageType.Info;

        Debug.Log($"[StageSetupTool] 시드 굽기 완료 — Seed: {seed}");
        Repaint();
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private void InitEntries()
    {
        _entries.Clear();
        var chars = _characterRegistry.Characters;
        for (int i = 0; i < chars.Count; i++)
        {
            _entries.Add(new ToolEntry
            {
                character = chars[i],
                roleIndex = i % _stageRoleConfig.Roles.Count,
                zoneId    = 0
            });
        }
        _statusMsg = "";
    }

    private void CheckDuplicateRoles(string[] roleNames)
    {
        var seen = new HashSet<int>();
        var dups = new HashSet<int>();
        foreach (var e in _entries)
        {
            if (!seen.Add(e.roleIndex)) dups.Add(e.roleIndex);
        }
        if (dups.Count > 0)
        {
            var names = new System.Text.StringBuilder();
            foreach (int idx in dups)
                names.Append($"'{roleNames[idx]}' ");
            EditorGUILayout.HelpBox($"중복 직업: {names}— 같은 직업이 여러 캐릭터에 배정되어 있습니다.", MessageType.Warning);
        }
    }

    private static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(2);
    }
}
