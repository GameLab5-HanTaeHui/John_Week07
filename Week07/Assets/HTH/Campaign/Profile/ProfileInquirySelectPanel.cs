using System.Collections.Generic;
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

        [Header("연결")]
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private ProfileDataSO _profileData;

        // ── 퍼스널 컬러 ───────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f), // #1 엔비
            new Color(0x48/255f, 0x78/255f, 0x48/255f), // #2 메이
            new Color(0x58/255f, 0x58/255f, 0x88/255f), // #3 데우스
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f), // #4 루이스
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f), // #5 토니
            new Color(0xE8/255f, 0x88/255f, 0x68/255f), // #6 프리드
            new Color(0x98/255f, 0x88/255f, 0x68/255f), // #7 새턴
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // 캐릭터 ID 2~7 → 인덱스 0~5
        private readonly Dictionary<int, CharacterIconButton> _buttonMap = new();
        private bool _isBuilt;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            _closeButton?.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
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

            for (int charId = 2; charId <= 7; charId++)
            {
                if (!_buttonMap.TryGetValue(charId, out var btn) || btn == null) continue;

                // 10개 전부 수집했는지 확인 (clue 5개 = 수집 5개)
                int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
                bool canInquire = fragmentCount >= 5; // 5개 수집 = 10슬롯 해금

                // 최종 추리 완료 여부
                var record = saveData?.finalTalkRecords?.Find(r => r.characterId == charId);
                bool isCompleted = record?.completed ?? false;
                bool isSuccess = record?.success ?? false;

                var button = btn.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    if (isCompleted)
                    {
                        // 완료 → 비활성 + 퍼스널컬러
                        button.interactable = false;
                        var cb = button.colors;
                        cb.disabledColor = charId < PersonalColors.Length
                            ? PersonalColors[charId]
                            : Color.white;
                        button.colors = cb;
                    }
                    else
                    {
                        button.interactable = canInquire;
                    }
                }

                // ProfileText (completeText) — 완료 시 성공/실패 표시
                btn.SetClearedState(isCompleted, isSuccess);

                Debug.Log($"[ProfileInquirySelectPanel] #{charId} " +
                          $"조각:{fragmentCount}/5, 활성:{canInquire}, 완료:{isCompleted}");
            }
        }

        // ── Private — 이벤트 ─────────────────────────────────────────────

        private void OnCharacterSelected(int characterId)
        {
            foreach (var kv in _buttonMap)
                kv.Value?.SetSelected(kv.Key == characterId);

            Hide();
            _profileInquiryUI?.Show(characterId);
        }
    }
}