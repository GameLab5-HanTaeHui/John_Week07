using TMPro;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 프로파일 추리의 Step 1개 항목입니다.
    /// 질문 텍스트와 답안 슬롯만 포함합니다.
    /// 카드는 ProfileInquiryUI에서 별도 CardContainer에 생성합니다.
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   ProfileStepItem (VerticalLayoutGroup)
    ///   ├── QuestionText (TMP_Text) ← "과거 핵심 사건"
    ///   └── Slot (Image + ProfileAnswerSlot)
    ///         └── PlaceholderText (TMP_Text) ← "여기에 놓으세요"
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Question Text → QuestionText TMP
    ///   Slot          → ProfileAnswerSlot
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileStepItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private ProfileAnswerSlot _slot;

        public ProfileAnswerSlot Slot => _slot;

        public void Setup(int stepIndex, string question)
        {
            if (_questionText != null)
                _questionText.text = question;
        }
    }
}