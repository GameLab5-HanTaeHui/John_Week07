using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비에서 진행 데이터 초기화 등 중요한 결정을 할 때 사용하는 경고창 팝업입니다.
    /// Yes / No 버튼 입력을 받아 콜백을 실행합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WarningDialog : MonoBehaviour
    {
        [Header("UI 구성요소")]
        [Tooltip("경고창 전체를 묶는 패널 (보통 반투명 배경 포함)")]
        [SerializeField] private GameObject _panel;

        [Tooltip("경고 메시지를 표시할 텍스트 컴포넌트")]
        [SerializeField] private TMP_Text _messageText;

        [Tooltip("초기화(확인) 버튼")]
        [SerializeField] private Button _yesButton;

        [Tooltip("돌아가기(취소) 버튼")]
        [SerializeField] private Button _noButton;

        // 버튼 클릭 시 실행할 액션 저장용
        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            // 버튼 이벤트 연결
            _yesButton?.onClick.AddListener(OnYesClicked);
            _noButton?.onClick.AddListener(OnNoClicked);

            // 초기 상태는 숨김 처리
            Hide();
        }

        /// <summary>
        /// 경고창을 화면에 표시하고 콜백을 설정합니다.
        /// </summary>
        /// <param name="message">표시할 경고 메시지</param>
        /// <param name="onConfirm">Yes 버튼 클릭 시 실행할 동작</param>
        /// <param name="onCancel">No 버튼 클릭 시 실행할 동작 (생략 가능)</param>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            if (_messageText != null)
            {
                _messageText.text = message;
            }

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        /// <summary>
        /// 경고창을 숨기고 등록된 콜백을 메모리에서 해제합니다.
        /// </summary>
        public void Hide()
        {
            _onConfirm = null;
            _onCancel = null;

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnYesClicked()
        {
            // 실행 직전 Hide()로 인해 Action이 null이 되는 것을 방지하기 위해 임시 변수에 담습니다.
            var confirmAction = _onConfirm;

            // 팝업을 먼저 닫습니다.
            Hide();

            // 콜백(새 캠페인 시작 로직 등)을 실행합니다.
            confirmAction?.Invoke();
        }

        private void OnNoClicked()
        {
            var cancelAction = _onCancel;

            Hide();

            cancelAction?.Invoke();
        }
    }
}