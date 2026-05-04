using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
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

        // ── 힌트 텍스트 + 핀 ─────────────────────────────────────────────

        [Header("힌트 텍스트")]
        [Tooltip("힌트 텍스트 TMP_Text 슬롯입니다. 튜토리얼은 임시 텍스트로 채워도 됩니다.")]
        [SerializeField] private TMP_Text[] _hintTexts = new TMP_Text[5];

        [Header("핀 색상")]
        [SerializeField] private Color _pinnedColor = new Color(0.1f, 0.55f, 0.9f, 1f);
        [SerializeField] private Color _defaultColor = Color.black;
        [SerializeField] private Color _overflowColor = new Color(0.8f, 0.4f, 0.1f, 1f);

        /// <summary>슬롯 인덱스 → 핀 고정 여부</summary>
        private readonly bool[] _slotPinned = new bool[5];

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
            if (TutorialManager.IsActive && !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.HintPostItToggle))
                return;

            if (_isExpanded)
            {
                // ★ 확장 상태에서 힌트 텍스트 클릭 감지 → 핀 토글
                int slotIdx = GetClickedHintSlot(eventData);
                if (slotIdx >= 0)
                {
                    TogglePin(slotIdx);
                    return;
                }
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        // ── 핀 토글 ──────────────────────────────────────────────────────

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

        private void TogglePin(int slotIdx)
        {
            // 튜토리얼 권한 확인 — 핀 기능별 권한이 있으면 별도 체크 (없으면 기본 허용)
            var panel = TutorialPinnedHintPanel.Instance;
            if (panel == null) return;

            if (_hintTexts[slotIdx] == null) return;
            string text = _hintTexts[slotIdx].text;
            if (string.IsNullOrEmpty(text)) return;

            if (_slotPinned[slotIdx])
            {
                _slotPinned[slotIdx] = false;
                _hintTexts[slotIdx].color = _defaultColor;
                panel.Unpin(text);
            }
            else
            {
                if (panel.PinCount >= TutorialPinnedHintPanel.MaxPins)
                {
                    StartCoroutine(OverflowFeedback(slotIdx));
                    return;
                }
                _slotPinned[slotIdx] = true;
                _hintTexts[slotIdx].color = _pinnedColor;
                panel.Pin(text);
            }
        }

        private IEnumerator OverflowFeedback(int slotIdx)
        {
            if (_hintTexts[slotIdx] != null) _hintTexts[slotIdx].color = _overflowColor;
            yield return new WaitForSecondsRealtime(0.4f);
            if (_hintTexts[slotIdx] != null) _hintTexts[slotIdx].color = _defaultColor;
        }

        /// <summary>패널 닫힐 때 모든 핀 초기화</summary>
        public void ClearAllPins()
        {
            for (int i = 0; i < _slotPinned.Length; i++)
            {
                if (!_slotPinned[i]) continue;
                string text = _hintTexts[i]?.text ?? "";
                _slotPinned[i] = false;
                if (_hintTexts[i] != null) _hintTexts[i].color = _defaultColor;
                TutorialPinnedHintPanel.Instance?.Unpin(text);
            }
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