using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 이야기 초기화 확인 경고창입니다.
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   WarningDialog (Panel, 기본 비활성)
    ///     MessageText  (TMP_Text)  → 경고 메시지
    ///     ConfirmButton (Button)   → 확인 버튼
    ///     CancelButton  (Button)   → 취소 버튼
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   _warningDialog.Show(
    ///       message:   "초기화하시겠습니까?",
    ///       onConfirm: OnConfirmed,
    ///       onCancel:  null
    ///   );
    /// </summary>
    [DisallowMultipleComponent]
    public class WarningDialog : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Tooltip("경고 메시지를 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _messageText;

        [Tooltip("확인 버튼입니다.")]
        [SerializeField] private Button _confirmButton;

        [Tooltip("취소 버튼입니다.")]
        [SerializeField] private Button _cancelButton;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private Action _onConfirm;
        private Action _onCancel;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            gameObject.SetActive(false);

            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        private void OnDestroy()
        {
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 경고창을 표시합니다.
        /// </summary>
        /// <param name="message">표시할 경고 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백</param>
        /// <param name="onCancel">취소 버튼 클릭 시 콜백 (null이면 단순 닫기)</param>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            if (_messageText != null)
                _messageText.text = message;

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            gameObject.SetActive(true);
        }

        /// <summary>경고창을 닫습니다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            _onConfirm = null;
            _onCancel = null;
        }

        // ── Private ──────────────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            Hide();
            _onConfirm?.Invoke();
        }

        private void OnCancelClicked()
        {
            var callback = _onCancel;
            Hide();
            callback?.Invoke();
        }
    }
}