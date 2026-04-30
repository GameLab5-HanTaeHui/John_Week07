using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 개별 기록장 패널 7개를 관리하는 매니저입니다.
    /// 캠페인 씬 전용입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   캐릭터 클릭 시 해당 캐릭터의 CharacterRecordPanel을 엽니다.
    ///   현재 열려있는 패널이 있으면 닫고 새 패널을 엽니다.
    ///   FragmentCollector 이벤트를 수신해 열린 패널을 실시간 갱신합니다.
    ///   CampaignPanelManager에 닫기 콜백을 등록해 바탕화면 클릭 시 닫힙니다.
    ///
    /// ─── 변경 사항 ───────────────────────────────────────────────────────
    ///   IsPhase2Active 체크 제거 — 캠페인 씬은 항상 Phase2
    ///   OnPhase2Entered 구독/해제 제거
    ///   Start()에서 바로 슬롯 초기화
    ///   OpenPanel() / CloseCurrentPanel() → CampaignPanelManager 연동 추가
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Panels [7]           → CharacterRecordPanel 7개 (CharacterId 1~7 순서)
    ///   Fragment Collector   → _CampaignSystem/FragmentCollector
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

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly Dictionary<int, CharacterRecordPanel> _panelMap = new();
        private readonly Dictionary<int, string> _collectedNames = new();

        private CharacterRecordPanel _currentPanel;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // CharacterId 1~7 → 인덱스 0~6 매핑
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] == null) continue;
                _panelMap[i + 1] = _panels[i];
            }
        }

        private void Start()
        {
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;

            // 저장된 이름 복원
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
                foreach (var entry in saveData.collectedNames)
                    if (!string.IsNullOrEmpty(entry.name))
                        _collectedNames[entry.characterId] = entry.name;

            // ★ 캠페인 씬 진입 시 바로 슬롯 초기화 (OnPhase2Entered 대기 불필요)
            StartCoroutine(InitializeSlotsAfterActivation());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;

            // 패널이 열린 채로 씬 종료되면 CampaignPanelManager 콜백 해제
            CampaignPanelManager.Instance?.UnregisterPanel(CloseCurrentPanel);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 개별 기록장을 엽니다.
        /// 같은 패널 클릭 시 토글 닫기.
        /// 다른 패널이 열려있으면 먼저 닫고 새 패널을 엽니다.
        /// CampaignPanelManager에 닫기 콜백을 등록합니다.
        /// </summary>
        public void OpenPanel(int characterId)
        {
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
                CampaignPanelManager.Instance?.UnregisterPanel(CloseCurrentPanel);
                return;
            }

            // 다른 패널이 열려있으면 먼저 닫기
            if (_currentPanel != null && _currentPanel.IsOpen)
            {
                _currentPanel.Close();
                CampaignPanelManager.Instance?.UnregisterPanel(CloseCurrentPanel);
            }

            _currentPanel = panel;
            panel.Open(characterId);

            // ★ CampaignPanelManager에 닫기 콜백 등록
            CampaignPanelManager.Instance?.RegisterPanel(CloseCurrentPanel);

            Debug.Log($"[CharacterRecordPanelManager] 기록장 열림 — #{characterId}");
        }

        /// <summary>현재 열린 패널을 닫습니다. CampaignPanelManager 콜백으로도 호출됩니다.</summary>
        public void CloseCurrentPanel()
        {
            if (_currentPanel == null || !_currentPanel.IsOpen) return;
            _currentPanel.Close();
            _currentPanel = null;
            CampaignPanelManager.Instance?.UnregisterPanel(CloseCurrentPanel);
        }

        /// <summary>
        /// 캐릭터 이름을 수집 목록에 등록합니다.
        /// DialogueTriggerManager에서 이름 공개 시 호출합니다.
        /// </summary>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _collectedNames[characterId] = name;
            _currentPanel?.RefreshIfCurrent(characterId);

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            bool found = false;
            foreach (var entry in saveData.collectedNames)
            {
                if (entry.characterId != characterId) continue;
                entry.name = name;
                found = true;
                break;
            }
            if (!found)
                saveData.collectedNames.Add(new CollectedNameEntry
                { characterId = characterId, name = name });

            CampaignSaveManager.Instance.Save(saveData);
        }

        /// <summary>수집된 캐릭터 이름을 반환합니다. 미수집이면 빈 문자열.</summary>
        public string GetCollectedName(int characterId)
            => _collectedNames.TryGetValue(characterId, out var name) ? name : "";

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 패널 오브젝트가 활성화될 때까지 대기 후 슬롯을 초기화합니다.
        /// Phase2UITransition의 슬라이드업 완료를 기다립니다.
        /// </summary>
        private IEnumerator InitializeSlotsAfterActivation()
        {
            yield return null; // Awake 보장

            const float timeout = 3f;
            float elapsed = 0f;
            bool allReady = false;

            while (!allReady && elapsed < timeout)
            {
                allReady = true;
                foreach (var kvp in _panelMap)
                {
                    if (!kvp.Value.gameObject.activeInHierarchy)
                    {
                        allReady = false;
                        break;
                    }
                }

                if (!allReady)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }

            foreach (var kvp in _panelMap)
                kvp.Value.InitializeSlots(kvp.Key);

            Debug.Log($"[CharacterRecordPanelManager] 슬롯 초기화 완료 (대기: {elapsed:F2}초)");
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
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return -1;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return -1;
            return int.TryParse(fragmentId.Substring(1, idx - 1), out int id) ? id : -1;
        }
    }
}