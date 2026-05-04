using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace HTH
{
    /// <summary>
    /// 힌트 핀 고정 패널입니다.
    /// HintTextItem에서 핀을 고정/해제하면 이 패널이 슬라이드 인/아웃됩니다.
    ///
    /// ─── 씬 구조 ─────────────────────────────────────────────────────────
    ///   PinnedHintPanel (RectTransform — 이 컴포넌트)
    ///   ├── Slot0 (GameObject)
    ///   │   └── SlotText0 (TMP_Text)
    ///   └── Slot1 (GameObject)
    ///       └── SlotText1 (TMP_Text)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Slot Objects  → 슬롯 GameObject 2개 (핀 없으면 비활성)
    ///   Slot Texts    → 슬롯 TMP_Text 2개
    ///   Hidden Y      → 화면 밖 Y 위치 (숨겨진 상태, 예: 200)
    ///   Visible Y     → 화면 안 Y 위치 (보이는 상태, 예: -50)
    ///   Slide Duration→ 슬라이드 시간 (기본 0.35초)
    /// </summary>
    [DisallowMultipleComponent]
    public class PinnedHintPanel : MonoBehaviour
    {
        public static PinnedHintPanel Instance { get; private set; }

        [Header("슬롯 (최대 2개)")]
        [Tooltip("슬롯 GameObject 2개입니다. 핀 없으면 비활성화됩니다.")]
        [SerializeField] private GameObject[] _slotObjects = new GameObject[2];
        [SerializeField] private TMP_Text[] _slotTexts = new TMP_Text[2];

        [Header("슬라이드 설정")]
        [Tooltip("패널이 숨겨진 상태의 anchoredPosition.y (화면 위쪽 밖, 예: 200)")]
        [SerializeField] private float _hiddenY = 200f;

        [Tooltip("패널이 보이는 상태의 anchoredPosition.y (예: -50)")]
        [SerializeField] private float _visibleY = -50f;

        [SerializeField] private float _slideDuration = 0.35f;

        // ── 내부 ─────────────────────────────────────────────────────────

        private RectTransform _rect;
        private readonly List<string> _pinned = new(); // 최대 2개
        private bool _isVisible;
        private Tweener _tween;

        public const int MaxPins = 2;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _rect = GetComponent<RectTransform>();

            // ★ 초기 위치: _hiddenY (화면 밖)
            // Inspector에서 위치를 0,0,0으로 배치했어도 런타임엔 _hiddenY로 강제 초기화
            var pos = _rect.anchoredPosition;
            pos.y = _hiddenY;
            _rect.anchoredPosition = pos;
            _isVisible = false;

            RefreshSlots();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>현재 핀 고정 수</summary>
        public int PinCount => _pinned.Count;

        /// <summary>해당 텍스트가 핀 고정 중인지 여부</summary>
        public bool IsPinned(string text) => _pinned.Contains(text);

        /// <summary>
        /// 텍스트를 핀 고정합니다.
        /// 이미 고정됐거나 최대 2개면 무시합니다.
        /// </summary>
        public void Pin(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_pinned.Contains(text)) return;
            if (_pinned.Count >= MaxPins) return;

            _pinned.Add(text);
            RefreshSlots();
            ShowPanel();
        }

        /// <summary>
        /// 텍스트 핀을 해제합니다.
        /// 핀이 0개가 되면 패널을 자동으로 숨깁니다.
        /// </summary>
        public void Unpin(string text)
        {
            if (!_pinned.Remove(text)) return;

            RefreshSlots();

            if (_pinned.Count == 0)
                HidePanel();
        }

        /// <summary>모든 핀을 해제하고 패널을 숨깁니다.</summary>
        public void ClearAll()
        {
            _pinned.Clear();
            RefreshSlots();
            HidePanel();
        }

        // ── Private — 슬롯 갱신 ─────────────────────────────────────────

        private void RefreshSlots()
        {
            for (int i = 0; i < _slotTexts.Length; i++)
            {
                bool active = i < _pinned.Count;
                if (_slotObjects != null && i < _slotObjects.Length && _slotObjects[i] != null)
                    _slotObjects[i].SetActive(active);
                if (_slotTexts[i] != null)
                    _slotTexts[i].text = active ? _pinned[i] : "";
            }
        }

        // ── Private — 슬라이드 ──────────────────────────────────────────

        private void ShowPanel()
        {
            if (_isVisible) return;
            _isVisible = true;
            AnimateTo(_visibleY);
        }

        private void HidePanel()
        {
            if (!_isVisible) return;
            _isVisible = false;
            AnimateTo(_hiddenY);
        }

        private void AnimateTo(float targetY)
        {
            _tween?.Kill();
            _tween = _rect
                .DOAnchorPosY(targetY, _slideDuration)
                .SetEase(_isVisible ? Ease.OutBack : Ease.InBack)
                .SetUpdate(true); // TimeScale 영향 없이 동작
        }
    }
}