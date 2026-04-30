using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 출력 조건을 판별하는 평가기입니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   DialogueEntry가 현재 턴/구역 컨텍스트에서 출력 가능한지 판별합니다.
    ///   MonoBehaviour가 아닌 순수 C# 클래스로, DialogueTriggerManager에서 인스턴스화합니다.
    ///
    /// ─── 평가 단계 ───────────────────────────────────────────────────────
    ///   1단계  보상 / 출력 기록 체크
    ///         Core    → RewardFragmentId 미수집 시 통과
    ///         Hint    → DialogueId 미출력 시 통과
    ///         Special → DialogueId 미출력 시 통과
    ///         Normal  → DialogueId 미출력 시 통과 (출력됐으면 false → 대체 대사로 폴백)
    ///
    ///   2단계  해금 조건 체크 (UnlockConditionId)
    ///         "P01_01" 단일 조건 또는 "P01_01 또는 P01_02" OR 조건 지원
    ///
    ///   3단계  참가자 조합 매칭 (ParticipantIds)
    ///         단독 (1명) → 구역에 정확히 해당 캐릭터 1명만 있어야 함
    ///         조합 (2+명) → 구역에 모든 참가자가 포함되어야 함
    ///
    ///   4단계  상황 조건 매칭
    ///         Scope   → 사망 발생 범위 (전체/조사구역내/조사구역외)
    ///         Alive   → 생존/사망 상태
    ///         Gimmick → 특수 역할 발동 (살인자/배회자/희생양/복수자/연인연쇄)
    ///         SituationCharacterIds → 기믹별 관련 캐릭터 추가 검증
    ///
    /// ─── 사용법 ──────────────────────────────────────────────────────────
    ///   var evaluator = new DialogueConditionEvaluator();
    ///   bool canPlay = evaluator.CanPlay(entry, zoneIds, tracker, collector, ctx);
    /// </summary>
    public class DialogueConditionEvaluator
    {
        // ═══════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// DialogueEntry의 출력 가능 여부를 반환합니다.
        /// 4단계 평가를 모두 통과해야 true를 반환합니다.
        /// </summary>
        /// <param name="entry">평가할 대사 엔트리</param>
        /// <param name="zoneCharacterIds">현재 구역의 생존 캐릭터 ID 집합</param>
        /// <param name="tracker">출력 기록 추적기 (null이면 기록 체크 생략)</param>
        /// <param name="collector">대화 조각 수집기 (null이면 수집 체크 생략)</param>
        /// <param name="ctx">턴 컨텍스트 (사망/기믹 정보, null이면 AllSurvived 가정)</param>
        public bool CanPlay(DialogueEntry entry, HashSet<int> zoneCharacterIds,
            DialogueProgressTracker tracker, FragmentCollector collector,
            ConditionContext ctx = null)
        {
            if (entry == null) return false;

            // 1단계 — 보상 / 출력 기록 체크
            if (!CheckRewardOrPlayedRecord(entry, tracker, collector))
                return false;

            // 2단계 — 해금 조건
            if (!CheckUnlockCondition(entry.UnlockConditionId, collector))
                return false;

            // 3단계 — 참가자 조합 매칭
            if (!MatchesParticipants(entry.GetParticipantIds(), zoneCharacterIds))
                return false;

            // 4단계 — 상황 조건 매칭
            if (!MatchesSituation(entry, zoneCharacterIds, ctx))
                return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // 1단계 — 보상 / 출력 기록
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 보상 조각 수집 여부 또는 출력 기록 여부를 체크합니다.
        ///
        /// Core (RewardFragmentId 있음):
        ///   - 보상 조각이 이미 수집됐으면 false (재출력 안 함)
        ///   - 미수집이면 true (조건 충족 시 반복 출력 가능)
        ///
        /// Hint / Special / Normal (RewardFragmentId 없음):
        ///   - DialogueId가 이미 출력 기록에 있으면 false
        ///   - 미출력이면 true
        /// </summary>
        private bool CheckRewardOrPlayedRecord(DialogueEntry entry,
            DialogueProgressTracker tracker, FragmentCollector collector)
        {
            // Core 대사 — RewardFragmentId 기준
            if (!string.IsNullOrEmpty(entry.RewardFragmentId))
            {
                if (collector != null && collector.HasFragment(entry.RewardFragmentId))
                    return false;
                return true;
            }

            // Hint / Special / Normal 대사 — DialogueId 기준
            if (tracker != null && tracker.HasPlayed(entry.DialogueId))
                return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // 2단계 — 해금 조건 (UnlockConditionId)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// UnlockConditionId가 충족되었는지 체크합니다.
        /// 조건이 비어있으면 항상 true를 반환합니다.
        ///
        /// 지원 형식:
        ///   "P01_01"               단일 조건 (P01_01 수집 필요)
        ///   "P01_01 또는 P01_02"   OR 조건 (둘 중 하나만 수집해도 됨)
        /// </summary>
        private bool CheckUnlockCondition(string unlockConditionId, FragmentCollector collector)
        {
            if (string.IsNullOrEmpty(unlockConditionId)) return true;
            if (collector == null) return false;

            string[] orConditions = unlockConditionId.Split(
                new[] { " 또는 " }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string cond in orConditions)
                if (collector.HasFragment(cond.Trim()))
                    return true;

            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        // 3단계 — 참가자 조합 매칭
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// ParticipantIds와 현재 구역 캐릭터 조합을 매칭합니다.
        ///
        /// 단독 대사 (1명):
        ///   구역에 해당 캐릭터 1명만 있어야 함 (정확 매칭)
        ///   예: ParticipantIds=[1], zoneIds={1} → true
        ///       ParticipantIds=[1], zoneIds={1, 2} → false
        ///
        /// 조합 대사 (2명 이상):
        ///   구역에 모든 참가자가 포함되어야 함 (부분 매칭)
        ///   예: ParticipantIds=[1, 2], zoneIds={1, 2} → true
        ///       ParticipantIds=[1, 2], zoneIds={1, 2, 7} → true
        ///       ParticipantIds=[1, 2], zoneIds={1} → false
        /// </summary>
        private bool MatchesParticipants(List<int> participantIds, HashSet<int> zoneIds)
        {
            if (participantIds == null || participantIds.Count == 0) return true;
            if (zoneIds == null || zoneIds.Count == 0) return false;

            // 단독 대사 — 정확히 1명만 있어야 함
            if (participantIds.Count == 1)
                return zoneIds.Count == 1 && zoneIds.Contains(participantIds[0]);

            // 조합 대사 — 모든 참가자가 구역에 포함
            foreach (int id in participantIds)
                if (!zoneIds.Contains(id)) return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // 4단계 — 상황 조건 매칭
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Scope + Alive + Gimmick 조합 평가 진입점입니다.
        /// </summary>
        private bool MatchesSituation(DialogueEntry entry, HashSet<int> zoneIds, ConditionContext ctx)
        {
            if (!MatchesScopeAlive(entry.Scope, entry.Alive, ctx))
                return false;

            if (!MatchesGimmick(entry.Gimmick, entry.GetSituationCharacterIds(), zoneIds, ctx))
                return false;

            return true;
        }

        /// <summary>
        /// Scope + Alive 조합을 평가합니다.
        ///
        /// Scope.AllZone:
        ///   AllSurvived → 전체 구역에서 사망 없음
        ///   SomeoneDied → 전체 구역에서 사망 있음
        ///
        /// Scope.InZone:
        ///   AllSurvived → 조사 구역(Zone2) 내 사망 없음
        ///   SomeoneDied → 조사 구역(Zone2) 내 사망 있음
        ///
        /// Scope.OutZone:
        ///   AllSurvived → 조사 구역 외에서도 사망 없음 (전체 사망 없음)
        ///   SomeoneDied → 조사 구역 외에서 사망 발생, Zone2는 생존
        /// </summary>
        private bool MatchesScopeAlive(SituationScope scope, SituationAlive alive, ConditionContext ctx)
        {
            if (ctx == null)
                return alive == SituationAlive.AllSurvived;

            bool hasDeathAll = ctx.AllDeadThisTurn != null && ctx.AllDeadThisTurn.Count > 0;
            bool hasDeathInZone = ctx.ZoneDeadIds != null && ctx.ZoneDeadIds.Count > 0;
            bool hasDeathOutZone = hasDeathAll && !hasDeathInZone;

            switch (scope)
            {
                case SituationScope.AllZone:
                    return alive == SituationAlive.AllSurvived ? !hasDeathAll : hasDeathAll;

                case SituationScope.InZone:
                    return alive == SituationAlive.AllSurvived ? !hasDeathInZone : hasDeathInZone;

                case SituationScope.OutZone:
                    return alive == SituationAlive.AllSurvived ? !hasDeathAll : hasDeathOutZone;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 역할 기믹 발동 여부를 평가합니다.
        ///
        /// Killer (살인자) — 토니(#5)가 Zone2에서 살해
        /// Wanderer (배회자) — 새턴(#7)이 이동 전 구역 사망 후 Zone2 진입
        /// Sacrifice (희생양) — 프리드(#6)가 타인 대신 사망
        /// Avenger (복수자) — 메이(#2)가 살인자 처치
        /// LoverChain (연인 연쇄) — 연인A 사망 시 연인B 같은 턴 사망
        /// </summary>
        private bool MatchesGimmick(SituationGimmick gimmick, List<int> situationCharacterIds,
            HashSet<int> zoneIds, ConditionContext ctx)
        {
            if (gimmick == SituationGimmick.None) return true;
            if (ctx == null) return false;

            switch (gimmick)
            {
                case SituationGimmick.Killer:
                    if (!ctx.KillerActed) return false;
                    if (!zoneIds.Contains(5)) return false; // 토니 Zone2 생존
                    return CheckCharactersInDeadSet(situationCharacterIds, ctx.AllDeadThisTurn);

                case SituationGimmick.Wanderer:
                    if (!ctx.WandererActed) return false;
                    if (!zoneIds.Contains(7)) return false; // 새턴 Zone2 위치
                    return CheckCharactersInDeadSet(situationCharacterIds, ctx.AllDeadThisTurn);

                case SituationGimmick.Sacrifice:
                    if (!ctx.SacrificeActed) return false;
                    if (situationCharacterIds != null && situationCharacterIds.Count > 0)
                        foreach (int id in situationCharacterIds)
                            if (!ctx.SacrificeTargetIds.Contains(id)) return false;
                    return true;

                case SituationGimmick.Avenger:
                    if (!ctx.AvengerActed) return false;
                    return CheckCharactersInDeadSet(situationCharacterIds, ctx.AllDeadThisTurn);

                case SituationGimmick.LoverChain:
                    if (!ctx.LoverChainActed) return false;
                    return CheckCharactersInDeadSet(situationCharacterIds, ctx.AllDeadThisTurn);

                default:
                    return true;
            }
        }

        /// <summary>
        /// SituationCharacterIds가 모두 deadSet에 포함되는지 확인합니다.
        /// 비어있으면 조건 없음으로 간주해 true를 반환합니다.
        /// </summary>
        private bool CheckCharactersInDeadSet(List<int> ids, HashSet<int> deadSet)
        {
            if (ids == null || ids.Count == 0) return true;
            if (deadSet == null) return false;
            foreach (int id in ids)
                if (!deadSet.Contains(id)) return false;
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 조건 평가 컨텍스트
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 조건 평가에 필요한 턴 컨텍스트입니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   현재 턴의 사망 정보와 역할 기믹 발동 여부를 저장합니다.
    ///   DialogueTriggerManager가 턴 종료 시점에 GameState를 분석하여 생성합니다.
    /// </summary>
    public class ConditionContext
    {
        // ── 사망 정보 ────────────────────────────────────────────────────

        /// <summary>이번 턴 전체 구역에서 사망한 캐릭터 ID 집합입니다.</summary>
        public HashSet<int> AllDeadThisTurn = new();

        /// <summary>이번 턴 조사 구역(Zone2) 내에서 사망한 캐릭터 ID 집합입니다.</summary>
        public HashSet<int> ZoneDeadIds = new();

        /// <summary>이번 턴까지 누적 사망 수입니다.</summary>
        public int TotalDeathCount;

        // ── 역할 기믹 발동 여부 ───────────────────────────────────────────

        /// <summary>살인자(#5) 기믹 — 토니가 Zone2에서 살해했는지.</summary>
        public bool KillerActed;

        /// <summary>배회자(#7) 기믹 — 새턴 이동 전 구역에서 사망 발생했는지.</summary>
        public bool WandererActed;

        /// <summary>희생양(#6) 기믹 — 프리드가 타인 대신 사망했는지.</summary>
        public bool SacrificeActed;

        /// <summary>희생양 기믹 발동 시 원래 죽을 뻔했던 캐릭터 ID 집합입니다.</summary>
        public HashSet<int> SacrificeTargetIds = new();

        /// <summary>복수자(#2) 기믹 — 메이가 살인자를 처치했는지.</summary>
        public bool AvengerActed;

        /// <summary>연인 연쇄 기믹 — 연인A와 연인B가 같은 턴에 사망했는지.</summary>
        public bool LoverChainActed;
    }
}