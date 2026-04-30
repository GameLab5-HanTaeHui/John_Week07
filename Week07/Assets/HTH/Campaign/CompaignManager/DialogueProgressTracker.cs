using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 기록을 관리합니다.
    ///
    /// ─── 기록 단위 ───────────────────────────────────────────────────────
    ///   DialogueId 기준으로 기록합니다. (CORE_P01_01 / HINT_P01_01 / NORMAL_C001 등)
    ///   같은 캐릭터 조합이라도 DialogueId가 다르면 별개로 관리됩니다.
    ///
    /// ─── 이전 버전과의 차이 ──────────────────────────────────────────────
    ///   ComboId / SoloId 이중 구조 → DialogueId 단일 기준으로 통합.
    ///   저장 키: playedComboIds → playedDialogueIds
    ///
    /// ─── FragmentId 기록 ─────────────────────────────────────────────────
    ///   FragmentId 수집 여부는 FragmentCollector에서 영구 관리합니다.
    ///   ProgressTracker는 FragmentId를 별도 추적하지 않습니다.
    ///
    /// ─── 저장 정책 ───────────────────────────────────────────────────────
    ///   씬 진입마다 메모리 초기화 후, CampaignSaveData.playedDialogueIds를 복원합니다.
    ///   같은 씬 내에서 출력된 DialogueId를 누적 관리합니다.
    ///   MarkPlayed() 호출 시 즉시 JSON 저장까지 반영합니다.
    ///
    /// ─── Core 대사 처리 ──────────────────────────────────────────────────
    ///   Core 타입은 RewardFragmentId 수집 여부로 재출력을 막습니다.
    ///   (DialogueConditionEvaluator 1단계에서 처리)
    ///   ProgressTracker는 Core/Non-Core를 구분하지 않고 DialogueId만 기록합니다.
    /// </summary>
    public class DialogueProgressTracker
    {
        /// <summary>
        /// 이번 씬 진입 이후 재생된 DialogueId 집합입니다.
        /// 씬 재시작 시 리셋됩니다.
        /// </summary>
        private readonly HashSet<string> _playedIds = new();

        private string _currentStageId;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 씬 진입 시 호출합니다.
        /// 메모리를 초기화하고 저장 데이터에서 출력 기록을 복원합니다.
        /// </summary>
        public void Initialize(string stageId)
        {
            _currentStageId = stageId;
            _playedIds.Clear();

            // 이어하기: 저장 데이터에서 출력된 DialogueId 복원
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData != null)
            {
                foreach (var id in saveData.playedDialogueIds)
                    if (!string.IsNullOrEmpty(id))
                        _playedIds.Add(id);
            }

            Debug.Log($"[DialogueProgressTracker] 초기화 완료 — {stageId} " +
                      $"(DialogueId {_playedIds.Count}개 복원)");
        }

        // ── 출력 여부 확인 ────────────────────────────────────────────────

        /// <summary>
        /// 해당 DialogueId가 이번 씬에서 이미 출력됐는지 확인합니다.
        /// DialogueConditionEvaluator의 1단계(Non-Core) / 4단계에서 호출합니다.
        /// </summary>
        public bool HasPlayed(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return false;
            return _playedIds.Contains(dialogueId);
        }

        // ── 출력 기록 ─────────────────────────────────────────────────────

        /// <summary>
        /// DialogueId 기준으로 대사 출력 완료를 기록합니다.
        /// DialogueTriggerManager에서 대사 재생 완료 후 호출합니다.
        /// 메모리 기록과 JSON 저장을 함께 처리합니다.
        /// </summary>
        public void MarkPlayed(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return;
            if (!_playedIds.Add(dialogueId)) return; // 이미 기록된 경우 무시

            Debug.Log($"[DialogueProgressTracker] 대사 기록 — {dialogueId}");

            // JSON 저장 즉시 반영
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            if (!saveData.playedDialogueIds.Contains(dialogueId))
                saveData.playedDialogueIds.Add(dialogueId);

            CampaignSaveManager.Instance.Save(saveData);
        }

        // ── 디버그 ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>[에디터 전용] 현재 기록된 모든 DialogueId를 출력합니다.</summary>
        [ContextMenu("Dump Played DialogueIds")]
        public void DumpPlayedIds()
        {
            if (_playedIds.Count == 0)
            {
                Debug.Log("[DialogueProgressTracker] 기록된 DialogueId 없음");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[DialogueProgressTracker] 기록된 DialogueId ({_playedIds.Count}개)");
            foreach (var id in _playedIds)
                sb.AppendLine($"  - {id}");
            Debug.Log(sb.ToString());
        }
#endif
    }
}