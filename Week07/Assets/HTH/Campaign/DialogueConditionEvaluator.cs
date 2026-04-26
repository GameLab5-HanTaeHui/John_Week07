using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 조건을 판별합니다.
    ///
    /// ─── 지원하는 ConditionType ──────────────────────────────────────────
    ///   solo
    ///     해당 캐릭터가 조사 구역에 혼자 있을 때 출력합니다.
    ///
    ///   exact_group
    ///     조사 구역의 생존자 조합이 지정 참가자와 정확히 일치할 때 출력합니다.
    ///
    ///   group_contains
    ///     조사 구역에 지정 참가자가 모두 포함될 때 출력합니다.
    ///     나머지 인물이 있어도 허용됩니다.
    ///
    ///   after_death
    ///     직전 턴 또는 이번 턴 결과에서 사망이 발생한 뒤 출력합니다.
    ///     TriggerDeadIds가 있으면 해당 캐릭터의 사망 여부를 추가 확인합니다.
    ///
    ///   wanderer_kill (wanderer_death_then_exact_group)
    ///     새턴(#7)이 이동하고 이동 전 칸에 남은 인물이 배회자 효과로 사망했을 때.
    ///
    ///   sacrifice_intercept_trigger
    ///     프리드(#6)가 살인자/배회자 효과로 죽을 인물을 대신 받아냈을 때.
    ///
    ///   required_clue_count
    ///     해당 캐릭터의 핵심문장 획득 수가 일정 이상일 때 출력합니다.
    ///
    ///   solo_after_event
    ///     solo 조건 + 사망 사건 1회 이상 발생 후 출력합니다.
    ///
    ///   after_death_memory / after_death_or_memory
    ///     특정 캐릭터 사망 후 회상 조각으로 출력합니다.
    ///
    /// ─── 사용 방법 ───────────────────────────────────────────────────────
    ///   DialogueTriggerManager에서 new DialogueConditionEvaluator()로 생성합니다.
    ///   MonoBehaviour가 아닌 순수 C# 클래스입니다.
    ///
    ///   ConditionContext ctx = new ConditionContext
    ///   {
    ///       ZoneCharacterIds     = 현재 구역 캐릭터 ID 집합,
    ///       AllDeadThisTurn      = 이번 턴 사망 캐릭터 ID 집합,
    ///       TotalDeathCount      = 누적 사망 수,
    ///       WandererKillOccurred = 배회자 효과 발동 여부,
    ///       SacrificeOccurred    = 희생양 효과 발동 여부,
    ///   };
    ///   bool canPlay = evaluator.CanPlay(entry, zoneIds, tracker, collector, ctx);
    /// </summary>
    public class DialogueConditionEvaluator
    {
        // ── 그룹 대사 조건 판별 ───────────────────────────────────────────

        /// <summary>
        /// GroupDialogueEntry 출력 가능 여부를 반환합니다.
        /// SituationType 및 ComboKey 기반으로 7가지 조건을 평가합니다.
        /// </summary>
        /// <param name="entry">평가할 GroupDialogueEntry</param>
        /// <param name="zoneCharacterIds">현재 구역의 생존 캐릭터 ID 집합</param>
        /// <param name="tracker">출력 기록 추적기</param>
        /// <param name="collector">대화 조각 수집기</param>
        /// <param name="ctx">턴 컨텍스트 (사망 정보 등)</param>
        public bool CanPlay(
            GroupDialogueEntry entry,
            HashSet<int> zoneCharacterIds,
            DialogueProgressTracker tracker,
            FragmentCollector collector,
            ConditionContext ctx = null)
        {
            if (entry == null) return false;

            if (!CampaignModeManager.IsPhase2Active) return false;

            // 이미 출력된 조합 스킵
            if (tracker != null && tracker.HasPlayedCombo(entry.ComboId)) return false;

            // FragmentId 있으면 이미 수집된 조각 스킵
            if (!string.IsNullOrEmpty(entry.FragmentId))
                if (collector != null && collector.HasFragment(entry.FragmentId)) return false;

            // SituationType 기반 ConditionType 평가
            if (!string.IsNullOrEmpty(entry.SituationType))
            {
                if (!EvaluateSituationType(entry, zoneCharacterIds, collector, ctx))
                    return false;
            }
            else
            {
                // SituationType 없으면 기존 Condition 필드로 폴백 (하위 호환)
                if (entry.Condition != null && !entry.Condition.IsEmpty)
                    if (!EvaluateLegacyCondition(entry.Condition, collector))
                        return false;
            }

            return true;
        }

        // ── 단독 대사 조건 판별 ───────────────────────────────────────────

        /// <summary>
        /// SoloDialogueEntry 출력 가능 여부를 반환합니다.
        /// </summary>
        public bool CanPlay(
            SoloDialogueEntry entry,
            DialogueProgressTracker tracker,
            FragmentCollector collector)
        {
            if (entry == null) return false;
            if (!CampaignModeManager.IsPhase2Active) return false;

            // 이미 재생된 단독 대사 스킵
            if (tracker != null && tracker.HasPlayedSolo(entry.CharacterId)) return false;

            // FragmentId 있으면 이미 수집된 조각 스킵
            if (!string.IsNullOrEmpty(entry.FragmentId))
                if (collector != null && collector.HasFragment(entry.FragmentId)) return false;

            if (entry.Condition != null && !entry.Condition.IsEmpty)
                if (!EvaluateLegacyCondition(entry.Condition, collector)) return false;

            return true;
        }

        // ── Private — SituationType 평가 ─────────────────────────────────

        /// <summary>
        /// SituationType에 따라 조건을 분기 평가합니다.
        /// </summary>
        private bool EvaluateSituationType(
            GroupDialogueEntry entry,
            HashSet<int> zoneIds,
            FragmentCollector collector,
            ConditionContext ctx)
        {
            switch (entry.SituationType)
            {
                // ── 프로파일 계열 ─────────────────────────────────────────
                // ProfileClue 조건 시스템에서 별도 처리하므로 항상 true
                case "프로파일 핵심문장":
                case "프로파일 유도대사":
                    return true;

                // ── 사망 반응 계열 ────────────────────────────────────────
                case "사망 반응":
                case "사망 반응 / 연인 연쇄":
                case "사망 반응 / 배회자":
                case "사망 반응 / 살인자":
                case "사망 반응 / 복수자":
                case "사망 반응 / 희생양":
                    return EvaluateAfterDeath(entry, zoneIds, ctx);

                // ── 일반 대사 계열 ────────────────────────────────────────
                case "생존 조합 대사":
                case "2인 대화":
                case "3인 대화":
                case "전체 파티 대화":
                    return EvaluateComboKey(entry, zoneIds, collector);

                // ── 개인 독백 (solo / solo_after_event) ──────────────────
                case "개인 독백":
                    return EvaluateSolo(entry, zoneIds, ctx);

                default:
                    // 알 수 없는 SituationType은 ComboKey로 폴백
                    return EvaluateComboKey(entry, zoneIds, collector);
            }
        }

        // ── Private — ComboKey 평가 ───────────────────────────────────────

        /// <summary>
        /// ComboKey (#1|#2|#7, #1|ANY 등)를 파싱해 구역 조합과 매칭합니다.
        ///
        /// 규칙:
        ///   #1        → solo: zoneIds == {1}
        ///   #1|#2     → group_contains: zoneIds에 1, 2 모두 포함
        ///   #1|#2|#7  → group_contains: zoneIds에 1, 2, 7 모두 포함
        ///   #1|ANY    → group_contains: zoneIds에 1 포함 (나머지 무관)
        ///   #1|#2 또는 #3|#4 → 두 조합 중 하나라도 충족하면 true
        /// </summary>
        private bool EvaluateComboKey(
            GroupDialogueEntry entry,
            HashSet<int> zoneIds,
            FragmentCollector collector)
        {
            if (string.IsNullOrEmpty(entry.ComboKey)) return true;

            // "또는" 구분자로 복수 조합 처리
            string[] orCombos = entry.ComboKey.Split(new[] { " 또는 " },
                System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string combo in orCombos)
            {
                if (MatchesSingleCombo(combo.Trim(), zoneIds))
                {
                    // UnlockConditionId 추가 체크
                    if (!string.IsNullOrEmpty(entry.UnlockConditionId) && collector != null)
                        if (!collector.HasFragment(entry.UnlockConditionId)) return false;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 단일 ComboKey 문자열이 구역 조합과 일치하는지 확인합니다.
        /// </summary>
        private bool MatchesSingleCombo(string combo, HashSet<int> zoneIds)
        {
            // ANY 포함 → group_contains (지정 참가자만 있으면 나머지 무관)
            bool hasAny = combo.Contains("ANY");

            // #1|#2|ANY → [1, 2]만 파싱
            var parts = combo.Split('|');
            var requiredIds = new List<int>();

            foreach (string part in parts)
            {
                string p = part.Trim();
                if (p == "ANY") continue;
                if (p.StartsWith("#") && int.TryParse(p.Substring(1), out int id))
                    requiredIds.Add(id);
            }

            // 필수 참가자 포함 여부 확인
            foreach (int rid in requiredIds)
                if (!zoneIds.Contains(rid)) return false;

            // ANY 없고 단독이면 exact (zoneIds가 requiredIds와 정확히 일치)
            if (!hasAny && requiredIds.Count == 1)
                return zoneIds.Count == 1 && zoneIds.Contains(requiredIds[0]);

            return true;
        }

        // ── Private — Solo 평가 ───────────────────────────────────────────

        /// <summary>
        /// 개인 독백(solo / solo_after_event) 조건을 평가합니다.
        /// </summary>
        private bool EvaluateSolo(
            GroupDialogueEntry entry,
            HashSet<int> zoneIds,
            ConditionContext ctx)
        {
            if (entry.ParticipantIds == null || entry.ParticipantIds.Count == 0) return false;

            int soloId = entry.ParticipantIds[0];

            // 해당 캐릭터가 구역에 혼자 있어야 함
            if (!zoneIds.Contains(soloId)) return false;
            if (zoneIds.Count != 1) return false;

            // solo_after_event: 사망 사건 1회 이상 필요
            if (entry.ComboKey != null && entry.ComboKey.Contains("death_seen"))
                if (ctx == null || ctx.TotalDeathCount <= 0) return false;

            return true;
        }

        // ── Private — 사망 반응 평가 ──────────────────────────────────────

        /// <summary>
        /// 사망 반응 계열 조건을 평가합니다.
        /// </summary>
        private bool EvaluateAfterDeath(
            GroupDialogueEntry entry,
            HashSet<int> zoneIds,
            ConditionContext ctx)
        {
            if (ctx == null) return false;

            // 이번 턴 사망 발생 필수
            if (ctx.AllDeadThisTurn == null || ctx.AllDeadThisTurn.Count == 0)
                return false;

            // TriggerDeadIds: 특정 캐릭터가 이번 턴에 사망했어야 함
            if (entry.TriggerDeadIds != null && entry.TriggerDeadIds.Count > 0)
                foreach (int deadId in entry.TriggerDeadIds)
                    if (!ctx.AllDeadThisTurn.Contains(deadId)) return false;

            // 역할 기믹별 추가 조건
            switch (entry.SituationType)
            {
                case "사망 반응 / 배회자":
                    // 새턴(#7) 배회자 효과 발동 필수
                    return ctx.WandererKillOccurred;

                case "사망 반응 / 희생양":
                    // 프리드(#6) 희생양 효과 발동 필수
                    return ctx.SacrificeOccurred;

                case "사망 반응 / 살인자":
                    // 살인자(토니 #5)가 구역에 생존해 있어야 함
                    return zoneIds.Contains(5);

                case "사망 반응 / 복수자":
                    // 복수자(메이 #2)가 구역에 생존해 있어야 함
                    return zoneIds.Contains(2);

                case "사망 반응 / 연인 연쇄":
                    // 데우스(#3)와 루이스(#4) 둘 다 이번 턴 사망
                    return ctx.AllDeadThisTurn.Contains(3) &&
                           ctx.AllDeadThisTurn.Contains(4);

                default:
                    // 일반 사망 반응: 사망 발생만 확인
                    return true;
            }
        }

        // ── Private — 기존 Condition 필드 폴백 ───────────────────────────

        /// <summary>
        /// 기존 DialogueConditionData 필드를 평가합니다. (하위 호환)
        /// </summary>
        private bool EvaluateLegacyCondition(
            DialogueConditionData condition,
            FragmentCollector collector)
        {
            if (!string.IsNullOrEmpty(condition.RequiredFragmentId))
            {
                if (collector == null) return false;
                if (!collector.HasFragment(condition.RequiredFragmentId)) return false;
            }

            if (condition.MinLoopCount > 0)
            {
                var gfc = GameFlowController.Instance;
                if (gfc == null) return false;
                if (gfc.LoopCount < condition.MinLoopCount) return false;
            }

            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 조건 평가 컨텍스트
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 조건 평가에 필요한 턴 컨텍스트입니다.
    /// DialogueTriggerManager에서 턴 종료 시 생성해 전달합니다.
    /// </summary>
    public class ConditionContext
    {
        /// <summary>현재 구역의 생존 캐릭터 ID 집합입니다.</summary>
        public HashSet<int> ZoneCharacterIds;

        /// <summary>이번 턴에 사망한 캐릭터 ID 집합입니다.</summary>
        public HashSet<int> AllDeadThisTurn;

        /// <summary>이번 턴까지 누적 사망 수입니다.</summary>
        public int TotalDeathCount;

        /// <summary>새턴(#7) 배회자 효과 발동 여부입니다.</summary>
        public bool WandererKillOccurred;

        /// <summary>프리드(#6) 희생양 효과 발동 여부입니다.</summary>
        public bool SacrificeOccurred;
    }
}