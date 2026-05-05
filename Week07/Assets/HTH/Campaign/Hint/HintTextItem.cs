using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH
{
    /// <summary>
    /// HintPanel 내의 개별 힌트 텍스트 아이템입니다.
    /// 클릭하면 PinnedHintPanel에 핀 고정/해제를 토글합니다.
    ///
    /// ─── 주의 ────────────────────────────────────────────────────────────
    ///   PinnedHintPanel.Pin()은 (text, characterId, source) 시그니처입니다.
    ///   HintTextItem은 HintPanel 바깥에서 단독으로 쓰이므로
    ///   characterId = -1 (퍼스널 컬러 없음), source = null 로 호출합니다.
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
        [SerializeField] private Color _defaultColor = Color.black;
        [SerializeField] private Color _pinnedColor = new Color(0.1f, 0.55f, 0.9f, 1f);
        [SerializeField] private Color _overflowColor = new Color(0.8f, 0.4f, 0.1f, 1f);

        // ── 내부 ─────────────────────────────────────────────────────────

        private TMP_Text _text;
        private bool _isPinned;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            if (string.IsNullOrEmpty(_hintText))
                _hintText = _text.text;
            _text.color = _defaultColor;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        public void Setup(string hintText)
        {
            _hintText = hintText;
            if (_text != null) _text.text = hintText;
            SetPinnedVisual(false);
        }

        public void ResetPin()
        {
            if (!_isPinned) return;
            _isPinned = false;
            SetPinnedVisual(false);
            PinnedHintPanel.Instance?.Unpin(_hintText, null);
        }

        // ── IPointerClickHandler ─────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var panel = PinnedHintPanel.Instance;
            if (panel == null) return;

            if (_isPinned)
            {
                _isPinned = false;
                SetPinnedVisual(false);
                panel.Unpin(_hintText, null);
            }
            else
            {
                if (panel.PinCount >= PinnedHintPanel.MaxPins)
                {
                    StartCoroutine(OverflowFeedback());
                    return;
                }
                _isPinned = true;
                SetPinnedVisual(true);
                // characterId = -1 (퍼스널 컬러 없음), source = null
                panel.Pin(_hintText, -1, null);
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