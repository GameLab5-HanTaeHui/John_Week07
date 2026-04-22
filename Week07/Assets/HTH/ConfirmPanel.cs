using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace HTH
{
    /// <summary>
    /// HoldToAdvanceTurn / HoldToEnterFinalDecision 공유 확인 패널입니다.
    /// 클릭으로 Fill 애니메이션 완료 후 이 패널이 표시됩니다.
    ///
    /// Canvas 구조 예시:
    ///   ConfirmPanel (이 컴포넌트)
    ///   ├── MessageText (TMP_Text) ← _messageText
    ///   ├── ConfirmButton (Button) ← _confirmButton
    ///   └── CancelButton  (Button) ← _cancelButton
    /// </summary>
    [DisallowMultipleComponent]
    public class ConfirmPanel : MonoBehaviour
    {
        public static ConfirmPanel Instance { get; private set; }

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // 버튼 이벤트 등록 — Show()에서 콜백만 교체하면 자동 반영됨
            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);

            _panel?.SetActive(false);
        }

        private void OnDestroy()
        {
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
        }

        /// <summary>
        /// 확인 패널을 표시합니다.
        /// </summary>
        /// <param name="message">패널에 표시할 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백</param>
        /// <param name="onCancel">취소 버튼 클릭 시 콜백</param>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            if (_messageText != null)
                _messageText.text = message;

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            _panel?.SetActive(true);
        }

        public void Hide()
        {
            _panel?.SetActive(false);
            _onConfirm = null;
            _onCancel = null;
        }

        private void OnConfirmClicked()
        {
            var callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }

        private void OnCancelClicked()
        {
            var callback = _onCancel;
            Hide();
            callback?.Invoke();
        }
    }
}