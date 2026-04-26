using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 기록을 관리합니다.
    ///
    /// ─── 기록 단위 ───────────────────────────────────────────────────────
    ///   ComboId 기준으로 기록합니다. (C001, COND_P01_01 등)
    ///   같은 캐릭터 조합이라도 ComboId가 다르면 별개로 관리됩니다.
    ///   예: [1,2,7] 구역에서 C026이 재생됐어도
    ///       COND_P01_01은 아직 미출력 → 다음 턴에 재생 가능
    ///
    /// ─── FragmentId 기록 ─────────────────────────────────────────────────
    ///   FragmentId 수집 여부는 FragmentCollector에서 영구 관리합니다.
    ///   ProgressTracker는 FragmentId를 별도 추적하지 않습니다.
    ///
    /// ─── 저장 정책 ───────────────────────────────────────────────────────
    ///   씬 진입마다 메모리 초기화 (PlayerPrefs 저장 없음)
    ///   같은 씬 내에서는 출력된 ComboId를 누적 관리합니다.
    ///   씬 재시작 시 리셋되므로 같은 씬에서 중복 재생만 방지합니다.
    ///
    /// ─── 단독 대사 ───────────────────────────────────────────────────────
    ///   SoloDialogue는 캐릭터 ID 기준으로 씬 내 1회만 재생합니다.
    /// </summary>
    public class DialogueProgressTracker
    {
        /// <summary>
        /// 이번 씬 진입 이후 재생된 ComboId 집합입니다.
        /// 씬 재시작 시 리셋됩니다.
        /// </summary>
        private readonly HashSet<string> _playedComboIds = new();

        /// <summary>이번 씬에서 재생된 단독 대사 캐릭터 ID 집합입니다.</summary>
        private readonly HashSet<int> _playedSoloIds = new();

        private string _currentStageId;

        // ── 초기화 ───────────────────────────────────────────────────────

        public void Initialize(string stageId)
        {
            _currentStageId = stageId;
            _playedComboIds.Clear();
            _playedSoloIds.Clear();

            // JSON 저장에서 재생된 ComboId 로드 (이어하기)
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
            {
                foreach (var comboId in saveData.playedComboIds)
                    if (!string.IsNullOrEmpty(comboId))
                        _playedComboIds.Add(comboId);
            }

            Debug.Log($"[DialogueProgressTracker] 초기화 완료 — {stageId} " +
                      $"(ComboId {_playedComboIds.Count}개 복원)");
        }

        // ── 출력 여부 확인 ────────────────────────────────────────────────

        /// <summary>
        /// 해당 ComboId의 대사가 이번 씬에서 이미 출력됐는지 확인합니다.
        /// </summary>
        public bool HasPlayedCombo(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return false;
            return _playedComboIds.Contains(comboId);
        }

        /// <summary>해당 캐릭터의 단독 대사가 이미 출력됐는지 확인합니다.</summary>
        public bool HasPlayedSolo(int characterId)
            => _playedSoloIds.Contains(characterId);

        // ── 출력 기록 ─────────────────────────────────────────────────────

        /// <summary>
        /// ComboId 기준으로 대사 출력 완료를 기록합니다.
        /// DialogueTriggerManager.PlayGroupDialogue() 완료 후 호출합니다.
        /// </summary>
        public void MarkComboPlayed(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            if (!_playedComboIds.Add(comboId)) return;

            Debug.Log($"[DialogueProgressTracker] 대사 기록 — ComboId:{comboId}");

            // JSON 저장 업데이트
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            if (!saveData.playedComboIds.Contains(comboId))
                saveData.playedComboIds.Add(comboId);

            CampaignSaveManager.Instance.Save(saveData);
        }

        /// <summary>단독 대사 출력 완료를 기록합니다.</summary>
        public void MarkSoloPlayed(int characterId)
        {
            _playedSoloIds.Add(characterId);
            Debug.Log($"[DialogueProgressTracker] 단독 대사 기록 — ID:{characterId}");
        }
    }
}