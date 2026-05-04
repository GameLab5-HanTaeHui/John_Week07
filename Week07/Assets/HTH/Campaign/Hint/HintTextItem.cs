using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH
{
    /// <summary>
    /// HintPanel 내의 개별 힌트 텍스트 아이템입니다.
    /// 클릭하면 PinnedHintPanel에 핀 고정/해제를 토글합니다.
    ///
    /// ─── 씬 구조 ─────────────────────────────────────────────────────────
    ///   HintTextItem (이 컴포넌트 + TMP_Text)
    ///   → TMP_Text.raycastTarget = true 필수
    ///
    /// ─── 동작 ────────────────────────────────────────────────────────────
    ///   클릭 → 핀 고정   : 텍스트 색상 → _pinnedColor, PinnedHintPanel에 추가
    ///   재클릭 → 핀 해제 : 텍스트 색상 → _defaultColor, PinnedHintPanel에서 제거
    ///   최대 2개 초과 시  : 핀 고정 무시 (시각적 피드백만)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Text        → TMP_Text 컴포넌트 (없으면 자동 참조)
    ///   Hint Text   → 표시할 힌트 문자열 (빈 경우 Text.text 사용)
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class HintTextItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("텍스트")]
        [Tooltip("힌트 내용입니다. 비워두면 TMP_Text.text를 그대로 사용합니다.")]
        [SerializeField] private string _hintText;

        [Header("색상")]
        [Tooltip("기본(핀 없음) 색상")]
        [SerializeField] private Color _defaultColor = Color.black;

        [Tooltip("핀 고정 중 색상")]
        [SerializeField] private Color _pinnedColor = new Color(0.1f, 0.55f, 0.9f, 1f);

        [Tooltip("최대 핀 초과 시 클릭했을 때 잠깐 표시할 색상")]
        [SerializeField] private Color _overflowColor = new Color(0.8f, 0.4f, 0.1f, 1f);

        // ── 내부 ─────────────────────────────────────────────────────────

        private TMP_Text _text;
        private bool _isPinned;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();

            // _hintText 비어있으면 TMP 현재 텍스트 사용
            if (string.IsNullOrEmpty(_hintText))
                _hintText = _text.text;

            _text.color = _defaultColor;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>힌트 텍스트를 코드에서 설정합니다.</summary>
        public void Setup(string hintText)
        {
            _hintText = hintText;
            if (_text != null) _text.text = hintText;
            SetPinnedVisual(false);
        }

        /// <summary>핀 상태를 초기화합니다. HintPanel 닫힐 때 호출하세요.</summary>
        public void ResetPin()
        {
            if (!_isPinned) return;
            _isPinned = false;
            SetPinnedVisual(false);
            // 패널에서도 제거
            PinnedHintPanel.Instance?.Unpin(_hintText);
        }

        // ── IPointerClickHandler ─────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var panel = PinnedHintPanel.Instance;
            if (panel == null) return;

            if (_isPinned)
            {
                // 핀 해제
                _isPinned = false;
                SetPinnedVisual(false);
                panel.Unpin(_hintText);
            }
            else
            {
                // 핀 고정 시도
                if (panel.PinCount >= PinnedHintPanel.MaxPins)
                {
                    // 최대 초과 — 잠깐 오렌지로 피드백
                    StartCoroutine(OverflowFeedback());
                    return;
                }

                _isPinned = true;
                SetPinnedVisual(true);
                panel.Pin(_hintText);
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private void SetPinnedVisual(bool pinned)
        {
            if (_text != null)
                _text.color = pinned ? _pinnedColor : _defaultColor;
        }

        private System.Collections.IEnumerator OverflowFeedback()
        {
            if (_text != null) _text.color = _overflowColor;
            yield return new WaitForSecondsRealtime(0.4f);
            if (_text != null) _text.color = _defaultColor;
        }
    }
}