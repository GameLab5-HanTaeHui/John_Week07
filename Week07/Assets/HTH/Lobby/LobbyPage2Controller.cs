using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비 2페이지 (도감)를 관리합니다.
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   왼쪽 페이지
    ///     캐릭터 초상화 버튼 7개 (2열 배치)
    ///     해금된 캐릭터만 활성화, 미해금은 잠금 표시
    ///
    ///   오른쪽 페이지
    ///     캐릭터 이름 TMP
    ///     시점 완결문 TMP (스크롤 가능)
    ///     기본 상태: 안내 문구 표시
    ///
    ///   하단
    ///     ← 버튼 (1페이지로 돌아가기)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Portrait Buttons[7]   → 캐릭터 초상화 버튼 7개 (CharacterId 1~7 순서)
    ///   Lock Overlays[7]      → 미해금 잠금 오버레이 7개
    ///   Character Name Text   → 오른쪽 페이지 캐릭터 이름 TMP
    ///   Epilogue Text         → 오른쪽 페이지 시점 완결문 TMP
    ///   Default Message       → 캐릭터 미선택 시 안내 문구 GameObject
    ///   Prev Page Button      → 1페이지로 돌아가는 버튼
    ///   Epilogue Data SO      → EpilogueDataSO 에셋 (시점 완결문 데이터)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage2Controller : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("왼쪽 페이지 — 캐릭터 초상화")]
        [Tooltip("캐릭터 초상화 버튼 7개입니다.\n" +
                 "Element 0 = CharacterId 1 (엔비), Element 6 = CharacterId 7 (새턴)")]
        [SerializeField] private Button[] _portraitButtons = new Button[7];

        [Tooltip("미해금 캐릭터에 표시할 잠금 오버레이 7개입니다.")]
        [SerializeField] private GameObject[] _lockOverlays = new GameObject[7];

        [Header("오른쪽 페이지 — 시점 완결문")]
        [Tooltip("선택된 캐릭터 이름을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Tooltip("시점 완결문 내용을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _epilogueText;

        [Tooltip("캐릭터 미선택 시 표시할 안내 문구 GameObject입니다.")]
        [SerializeField] private GameObject _defaultMessage;

        [Header("페이지 이동")]
        [Tooltip("1페이지로 돌아가는 버튼입니다.")]
        [SerializeField] private Button _prevPageButton;

        [Header("데이터")]
        [Tooltip("시점 완결문 데이터 에셋입니다.")]
        [SerializeField] private EpilogueDataSO _epilogueData;

        [Tooltip("캠페인 보상 해금 기록 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private int _selectedCharacterId = -1;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            // 초상화 버튼 리스너 등록
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                int characterId = i + 1; // CharacterId 1~7
                _portraitButtons[i]?.onClick.AddListener(() => OnPortraitClicked(characterId));
            }

            _prevPageButton?.onClick.AddListener(OnPrevPageClicked);

            Refresh();
        }

        private void OnEnable()
        {
            // 페이지 진입 시마다 해금 상태 갱신
            Refresh();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>해금 상태에 따라 UI를 갱신합니다.</summary>
        public void Refresh()
        {
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                int characterId = i + 1;
                bool unlocked = _rewardSaveData != null &&
                              _rewardSaveData.IsEpilogueUnlocked(characterId);

                if (_portraitButtons[i] != null)
                    _portraitButtons[i].interactable = unlocked;

                if (_lockOverlays.Length > i && _lockOverlays[i] != null)
                    _lockOverlays[i].SetActive(!unlocked);
            }

            // 선택된 캐릭터가 있으면 유지, 없으면 기본 메시지
            ShowEpilogue(_selectedCharacterId);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void OnPortraitClicked(int characterId)
        {
            _selectedCharacterId = characterId;
            ShowEpilogue(characterId);
        }

        private void OnPrevPageClicked()
        {
            _selectedCharacterId = -1;
            var uiManager = FindObjectOfType<LobbyUIManager>();
            uiManager?.ShowPreviousChapter();
        }

        /// <summary>
        /// 선택된 캐릭터의 시점 완결문을 표시합니다.
        /// characterId가 -1이면 기본 안내 메시지를 표시합니다.
        /// </summary>
        private void ShowEpilogue(int characterId)
        {
            bool hasSelection = characterId > 0 &&
                              _rewardSaveData != null &&
                              _rewardSaveData.IsEpilogueUnlocked(characterId);

            // 기본 메시지 토글
            if (_defaultMessage != null)
                _defaultMessage.SetActive(!hasSelection);

            if (_characterNameText != null)
                _characterNameText.gameObject.SetActive(hasSelection);

            if (_epilogueText != null)
                _epilogueText.gameObject.SetActive(hasSelection);

            if (!hasSelection) return;

            // 캐릭터 이름 표시
            if (_characterNameText != null)
                _characterNameText.text = GetCharacterName(characterId);

            // 시점 완결문 내용 표시
            if (_epilogueText != null)
            {
                string content = _epilogueData != null
                    ? _epilogueData.GetEpilogue(characterId)
                    : $"[에필로그 데이터 없음 — CharacterId={characterId}]";
                _epilogueText.text = content;
            }
        }

        /// <summary>캐릭터 ID로 이름을 반환합니다.</summary>
        private string GetCharacterName(int characterId) => characterId switch
        {
            1 => "엔비",
            2 => "메이",
            3 => "데우스",
            4 => "루이스",
            5 => "토니",
            6 => "프리드",
            7 => "새턴",
            _ => $"#{characterId}"
        };
    }
}