using DG.Tweening;
using HTH.Campaign;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH
{
    /// <summary>
    /// 힌트 핀 고정 패널입니다.
    /// 슬롯을 클릭하면 HintPanel에도 연동해 핀을 해제합니다.
    ///
    /// ─── 퍼스널 컬러 ─────────────────────────────────────────────────────
    ///   핀 고정 시 캐릭터 ID에 맞는 배경 색상을 표시합니다.
    ///   _slotBackgrounds[i] → Image 컴포넌트 연결
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Slot Objects      → 슬롯 GameObject 2개
    ///   Slot Texts        → 슬롯 TMP_Text 2개
    ///   Slot Backgrounds  → 슬롯 배경 Image 2개 (퍼스널 컬러용)
    ///   Hidden Y / Visible Y / Slide Duration
    /// </summary>
    [DisallowMultipleComponent]
    public class PinnedHintPanel : MonoBehaviour
    {
        public static PinnedHintPanel Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────

        [Header("슬롯 (최대 2개)")]
        [SerializeField] private GameObject[] _slotObjects = new GameObject[2];
        [SerializeField] private TMP_Text[] _slotTexts = new TMP_Text[2];

        [Tooltip("퍼스널 컬러를 표시할 슬롯 배경 Image 2개입니다.")]
        [SerializeField] private UnityEngine.UI.Image[] _slotBackgrounds = new UnityEngine.UI.Image[2];

        [Header("슬라이드 설정")]
        [SerializeField] private float _hiddenY = 200f;
        [SerializeField] private float _visibleY = -50f;
        [SerializeField] private float _slideDuration = 0.35f;

        // ── 퍼스널 컬러 ──────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,                                    // [0] 미사용
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),    // [1] 엔비
            new Color(0x48/255f, 0x78/255f, 0x48/255f),    // [2] 메이
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),    // [3] 루이스
            new Color(0x58/255f, 0x58/255f, 0x88/255f),    // [4] 데우스
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),    // [5] 토니
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),    // [6] 프리드
            new Color(0x98/255f, 0x88/255f, 0x68/255f),    // [7] 새턴
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private RectTransform _rect;
        private Tweener _tween;
        private bool _isVisible;

        public const int MaxPins = 2;

        // 핀 항목: (텍스트, 캐릭터ID, 호출한 HintPanel)
        private readonly List<(string text, int charId, HintPanel source)> _pinned = new();

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _rect = GetComponent<RectTransform>();

            var pos = _rect.anchoredPosition;
            pos.y = _hiddenY;
            _rect.anchoredPosition = pos;
            _isVisible = false;

            // 슬롯 클릭 이벤트 등록
            RegisterSlotClicks();
            RefreshSlots();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 슬롯 클릭 이벤트 등록 ────────────────────────────────────────

        private void RegisterSlotClicks()
        {
            for (int i = 0; i < _slotObjects.Length; i++)
            {
                if (_slotObjects[i] == null) continue;
                int idx = i;

                // PointerClick 리스너를 EventTrigger로 추가
                var trigger = _slotObjects[i].GetComponent<EventTrigger>()
                           ?? _slotObjects[i].AddComponent<EventTrigger>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(_ => OnSlotClicked(idx));
                trigger.triggers.Add(entry);
            }
        }

        private void OnSlotClicked(int slotIdx)
        {
            if (slotIdx >= _pinned.Count) return;

            var (text, _, source) = _pinned[slotIdx];

            // HintPanel에 핀 해제 통보 (색상 복원)
            source?.UnpinByText(text);

            // 자체 핀 해제
            _pinned.RemoveAt(slotIdx);
            RefreshSlots();

            if (_pinned.Count == 0)
                HidePanel();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>현재 핀 고정 수</summary>
        public int PinCount => _pinned.Count;

        /// <summary>
        /// 핀을 고정합니다.
        /// </summary>
        /// <param name="text">표시할 텍스트</param>
        /// <param name="characterId">캐릭터 ID (퍼스널 컬러용)</param>
        /// <param name="source">호출한 HintPanel (역참조용)</param>
        public void Pin(string text, int characterId, HintPanel source)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_pinned.Exists(p => p.text == text)) return;
            if (_pinned.Count >= MaxPins) return;

            _pinned.Add((text, characterId, source));
            RefreshSlots();
            ShowPanel();
        }

        /// <summary>
        /// 핀을 해제합니다. HintPanel의 TogglePin/UnpinAll/OnFragmentCollected에서 호출합니다.
        /// </summary>
        public void Unpin(string text, HintPanel source)
        {
            int idx = _pinned.FindIndex(p => p.text == text);
            if (idx < 0) return;

            _pinned.RemoveAt(idx);
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
                    _slotTexts[i].text = active ? _pinned[i].text : "";

                // 퍼스널 컬러 배경 적용
                if (_slotBackgrounds != null && i < _slotBackgrounds.Length && _slotBackgrounds[i] != null)
                {
                    if (active)
                    {
                        int charId = _pinned[i].charId;
                        _slotBackgrounds[i].color = charId >= 0 && charId < PersonalColors.Length
                            ? PersonalColors[charId]
                            : Color.white;
                    }
                    else
                    {
                        _slotBackgrounds[i].color = Color.white;
                    }
                }
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
                .SetUpdate(true);
        }
    }
}