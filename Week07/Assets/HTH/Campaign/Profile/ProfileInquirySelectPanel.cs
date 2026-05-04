using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 최종 추리 캐릭터 선택 패널입니다.
    ///
    /// ─── 동작 흐름 ────────────────────────────────────────────────────────
    ///   1. Show()           → 캐릭터 아이콘 그리드 표시
    ///   2. 아이콘 클릭      → 경고 패널 표시 (되돌릴 수 없음 안내)
    ///   3. [확인] 클릭      → Hide() + FinalTalkUI.Show(characterId)
    ///   4. [취소] 클릭      → 경고 패널 닫기, 선택 해제
    ///
    /// ─── 버튼 활성화 조건 ────────────────────────────────────────────────
    ///   조각 5개 이상 수집 시 활성화 (clue 5 + hint 5 = 슬롯 10개)
    ///   이미 완료한 캐릭터는 비활성 + 퍼스널 컬러
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   메인 패널, 아이콘 프리팹/그리드, 닫기 버튼
    ///   경고 패널 (루트 GameObject, TitleText, ConfirmButton, CancelButton)
    ///   FinalTalkUI, FragmentCollector, ProfileDataSO
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquirySelectPanel : MonoBehaviour
    {
        // ── Inspector — 메인 패널 ────────────────────────────────────────

        [Header("메인 패널")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private CharacterIconButton _iconButtonPrefab;
        [SerializeField] private Transform _iconGrid;
        [SerializeField] private Button _closeButton;

        [Tooltip("'최종 대화 2 / 6 완료' 형식 진행도 TMP")]
        [SerializeField] private TMP_Text _progressText;

        // ── Inspector — 경고 패널 ────────────────────────────────────────

        [Header("경고 패널")]
        [Tooltip("경고 패널 루트 GameObject")]
        [SerializeField] private GameObject _warningPanel;

        [Tooltip("경고 패널 타이틀 TMP")]
        [SerializeField] private TMP_Text _warningTitleText;

        [Tooltip("확인 버튼 (최종 대화 진입)")]
        [SerializeField] private Button _warningConfirmButton;

        [Tooltip("취소 버튼 (선택 해제)")]
        [SerializeField] private Button _warningCancelButton;

        [Tooltip("경고 패널에 표시할 메시지")]
        [SerializeField] private string _warningMessage = "최종 대화는 되돌릴 수 없습니다.\n정말 진행하시겠습니까?";

        // ── Inspector — 연결 ─────────────────────────────────────────────

        [Header("연결")]
        [SerializeField] private FinalTalkUI _finalTalkUI;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private ProfileDataSO _profileData;

        // ── Inspector — 설정 ─────────────────────────────────────────────

        [Header("설정")]
        [Tooltip("전체 최종 대화 대상 캐릭터 수. 기본 6.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 퍼스널 컬러 (캐릭터 ID 인덱스 대응) ─────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,                                    // [0] 미사용
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),    // [1] 엔비
            new Color(0x48/255f, 0x78/255f, 0x48/255f),    // [2] 메이
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),    // [3] 루이스
            new Color(0x58/255f, 0x58/255f, 0x88/255f),    // [4] 데우스
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),    // [5] 토니
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),    // [6] 프리드
            new Color(0x98/255f, 0x88/255f, 0x68/255f),    // [7] 새턴
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly Dictionary<int, CharacterIconButton> _buttonMap = new();
        private bool _isBuilt;
        private int _pendingCharacterId = -1;  // 경고 패널 확인 대기 중인 캐릭터 ID
        private int _selectAttemptCount;       // C12 로그용 선택 시도 카운터

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_warningPanel != null) _warningPanel.SetActive(false);

            _closeButton?.onClick.AddListener(Hide);
            _warningConfirmButton?.onClick.AddListener(OnWarningConfirmed);
            _warningCancelButton?.onClick.AddListener(OnWarningCancelled);
        }

        private void Start()
        {
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
            _warningConfirmButton?.onClick.RemoveListener(OnWarningConfirmed);
            _warningCancelButton?.onClick.RemoveListener(OnWarningCancelled);

            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        public void Show()
        {
            if (!_isBuilt) BuildGrid();
            else RefreshButtonStates();

            if (_panel != null) _panel.SetActive(true);
        }

        public void Hide()
        {
            foreach (var btn in _buttonMap.Values)
                btn?.SetSelected(false);

            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// FinalTalkUI에서 캐릭터 1명 완료 후 호출합니다.
        /// 완료된 캐릭터 버튼을 퍼스널 컬러 + 비활성으로 갱신합니다.
        /// </summary>
        public void FinalTalkAndButtonUpdate()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;

            for (int charId = 2; charId <= 7; charId++)
            {
                if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) continue;

                var record = saveData?.finalTalkRecords?.Find(r => r.characterId == charId);
                bool isCompleted = record?.completed ?? false;
                bool isSuccess = record?.success ?? false;

                if (!isCompleted) continue;

                btn.SetCompletedColor(PersonalColors[charId]);
                btn.SetClearedState(isCompleted, isSuccess);
            }
        }

        // ── 조각 획득 콜백 ───────────────────────────────────────────────

        private void OnFragmentCollected(string fragmentId)
        {
            // "P02_01" → charId = 2
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return;
            int sep = fragmentId.IndexOf('_');
            if (sep <= 1) return;
            if (!int.TryParse(fragmentId.Substring(1, sep - 1), out int charId)) return;
            if (charId < 2 || charId > 7) return;

            RefreshSingleButton(charId);
        }

        // ── 그리드 구성 ──────────────────────────────────────────────────

        private void BuildGrid()
        {
            if (_iconButtonPrefab == null || _iconGrid == null) return;

            _isBuilt = true;

            for (int charId = 2; charId <= 7; charId++)
            {
                int capturedId = charId;

                var profile = _profileData?.FindProfile(capturedId);
                Sprite icon = profile?.CharacterIcon;
                string name = profile?.CharacterFullName ?? $"#{capturedId}";

                var btn = Instantiate(_iconButtonPrefab, _iconGrid);
                btn.Setup(
                    characterId: capturedId,
                    icon: icon,
                    collectedName: name,
                    onClicked: () => OnCharacterSelected(capturedId));
                _buttonMap[capturedId] = btn;
            }

            RefreshButtonStates();
        }

        private void RefreshButtonStates()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;

            int completedCount = saveData?.finalTalkRecords?.FindAll(r => r.completed).Count ?? 0;
            if (_progressText != null)
                _progressText.text = $"최종 대화 {completedCount} / {_totalCharacters} 완료";

            for (int charId = 2; charId <= 7; charId++)
                RefreshSingleButton(charId);
        }

        private void RefreshSingleButton(int charId)
        {
            if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) return;

            var profile = _profileData?.FindProfile(charId);
            string name = profile?.CharacterFullName ?? $"#{charId}";
            int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
            bool canInquire = fragmentCount >= 5;

            btn.RefreshDisplay(name, fragmentCount, maxFragments: 10);

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            var record = saveData?.finalTalkRecords?.Find(r => r.characterId == charId);
            bool isCompleted = record?.completed ?? false;
            bool isSuccess = record?.success ?? false;

            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                if (isCompleted)
                {
                    button.interactable = false;
                    btn.SetCompletedColor(PersonalColors[charId]);
                }
                else
                    button.interactable = canInquire;
            }
            btn.SetClearedState(isCompleted, isSuccess);
        }

        // ── 캐릭터 선택 ──────────────────────────────────────────────────

        private void OnCharacterSelected(int characterId)
        {
            foreach (var kv in _buttonMap)
                kv.Value?.SetSelected(kv.Key == characterId);

            // C12 — final_talk_character_select
            GameLogger.Instance?.LogEvent("final_talk_character_select", new Dictionary<string, object>
            {
                { "character_id",  characterId         },
                { "attempt_index", _selectAttemptCount },
            });
            _selectAttemptCount++;

            ShowWarningPanel(characterId);
        }

        // ── 경고 패널 ────────────────────────────────────────────────────

        private void ShowWarningPanel(int characterId)
        {
            _pendingCharacterId = characterId;

            if (_warningTitleText != null)
                _warningTitleText.text = _warningMessage;

            if (_warningPanel != null)
                _warningPanel.SetActive(true);
        }

        private void HideWarningPanel()
        {
            if (_warningPanel != null)
                _warningPanel.SetActive(false);

            _pendingCharacterId = -1;
        }

        private void OnWarningConfirmed()
        {
            int characterId = _pendingCharacterId;
            HideWarningPanel();
            Hide();
            _finalTalkUI?.Show(characterId);
        }

        private void OnWarningCancelled()
        {
            HideWarningPanel();

            foreach (var kv in _buttonMap)
                kv.Value?.SetSelected(false);
        }
    }
}