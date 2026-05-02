using UnityEngine;
using UnityEngine.UI;

namespace HTH.Tutorial
{
    /// <summary>
    /// [튜토리얼 전용] 최종 질문을 선택하고 확정(Submit)하는 패널을 관리합니다.
    /// </summary>
    public class TutorialFinalDecisionPanel : MonoBehaviour
    {
        [Header("선택지 버튼들")]
        [Tooltip("4개의 질문 버튼을 연결해주세요.")]
        [SerializeField] private Button[] _optionButtons;

        [Header("확정 버튼")]
        [Tooltip("최종 선택을 확정하는 Submit 버튼입니다.")]
        [SerializeField] private Button _submitButton;

        [Header("선택 시 시각 효과")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = Color.yellow;

        private int _selectedIndex = -1;

        private void Start()
        {
            // 시작할 때는 확정 버튼 비활성화
            if (_submitButton != null)
                _submitButton.interactable = false;

            // 각 선택지 버튼에 클릭 이벤트 연결
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                int index = i; // 클로저 이슈 방지
                if (_optionButtons[i] != null)
                {
                    _optionButtons[i].onClick.AddListener(() => OnOptionClicked(index));
                }
            }

            if (_submitButton != null)
            {
                _submitButton.onClick.AddListener(OnSubmitClicked);
            }
        }

        private void OnOptionClicked(int index)
        {
            // 권한 체크: 질문 선택 단계가 아니면 클릭 무시
            if (!TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.FinalDecisionSelect))
                return;

            _selectedIndex = index;

            TutorialManager.Instance?.NotifyFinalDecisionSelected(index + 1);

            // 시각적 피드백 (선택된 버튼만 색상 변경)
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                var image = _optionButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = (i == _selectedIndex) ? _selectedColor : _normalColor;
                }
            }

            // 확정 버튼 활성화
            if (_submitButton != null)
                _submitButton.interactable = true;
        }

        private void OnSubmitClicked()
        {
            // 튜토리얼 기획상 1번(인덱스 0번)이 정답이므로 확인
            // (만약 인스펙터상에서 정답 버튼이 1번째라면 index는 0입니다)
            if (_selectedIndex == 0) // 1번 보기
            {
                // UI 패널을 끄고 매니저에게 완료 보고!
                gameObject.SetActive(false);
                TutorialManager.Instance?.NotifyFinalSubmit();
            }
        }
    }
}