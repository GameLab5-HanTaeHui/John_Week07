using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 버튼에 붙여서 씬 진입을 설정합니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   이 버튼이 담당하는 씬 진입 방식(튜토리얼/기본/캠페인)을 설정합니다.
    ///   씬 이동과 NewGameConfig 설정은 CampaignLobbyNavigator에 위임합니다.
    ///   스테이지 클리어 취소선 표시를 담당합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Mode           → 진입 모드 선택 (Tutorial / NewCampaign / CustomSeed)
    ///   Stage Id       → CustomSeed 모드에서 사용할 스테이지 ID
    ///   Game Scene Name → CustomSeed 모드에서 이동할 씬 이름
    ///   Seed           → CustomSeed 모드에서 사용할 시드값
    ///   Is Enabled     → false이면 버튼 비활성화 (미개방 스테이지 차단)
    ///   Label          → 클리어 취소선 표시할 TMP_Text
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LobbyPresetSeedButton : MonoBehaviour
    {
        public enum EntryMode
        {
            /// <summary>튜토리얼 씬으로 이동합니다.</summary>
            Tutorial,
            /// <summary>캠페인을 처음부터 시작합니다. (Navigator.StartNewCampaign)</summary>
            NewCampaign,
            /// <summary>지정한 Seed/StageId/Scene으로 이동합니다. (기존 동작 유지)</summary>
            CustomSeed,
        }

        [Header("진입 모드")]
        [Tooltip("Tutorial       — 튜토리얼 씬으로 이동\n" +
                 "NewCampaign    — 캠페인 처음부터 시작\n" +
                 "CustomSeed     — 직접 Seed/StageId/Scene 지정")]
        [SerializeField] private EntryMode _mode = EntryMode.CustomSeed;

        [Header("CustomSeed 설정 (_mode = CustomSeed일 때만 사용)")]
        [SerializeField] private int _seed;
        [SerializeField] private string _stageId;
        [SerializeField] private string _gameSceneName = "Stage_1";

        [Header("스테이지 활성화")]
        [Tooltip("false로 설정하면 버튼이 비활성화됩니다.")]
        [SerializeField] private bool _isEnabled = true;

        [Header("클리어 취소선")]
        [SerializeField] private TMP_Text _label;

        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var btn = GetComponent<Button>();
            btn.onClick.AddListener(OnClicked);
            btn.interactable = _isEnabled;
            RefreshLabel();
        }

        private void OnDisable()
        {
            GetComponent<Button>().onClick.RemoveListener(OnClicked);
        }

        // ── 버튼 클릭 ────────────────────────────────────────────────────

        private void OnClicked()
        {
            switch (_mode)
            {
                case EntryMode.Tutorial:
                    CampaignLobbyNavigator.Instance?.StartTutorial();
                    break;

                case EntryMode.NewCampaign:
                    CampaignLobbyNavigator.Instance?.StartNewCampaign();
                    break;

                case EntryMode.CustomSeed:
                    TurnHistoryRepository.Instance.ClearAll();
                    NewGameConfig.SetSeed(_seed, _stageId);
                    NewGameConfig.ForceStartAsPhase2 = false;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
                    break;
            }
        }

        // ── 취소선 갱신 ──────────────────────────────────────────────────

        private void RefreshLabel()
        {
            if (_label == null) return;
            bool cleared = StageClearRepository.Instance.HasCleared(_stageId);
            string raw = StripStrikethrough(_label.text);
            _label.text = cleared ? $"<s>{raw}</s>" : raw;
        }

        private static string StripStrikethrough(string text)
            => text.Replace("<s>", "").Replace("</s>", "");
    }
}