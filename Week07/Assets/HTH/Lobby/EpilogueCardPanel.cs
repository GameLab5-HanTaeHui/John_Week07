using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH.Campaign
{
    /// <summary>
    /// 포스트잇 스타일의 에필로그 카드 패널입니다.
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   EpilogueCardPanel (Image + 이 컴포넌트)  ← 토글 버튼 + 크기 조절 주체
    ///   └── EpilogueText (TMP_Text, Auto Size ON) ← 부모 크기 자동 추종
    ///
    ///   TMP는 Auto Size가 켜져 있어 부모(카드) 크기가 변하면
    ///   글자 크기가 자동으로 맞춰집니다.
    ///   별도 LargeView / SmallText / 텍스트 청킹 없음.
    ///
    /// ─── 상태 ────────────────────────────────────────────────────────────
    ///   작은 상태 (기본)
    ///     씬에 배치된 위치 유지
    ///     Rotation Z: 랜덤 또는 고정 기울기
    ///     크기: _smallSize
    ///     호버 시 살짝 올라오는 연출
    ///
    ///   큰 상태 (클릭 후)
    ///     AnchoredPosition → _largePos (기본 0, 0)
    ///     Rotation Z → 0
    ///     크기: _largeSize
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Epilogue Text   → 자식 TMP_Text (Auto Size ON)  ← 필수
    ///   Small Size      → 작은 상태 크기
    ///   Large Pos       → 큰 상태 위치 (기본: 0, 0)
    ///   Large Size      → 큰 상태 크기
    ///   Expand Duration → 확장/축소 시간
    ///   Hover Offset Y  → 호버 시 올라가는 거리
    ///   Use Random Rotation → 랜덤 기울기 여부
    ///   Random Rotation Range → 기울기 범위 ±도
    ///   Fixed Rotation Z → 고정 기울기
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class EpilogueCardPanel : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI 참조")]
        [Tooltip("자식 TMP_Text입니다. Auto Size를 켜두면 카드 크기에 맞게 자동 조절됩니다.")]
        [SerializeField] private TMP_Text _epilogueText;

        [Header("작은 상태")]
        [SerializeField] private Vector2 _smallSize = new Vector2(160f, 220f);

        [Header("큰 상태")]
        [SerializeField] private Vector2 _largePos = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _largeSize = new Vector2(400f, 540f);

        [Header("애니메이션")]
        [SerializeField] private float _expandDuration = 0.3f;
        [SerializeField] private float _hoverOffsetY = 10f;
        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private Ease _expandEase = Ease.OutBack;
        [SerializeField] private Ease _collapseEase = Ease.InBack;
        [SerializeField] private Ease _hoverEase = Ease.OutQuad;

        [Header("회전")]
        [SerializeField] private bool _useRandomRotation = true;
        [SerializeField] private float _randomRotationRange = 8f;
        [SerializeField] private float _fixedRotationZ = 5f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private RectTransform _rect;
        private bool _isExpanded;
        private Vector2 _smallPos;      // Awake에서 씬 배치 위치 기록
        private float _collapsedRotZ; // Awake에서 결정된 기울기

        private Tweener _posTween;
        private Tweener _sizeTween;
        private Tweener _rotTween;
        private Tweener _hoverTween;

        public bool IsExpanded => _isExpanded;

        /// <summary>카드가 열릴 때 발생합니다. 다른 카드를 닫기 위해 사용합니다.</summary>
        public System.Action<EpilogueCardPanel> OnExpanded;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();

            // 씬 배치 위치 기록
            _smallPos = _rect.anchoredPosition;

            // 기울기 결정
            _collapsedRotZ = _useRandomRotation
                ? Random.Range(-_randomRotationRange, _randomRotationRange)
                : _fixedRotationZ;

            // 초기 상태 적용
            ApplySmallInstant();
        }

        private void OnDestroy()
        {
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();
        }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 에필로그 텍스트를 주입합니다.
        /// LobbyPage2Controller.InitEpilogueCards()에서 호출합니다.
        /// </summary>
        public void Setup(string epilogueText)
        {
            if (_epilogueText == null)
            {
                Debug.LogWarning($"[EpilogueCardPanel] {name}: Epilogue Text 미연결");
                return;
            }

            _epilogueText.text = epilogueText;
        }

        /// <summary>즉시 작은 상태로 초기화합니다.</summary>
        public void CollapseInstant() => ApplySmallInstant();

        // ── 포인터 이벤트 ─────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isExpanded) return;
            _hoverTween?.Kill();
            _hoverTween = _rect
                .DOAnchorPosY(_smallPos.y + _hoverOffsetY, _hoverDuration)
                .SetEase(_hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isExpanded) return;
            _hoverTween?.Kill();
            _hoverTween = _rect
                .DOAnchorPosY(_smallPos.y, _hoverDuration)
                .SetEase(_hoverEase);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isExpanded) Collapse();
            else Expand();
        }

        // ── 확대 / 축소 ───────────────────────────────────────────────────

        public void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            _hoverTween?.Kill();
            transform.SetAsLastSibling();
            OnExpanded?.Invoke(this);

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_largePos, _expandDuration)
                .SetEase(_expandEase);

            _sizeTween?.Kill();
            _sizeTween = _rect.DOSizeDelta(_largeSize, _expandDuration)
                .SetEase(_expandEase);

            _rotTween?.Kill();
            _rotTween = _rect.DOLocalRotate(Vector3.zero, _expandDuration)
                .SetEase(_expandEase);
        }

        public void Collapse()
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_smallPos, _expandDuration)
                .SetEase(_collapseEase);

            _sizeTween?.Kill();
            _sizeTween = _rect.DOSizeDelta(_smallSize, _expandDuration)
                .SetEase(_collapseEase);

            _rotTween?.Kill();
            _rotTween = _rect
                .DOLocalRotate(new Vector3(0f, 0f, _collapsedRotZ), _expandDuration)
                .SetEase(_collapseEase);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void ApplySmallInstant()
        {
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();

            _rect.anchoredPosition = _smallPos;
            _rect.sizeDelta = _smallSize;
            _rect.localEulerAngles = new Vector3(0f, 0f, _collapsedRotZ);
            _isExpanded = false;
        }
    }
}