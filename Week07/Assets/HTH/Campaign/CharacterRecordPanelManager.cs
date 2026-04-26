using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 개별 기록장 패널 7개를 관리하는 매니저입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   Phase2에서 캐릭터 클릭 시 해당 캐릭터의 CharacterRecordPanel을 엽니다.
    ///   현재 열려있는 패널이 있으면 닫고 새 패널을 엽니다.
    ///   FragmentCollector 이벤트를 수신해 열린 패널을 실시간 갱신합니다.
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Panels [7]           → CharacterRecordPanel 7개 (CharacterId 1~7 순서)
    ///   Fragment Collector   → _CampaignSystem/FragmentCollector
    ///
    /// ─── 외부에서 호출 ───────────────────────────────────────────────────
    ///   CharacterRecordPanelManager.Instance.OpenPanel(characterId);
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterRecordPanelManager : MonoBehaviour
    {
        public static CharacterRecordPanelManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────

        [Header("개별 기록장 패널 (CharacterId 1~7 순서)")]
        [Tooltip("Element 0 = CharacterId 1, Element 6 = CharacterId 7")]
        [SerializeField] private CharacterRecordPanel[] _panels = new CharacterRecordPanel[7];

        [Header("컴포넌트 참조")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        // 수집된 캐릭터 이름 (characterId → 이름)
        // DialogueTriggerManager.RevealCharacterNamesFromLines()에서 등록됩니다.
        private readonly Dictionary<int, string> _collectedNames = new();

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // characterId → Panel 매핑
        private readonly Dictionary<int, CharacterRecordPanel> _panelMap = new();

        private CharacterRecordPanel _currentPanel;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // CharacterId 1~7을 인덱스 0~6에 매핑
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] == null) continue;
                int characterId = i + 1;
                _panelMap[characterId] = _panels[i];
            }
        }

        private void Start()
        {
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;

            // Phase2 진입 시에만 활성화
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 개별 기록장을 엽니다.
        /// Phase2에서 캐릭터 클릭 시 호출합니다.
        /// 이미 같은 패널이 열려있으면 닫습니다 (토글).
        /// 다른 패널이 열려있으면 먼저 닫고 새 패널을 엽니다.
        /// </summary>
        public void OpenPanel(int characterId)
        {
            if (!CampaignModeManager.IsPhase2Active) return;

            if (!_panelMap.TryGetValue(characterId, out var panel))
            {
                Debug.LogWarning($"[CharacterRecordPanelManager] CharacterId={characterId} 패널 없음");
                return;
            }

            // 같은 패널 클릭 → 토글 닫기
            if (_currentPanel == panel && panel.IsOpen)
            {
                panel.Close();
                _currentPanel = null;
                return;
            }

            // 다른 패널이 열려있으면 먼저 닫기
            if (_currentPanel != null && _currentPanel.IsOpen)
                _currentPanel.Close();

            _currentPanel = panel;
            panel.Open(characterId);

            Debug.Log($"[CharacterRecordPanelManager] 기록장 열림 — #{characterId}");
        }

        /// <summary>현재 열린 패널을 닫습니다.</summary>
        public void CloseCurrentPanel()
        {
            if (_currentPanel == null || !_currentPanel.IsOpen) return;
            _currentPanel.Close();
            _currentPanel = null;
        }
        /// <summary>
        /// 캐릭터 이름을 등록합니다.
        /// DialogueTriggerManager에서 이름 공개 시 호출합니다.
        /// </summary>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_collectedNames.ContainsKey(characterId)) return;

            _collectedNames[characterId] = name;

            // 현재 열려있는 패널이 해당 캐릭터면 헤더 갱신
            if (_currentPanel != null && _currentPanel.IsOpen)
                _currentPanel.RefreshIfCurrent(characterId);
        }

        /// <summary>수집된 캐릭터 이름을 반환합니다. 미수집 시 null.</summary>
        public string GetCollectedName(int characterId)
        {
            _collectedNames.TryGetValue(characterId, out string name);
            return name;
        }

        // ── Private ──────────────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            // Phase2 진입 시 모든 패널 초기화
            CloseCurrentPanel();
        }

        private void OnFragmentCollected(string fragmentId)
        {
            if (_currentPanel == null || !_currentPanel.IsOpen) return;

            int charId = ParseCharacterIdFromFragment(fragmentId);
            if (charId < 0) return;

            _currentPanel.RefreshIfCurrent(charId);
        }

        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;

            // char{id} 형식
            const string charMarker = "_char";
            int charIdx = fragmentId.IndexOf(charMarker, System.StringComparison.Ordinal);
            if (charIdx >= 0)
            {
                int start = charIdx + charMarker.Length;
                int end = fragmentId.IndexOf('_', start);
                if (end < 0) end = fragmentId.Length;
                if (int.TryParse(fragmentId.Substring(start, end - start), out int charId))
                    return charId;
            }

            // group_{첫번째Id}_... 형식 폴백
            const string groupMarker = "_group_";
            int groupIdx = fragmentId.IndexOf(groupMarker, System.StringComparison.Ordinal);
            if (groupIdx >= 0)
            {
                int start = groupIdx + groupMarker.Length;
                int end = fragmentId.IndexOf('_', start);
                if (end < 0) end = fragmentId.Length;
                if (int.TryParse(fragmentId.Substring(start, end - start), out int groupId))
                    return groupId;
            }

            return -1;
        }
    }
}