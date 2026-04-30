using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// LobbyPage2 테스트용 헬퍼입니다.
    ///
    /// ─── 기능 ────────────────────────────────────────────────────────────
    ///   에필로그 전체 해금   → 7개 캐릭터 에필로그를 즉시 해금
    ///   에필로그 단일 해금   → 특정 캐릭터 에필로그 해금
    ///   전체 초기화         → 에필로그 해금 기록 전부 삭제
    ///   퍼즐 정답 힌트       → 현재 SecretOrder 출력
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   LobbyScene에 빈 GameObject 생성 → LobbyPage2TestHelper 추가
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Reward Save Data      → RewardSaveData 에셋
    ///   Page2 Controller      → LobbyPage2Controller
    ///   Stage Id              → "Stage_1_Phase2"
    ///   Unlock All Button     → 전체 해금 버튼
    ///   Unlock One Button     → 단일 해금 버튼
    ///   Clear All Button      → 전체 초기화 버튼
    ///   Print Order Button    → 퍼즐 순서 출력 버튼
    ///   Unlock Character Id   → 단일 해금 대상 캐릭터 ID (1~7)
    ///   Status Text           → 해금 현황 TMP (선택)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage2TestHelper : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("연결")]
        [Tooltip("RewardSaveData 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Tooltip("LobbyPage2Controller 컴포넌트입니다.")]
        [SerializeField] private LobbyPage02Manager _page2Controller;

        [Header("설정")]
        [Tooltip("스테이지 ID입니다.")]
        [SerializeField] private string _stageId = "Stage_1_Phase2";

        [Tooltip("단일 해금 대상 캐릭터 ID (1~7)")]
        [SerializeField][Range(1, 7)] private int _unlockCharacterId = 1;

        [Header("버튼")]
        [Tooltip("7개 에필로그 전부 즉시 해금합니다.")]
        [SerializeField] private Button _unlockAllButton;

        [Tooltip("_unlockCharacterId 번 캐릭터만 해금합니다.")]
        [SerializeField] private Button _unlockOneButton;

        [Tooltip("모든 에필로그 해금 기록을 삭제합니다.")]
        [SerializeField] private Button _clearAllButton;

        [Tooltip("Inspector/Console에 현재 해금 상태를 출력합니다.")]
        [SerializeField] private Button _printStatusButton;

        [Header("UI")]
        [Tooltip("현재 해금 상태를 표시하는 TMP입니다. (선택)")]
        [SerializeField] private TMP_Text _statusText;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _unlockAllButton?.onClick.AddListener(UnlockAll);
            _unlockOneButton?.onClick.AddListener(UnlockOne);
            _clearAllButton?.onClick.AddListener(ClearAll);
            _printStatusButton?.onClick.AddListener(PrintStatus);
        }

        private void OnDestroy()
        {
            _unlockAllButton?.onClick.RemoveListener(UnlockAll);
            _unlockOneButton?.onClick.RemoveListener(UnlockOne);
            _clearAllButton?.onClick.RemoveListener(ClearAll);
            _printStatusButton?.onClick.RemoveListener(PrintStatus);
        }

        private void Start()
        {
            RefreshStatusText();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>7개 캐릭터 에필로그를 전부 즉시 해금합니다.</summary>
        [ContextMenu("Unlock All Epilogues")]
        public void UnlockAll()
        {
            if (_rewardSaveData == null)
            {
                Debug.LogError("[LobbyPage2TestHelper] RewardSaveData 미연결");
                return;
            }

            for (int id = 1; id <= 7; id++)
                _rewardSaveData.SaveEpilogueUnlock(id);

            Debug.Log("[LobbyPage2TestHelper] 에필로그 7개 전부 해금");

            RefreshPage();
            RefreshStatusText();
        }

        /// <summary>특정 캐릭터 에필로그를 해금합니다.</summary>
        [ContextMenu("Unlock One Epilogue")]
        public void UnlockOne()
        {
            if (_rewardSaveData == null)
            {
                Debug.LogError("[LobbyPage2TestHelper] RewardSaveData 미연결");
                return;
            }

            _rewardSaveData.SaveEpilogueUnlock(_unlockCharacterId);

            Debug.Log($"[LobbyPage2TestHelper] #{_unlockCharacterId} 에필로그 해금");

            RefreshPage();
            RefreshStatusText();
        }

        /// <summary>에필로그 해금 기록을 전부 초기화합니다.</summary>
        [ContextMenu("Clear All Epilogues")]
        public void ClearAll()
        {
            if (_rewardSaveData == null) return;

            // CampaignSaveManager를 통해 저장 데이터 초기화
            var saveManager = CampaignSaveManager.Instance;
            if (saveManager != null)
            {
                saveManager.Delete(_stageId);
                saveManager.Load(_stageId);
            }

            // RewardSaveData 내부 초기화
            _rewardSaveData.Clear();

            Debug.Log("[LobbyPage2TestHelper] 에필로그 전체 초기화");

            RefreshPage();
            RefreshStatusText();
        }

        /// <summary>현재 해금 상태를 Console에 출력합니다.</summary>
        [ContextMenu("Print Status")]
        public void PrintStatus()
        {
            if (_rewardSaveData == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[LobbyPage2TestHelper] 에필로그 해금 현황");

            int count = 0;
            for (int id = 1; id <= 7; id++)
            {
                bool unlocked = _rewardSaveData.IsEpilogueUnlocked(id);
                sb.AppendLine($"  #{id}: {(unlocked ? "✓ 해금" : "× 잠금")}");
                if (unlocked) count++;
            }

            sb.AppendLine($"  총 {count}/7 해금");
            sb.AppendLine($"  퍼즐 모드 활성화: {(count == 7 ? "✓" : "×")}");

            Debug.Log(sb.ToString());
            RefreshStatusText();
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>LobbyPage2Controller를 갱신합니다.</summary>
        private void RefreshPage()
        {
            if (_page2Controller == null) return;

            // OnEnable을 직접 호출하는 것과 동일한 효과
            _page2Controller.gameObject.SetActive(false);
            _page2Controller.gameObject.SetActive(true);
        }

        private void RefreshStatusText()
        {
            if (_statusText == null || _rewardSaveData == null) return;

            int count = 0;
            var sb = new System.Text.StringBuilder();

            for (int id = 1; id <= 7; id++)
            {
                bool unlocked = _rewardSaveData.IsEpilogueUnlocked(id);
                sb.Append($"#{id}{(unlocked ? "✓" : "×")} ");
                if (unlocked) count++;
            }

            sb.AppendLine();
            sb.Append($"총 {count}/7");
            if (count == 7) sb.Append(" → 퍼즐 모드");

            _statusText.text = sb.ToString();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshStatusText();
        }
#endif
    }
}