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

        /// <summary>버튼 클릭 후 재클릭을 막을 시간(초)입니다.</summary>
        [SerializeField] private float _clickCooldown = 0.5f;

        /// <summary>현재 쿨다운 중인지 여부입니다.</summary>
        private bool _isCoolingDown;

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
            // 애니메이션 진행 중 재클릭 방지
            if (_isCoolingDown) return;
            StartCoroutine(ClickCooldownCoroutine());
            CharacterRecordPanelManager.Instance?.OpenPanel(_characterId);
        }
        /// <summary>
        /// 클릭 후 _clickCooldown 동안 재클릭을 차단합니다.
        /// 애니메이션 duration과 동일하거나 약간 길게 설정하세요.
        /// </summary>
        private System.Collections.IEnumerator ClickCooldownCoroutine()
        {
            _isCoolingDown = true;
            yield return new WaitForSeconds(_clickCooldown);
            _isCoolingDown = false;
        }
    }
}