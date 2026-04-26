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

        // ── 내부 상태 ─────────────────────────────────────────────────────

        /// <summary>characterId → Panel 매핑입니다.</summary>
        private readonly Dictionary<int, CharacterRecordPanel> _panelMap = new();

        /// <summary>현재 열린 패널입니다.</summary>
        private CharacterRecordPanel _currentPanel;

        /// <summary>
        /// 수집된 캐릭터 이름 캐시입니다.
        /// 대화 중 이름이 공개되면 RegisterCharacterName()으로 등록합니다.
        /// </summary>
        private readonly Dictionary<int, string> _collectedNames = new();


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
        /// 캐릭터 이름을 수집 목록에 등록합니다.
        /// 대화 중 RevealCharacterId가 있을 때 DialogueTriggerManager에서 호출합니다.
        /// </summary>
        /// <param name="characterId">공개된 캐릭터 ID</param>
        /// <param name="name">공개된 이름</param>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _collectedNames[characterId] = name;
            _currentPanel?.RefreshIfCurrent(characterId);

            // JSON 저장 업데이트
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            // 기존 항목 업데이트 또는 추가
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

        /// <summary>
        /// 수집된 캐릭터 이름을 반환합니다.
        /// 미수집이면 빈 문자열을 반환합니다.
        /// </summary>
        /// <param name="characterId">조회할 캐릭터 ID</param>
        public string GetCollectedName(int characterId)
        {
            return _collectedNames.TryGetValue(characterId, out var name) ? name : "";
        }

        // ── Private ──────────────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            CloseCurrentPanel();

            // 저장된 이름 복원
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
            {
                foreach (var entry in saveData.collectedNames)
                    if (!string.IsNullOrEmpty(entry.name))
                        _collectedNames[entry.characterId] = entry.name;
            }

            StartCoroutine(InitializeSlotsAfterActivation());
        }
        /// <summary>
        /// 패널 오브젝트가 활성화될 때까지 대기 후 슬롯을 초기화합니다.
        /// Phase2UITransition의 슬라이드업 애니메이션 완료를 기다립니다.
        /// </summary>
        private System.Collections.IEnumerator InitializeSlotsAfterActivation()
        {
            // 모든 패널이 활성화될 때까지 대기
            // Phase2UITransition에서 SetActive(true) 후 슬라이드업하므로
            // 한 프레임 대기로 Awake() 실행 보장
            yield return null;

            // 패널이 활성화 상태가 될 때까지 추가 대기 (최대 3초)
            float timeout = 3f;
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

            // 슬롯 초기화
            foreach (var kvp in _panelMap)
                kvp.Value.InitializeSlots(kvp.Key);

            Debug.Log($"[CharacterRecordPanelManager] 전체 패널 슬롯 초기화 완료 " +
                      $"(대기 시간: {elapsed:F2}초)");
        }

        private void OnFragmentCollected(string fragmentId)
        {
            if (_currentPanel == null || !_currentPanel.IsOpen) return;

            int charId = ParseCharacterIdFromFragment(fragmentId);
            if (charId < 0) return;

            _currentPanel.RefreshIfCurrent(charId);
        }

        /// <summary>
        /// P01_01 형식의 FragmentId에서 캐릭터 ID를 파싱합니다.
        /// 예: "P01_01" → 1 / "P07_05" → 7
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;

            // P{cc}_{ii} 형식 — 예: P01_01, P07_05
            if (fragmentId.Length >= 3 && fragmentId[0] == 'P')
            {
                int underscoreIdx = fragmentId.IndexOf('_');
                if (underscoreIdx > 1)
                {
                    string charPart = fragmentId.Substring(1, underscoreIdx - 1);
                    if (int.TryParse(charPart, out int charId))
                        return charId;
                }
            }

            Debug.LogWarning($"[CharacterRecordPanelManager] FragmentId 파싱 실패 — {fragmentId}\n" +
                             "P{{cc}}_{{ii}} 형식을 사용해야 합니다. 예: P01_01");
            return -1;
        }
    }
}