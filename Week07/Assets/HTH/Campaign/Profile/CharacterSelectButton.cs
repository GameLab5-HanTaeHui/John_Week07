using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// ProfileInquiryAllUI의 캐릭터 선택 버튼 1개 UI 컴포넌트입니다.
    /// ProfileInquiryAllUI에서 프리팹으로 동적 생성됩니다.
    ///
    /// ─── 상태 표시 ───────────────────────────────────────────────────────
    ///   잠금 (조각 부족): LockedIcon 표시, 버튼 비활성화
    ///   추리 가능       : 버튼 활성화
    ///   추리 완료       : CompletedIcon 표시
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   CharacterSelectButton (이 컴포넌트)
    ///   ├── CharacterIdText   (TMP_Text)  → "#1"
    ///   ├── CharacterNameText (TMP_Text)  → "???" or 이름
    ///   ├── FragmentCountText (TMP_Text)  → "3/3"
    ///   ├── CompletedIcon     (GameObject) → 완료 아이콘
    ///   ├── LockedIcon        (GameObject) → 잠금 아이콘
    ///   └── Button            (Button)
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSelectButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _fragmentCountText;
        [SerializeField] private GameObject _completedIcon;
        [SerializeField] private GameObject _lockedIcon;
        [SerializeField] private Button _button;

        /// <summary>이 버튼에 연결된 캐릭터 ID입니다.</summary>
        public int CharacterId { get; private set; }

        private System.Action _onClicked;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 선택 버튼을 초기화합니다.
        /// ProfileInquiryAllUI.BuildCharacterButtons()에서 호출합니다.
        /// </summary>
        public void Setup(int characterId,
                          string name,
                          int fragmentCount,
                          int requiredCount,
                          bool isUnlocked,
                          bool isCompleted,
                          System.Action onClicked)
        {
            CharacterId = characterId;
            _onClicked = onClicked;

            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

            if (_characterNameText != null)
                _characterNameText.text = string.IsNullOrEmpty(name) ? "???" : name;

            if (_fragmentCountText != null)
                _fragmentCountText.text = $"{fragmentCount}/{requiredCount}";

            if (_completedIcon != null) _completedIcon.SetActive(isCompleted);
            if (_lockedIcon != null) _lockedIcon.SetActive(!isUnlocked);

            if (_button != null)
            {
                _button.interactable = isUnlocked;
                _button.onClick.AddListener(() => _onClicked?.Invoke());
            }
        }

        // ── 상태 갱신 ─────────────────────────────────────────────────────

        /// <summary>완료 아이콘 표시 여부를 갱신합니다.</summary>
        public void SetCompleted(bool completed)
        {
            if (_completedIcon != null)
                _completedIcon.SetActive(completed);
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveAllListeners();
        }
    }
}