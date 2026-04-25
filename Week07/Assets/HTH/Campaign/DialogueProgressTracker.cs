using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 기록을 관리합니다.
    ///
    /// ─── 관리 데이터 ─────────────────────────────────────────────────────
    ///   출력된 GroupDialogue 키 목록 (조합 키: "1,3,5" 형식)
    ///   출력된 SoloDialogue 캐릭터 ID 목록
    ///   PlayerPrefs를 통한 세이브/로드
    ///
    /// ─── 1회차 규칙 ──────────────────────────────────────────────────────
    ///   각 캐릭터가 한 번씩 출력되면 이후 스킵
    ///   (기획서: "각 캐릭터가 한 번씩 다이얼로그를 출력하면 그 이후에는 출력하지 않도록")
    ///
    /// ─── 조합 키 규칙 ────────────────────────────────────────────────────
    ///   캐릭터 ID를 오름차순 정렬 후 쉼표로 연결
    ///   예: {1, 3, 5} → "1,3,5"
    ///   같은 조합은 항상 같은 키를 가집니다.
    ///
    /// ─── PlayerPrefs 키 규칙 ─────────────────────────────────────────────
    ///   그룹 대사: "hth_campaign_{stageId}_group_{groupKey}"
    ///   단독 대사: "hth_campaign_{stageId}_solo_{characterId}"
    /// </summary>
    public class DialogueProgressTracker
    {
        private const string GROUP_KEY_PREFIX = "hth_campaign_{0}_group_{1}";
        private const string SOLO_KEY_PREFIX = "hth_campaign_{0}_solo_{1}";

        private readonly HashSet<string> _playedGroupKeys = new();
        private readonly HashSet<int> _playedSoloIds = new();
        private readonly HashSet<string> _playedFragmentIds = new();  // 이번 세션에서 출력된 FragmentId

        private string _currentStageId;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 ID를 설정하고 저장된 기록을 로드합니다.
        /// CampaignModeManager.OnPhase2Entered 이벤트 수신 시 호출합니다.
        /// </summary>
        public void Initialize(string stageId)
        {
            // 스테이지 진입마다 초기화 — 메모리에만 유지, PlayerPrefs 저장 안 함
            _currentStageId = stageId;
            _playedGroupKeys.Clear();
            _playedSoloIds.Clear();
            _playedFragmentIds.Clear();
            Debug.Log($"[DialogueProgressTracker] 초기화 완료 — {stageId} (출력 기록 리셋)");
        }

        // ── 출력 여부 확인 ────────────────────────────────────────────────

        /// <summary>해당 캐릭터 조합의 그룹 대사가 이미 출력됐는지 확인합니다.</summary>
        public bool HasPlayedGroup(HashSet<int> characterIds)
        {
            if (characterIds == null || characterIds.Count == 0) return false;
            string key = BuildGroupKey(characterIds);
            return _playedGroupKeys.Contains(key);
        }

        /// <summary>해당 캐릭터의 단독 대사가 이미 출력됐는지 확인합니다.</summary>
        public bool HasPlayedSolo(int characterId)
            => _playedSoloIds.Contains(characterId);

        // ── 출력 기록 ─────────────────────────────────────────────────────

        /// <summary>
        /// 그룹 대사 출력 완료를 기록합니다.
        /// DialogueTriggerManager에서 DialoguePlayer 재생 완료 후 호출합니다.
        /// </summary>
        public void MarkGroupPlayed(HashSet<int> characterIds)
        {
            if (characterIds == null || characterIds.Count == 0) return;

            string key = BuildGroupKey(characterIds);
            if (_playedGroupKeys.Add(key))
                Debug.Log($"[DialogueProgressTracker] 그룹 대사 기록 — {key}");
        }

        /// <summary>
        /// 단독 대사 출력 완료를 기록합니다.
        /// DialogueTriggerManager에서 DialoguePlayer 재생 완료 후 호출합니다.
        /// </summary>
        public void MarkSoloPlayed(int characterId)
        {
            _playedSoloIds.Add(characterId);
            Debug.Log($"[DialogueProgressTracker] 단독 대사 기록 — ID:{characterId}");
        }
        /// <summary>
        /// FragmentId 출력 완료를 기록합니다.
        /// 이번 세션에서 이미 대사가 나온 FragmentId를 추적합니다.
        /// 씬 재시작(로비 이동, 게임 종료) 시 리셋됩니다.
        /// </summary>
        public void MarkFragmentPlayed(string fragmentId)
        {
            if (!string.IsNullOrEmpty(fragmentId))
                _playedFragmentIds.Add(fragmentId);
        }

        /// <summary>이번 세션에서 해당 FragmentId의 대사가 출력됐는지 확인합니다.</summary>
        public bool HasPlayedFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return false;
            return _playedFragmentIds.Contains(fragmentId);
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 ID 집합으로 조합 키를 생성합니다.
        /// 정렬 후 쉼표로 연결: {1,3,5} → "1,3,5"
        /// </summary>
        private string BuildGroupKey(HashSet<int> characterIds)
        {
            var sorted = new List<int>(characterIds);
            sorted.Sort();

            var sb = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(sorted[i]);
            }
            return sb.ToString();
        }
    }
}