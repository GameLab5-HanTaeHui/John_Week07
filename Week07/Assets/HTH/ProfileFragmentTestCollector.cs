using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 프로파일 추리 테스트용 대화 조각 수집 컴포넌트입니다.
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   1. 씬에 빈 GameObject 생성 → ProfileFragmentTestCollector 추가
    ///   2. Inspector에서 FragmentCollector, CharacterRecordPanelManager 연결
    ///   3. Collect Character Id → 테스트할 캐릭터 ID (1~7)
    ///   4. Collect All Button → 버튼 연결 (클릭 시 해당 캐릭터 조각 5개 수집)
    ///   5. Status Text → 수집 현황 TMP (선택)
    ///
    /// ─── 동작 ────────────────────────────────────────────────────────────
    ///   CollectAll()   → 해당 캐릭터 P{id}_01 ~ P{id}_05 전부 수집
    ///   CollectOne()   → 조각 1개씩 순서대로 수집
    ///   ClearAll()     → 해당 캐릭터 조각 전부 초기화 (테스트 리셋)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Fragment Collector   → FragmentCollector 컴포넌트
    ///   Stage Id             → "Stage_1_Phase2"
    ///   Collect Character Id → 1~7
    ///   Collect All Button   → 전부 수집 버튼
    ///   Collect One Button   → 1개씩 수집 버튼
    ///   Clear Button         → 초기화 버튼
    ///   Status Text          → 수집 현황 TMP (선택)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileFragmentTestCollector : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("연결")]
        [Tooltip("FragmentCollector 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("설정")]
        [Tooltip("스테이지 ID입니다.\n예: Stage_1_Phase2")]
        [SerializeField] private string _stageId = "Stage_1_Phase2";

        [Tooltip("조각을 수집할 캐릭터 ID입니다. (1~7)")]
        [SerializeField][Range(1, 7)] private int _collectCharacterId = 1;

        [Tooltip("조각 총 개수입니다. (기본 5)")]
        [SerializeField][Range(1, 5)] private int _totalFragments = 5;

        [Header("버튼")]
        [Tooltip("해당 캐릭터의 조각 전부를 수집합니다.")]
        [SerializeField] private Button _collectAllButton;

        [Tooltip("조각을 1개씩 순서대로 수집합니다.")]
        [SerializeField] private Button _collectOneButton;

        [Tooltip("해당 캐릭터의 수집 기록을 초기화합니다.")]
        [SerializeField] private Button _clearButton;

        [Header("UI")]
        [Tooltip("수집 현황을 표시하는 TMP입니다. (선택)")]
        [SerializeField] private TMP_Text _statusText;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private int _nextFragmentIndex = 1; // 다음에 수집할 조각 인덱스

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _collectAllButton?.onClick.AddListener(CollectAll);
            _collectOneButton?.onClick.AddListener(CollectOne);
            _clearButton?.onClick.AddListener(ClearAll);
        }

        private void OnDestroy()
        {
            _collectAllButton?.onClick.RemoveListener(CollectAll);
            _collectOneButton?.onClick.RemoveListener(CollectOne);
            _clearButton?.onClick.RemoveListener(ClearAll);
        }

        private void Start()
        {
            // FragmentCollector 초기화
            _fragmentCollector?.Initialize(_stageId);
            RefreshStatus();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 해당 캐릭터의 조각 전부를 수집합니다.
        /// P{id:00}_01 ~ P{id:00}_05 형식으로 수집합니다.
        /// </summary>
        [ContextMenu("Collect All Fragments")]
        public void CollectAll()
        {
            if (_fragmentCollector == null)
            {
                Debug.LogError("[TestCollector] FragmentCollector가 연결되지 않았습니다.");
                return;
            }

            for (int i = 1; i <= _totalFragments; i++)
            {
                string fragmentId = MakeFragmentId(_collectCharacterId, i);
                _fragmentCollector.TryCollectFragment(fragmentId);
            }

            _nextFragmentIndex = _totalFragments + 1;
            RefreshStatus();
            Debug.Log($"[TestCollector] #{_collectCharacterId} 조각 전부 수집 완료");
        }

        /// <summary>
        /// 조각을 1개씩 순서대로 수집합니다.
        /// </summary>
        [ContextMenu("Collect One Fragment")]
        public void CollectOne()
        {
            if (_fragmentCollector == null)
            {
                Debug.LogError("[TestCollector] FragmentCollector가 연결되지 않았습니다.");
                return;
            }

            if (_nextFragmentIndex > _totalFragments)
            {
                Debug.Log($"[TestCollector] #{_collectCharacterId} 조각 전부 수집됨");
                return;
            }

            string fragmentId = MakeFragmentId(_collectCharacterId, _nextFragmentIndex);
            _fragmentCollector.TryCollectFragment(fragmentId);
            _nextFragmentIndex++;

            RefreshStatus();
            Debug.Log($"[TestCollector] 수집 — {fragmentId}");
        }

        /// <summary>
        /// 해당 캐릭터의 수집 기록을 초기화합니다.
        /// </summary>
        [ContextMenu("Clear All Fragments")]
        public void ClearAll()
        {
            if (_fragmentCollector == null) return;

            _fragmentCollector.Clear(_stageId);
            _fragmentCollector.Initialize(_stageId);
            _nextFragmentIndex = 1;

            RefreshStatus();
            Debug.Log($"[TestCollector] #{_collectCharacterId} 수집 기록 초기화");
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>FragmentId를 생성합니다. 예: "P01_01"</summary>
        private string MakeFragmentId(int charId, int fragIndex)
            => $"P{charId:00}_{fragIndex:00}";

        private void RefreshStatus()
        {
            if (_statusText == null || _fragmentCollector == null) return;

            int count = _fragmentCollector.GetFragmentCount(_collectCharacterId);
            int total = _totalFragments;

            _statusText.text =
                $"#{_collectCharacterId} 수집 현황\n" +
                $"{count} / {total} 조각\n" +
                $"추리 가능: {(count >= total ? "✓" : $"{total - count}개 부족")}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshStatus();
        }
#endif
    }
}