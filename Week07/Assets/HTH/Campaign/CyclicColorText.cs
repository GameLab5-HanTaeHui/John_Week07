using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH
{
    /// <summary>
    /// TMP_Text를 클릭하면 색상과 취소선이 순환하는 컴포넌트입니다.
    ///
    /// ─── 상태 순환 ───────────────────────────────────────────────────────
    ///   0. 검은색  (기본)
    ///   1. 초록색
    ///   2. 붉은색 + 취소선
    ///   → 다시 0으로
    ///
    /// ─── 입력 ────────────────────────────────────────────────────────────
    ///   좌클릭 → 다음 상태 (0→1→2→0)
    ///   우클릭 → 이전 상태 (0→2→1→0)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   같은 GameObject의 TMP_Text를 자동 참조합니다.
    ///   색상은 Inspector에서 커스터마이즈 가능합니다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class CyclicColorText : MonoBehaviour,
        IPointerClickHandler
    {
        [Header("상태 색상")]
        [Tooltip("상태 0 — 기본 색상")]
        [SerializeField] private Color _colorDefault = Color.black;

        [Tooltip("상태 1 — 초록색")]
        [SerializeField] private Color _colorGreen = new Color(0.1f, 0.6f, 0.1f, 1f);

        [Tooltip("상태 2 — 붉은색 (취소선 포함)")]
        [SerializeField] private Color _colorRed = new Color(0.8f, 0.1f, 0.1f, 1f);

        [Header("취소선")]
        [Tooltip("취소선을 적용할 상태 인덱스 목록입니다.\n기본값: 상태 2(붉은색)에만 적용.")]
        [SerializeField] private List<int> _strikethroughStates = new() { 2 };

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>상태가 변경될 때 발생합니다. (sender, newState)</summary>
        public event System.Action<CyclicColorText, int> OnStateChanged;

        // ── 내부 ─────────────────────────────────────────────────────────

        private TMP_Text _text;
        private int _state; // 0 = 검정, 1 = 초록, 2 = 붉은+취소선

        private Color[] _colors;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _colors = new[] { _colorDefault, _colorGreen, _colorRed };

            ApplyState();
        }

        // ── IPointerClickHandler ─────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _state = (_state + 1) % _colors.Length;
            else if (eventData.button == PointerEventData.InputButton.Right)
                _state = (_state - 1 + _colors.Length) % _colors.Length;
            else
                return;

            ApplyState();
            OnStateChanged?.Invoke(this, _state); // ★ 클릭 시 저장 트리거
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>현재 상태 인덱스 (0=검정, 1=초록, 2=붉은+취소선)</summary>
        public int State => _state;

        /// <summary>상태를 코드에서 직접 설정합니다.</summary>
        public void SetState(int state)
        {
            _state = Mathf.Clamp(state, 0, _colors.Length - 1);
            ApplyState();
        }

        /// <summary>상태를 기본(0)으로 초기화합니다.</summary>
        public void ResetState()
        {
            _state = 0;
            ApplyState();
        }

        // ── Private ──────────────────────────────────────────────────────

        private void ApplyState()
        {
            if (_text == null) return;

            _text.color = _colors[_state];

            bool strikethrough = _strikethroughStates != null
                                 && _strikethroughStates.Contains(_state);

            _text.fontStyle = strikethrough
                ? _text.fontStyle | FontStyles.Strikethrough
                : _text.fontStyle & ~FontStyles.Strikethrough;
        }
    }
}