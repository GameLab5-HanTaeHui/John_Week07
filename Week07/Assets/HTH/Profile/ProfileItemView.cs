using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 프로파일 항목 1개의 UI 컴포넌트입니다.
    /// ProfileInquiryUI에서 프리팹으로 동적 생성됩니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Setup(itemIndex, item, isLocked) 호출
    ///   → 질문 텍스트 표시
    ///   → isLocked = true: LockedOverlay 표시, 선택지 미생성
    ///   → isLocked = false: 선택지 버튼 동적 생성
    ///   → 선택지 클릭 시 SelectedIndex 갱신
    ///   → 부모 ProfileInquiryUI.OnItemSelectionChanged() 호출
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   ProfileItemView (이 컴포넌트)
    ///   ├── QuestionText    (TMP_Text)  → "가장 두려워하는 것"
    ///   ├── LockedOverlay   (GameObject) → 잠금 상태 표시 (기본 비활성)
    ///   └── ChoiceContainer (Transform) → 선택지 버튼 부모
    ///       └── (ChoiceButton 프리팹이 코드로 생성됨)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private Transform _choiceContainer;
        [SerializeField] private Button _choiceButtonPrefab;

        private readonly List<Button> _choiceButtons = new();
        private int _itemIndex;
        private System.Action _onSelectionChanged;

        /// <summary>현재 선택된 선택지 인덱스입니다. 미선택 시 -1.</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>선택이 완료됐는지 여부입니다.</summary>
        public bool HasSelection => SelectedIndex >= 0;

        /// <summary>잠금 상태 여부입니다.</summary>
        public bool IsLocked { get; private set; }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 프로파일 항목 뷰를 초기화합니다.
        /// ProfileInquiryUI.BuildProfileItems()에서 호출합니다.
        /// </summary>
        /// <param name="itemIndex">이 항목의 인덱스 (0~3)</param>
        /// <param name="item">프로파일 항목 데이터</param>
        /// <param name="isLocked">필요 조각 미수집 시 잠금</param>
        public void Setup(int itemIndex, ProfileItem item, bool isLocked,
                          System.Action onSelectionChanged = null)
        {
            _onSelectionChanged = onSelectionChanged;


            _itemIndex = itemIndex;
            IsLocked = isLocked;
            SelectedIndex = -1;

            // 질문 텍스트
            if (_questionText != null)
                _questionText.text = item.Question;

            // 잠금 오버레이
            if (_lockedOverlay != null)
                _lockedOverlay.SetActive(isLocked);

            // 선택지 버튼 생성 (잠금이 아닐 때만)
            if (!isLocked)
                BuildChoiceButtons(item.Choices);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void BuildChoiceButtons(List<string> choices)
        {
            if (_choiceButtonPrefab == null || _choiceContainer == null) return;

            for (int i = 0; i < choices.Count; i++)
            {
                int choiceIndex = i; // 클로저 캡처용
                var btn = Instantiate(_choiceButtonPrefab, _choiceContainer);

                // 버튼 텍스트 설정
                var btnText = btn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                    btnText.text = choices[i];

                btn.onClick.AddListener(() => OnChoiceClicked(choiceIndex, btn));
                _choiceButtons.Add(btn);
            }
        }

        private void OnChoiceClicked(int choiceIndex, Button clickedButton)
        {
            SelectedIndex = choiceIndex;

            // 선택된 버튼 하이라이트 (나머지 초기화)
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] == null) continue;

                var colors = _choiceButtons[i].colors;
                colors.normalColor = (i == choiceIndex)
                    ? new Color(0.8f, 0.9f, 1f)  // 선택됨: 밝은 파란색
                    : Color.white;                 // 미선택: 흰색
                _choiceButtons[i].colors = colors;
            }

            // 선택 변경 콜백 호출
            _onSelectionChanged?.Invoke();
        }

        private void OnDestroy()
        {
            foreach (var btn in _choiceButtons)
                btn?.onClick.RemoveAllListeners();
        }
    }
}