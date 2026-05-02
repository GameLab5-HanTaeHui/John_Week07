using UnityEngine;
using UnityEngine.UI;
using HTH.Campaign;

namespace HTH.Tutorial
{
    /// <summary>
    /// [튜토리얼 전용] 캐릭터 개별 기록장을 열고 닫는 토글 버튼입니다.
    /// TutorialManager의 권한을 엄격하게 검사하여 허락된 타이밍에만 작동합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class TutorialCharacterRecordToggleButton : MonoBehaviour
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

            // 튜토리얼에서는 기본적으로 켜두고 권한(Permission)으로 통제합니다.
            _button.interactable = true;
        }

        // ── 이벤트 ───────────────────────────────────────────────────────

        private void OnClicked()
        {
            // ── [튜토리얼 철벽 방어선] ──────────────────────────────────────────

            // 1. 다이어리(캐릭터 카드) 토글 권한이 없다면 클릭을 완벽히 무시합니다.
            if (!TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.CharacterCardToggle))
                return;

            // ──────────────────────────────────────────────────────────────────

            // 애니메이션 진행 중 재클릭 방지
            if (_isCoolingDown) return;
            StartCoroutine(ClickCooldownCoroutine());

            // 본편과 동일한 매니저를 호출하여 패널을 엽니다.
            TutorialCharacterRecordPanelManager.Instance?.OpenPanel(_characterId);
        }

        /// <summary>
        /// 클릭 후 _clickCooldown 동안 재클릭을 차단합니다.
        /// </summary>
        private System.Collections.IEnumerator ClickCooldownCoroutine()
        {
            _isCoolingDown = true;
            yield return new WaitForSeconds(_clickCooldown);
            _isCoolingDown = false;
        }
    }
}