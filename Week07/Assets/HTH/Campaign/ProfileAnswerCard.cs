using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 씬에 미리 배치된 프로파일 추리 답안 카드입니다.
    ///
    /// ─── 드롭 방식 ───────────────────────────────────────────────────────
    ///   OnEndDrag에서 모든 슬롯을 순회하며 ContainsScreenPoint로 탐색합니다.
    ///   StepIndex 일치 조건이 없습니다. 어느 슬롯에나 드롭 가능합니다.
    ///   (게임의 본질: 플레이어가 진실/거짓 조각을 직접 판별)
    ///
    /// ─── 홈 위치 ─────────────────────────────────────────────────────────
    ///   Start()에서 씬 배치 위치를 홈으로 기록합니다.
    ///   Show() 호출 시 RecordHome()을 재기록합니다.
    ///   ReturnHome() 시 DOTween으로 홈 위치로 복귀합니다.
    ///
    /// ─── 오브젝트 구조 ───────────────────────────────────────────────────
    ///   Card_N (Image + CanvasGroup + ProfileAnswerCard)
    ///   └── CardText (TMP_Text) — raycastTarget OFF 권장
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public class ProfileAnswerCard : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_Text _cardText;

        // ── 데이터 ───────────────────────────────────────────────────────

        /// <summary>진실(CLUE) 조각이면 true, 거짓(HINT) 조각이면 false입니다.</summary>
        public bool IsClue { get; private set; }

        /// <summary>이 카드가 속한 Step 인덱스입니다. (0~4)</summary>
        public int StepIndex { get; private set; }

        // ── 컴포넌트 ─────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private Canvas _rootCanvas;
        private Camera _uiCamera;

        // ── 홈 위치 ──────────────────────────────────────────────────────

        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector2 _homeAnchoredPos;
        private bool _homeRecorded;

        // ── 슬롯 ─────────────────────────────────────────────────────────

        private ProfileAnswerSlot _currentSlot;
        private ProfileAnswerSlot[] _allSlots;

        private Tween _returnTween;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
            GetComponent<Image>().raycastTarget = true;

            // TMP의 raycastTarget은 반드시 OFF
            if (_cardText != null)
                _cardText.raycastTarget = false;
        }

        private void Start()
        {
            RecordHome();
        }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 수집된 조각 데이터를 카드에 주입합니다.
        /// ProfileInquiryUI.SetupSlotsAndCards()에서 호출합니다.
        /// </summary>
        public void SetupData(int stepIndex, bool isClue, string text,
                              ProfileAnswerSlot[] allSlots)
        {
            StepIndex = stepIndex;
            IsClue = isClue;
            _allSlots = allSlots;

            if (_cardText != null)
                _cardText.text = text;
        }

        /// <summary>
        /// 현재 씬 위치를 홈으로 기록합니다.
        /// Start() 또는 ProfileInquiryUI.RecordHomesNextFrame()에서 호출합니다.
        /// </summary>
        public void RecordHome()
        {
            _originalParent = _rect.parent;
            _originalSiblingIndex = _rect.GetSiblingIndex();
            _homeAnchoredPos = _rect.anchoredPosition;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _rootCanvas = canvas.rootCanvas;
                _uiCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : _rootCanvas.worldCamera;
            }

            _homeRecorded = true;
        }

        // ── 드래그 ───────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_homeRecorded) RecordHome();

            _returnTween?.Kill();

            // 슬롯에서 해제 (홈 복귀 없이 슬롯 상태만 초기화)
            if (_currentSlot != null)
            {
                _currentSlot.Release(this);
                _currentSlot = null;
            }

            // rootCanvas로 이동 → 모든 UI 위에 렌더링
            if (_rootCanvas != null)
                _rect.SetParent(_rootCanvas.transform, worldPositionStays: true);

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.85f;
            _rect.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rootCanvas == null) return;
            _rect.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;

            // 가장 가까운 슬롯 탐색 (StepIndex 제한 없음)
            var slot = FindNearestSlot(eventData.position);
            if (slot != null)
                slot.Accept(this);
            else
                ReturnHome();
        }

        // ── 슬롯 상호작용 ─────────────────────────────────────────────────

        /// <summary>슬롯 중심에 스냅합니다. ProfileAnswerSlot.Accept()에서 호출.</summary>
        public void SnapToSlot(ProfileAnswerSlot slot)
        {
            _returnTween?.Kill();
            _currentSlot = slot;

            RestoreParent();

            var slotRT = (RectTransform)slot.transform;
            _rect.position = slotRT.position;
            _rect.localRotation = Quaternion.identity;
        }

        /// <summary>홈으로 DOTween 복귀합니다.</summary>
        public void ReturnHome()
        {
            _returnTween?.Kill();

            if (_currentSlot != null)
            {
                _currentSlot.Release(this);
                _currentSlot = null;
            }

            RestoreParent();

            _returnTween = _rect
                .DOAnchorPos(_homeAnchoredPos, 0.25f)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
        }

        /// <summary>즉시 홈으로 복귀합니다. UI 닫을 때 사용.</summary>
        public void ReturnHomeInstant()
        {
            _returnTween?.Kill();

            if (_currentSlot != null)
            {
                _currentSlot.Release(this);
                _currentSlot = null;
            }

            RestoreParent();

            _rect.anchoredPosition = _homeAnchoredPos;
            _rect.localRotation = Quaternion.identity;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 드롭 위치에서 가장 가까운 슬롯을 찾습니다.
        /// StepIndex 제한이 없으므로 모든 슬롯 대상으로 탐색합니다.
        /// 겹치는 슬롯이 여러 개면 중심까지의 거리가 가장 가까운 슬롯을 반환합니다.
        /// </summary>
        private ProfileAnswerSlot FindNearestSlot(Vector2 screenPos)
        {
            if (_allSlots == null) return null;

            ProfileAnswerSlot nearest = null;
            float minDist = float.MaxValue;

            foreach (var slot in _allSlots)
            {
                if (slot == null) continue;
                if (!slot.ContainsScreenPoint(screenPos, _uiCamera)) continue;

                // 슬롯 중심까지의 거리 계산
                var slotRT = (RectTransform)slot.transform;
                var slotCenter = RectTransformUtility.WorldToScreenPoint(
                    _uiCamera, slotRT.position);
                float dist = Vector2.Distance(screenPos, slotCenter);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = slot;
                }
            }

            return nearest;
        }

        private void RestoreParent()
        {
            if (_originalParent == null) return;
            if (_rect.parent == _originalParent) return;

            _rect.SetParent(_originalParent, worldPositionStays: true);
            _rect.SetSiblingIndex(_originalSiblingIndex);
        }
    }
}