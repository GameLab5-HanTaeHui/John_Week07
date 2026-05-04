using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace HTH.Tutorial
{
    /// <summary>
    /// [튜토리얼 전용] 힌트 핀 고정 패널입니다.
    /// HTH.PinnedHintPanel과 동일한 구조이나 튜토리얼 씬에서 독립적으로 동작합니다.
    ///
    /// ─── 씬 구조 ─────────────────────────────────────────────────────────
    ///   TutorialPinnedHintPanel (RectTransform — 이 컴포넌트)
    ///   ├── Slot0 (GameObject)
    ///   │   └── SlotText0 (TMP_Text)
    ///   └── Slot1 (GameObject)
    ///       └── SlotText1 (TMP_Text)
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialPinnedHintPanel : MonoBehaviour
    {
        public static TutorialPinnedHintPanel Instance { get; private set; }

        [Header("슬롯 (최대 2개)")]
        [SerializeField] private GameObject[] _slotObjects = new GameObject[2];
        [SerializeField] private TMP_Text[] _slotTexts = new TMP_Text[2];

        [Header("슬라이드 설정")]
        [Tooltip("패널이 숨겨진 상태의 anchoredPosition.y (화면 위쪽 밖, 예: 200)")]
        [SerializeField] private float _hiddenY = 200f;

        [Tooltip("패널이 보이는 상태의 anchoredPosition.y (예: -50)")]
        [SerializeField] private float _visibleY = -50f;

        [SerializeField] private float _slideDuration = 0.35f;

        private RectTransform _rect;
        private readonly List<string> _pinned = new();
        private bool _isVisible;
        private Tweener _tween;

        public const int MaxPins = 2;

        public int PinCount => _pinned.Count;
        public bool IsPinned(string text) => _pinned.Contains(text);

        private void Awake()
        {
            Instance = this;
            _rect = GetComponent<RectTransform>();

            // ★ 항상 _hiddenY로 강제 초기화
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

        public void Pin(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_pinned.Contains(text)) return;
            if (_pinned.Count >= MaxPins) return;

            _pinned.Add(text);
            RefreshSlots();
            ShowPanel();

            // 튜토리얼 매니저에 핀 고정 완료 보고
            TutorialManager.Instance?.NotifyHintPinned();
        }

        public void Unpin(string text)
        {
            if (!_pinned.Remove(text)) return;
            RefreshSlots();
            if (_pinned.Count == 0) HidePanel();
        }

        public void ClearAll()
        {
            _pinned.Clear();
            RefreshSlots();
            HidePanel();
        }

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
                .SetUpdate(true);
        }
    }
}