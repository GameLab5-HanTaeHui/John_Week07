using UnityEngine;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장의 문장 조각 슬롯 1줄입니다.
    /// CharacterDetailView에서 조각 수만큼 동적으로 생성됩니다.
    ///
    /// ─── 상태 ────────────────────────────────────────────────────────────
    ///   미수집: 회색 힌트 텍스트 표시 ("이건 힌트야~1")
    ///   수집됨: 검은색 실제 대사 표시 ("네가 향을 맡할 때마다...")
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   FragmentSlotView (이 컴포넌트)
    ///   ├── NumberText   (TMP_Text) ← "1."
    ///   └── ContentText  (TMP_Text) ← 힌트 또는 실제 대사
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private TMP_Text _contentText;

        [Header("색상")]
        [Tooltip("조각 수집 시 텍스트 색상")]
        [SerializeField] private Color _collectedColor = new Color(0.1f, 0.1f, 0.1f);

        [Tooltip("미수집 시 힌트 텍스트 색상")]
        [SerializeField] private Color _hintColor = new Color(0.5f, 0.5f, 0.5f);

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 슬롯을 초기화합니다.
        /// CharacterDetailView.BuildFragmentSlots()에서 호출합니다.
        /// </summary>
        /// <param name="index">슬롯 번호 (0-based, 표시는 1-based)</param>
        /// <param name="hintText">미수집 시 표시할 힌트 텍스트</param>
        /// <param name="fragmentText">수집된 실제 대사 (null이면 미수집 상태)</param>
        public void Setup(int index, string hintText, string fragmentText)
        {
            if (_numberText != null)
                _numberText.text = $"{index + 1}.";

            bool isCollected = !string.IsNullOrEmpty(fragmentText);

            if (_contentText != null)
            {
                _contentText.text = isCollected ? fragmentText : hintText;
                _contentText.color = isCollected ? _collectedColor : _hintColor;
            }
        }

        /// <summary>슬롯 내용을 갱신합니다. 조각 수집 시 호출됩니다.</summary>
        public void Refresh(string hintText, string fragmentText)
        {
            Setup(0, hintText, fragmentText); // 번호는 유지되므로 내용만 갱신
            // 번호 텍스트 유지
        }
    }
}