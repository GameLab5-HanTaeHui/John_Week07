using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장의 캐릭터 항목 1개 UI 컴포넌트입니다.
    /// CharacterRecordBook에서 프리팹으로 동적 생성됩니다.
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   CharacterEntryView (이 컴포넌트)
    ///   ├── CharacterIdText   (TMP_Text)  → "#1"
    ///   ├── CharacterNameText (TMP_Text)  → "???" or 수집된 이름
    ///   ├── FragmentCountText (TMP_Text)  → "0/3"
    ///   ├── HintContainer     (Transform) → 힌트 텍스트 부모
    ///   ├── InquiryButton     (Button)    → "추리하기"
    ///   └── InquiryAvailableBadge (GameObject) → 추리 가능 알림
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _fragmentCountText;
        [SerializeField] private Transform _hintContainer;
        [SerializeField] private TMP_Text _hintTextPrefab;
        [SerializeField] private Button _inquiryButton;
        [SerializeField] private GameObject _inquiryAvailableBadge;

        private System.Action _onInquiryClicked;
        private int _characterId;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 항목 뷰를 초기화합니다.
        /// CharacterRecordBook.BuildEntries()에서 호출합니다.
        /// </summary>
        public void Setup(int characterId,
                          int requiredFragmentCount,
                          List<string> hints,
                          System.Action onInquiryClicked)
        {
            _characterId = characterId;
            _onInquiryClicked = onInquiryClicked;

            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

            if (_characterNameText != null)
                _characterNameText.text = "???";

            if (_fragmentCountText != null)
                _fragmentCountText.text = $"0/{requiredFragmentCount}";

            BuildHints(hints);

            if (_inquiryButton != null)
            {
                _inquiryButton.onClick.AddListener(() => _onInquiryClicked?.Invoke());
                _inquiryButton.interactable = false;
            }

            if (_inquiryAvailableBadge != null)
                _inquiryAvailableBadge.SetActive(false);
        }

        // ── 갱신 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 수집 현황을 갱신합니다.
        /// FragmentCollector.OnFragmentCollected 이벤트 수신 시 호출됩니다.
        /// </summary>
        public void Refresh(int fragmentCount, int requiredCount, string collectedName)
        {
            if (_fragmentCountText != null)
                _fragmentCountText.text = $"{fragmentCount}/{requiredCount}";

            UpdateName(collectedName);

            // 조각 수 충족 시 추리 버튼 활성화
            if (_inquiryButton != null)
                _inquiryButton.interactable = fragmentCount >= requiredCount;
        }

        /// <summary>캐릭터 이름을 갱신합니다. null이면 "???" 표시.</summary>
        public void UpdateName(string name)
        {
            if (_characterNameText == null) return;
            _characterNameText.text = string.IsNullOrEmpty(name) ? "???" : name;
        }

        /// <summary>추리 가능 알림 배지를 표시합니다.</summary>
        public void ShowInquiryAvailable()
        {
            if (_inquiryAvailableBadge != null)
                _inquiryAvailableBadge.SetActive(true);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void BuildHints(List<string> hints)
        {
            if (_hintTextPrefab == null || _hintContainer == null) return;

            foreach (Transform child in _hintContainer)
                Destroy(child.gameObject);

            if (hints == null) return;

            foreach (var hint in hints)
            {
                if (string.IsNullOrEmpty(hint)) continue;
                var hintText = Instantiate(_hintTextPrefab, _hintContainer);
                hintText.text = $"• {hint}";
            }
        }

        private void OnDestroy()
        {
            _inquiryButton?.onClick.RemoveAllListeners();
        }
    }
}