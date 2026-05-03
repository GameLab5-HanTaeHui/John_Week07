using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 최종 대화 선택지 UI 컴포넌트입니다.
    /// ProfileInquiryUI에서 프리팹으로 동적 생성되거나 씬에 직접 배치됩니다.
    ///
    /// ─── ProfileDataSO 구조 변경 반영 ────────────────────────────────────
    ///   ProfileItem(다단계 추리) 제거.
    ///   FinalTalkData(질문 1개 + 선택지 4개) 기반으로 재설계.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Setup(data, isLocked) 호출
    ///   → 질문 텍스트 표시 (FinalTalkData.Question)
    ///   → isLocked = true  : LockedOverlay 표시, 선택지 미생성
    ///   → isLocked = false : 선택지 버튼 4개 동적 생성
    ///   → 선택지 클릭 시 SelectedIndex 갱신
    ///   → onSelectionChanged 콜백 호출
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   ProfileItemView (이 컴포넌트)
    ///   ├── QuestionText     (TMP_Text)   → FinalTalkData.Question
    ///   ├── LockedOverlay    (GameObject) → 미해금 상태 (기본 비활성)
    ///   └── ChoiceContainer  (Transform)  → 선택지 버튼 부모
    ///       └── (ChoiceButtonPrefab 코드로 생성됨)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private Transform _choiceContainer;

        [Tooltip("선택지 버튼 프리팹입니다. Button + TMP_Text 구조여야 합니다.")]
        [SerializeField] private Button _choiceButtonPrefab;

        private readonly List<Button> _choiceButtons = new();
        private System.Action _onSelectionChanged;

        /// <summary>현재 선택된 선택지 인덱스입니다. 미선택 시 -1.</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>선택이 완료됐는지 여부입니다.</summary>
        public bool HasSelection => SelectedIndex >= 0;

        /// <summary>잠금 상태 여부입니다.</summary>
        public bool IsLocked { get; private set; }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 최종 대화 뷰를 초기화합니다.
        /// ProfileInquiryUI에서 Show() 시 호출합니다.
        /// </summary>
        /// <param name="data">FinalTalkData — 질문 + 선택지 4개</param>
        /// <param name="isLocked">최종 대화 미해금 시 true</param>
        /// <param name="onSelectionChanged">선택지 클릭 시 호출될 콜백</param>
        public void Setup(FinalTalkData data, bool isLocked,
                          System.Action onSelectionChanged = null)
        {
            _onSelectionChanged = onSelectionChanged;
            IsLocked = isLocked;
            SelectedIndex = -1;

            ClearChoiceButtons();

            // 질문 텍스트
            if (_questionText != null)
                _questionText.text = (data != null && !string.IsNullOrEmpty(data.Question))
                    ? data.Question
                    : string.Empty;

            // 잠금 오버레이
            _lockedOverlay?.SetActive(isLocked);

            // 잠금 상태면 선택지 생성 안 함
            if (isLocked || data == null) return;

            BuildChoiceButtons(data.Choices);
        }

        /// <summary>
        /// 선택 상태를 외부에서 초기화합니다.
        /// 확인 팝업에서 "다시 생각한다" 선택 시 사용합니다.
        /// </summary>
        public void ResetSelection()
        {
            SelectedIndex = -1;
            RefreshButtonHighlight(-1);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void BuildChoiceButtons(List<string> choices)
        {
            if (_choiceButtonPrefab == null || _choiceContainer == null) return;

            for (int i = 0; i < choices.Count; i++)
            {
                if (string.IsNullOrEmpty(choices[i])) continue;

                int capturedIndex = i;
                var btn = Instantiate(_choiceButtonPrefab, _choiceContainer);

                var btnText = btn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                    btnText.text = choices[i];

                btn.onClick.AddListener(() => OnChoiceClicked(capturedIndex));
                _choiceButtons.Add(btn);
            }
        }

        private void OnChoiceClicked(int choiceIndex)
        {
            SelectedIndex = choiceIndex;
            RefreshButtonHighlight(choiceIndex);
            _onSelectionChanged?.Invoke();
        }

        private void RefreshButtonHighlight(int selectedIndex)
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                if (_choiceButtons[i] == null) continue;
                var colors = _choiceButtons[i].colors;
                colors.normalColor = (i == selectedIndex)
                    ? new Color(0.8f, 0.9f, 1f) // 선택됨 — 밝은 파란색
                    : Color.white;               // 미선택 — 흰색
                _choiceButtons[i].colors = colors;
            }
        }

        private void ClearChoiceButtons()
        {
            foreach (var btn in _choiceButtons)
                if (btn != null) Destroy(btn.gameObject);
            _choiceButtons.Clear();
        }

        private void OnDestroy()
        {
            foreach (var btn in _choiceButtons)
                btn?.onClick.RemoveAllListeners();
        }
    }
}