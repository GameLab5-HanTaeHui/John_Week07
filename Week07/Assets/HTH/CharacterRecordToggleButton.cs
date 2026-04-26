using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 개별 기록장을 열고 닫는 토글 버튼입니다.
    ///
    /// ─── 동작 방식 ───────────────────────────────────────────────────────
    ///   버튼 클릭 → 패널 닫힘 상태 → 슬라이드업 등장
    ///   버튼 클릭 → 패널 열림 상태 → 슬라이드다운 닫힘
    ///   다른 캐릭터 버튼 클릭 → 현재 패널 닫힘 → 새 패널 열림
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   인게임 씬에서 캐릭터별로 배치합니다.
    ///   Phase2 진입 전에는 버튼이 비활성화 상태입니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Character Id → 이 버튼이 열 캐릭터 ID (1~7)
    ///   Button       → Button 컴포넌트 (자동 참조)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class CharacterRecordToggleButton : MonoBehaviour
    {
        [Tooltip("이 버튼이 열 캐릭터 ID (1~7)")]
        [SerializeField] private int _characterId;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);

            // Phase2 진입 전 비활성화
            _button.interactable = true;
        }

        private void Start()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveListener(OnClicked);

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;
        }

        // ── 이벤트 ───────────────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            // Phase2 진입 시 버튼 활성화
            if (_button != null)
                _button.interactable = true;
        }

        private void OnClicked()
        {
            CharacterRecordPanelManager.Instance?.OpenPanel(_characterId);
        }
    }
}