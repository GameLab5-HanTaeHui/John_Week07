using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 최종 추리 캐릭터 선택 패널입니다.
    ///
    /// ─── 버튼 활성화 조건 ────────────────────────────────────────────────
    ///   캐릭터별 clue(진실) 5개 + hint(거짓) 5개 = 총 10개 전부 수집 시 활성화
    ///   이미 최종 추리를 완료한 캐릭터는 비활성 + 퍼스널컬러 + ProfileText 표시
    ///
    /// ─── ProfileText (completeText) 표시 ─────────────────────────────────
    ///   최종 추리 성공 → "성공" 텍스트 표시
    ///   최종 추리 실패 → "실패" 텍스트 표시
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Panel               → 전체 패널 GameObject
    ///   Icon Button Prefab  → CharacterIconButton 프리팹
    ///   Icon Grid           → 버튼 배치 부모 Transform
    ///   Close Button        → 닫기 버튼
    ///   Profile Inquiry UI  → ProfileInquiryUI
    ///   Fragment Collector  → FragmentCollector
    ///   Profile Data        → ProfileDataSO
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquirySelectPanel : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private CharacterIconButton _iconButtonPrefab;
        [SerializeField] private Transform _iconGrid;
        [SerializeField] private Button _closeButton;

        [Tooltip("'최종 대화 2 / 6 완료' 형식의 진행도 TMP입니다.")]
        [SerializeField] private TMP_Text _progressText;

        [Tooltip("'최종 대화는 되돌릴 수 없습니다.' 경고 문구 TMP입니다.")]
        [SerializeField] private TMP_Text _warningText;

        [Header("연결")]
        [SerializeField] private FinalTalkUI _finalTalkUI;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private ProfileDataSO _profileData;

        [Header("설정")]
        [Tooltip("전체 최종 대화 대상 캐릭터 수입니다. 기본 6.")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 퍼스널 컬러 ───────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f), // #1 엔비
            new Color(0x48/255f, 0x78/255f, 0x48/255f), // #2 메이
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f), // #3 루이스
            new Color(0x58/255f, 0x58/255f, 0x88/255f), // #4 데우스
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f), // #5 토니
            new Color(0xE8/255f, 0x88/255f, 0x68/255f), // #6 프리드
            new Color(0x98/255f, 0x88/255f, 0x68/255f), // #7 새턴
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // 캐릭터 ID 2~7 → 인덱스 0~5
        private readonly Dictionary<int, CharacterIconButton> _buttonMap = new();
        private bool _isBuilt;
        private int _selectAttemptCount; // C12 — 최종 대화 선택 순서 카운터

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            _closeButton?.onClick.AddListener(Hide);
        }

        private void Start()
        {
            // ★ 조각 획득 시 버튼 즉시 갱신 — 패널이 열려있지 않아도 다음 Show() 시 반영
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
            if (_fragmentCollector != null)
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;
        }

        // ── 조각 획득 콜백 ───────────────────────────────────────────────

        private void OnFragmentCollected(string fragmentId)
        {
            // 조각 ID에서 캐릭터 ID 파싱 (P02_01 → 2)
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return;
            if (!int.TryParse(fragmentId.Substring(1, idx - 1), out int charId)) return;
            if (charId < 2 || charId > 7) return;

            // 해당 캐릭터 버튼만 갱신
            RefreshSingleButton(charId);
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
        public void FinalTalkAndButtonUpdate()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;

            for (int charId = 2; charId <= 7; charId++)
            {
                if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) continue; // ★ return 아닌 continue

                var record = saveData?.finalTalkRecords?.Find(r => r.characterId == charId);
                bool isCompleted = record?.completed ?? false;
                bool isSuccess = record?.success ?? false;

                if (!isCompleted) continue; // 완료되지 않은 캐릭터는 건드리지 않음

                btn.SetCompletedColor(PersonalColors[charId]);
                btn.SetClearedState(isCompleted, isSuccess);
            }
        }

        // ── Private — 그리드 생성 ─────────────────────────────────────────

        private void BuildGrid()
        {
            if (_iconButtonPrefab == null || _iconGrid == null) return;

            _isBuilt = true;

            // 캐릭터 #2~#7 (엔비 #1은 플레이어블이라 제외)
            for (int charId = 2; charId <= 7; charId++)
            {
                int capturedId = charId;

                var profile = _profileData?.FindProfile(capturedId);
                Sprite icon = profile?.CharacterIcon;
                string name = profile?.CharacterFullName ?? $"#{capturedId}";

                int fragmentCount = _fragmentCollector?.GetFragmentCount(capturedId) ?? 0;
                // clue+hint 각 5개 → 수집 1개당 2슬롯 = 최대 10개
                string countLabel = $"({fragmentCount * 2}/10)";
                string displayName = $"{name}\n<size=80%>조각 {countLabel}</size>";

                var btn = Instantiate(_iconButtonPrefab, _iconGrid);
                btn.Setup(
                    characterId: capturedId,
                    icon: icon,
                    collectedName: displayName,
                    onClicked: () => OnCharacterSelected(capturedId));

                _buttonMap[capturedId] = btn;
            }

            RefreshButtonStates();
        }

        private void RefreshButtonStates()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;

            // 진행도 갱신
            int completedCount = saveData?.finalTalkRecords?.FindAll(r => r.completed).Count ?? 0;
            if (_progressText != null)
                _progressText.text = $"최종 대화 {completedCount} / {_totalCharacters} 완료";

            // 경고 문구 (항상 표시)
            if (_warningText != null)
                _warningText.text = "최종 대화는 되돌릴 수 없습니다.";

            for (int charId = 2; charId <= 7; charId++)
                RefreshSingleButton(charId);
        }

        /// <summary>
        /// 단일 캐릭터 버튼의 이름/조각수/활성화 상태를 갱신합니다.
        /// 조각 획득 시 해당 캐릭터 버튼만 즉시 갱신하는 데 사용합니다.
        /// </summary>
        private void RefreshSingleButton(int charId)
        {
            if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) return;

            var profile = _profileData?.FindProfile(charId);
            string name = profile?.CharacterFullName ?? $"#{charId}";
            int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
            bool canInquire = fragmentCount >= 5; // 5개 수집 = 10슬롯 해금

            // ★ 이름 + 조각 수 갱신
            btn.RefreshDisplay(name, fragmentCount, maxFragments: 10);

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            var record = saveData?.finalTalkRecords?.Find(r => r.characterId == charId);
            bool isCompleted = record?.completed ?? false;
            bool isSuccess = record?.success ?? false;

            var button = btn.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                if (isCompleted)
                {
                    button.interactable = false;
                    var cb = button.colors;
                    cb.disabledColor = charId < PersonalColors.Length
                        ? PersonalColors[charId]
                        : Color.white;
                    button.colors = cb;
                }
                else
                    button.interactable = canInquire;
            }

            btn.SetClearedState(isCompleted, isSuccess);
        }

        // ── Private — 이벤트 ─────────────────────────────────────────────

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

            Hide();
            _finalTalkUI?.Show(characterId);
        }
    }
}