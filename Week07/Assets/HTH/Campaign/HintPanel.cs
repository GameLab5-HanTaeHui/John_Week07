using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 힌트 패널입니다.
    ///
    /// ─── 상태 ────────────────────────────────────────────────────────────
    ///   작은 상태 (기본)
    ///     Size: 200 × 150
    ///     PosX: -140 / PosY: 52.5
    ///     Rotation Z: 8
    ///     마우스 호버 시 살짝 올라오는 연출
    ///
    ///   큰 상태 (클릭 후)
    ///     Size: 620 × 400
    ///     PosX: 0 / PosY: 0
    ///     Rotation Z: 0
    ///     힌트 텍스트 5개 표시
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   마우스 호버 → 살짝 위로 올라오는 연출
    ///   클릭 → 큰 상태로 확장
    ///   재클릭 → 작은 상태로 축소
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Hint Data        → FragmentHintDataSO 에셋
    ///   Hint Texts[5]    → TMP_Text 5개
    ///   Small Pos        → 작은 상태 위치 (기본: -140, 52.5)
    ///   Small Size       → 작은 상태 크기 (기본: 200, 150)
    ///   Small Rotation Z → 작은 상태 Z 회전 (기본: 8)
    ///   Large Pos        → 큰 상태 위치 (기본: 0, 0)
    ///   Large Size       → 큰 상태 크기 (기본: 620, 400)
    ///   Expand Duration  → 확장/축소 애니메이션 시간
    ///   Hover Offset Y   → 호버 시 올라가는 거리
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
        [Tooltip("캐릭터별 힌트 텍스트 데이터입니다.")]
        [SerializeField] private FragmentHintDataSO _hintData;

        [Header("의존성")]
        [Tooltip("조각 수집 상태를 확인하기 위한 참조입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("UI 참조")]
        [Tooltip("힌트 텍스트 TMP_Text 5개입니다.")]
        [SerializeField] private TMP_Text[] _hintTexts = new TMP_Text[5];

        [Header("작은 상태 (기본)")]
        [Tooltip("작은 상태의 위치입니다.")]
        [SerializeField] private Vector2 _smallPos = new Vector2(-140f, 52.5f);

        [Tooltip("작은 상태의 크기입니다.")]
        [SerializeField] private Vector2 _smallSize = new Vector2(200f, 150f);

        [Tooltip("작은 상태의 Z 회전값입니다.")]
        [SerializeField] private float _smallRotationZ = 8f;

        [Header("큰 상태 (클릭 후)")]
        [Tooltip("큰 상태의 위치입니다.")]
        [SerializeField] private Vector2 _largePos = new Vector2(0f, 0f);

        [Tooltip("큰 상태의 크기입니다.")]
        [SerializeField] private Vector2 _largeSize = new Vector2(620f, 400f);

        [Header("애니메이션")]
        [Tooltip("확장/축소 애니메이션 시간 (초)")]
        [SerializeField] private float _expandDuration = 0.3f;

        [Tooltip("호버 시 올라가는 거리 (px)")]
        [SerializeField] private float _hoverOffsetY = 10f;

        [Tooltip("호버 애니메이션 시간 (초)")]
        [SerializeField] private float _hoverDuration = 0.15f;

        [Tooltip("확장 Ease")]
        [SerializeField] private Ease _expandEase = Ease.OutBack;

        [Tooltip("축소 Ease")]
        [SerializeField] private Ease _collapseEase = Ease.InBack;

        [Tooltip("호버 Ease")]
        [SerializeField] private Ease _hoverEase = Ease.OutQuad;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        /// <summary>현재 패널이 확장된 상태인지 여부입니다.</summary>
        private bool _isExpanded;

        /// <summary>패널의 RectTransform 컴포넌트입니다.</summary>
        private RectTransform _rect;

        /// <summary>현재 표시 중인 캐릭터 ID입니다.</summary>
        private int _currentCharacterId = -1;

        /// <summary>진행 중인 위치 트윈입니다.</summary>
        private Tweener _posTween;

        /// <summary>진행 중인 크기 트윈입니다.</summary>
        private Tweener _sizeTween;

        /// <summary>진행 중인 회전 트윈입니다.</summary>
        private Tweener _rotTween;

        /// <summary>진행 중인 호버 트윈입니다.</summary>
        private Tweener _hoverTween;

        // ── Unity ────────────────────────────────────────────────────────

        /// <summary>
        /// RectTransform을 캐싱하고 작은 상태로 즉시 초기화합니다.
        /// </summary>
        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplySmallStateInstant();
        }

        /// <summary>진행 중인 트윈을 모두 정리합니다.</summary>
        private void OnDestroy()
        {
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 표시할 캐릭터를 설정합니다.
        /// CharacterRecordPanel.Open() 시 호출합니다.
        /// </summary>
        /// <param name="characterId">힌트를 표시할 캐릭터 ID (1~7)</param>
        public void SetCharacter(int characterId)
        {
            _currentCharacterId = characterId;

            // 캐릭터 변경 시 작은 상태로 즉시 초기화
            if (_isExpanded)
                ApplySmallStateInstant();

            LoadHintTexts();
        }

        // ── 포인터 이벤트 ─────────────────────────────────────────────────

        /// <summary>
        /// 마우스가 패널 위에 올라왔을 때 호출됩니다.
        /// 작은 상태일 때만 위로 살짝 올라오는 호버 연출을 적용합니다.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isExpanded) return;

            // 살짝 위로 올라오는 연출 — "누를 수 있어" 느낌
            _hoverTween?.Kill();
            _hoverTween = _rect
                .DOAnchorPosY(_smallPos.y + _hoverOffsetY, _hoverDuration)
                .SetEase(_hoverEase);
        }

        /// <summary>
        /// 마우스가 패널 밖으로 나갔을 때 호출됩니다.
        /// 작은 상태일 때만 원래 위치로 복귀합니다.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isExpanded) return;

            // 원래 위치로 복귀
            _hoverTween?.Kill();
            _hoverTween = _rect
                .DOAnchorPosY(_smallPos.y, _hoverDuration)
                .SetEase(_hoverEase);
        }

        /// <summary>
        /// 패널 클릭 시 확장/축소를 토글합니다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isExpanded)
                Collapse();
            else
                Expand();
        }

        // ── Private — 확장/축소 ───────────────────────────────────────────

        /// <summary>
        /// 패널을 큰 상태로 확장합니다.
        /// 위치, 크기, 회전을 동시에 애니메이션합니다.
        /// </summary>
        private void Expand()
        {
            _isExpanded = true;

            // 호버 트윈 취소
            _hoverTween?.Kill();

            // 위치: 작은 위치 → 큰 위치
            _posTween?.Kill();
            _posTween = _rect
                .DOAnchorPos(_largePos, _expandDuration)
                .SetEase(_expandEase);

            // 크기: smallSize → largeSize
            _sizeTween?.Kill();
            _sizeTween = DOTween.To(
                () => _rect.sizeDelta,
                size => _rect.sizeDelta = size,
                _largeSize,
                _expandDuration)
                .SetEase(_expandEase);

            // 회전: Z 8 → 0
            _rotTween?.Kill();
            _rotTween = _rect
                .DOLocalRotate(Vector3.zero, _expandDuration)
                .SetEase(_expandEase);
        }

        /// <summary>
        /// 패널을 작은 상태로 축소합니다.
        /// 위치, 크기, 회전을 동시에 애니메이션합니다.
        /// </summary>
        private void Collapse()
        {
            _isExpanded = false;

            // 위치: 큰 위치 → 작은 위치
            _posTween?.Kill();
            _posTween = _rect
                .DOAnchorPos(_smallPos, _expandDuration)
                .SetEase(_collapseEase);

            // 크기: largeSize → smallSize
            _sizeTween?.Kill();
            _sizeTween = DOTween.To(
                () => _rect.sizeDelta,
                size => _rect.sizeDelta = size,
                _smallSize,
                _expandDuration)
                .SetEase(_collapseEase);

            // 회전: Z 0 → 8
            _rotTween?.Kill();
            _rotTween = _rect
                .DOLocalRotate(new Vector3(0f, 0f, _smallRotationZ), _expandDuration)
                .SetEase(_collapseEase);
        }

        /// <summary>
        /// 애니메이션 없이 즉시 작은 상태로 초기화합니다.
        /// Awake() 및 캐릭터 변경 시 호출됩니다.
        /// </summary>
        private void ApplySmallStateInstant()
        {
            // 진행 중인 트윈 모두 취소
            _posTween?.Kill();
            _sizeTween?.Kill();
            _rotTween?.Kill();
            _hoverTween?.Kill();

            _rect.anchoredPosition = _smallPos;
            _rect.sizeDelta = _smallSize;
            _rect.localEulerAngles = new Vector3(0f, 0f, _smallRotationZ);
            _isExpanded = false;
        }

        // ── Private — 힌트 텍스트 ─────────────────────────────────────────

        /// <summary>
        /// FragmentHintDataSO에서 현재 캐릭터의 힌트를 로드해
        /// TMP_Text 배열에 적용합니다.
        /// </summary>
        private void LoadHintTexts()
        {
            if (_hintData == null || _currentCharacterId < 0) return;

            var hints = _hintData.GetHints(_currentCharacterId);

            for (int i = 0; i < _hintTexts.Length; i++)
            {
                if (_hintTexts[i] == null) continue;

                if (i < hints.Count)
                {
                    string baseHint = hints[i];

                    // 조각 ID 생성 (예: 1번 캐릭터의 1번째 단서면 "P01_01")
                    // 인덱스 i는 0부터 시작하므로 +1 해줍니다.
                    string fragmentId = $"P{_currentCharacterId:00}_{i + 1:00}";

                    // FragmentCollector에서 해당 조각을 수집했는지 확인
                    bool isCollected = false;
                    if (_fragmentCollector != null)
                    {
                        isCollected = _fragmentCollector.HasFragment(fragmentId);
                    }

                    // 수집 완료 시 취소선 <s> 태그 적용 (투명도 조절은 취향껏 빼셔도 됩니다)
                    if (isCollected)
                    {
                        _hintTexts[i].text = $"<alpha=#88><s>{i + 1}. {baseHint}</s>";
                    }
                    else
                    {
                        // 미수집 상태면 기본 텍스트
                        _hintTexts[i].text = $"{i + 1}. {baseHint}";
                    }
                }
                else
                {
                    // 힌트 개수가 모자란 남는 슬롯은 비워둠
                    _hintTexts[i].text = "";
                }
            }
        }
    }
}