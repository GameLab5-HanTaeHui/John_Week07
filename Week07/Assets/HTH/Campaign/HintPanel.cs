using DG.Tweening;
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

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplySmallStateInstant();
        }

        private void OnDestroy()
        {
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>표시할 캐릭터를 설정합니다. CharacterRecordPanel.Open() 시 호출합니다.</summary>
        public void SetCharacter(int characterId)
        {
            _currentCharacterId = characterId;
            if (_isExpanded) ApplySmallStateInstant();
            LoadHintTexts();
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
            if (_isExpanded) Collapse();
            else Expand();
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
                    continue;
                }

                var entry = entries[i];
                string hint = !string.IsNullOrEmpty(entry.HintDescription)
                    ? entry.HintDescription
                    : entry.HintText; // HintDescription 없으면 HintText 폴백

                bool isCollected = _fragmentCollector?.HasFragment(entry.ProfileClueId) ?? false;

                // 수집 완료: 취소선 + 반투명
                // 미수집: 기본 텍스트
                _hintTexts[i].text = isCollected
                    ? $"<alpha=#88><s>{i + 1}. {hint}</s>"
                    : $"{i + 1}. {hint}";
            }
        }
    }
}