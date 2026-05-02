using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH.Tutorial
{
    /// <summary>
    /// [튜토리얼 전용] 캐릭터 힌트 패널입니다.
    /// TutorialManager의 허락을 받아야만 열리고 닫히며,
    /// 조작이 성공하면 매니저에게 다음 대화로 넘어가라고 보고합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class TutorialHintPanel : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        // ── Inspector (기존과 동일) ─────────────────────────────────────────

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

        // 튜토리얼에서는 실제 힌트 데이터를 불러오지 않고 UI 연출만 처리하므로 
        // 데이터(SO) 연결 부분은 제거하여 가볍게 만들었습니다.

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private bool _isExpanded;
        private RectTransform _rect;
        private Tweener _posTween, _sizeTween, _rotTween, _hoverTween;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplySmallStateInstant();
        }

        private void OnDestroy()
        {
            _posTween?.Kill(); _sizeTween?.Kill(); _rotTween?.Kill(); _hoverTween?.Kill();
        }

        // ── 포인터 이벤트 ─────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isExpanded) return;

            // 🚨 [튜토리얼 방어선] 호버 권한이 없으면 올라오지 않습니다.
            if (TutorialManager.IsActive && !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.HintPostItToggle))
                return;

            _hoverTween?.Kill();
            _hoverTween = _rect.DOAnchorPosY(_smallPos.y + _hoverOffsetY, _hoverDuration).SetEase(_hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isExpanded) return;

            _hoverTween?.Kill();
            _hoverTween = _rect.DOAnchorPosY(_smallPos.y, _hoverDuration).SetEase(_hoverEase);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 🚨 [튜토리얼 방어선] 클릭 권한이 없으면 클릭을 무시합니다.
            if (TutorialManager.IsActive && !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.HintPostItToggle))
                return;

            if (_isExpanded)
                Collapse();
            else
                Expand();
        }

        // ── Private — 확장/축소 연출 ──────────────────────────────────────────

        private void Expand()
        {
            _isExpanded = true;
            _hoverTween?.Kill();

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_largePos, _expandDuration).SetEase(_expandEase);

            _sizeTween?.Kill();
            _sizeTween = DOTween.To(() => _rect.sizeDelta, size => _rect.sizeDelta = size, _largeSize, _expandDuration).SetEase(_expandEase);

            _rotTween?.Kill();
            _rotTween = _rect.DOLocalRotate(Vector3.zero, _expandDuration).SetEase(_expandEase)
                .OnComplete(() =>
                {
                    // 열기 연출 완료 후 매니저에게 다음 페이즈 진입을 보고합니다.
                    TutorialManager.Instance?.NotifyHintPostItOpened();
                });
        }

        private void Collapse()
        {
            _isExpanded = false;

            _posTween?.Kill();
            _posTween = _rect.DOAnchorPos(_smallPos, _expandDuration).SetEase(_collapseEase);

            _sizeTween?.Kill();
            _sizeTween = DOTween.To(() => _rect.sizeDelta, size => _rect.sizeDelta = size, _smallSize, _expandDuration).SetEase(_collapseEase);

            _rotTween?.Kill();
            _rotTween = _rect.DOLocalRotate(new Vector3(0f, 0f, _smallRotationZ), _expandDuration).SetEase(_collapseEase);

            // [수정된 부분] 트윈의 OnComplete에 의존하지 않고, 지정된 시간 뒤에 무조건 실행되게 보장합니다!
            DOVirtual.DelayedCall(_expandDuration, () =>
            {
                Debug.Log("[Tutorial] 포스트잇 닫기 연출 완료! 매니저에게 보고합니다.");
                TutorialManager.Instance.NotifyHintPostItClosed();
            });
        }

        private void ApplySmallStateInstant()
        {
            _posTween?.Kill(); _sizeTween?.Kill(); _rotTween?.Kill(); _hoverTween?.Kill();
            _rect.anchoredPosition = _smallPos;
            _rect.sizeDelta = _smallSize;
            _rect.localEulerAngles = new Vector3(0f, 0f, _smallRotationZ);
            _isExpanded = false;
        }
    }
}