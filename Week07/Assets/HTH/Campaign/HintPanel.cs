using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 힌트 패널입니다.
    ///
    /// ─── 변경 이력 ───────────────────────────────────────────────────────
    ///   FragmentHintDataSO 제거 → FragmentDataSO.FragmentEntry.HintDescription 사용.
    ///   힌트 텍스트: 미수집 조각의 HintDescription 표시
    ///   수집 완료 조각: 취소선 처리
    ///
    /// ─── 상태 ────────────────────────────────────────────────────────────
    ///   작은 상태 (기본): 마우스 호버 시 살짝 올라오는 연출
    ///   큰 상태 (클릭 후): 힌트 텍스트 5개 표시
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Fragment Data    → FragmentDataSO 에셋
    ///   Fragment Collector → FragmentCollector
    ///   Hint Texts[5]    → TMP_Text 5개
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class HintPanel : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("FragmentDataSO 에셋입니다. HintDescription을 읽어옵니다.")]
        [SerializeField] private FragmentDataSO _fragmentData;

        [Header("의존성")]
        [Tooltip("조각 수집 상태를 확인하기 위한 참조입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("HintPanel 확장 시 배경 클릭 우선권을 위해 CharacterRecordPanel 콜백을 잠시 해제합니다.")]
        [SerializeField] private CharacterRecordPanelManager _recordPanelManager;

        [Header("UI 참조")]
        [Tooltip("힌트 텍스트 TMP_Text 5개입니다.")]
        [SerializeField] private TMP_Text[] _hintTexts = new TMP_Text[5];

        [Header("작은 상태 (기본)")]
        [SerializeField] private Vector2 _smallPos = new Vector2(-140f, 52.5f);
        [SerializeField] private Vector2 _smallSize = new Vector2(200f, 150f);
        [SerializeField] private float _smallRotationZ = 8f;

        [Header("큰 상태 (클릭 후)")]
        [SerializeField] private Vector2 _largePos = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _largeSize = new Vector2(620f, 400f);

        [Header("애니메이션")]
        [SerializeField] private float _expandDuration = 0.3f;
        [SerializeField] private float _hoverOffsetY = 10f;
        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private Ease _expandEase = Ease.OutBack;
        [SerializeField] private Ease _collapseEase = Ease.InBack;
        [SerializeField] private Ease _hoverEase = Ease.OutQuad;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private bool _isExpanded;
        private RectTransform _rect;
        private int _currentCharacterId = -1;

        private Tweener _posTween;
        private Tweener _sizeTween;
        private Tweener _rotTween;
        private Tweener _hoverTween;

        /// <summary>
        /// 슬롯 인덱스 → ProfileClueId 매핑.
        /// LoadHintTexts()에서 채워지며 핀 해제 시 사용합니다.
        /// </summary>
        private readonly Dictionary<int, string> _slotClueIds = new();

        /// <summary>
        /// 슬롯 인덱스 → 현재 핀 고정 여부.
        /// </summary>
        private readonly bool[] _slotPinned = new bool[5];

        // ── 색상 ─────────────────────────────────────────────────────────

        [Header("힌트 핀 색상")]
        [Tooltip("핀 고정 중인 힌트 텍스트 색상")]
        [SerializeField] private Color _pinnedColor = new Color(0.1f, 0.55f, 0.9f, 1f);
        [Tooltip("기본(미핀) 힌트 텍스트 색상")]
        [SerializeField] private Color _defaultColor = Color.black;
        [Tooltip("최대 핀 초과 클릭 시 잠깐 표시할 색상")]
        [SerializeField] private Color _overflowColor = new Color(0.8f, 0.4f, 0.1f, 1f);

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplySmallStateInstant();
        }

        private void Start()
        {
            // 조각 획득 시 → 해당 힌트 자동 핀 해제 + 선택 불가
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
        }

        private void OnDestroy()
        {
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();

            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;

            // 씬 종료 시 등록 해제
            CampaignPanelManager.Instance?.UnregisterPanel(Collapse);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>표시할 캐릭터를 설정합니다. CharacterRecordPanel.Open() 시 호출합니다.</summary>
        public void SetCharacter(int characterId)
        {
            bool isSameCharacter = (_currentCharacterId == characterId);
            _currentCharacterId = characterId;

            if (_isExpanded) ApplySmallStateInstant();

            // ★ 다른 캐릭터 카드로 전환할 때만 핀 초기화
            // 같은 캐릭터 카드를 다시 열면 기존 핀 유지
            if (!isSameCharacter)
                UnpinAll();

            LoadHintTexts();
        }

        // ── 공개 상태 ────────────────────────────────────────────────────

        /// <summary>현재 확장 상태인지 여부입니다.</summary>
        public bool IsExpanded => _isExpanded;

        /// <summary>
        /// 외부(CharacterRecordPanel 등)에서 HintPanel을 닫을 때 호출합니다.
        /// 확장 상태일 때만 동작합니다.
        /// </summary>
        public void CollapseExternal()
        {
            if (!_isExpanded) return;
            Collapse();
        }

        // ── 포인터 이벤트 ─────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isExpanded) return;
            _hoverTween?.Kill();
            _hoverTween = _rect.DOAnchorPosY(_smallPos.y + _hoverOffsetY, _hoverDuration)
                              .SetEase(_hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isExpanded) return;
            _hoverTween?.Kill();
            _hoverTween = _rect.DOAnchorPosY(_smallPos.y, _hoverDuration)
                              .SetEase(_hoverEase);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // ★ 확장 상태에서 힌트 텍스트 클릭 감지
            if (_isExpanded)
            {
                int slotIdx = GetClickedHintSlot(eventData);
                if (slotIdx >= 0)
                {
                    TogglePin(slotIdx);
                    return; // 텍스트 클릭은 Collapse 안 함
                }
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        /// <summary>
        /// 클릭 위치가 어떤 힌트 슬롯 위에 있는지 반환합니다.
        /// 없으면 -1 반환.
        /// </summary>
        private int GetClickedHintSlot(PointerEventData eventData)
        {
            for (int i = 0; i < _hintTexts.Length; i++)
            {
                if (_hintTexts[i] == null) continue;
                var rect = _hintTexts[i].GetComponent<RectTransform>();
                if (rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        rect, eventData.position, eventData.pressEventCamera))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 슬롯 인덱스의 핀 고정/해제를 토글합니다.
        /// 획득 완료된 조각은 선택 불가.
        /// </summary>
        private void TogglePin(int slotIdx)
        {
            if (!_slotClueIds.TryGetValue(slotIdx, out string clueId)) return;

            // 이미 획득한 조각 → 선택 불가
            bool isCollected = _fragmentCollector?.HasFragment(clueId) ?? false;
            if (isCollected) return;

            var panel = PinnedHintPanel.Instance;
            if (panel == null) return;

            // ★ 고정핀에는 순수 힌트 텍스트 (TMP 태그/번호 없이)
            string pinText = GetPinText(slotIdx);
            if (string.IsNullOrEmpty(pinText)) return;

            if (_slotPinned[slotIdx])
            {
                // 핀 해제
                _slotPinned[slotIdx] = false;
                _hintTexts[slotIdx].color = _defaultColor;
                panel.Unpin(pinText, this);
            }
            else
            {
                // 핀 고정 시도
                if (panel.PinCount >= PinnedHintPanel.MaxPins)
                {
                    StartCoroutine(OverflowFeedback(slotIdx));
                    return;
                }
                _slotPinned[slotIdx] = true;
                _hintTexts[slotIdx].color = _pinnedColor;
                panel.Pin(pinText, _currentCharacterId, this);
            }
        }

        /// <summary>
        /// PinnedHintPanel에서 텍스트로 핀을 해제할 때 호출합니다.
        /// 해당 슬롯의 _slotPinned를 false로 하고 색상을 기본으로 복원합니다.
        /// </summary>
        public void UnpinByText(string pinText)
        {
            for (int i = 0; i < _slotPinned.Length; i++)
            {
                if (!_slotPinned[i]) continue;
                if (GetPinText(i) != pinText) continue;

                _slotPinned[i] = false;
                if (i < _hintTexts.Length && _hintTexts[i] != null)
                    _hintTexts[i].color = _defaultColor;
                break;
            }
        }

        private System.Collections.IEnumerator OverflowFeedback(int slotIdx)
        {
            if (_hintTexts[slotIdx] != null) _hintTexts[slotIdx].color = _overflowColor;
            yield return new WaitForSecondsRealtime(0.4f);
            if (_hintTexts[slotIdx] != null) _hintTexts[slotIdx].color = _defaultColor;
        }

        /// <summary>
        /// 조각 획득 이벤트 콜백.
        /// 획득된 조각에 해당하는 힌트가 핀 고정 중이면 자동 해제합니다.
        /// </summary>
        private void OnFragmentCollected(string profileClueId)
        {
            foreach (var kv in _slotClueIds)
            {
                if (kv.Value != profileClueId) continue;

                int idx = kv.Key;
                if (!_slotPinned[idx]) continue;

                // 핀 해제
                string pinText = GetPinText(idx);
                _slotPinned[idx] = false;
                PinnedHintPanel.Instance?.Unpin(pinText, this);

                // 텍스트 갱신 (취소선 적용)
                RefreshSingleSlot(idx);
                break;
            }
        }

        /// <summary>모든 핀을 해제합니다. SetCharacter() 시 호출합니다.</summary>
        private void UnpinAll()
        {
            for (int i = 0; i < _slotPinned.Length; i++)
            {
                if (!_slotPinned[i]) continue;
                string pinText = GetPinText(i);
                _slotPinned[i] = false;
                PinnedHintPanel.Instance?.Unpin(pinText, this);
            }
        }

        /// <summary>슬롯 인덱스의 현재 표시 텍스트를 반환합니다.</summary>
        private string GetHintTextString(int slotIdx)
        {
            if (slotIdx < 0 || slotIdx >= _hintTexts.Length) return "";
            return _hintTexts[slotIdx]?.text ?? "";
        }

        /// <summary>
        /// 슬롯 인덱스의 순수 힌트 텍스트를 반환합니다. (PinnedHintPanel 등록용)
        /// HintDescription 우선, 없으면 HintText. TMP 태그/번호 없음.
        /// </summary>
        private string GetPinText(int slotIdx)
        {
            if (_fragmentData == null || _currentCharacterId < 0) return "";
            var entries = _fragmentData.GetByCharacter(_currentCharacterId);
            if (slotIdx >= entries.Count) return "";
            var entry = entries[slotIdx];
            return !string.IsNullOrEmpty(entry.HintDescription)
                ? entry.HintDescription
                : entry.HintText;
        }

        /// <summary>단일 슬롯 텍스트를 최신 수집 상태로 갱신합니다.</summary>
        private void RefreshSingleSlot(int slotIdx)
        {
            if (!_slotClueIds.TryGetValue(slotIdx, out string clueId)) return;
            if (_fragmentData == null) return;

            var entries = _fragmentData.GetByCharacter(_currentCharacterId);
            if (slotIdx >= entries.Count) return;

            var entry = entries[slotIdx];
            string hint = !string.IsNullOrEmpty(entry.HintDescription)
                ? entry.HintDescription : entry.HintText;

            bool isCollected = _fragmentCollector?.HasFragment(clueId) ?? false;
            _hintTexts[slotIdx].text = isCollected
                ? $"<alpha=#88><s>{slotIdx + 1}. {hint}</s>"
                : $"{slotIdx + 1}. {hint}";
            _hintTexts[slotIdx].color = _defaultColor;
        }

        // ── Private — 확장/축소 ───────────────────────────────────────────

        private void Expand()
        {
            _isExpanded = true;
            _hoverTween?.Kill();

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_largePos, _expandDuration).SetEase(_expandEase);

            _sizeTween?.Kill();
            _sizeTween = DOTween.To(() => _rect.sizeDelta, s => _rect.sizeDelta = s,
                _largeSize, _expandDuration).SetEase(_expandEase);

            _rotTween?.Kill();
            _rotTween = _rect.DOLocalRotate(Vector3.zero, _expandDuration).SetEase(_expandEase);

            // ★ 확장 시 HintPanel이 배경 클릭 우선권을 가짐
            // CharacterRecordPanel 콜백을 잠시 해제하고 HintPanel Collapse만 등록
            if (_recordPanelManager != null)
                CampaignPanelManager.Instance?.UnregisterPanel(_recordPanelManager.CloseCurrentPanel);
            CampaignPanelManager.Instance?.RegisterPanel(Collapse);
        }

        private void Collapse()
        {
            _isExpanded = false;

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_smallPos, _expandDuration).SetEase(_collapseEase);

            _sizeTween?.Kill();
            _sizeTween = DOTween.To(() => _rect.sizeDelta, s => _rect.sizeDelta = s,
                _smallSize, _expandDuration).SetEase(_collapseEase);

            _rotTween?.Kill();
            _rotTween = _rect.DOLocalRotate(new Vector3(0f, 0f, _smallRotationZ), _expandDuration)
                            .SetEase(_collapseEase);

            // ★ HintPanel 닫힐 때 — HintPanel 해제 후 CharacterRecordPanel 콜백 복원
            CampaignPanelManager.Instance?.UnregisterPanel(Collapse);
            if (_recordPanelManager != null)
                CampaignPanelManager.Instance?.RegisterPanel(_recordPanelManager.CloseCurrentPanel);
        }

        private void ApplySmallStateInstant()
        {
            _posTween?.Kill(); _sizeTween?.Kill();
            _rotTween?.Kill(); _hoverTween?.Kill();

            _rect.anchoredPosition = _smallPos;
            _rect.sizeDelta = _smallSize;
            _rect.localEulerAngles = new Vector3(0f, 0f, _smallRotationZ);
            _isExpanded = false;
        }

        // ── Private — 힌트 텍스트 ─────────────────────────────────────────

        /// <summary>
        /// FragmentDataSO에서 현재 캐릭터의 FragmentEntry 5개를 읽어
        /// HintDescription을 힌트 슬롯에 표시합니다.
        /// 수집 완료 조각은 취소선 처리합니다.
        /// </summary>
        private void LoadHintTexts()
        {
            if (_fragmentData == null || _currentCharacterId < 0) return;

            var entries = _fragmentData.GetByCharacter(_currentCharacterId);

            for (int i = 0; i < _hintTexts.Length; i++)
            {
                if (_hintTexts[i] == null) continue;

                if (i >= entries.Count)
                {
                    _hintTexts[i].text = "";
                    _hintTexts[i].color = _defaultColor;
                    continue;
                }

                var entry = entries[i];
                _slotClueIds[i] = entry.ProfileClueId;

                string hint = !string.IsNullOrEmpty(entry.HintDescription)
                    ? entry.HintDescription
                    : entry.HintText;

                bool isCollected = _fragmentCollector?.HasFragment(entry.ProfileClueId) ?? false;

                _hintTexts[i].text = isCollected
                    ? $"<alpha=#88><s>{i + 1}. {hint}</s>"
                    : $"{i + 1}. {hint}";

                // ★ 핀 고정 중이면 핀 색상 복원, 획득 완료이면 기본 색상
                _hintTexts[i].color = (!isCollected && _slotPinned[i])
                    ? _pinnedColor
                    : _defaultColor;
            }
        }
    }
}