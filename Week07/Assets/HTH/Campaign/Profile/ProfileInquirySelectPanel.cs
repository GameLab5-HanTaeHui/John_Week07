using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    [DisallowMultipleComponent]
    public class ProfileInquirySelectPanel : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private CharacterIconButton _iconButtonPrefab;
        [SerializeField] private Transform _iconGrid;
        [SerializeField] private Button _closeButton;

        [Header("확인 팝업 (대화 진입 확인용)")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField]
        [TextArea(2, 3)]
        private string _confirmMessage = "이 대화는 되돌릴 수 없습니다.\n정말 대화를 시작하시겠습니까?";

        [Header("연결")]
        [SerializeField] private FinalTalkUI _finalTalkUI;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private ProfileDataSO _profileData;

        [Header("설정")]
        [SerializeField] private int _totalCharacters = 6;

        // ── 퍼스널 컬러 ───────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),
            new Color(0x48/255f, 0x78/255f, 0x48/255f),
            new Color(0x58/255f, 0x58/255f, 0x88/255f),
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),
            new Color(0x98/255f, 0x88/255f, 0x68/255f),
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly Dictionary<int, CharacterIconButton> _buttonMap = new();
        private bool _isBuilt;
        private int _pendingCharacterId = -1;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_confirmPanel != null) _confirmPanel.SetActive(false);

            _closeButton?.onClick.AddListener(Hide);
            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
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

            _confirmPanel?.SetActive(false);
            _pendingCharacterId = -1;

            if (_panel != null) _panel.SetActive(false);
        }

        // ── Private — 그리드 생성 ─────────────────────────────────────────

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

                int fragmentCount = _fragmentCollector?.GetFragmentCount(capturedId) ?? 0;
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

            int completedCount = saveData?.finalTalkRecords?.FindAll(r => r.completed).Count ?? 0;

            for (int charId = 2; charId <= 7; charId++)
            {
                if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) continue;

                int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
                bool canInquire = fragmentCount >= 5;

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
                        cb.disabledColor = charId < PersonalColors.Length ? PersonalColors[charId] : Color.white;
                        button.colors = cb;
                    }
                    else
                    {
                        button.interactable = canInquire;
                    }
                }
                btn.SetClearedState(isCompleted, isSuccess);
            }
        }

        // ── Private — 이벤트 (팝업 로직) ────────────────────────────────────────

        private void OnCharacterSelected(int characterId)
        {
            // 시각적으로 선택 상태 표시
            foreach (var kv in _buttonMap)
                kv.Value?.SetSelected(kv.Key == characterId);

            _pendingCharacterId = characterId;

            // 팝업 띄우기
            if (_confirmPanel != null)
            {
                if (_confirmText != null) _confirmText.text = _confirmMessage;
                _confirmPanel.SetActive(true);
            }
            else
            {
                // 팝업 연결이 안 되어있다면 바로 진행하는 예외 처리
                OnConfirmClicked();
            }
        }

        private void OnConfirmClicked()
        {
            if (_pendingCharacterId < 0) return;

            int targetId = _pendingCharacterId;
            Hide(); // 현재 Select 패널(및 팝업) 숨기기

            _finalTalkUI?.Show(targetId); // 최종 대화 진입
        }

        private void OnCancelClicked()
        {
            _pendingCharacterId = -1;
            _confirmPanel?.SetActive(false);

            // 취소 시 버튼 선택 상태 해제
            foreach (var kv in _buttonMap)
                kv.Value?.SetSelected(false);
        }
    }
}