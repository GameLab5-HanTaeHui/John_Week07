namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 조건을 판별합니다.
    ///
    /// ─── 판별 조건 ───────────────────────────────────────────────────────
    ///   1. 이미 출력된 대사인지     (DialogueProgressTracker)
    ///   2. 필요한 대화 조각 수집됐는지 (FragmentCollector)
    ///   3. 최소 루프 횟수 충족 여부 (GameFlowController)
    ///   4. Phase2 활성 여부       (CampaignModeManager)
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   DialogueTriggerManager에서 new DialogueConditionEvaluator()로 생성합니다.
    ///   MonoBehaviour가 아닌 순수 C# 클래스입니다.
    /// </summary>
    public class DialogueConditionEvaluator
    {
        // ── 그룹 대사 조건 판별 ───────────────────────────────────────────

        /// <summary>
        /// GroupDialogueEntry 출력 가능 여부를 반환합니다.
        ///
        /// 차단 조건:
        ///   - Phase2가 활성화되지 않음
        ///   - 이미 출력된 대사
        ///   - 필요한 조각이 아직 미수집
        ///   - 루프 횟수 조건 미충족
        /// </summary>
        public bool CanPlay(GroupDialogueEntry entry,
                            System.Collections.Generic.HashSet<int> characterIds,
                            DialogueProgressTracker tracker,
                            FragmentCollector collector)
        {
            if (entry == null) return false;

            // Phase2 활성 체크
            if (!CampaignModeManager.IsPhase2Active) return false;

            // 이미 출력된 조합인지 체크
            if (tracker != null && tracker.HasPlayedGroup(characterIds)) return false;

            // 조건 데이터 체크
            if (entry.Condition != null && !entry.Condition.IsEmpty)
            {
                if (!EvaluateCondition(entry.Condition, collector)) return false;
            }

            return true;
        }

        // ── 단독 대사 조건 판별 ───────────────────────────────────────────

        /// <summary>
        /// SoloDialogueEntry 출력 가능 여부를 반환합니다.
        ///
        /// 차단 조건:
        ///   - Phase2가 활성화되지 않음
        ///   - 이미 출력된 캐릭터
        ///   - 필요한 조각이 아직 미수집
        ///   - 루프 횟수 조건 미충족
        /// </summary>
        public bool CanPlay(SoloDialogueEntry entry,
                            DialogueProgressTracker tracker,
                            FragmentCollector collector)
        {
            if (entry == null) return false;

            // Phase2 활성 체크
            if (!CampaignModeManager.IsPhase2Active) return false;

            // 이미 출력된 캐릭터인지 체크
            if (tracker != null && tracker.HasPlayedSolo(entry.CharacterId)) return false;

            // 조건 데이터 체크
            if (entry.Condition != null && !entry.Condition.IsEmpty)
            {
                if (!EvaluateCondition(entry.Condition, collector)) return false;
            }

            return true;
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>DialogueConditionData의 모든 조건을 평가합니다.</summary>
        private bool EvaluateCondition(DialogueConditionData condition,
                                       FragmentCollector collector)
        {
            // 필요 조각 수집 여부 체크
            if (!string.IsNullOrEmpty(condition.RequiredFragmentId))
            {
                if (collector == null) return false;
                if (!collector.HasFragment(condition.RequiredFragmentId)) return false;
            }

            // 최소 루프 횟수 체크
            if (condition.MinLoopCount > 0)
            {
                var gfc = GameFlowController.Instance;
                if (gfc == null) return false;

                // LoopCount는 1-based이므로 그대로 비교
                if (gfc.LoopCount < condition.MinLoopCount) return false;
            }

            return true;
        }
    }
}