using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 씬에 미리 배치된 프로파일 추리 답안 슬롯입니다.
    ///
    /// ─── 드롭 감지 방식 ──────────────────────────────────────────────────
    ///   IDropHandler를 사용하지 않습니다.
    ///   ProfileAnswerCard.OnEndDrag()에서 ContainsScreenPoint()로 탐색합니다.
    ///   슬롯 Image의 raycastTarget = false 입니다.
    ///
    /// ─── 정답 판정 ───────────────────────────────────────────────────────
    ///   IsCorrect → CurrentCard.IsClue == true 이면 정답
    ///   어느 카드든 드롭 가능합니다. (진실/거짓 구분 없이 수락)
    ///   정답 판정은 Submit 시 ProfileInquiryUI에서 처리합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Step Index       → 0~4 (직접 입력)
    ///   Question Text    → QuestionText TMP
    ///   Placeholder Text → PlaceholderText TMP
    ///   Hit Padding      → 슬롯 판정 영역 확장 (px, 기본 20)
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProfileAnswerSlot : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Tooltip("이 슬롯의 Step 인덱스입니다. (0~4)")]
        [SerializeField] private int _stepIndex;

        [Tooltip("Step 질문을 표시하는 TMP입니다.")]
        [SerializeField] private TMP_Text _questionText;

        [Tooltip("빈 슬롯 안내 TMP입니다.")]
        [SerializeField] private TMP_Text _placeholderText;

        [Tooltip("비어있을 때 슬롯 색상")]
        [SerializeField] private Color _emptyColor = new Color(0.9f, 0.9f, 0.9f, 0.2f);

        [Tooltip("카드 들어왔을 때 슬롯 색상")]
        [SerializeField] private Color _filledColor = new Color(0.8f, 0.7f, 0.4f, 0.5f);

        [Tooltip("ContainsScreenPoint 판정 영역 확장 값 (px)\n" +
                 "값이 클수록 슬롯 가장자리에서도 드롭이 잘 됩니다.")]
        [SerializeField] private float _hitPadding = 20f;

        // ── 런타임 ───────────────────────────────────────────────────────

        public int StepIndex => _stepIndex;
        public ProfileAnswerCard CurrentCard { get; private set; }
        public bool HasCard => CurrentCard != null;

        /// <summary>
        /// 슬롯이 정답인지 여부입니다.
        /// 진실 조각(IsClue == true)이 드롭됐을 때 true입니다.
        /// </summary>
        public bool IsCorrect => HasCard && CurrentCard.IsClue;

        public System.Action<ProfileAnswerSlot> OnSlotChanged;

        private Image _image;
        private RectTransform _rect;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            _image.raycastTarget = false;
            RefreshVisual();
        }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>Step 인덱스를 런타임에서 설정합니다.</summary>
        public void Setup(int stepIndex)
        {
            _stepIndex = stepIndex;
        }

        /// <summary>Step 질문 텍스트를 주입합니다.</summary>
        public void SetupQuestion(string question)
        {
            if (_questionText != null)
                _questionText.text = question;
        }

        /// <summary>슬롯을 초기 상태로 리셋합니다.</summary>
        public void ResetSlot()
        {
            if (CurrentCard != null)
            {
                var c = CurrentCard;
                CurrentCard = null;
                c.ReturnHome();
            }
            RefreshVisual();
        }

        // ── 슬롯 상호작용 ─────────────────────────────────────────────────

        /// <summary>카드를 슬롯에 수락합니다.</summary>
        public void Accept(ProfileAnswerCard card)
        {
            // 기존 카드 밀어내기
            if (CurrentCard != null && CurrentCard != card)
            {
                var old = CurrentCard;
                CurrentCard = null;
                old.ReturnHome();
            }

            CurrentCard = card;
            card.SnapToSlot(this);
            RefreshVisual();
            OnSlotChanged?.Invoke(this);
        }

        /// <summary>카드를 슬롯에서 해제합니다.</summary>
        public void Release(ProfileAnswerCard card)
        {
            if (CurrentCard != card) return;
            CurrentCard = null;
            RefreshVisual();
            OnSlotChanged?.Invoke(this);
        }

        /// <summary>
        /// 스크린 좌표가 슬롯 영역(+패딩) 안에 있는지 확인합니다.
        /// _hitPadding으로 판정 범위를 확장해 인식률을 높입니다.
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera cam)
        {
            // 기본 RectTransform 판정
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    _rect, screenPoint, cam))
                return true;

            // 패딩 확장 판정
            if (_hitPadding <= 0f) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, screenPoint, cam, out Vector2 localPoint))
                return false;

            Vector2 size = _rect.rect.size;
            Vector2 halfExt = size * 0.5f + Vector2.one * _hitPadding;
            Vector2 pivot = _rect.pivot;
            Vector2 center = new Vector2(
                -pivot.x * size.x + size.x * 0.5f,
                -pivot.y * size.y + size.y * 0.5f);

            return Mathf.Abs(localPoint.x - center.x) <= halfExt.x
                && Mathf.Abs(localPoint.y - center.y) <= halfExt.y;
        }

        // ── Private ──────────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_image != null)
                _image.color = HasCard ? _filledColor : _emptyColor;

            if (_placeholderText != null)
                _placeholderText.gameObject.SetActive(!HasCard);
        }
    }
}