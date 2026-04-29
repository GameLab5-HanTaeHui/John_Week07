using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬의 다이얼로그 트리거 및 출력을 관리하는 매니저입니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   1. 캠페인 씬 진입 시 SO 데이터 로드 및 초기화
    ///   2. 턴 종료 시점의 사망/기믹 컨텍스트 캐싱
    ///   3. 활성 구역 순회 → 후보 선별 → 우선순위 정렬 → 출력
    ///   4. 시간대별 대사 트리거 (Morning/Lunch/Evening)
    ///   5. 이미 본 조합 → AlreadySeenLines 대체 출력
    ///   6. 보상 조각 수집 처리
    ///
    /// ─── 우선순위 ────────────────────────────────────────────────────────
    ///   1. Core    — 핵심 대화 (RewardFragmentId 미수집)
    ///   2. Hint    — 힌트 대화
    ///   3. Special — 그 외 대사
    ///   4. Normal  — 일반 대화 (이미 출력됐으면 AlreadySeenLines 폴백)
    ///   같은 우선순위 내에서 ParticipantIds 수 정확 일치 우선
    ///
    /// ─── 트리거 흐름 ─────────────────────────────────────────────────────
    ///   [턴 종료 진입]
    ///     ↓ OnTurnEndEntered
    ///   [컨텍스트 캐싱 — 사망/기믹 스냅샷]
    ///     ↓
    ///   [턴 종료 다이얼로그 재생 완료]
    ///     ↓ OnTurnEndDialogueFinished
    ///   [1초 대기 — 페이드 아웃 마무리]
    ///     ↓
    ///   [활성 구역 순회 → 대사 출력]
    ///     ↓
    ///   [완료 — 다음 턴 대기]
    ///
    /// ─── 외부 의존성 ─────────────────────────────────────────────────────
    ///   - CampaignModeManager : 진입/종료 트리거, 시간대 정보 (추후 연계)
    ///   - GameFlowController  : 턴 상태머신 이벤트, GameState 접근
    ///   - DialoguePlayer      : 실제 대사 화면 출력
    ///   - FragmentCollector   : 핵심 조각 수집/조회
    ///   - DialogueProgressTracker : 출력 기록 추적
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Data      → CampaignDialogueSO 에셋
    ///   Dialogue Player    → DialoguePlayer 컴포넌트
    ///   Fragment Collector → FragmentCollector 컴포넌트
    ///   Active Zones       → 구역별 대사 활성화 여부 (인덱스 = ZoneId)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueTriggerManager : MonoBehaviour
    {
        public static DialogueTriggerManager Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════
        // Inspector 필드
        // ═══════════════════════════════════════════════════════════════

        [Header("데이터")]
        [Tooltip("이 캠페인 씬의 다이얼로그 데이터입니다.")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [Tooltip("대사를 화면에 출력하는 컴포넌트입니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Tooltip("대화 조각 수집을 담당하는 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("구역 대사 활성화 (0 → 1 → 2 → 3 순서)")]
        [Tooltip("체크된 구역만 턴 종료 시 대사를 체크합니다.\n" +
                 "인덱스 = ZoneId (0=Zone0, 1=Zone1, 2=Zone2, 3=Zone3)\n" +
                 "조사 구역은 일반적으로 Zone2입니다.")]
        [SerializeField]
        private bool[] _activeZones =
            new bool[GameState.ZoneCount] { true, true, true, true };

        [Header("구역 판별 방식")]
        [Tooltip("true  — 캐릭터 기반: 앵커 캐릭터(#1)가 있는 Zone 하나만 대사 출력 구역으로 사용합니다.\n" +
                 "false — Zone 기반: _activeZones에 체크된 모든 Zone을 순회합니다.")]
        [SerializeField] private bool _useCharacterBasedZone = true;

        [Tooltip("캐릭터 기반 모드에서 사용할 앵커 캐릭터 ID입니다.\n" +
                 "이 캐릭터가 있는 Zone이 대사 출력 구역이 됩니다. (기본값 1 = 엔비)")]
        [SerializeField] private int _anchorCharacterId = 1;

        [Header("타이밍")]
        [Tooltip("턴 종료 다이얼로그 완료 후 캠페인 대사 트리거까지의 대기 시간입니다.")]
        [SerializeField] private float _triggerDelay = 1f;

        // ═══════════════════════════════════════════════════════════════
        // 내부 상태
        // ═══════════════════════════════════════════════════════════════

        /// <summary>출력 기록 추적기입니다.</summary>
        private DialogueProgressTracker _progressTracker;

        /// <summary>대사 출력 조건 평가기입니다.</summary>
        private DialogueConditionEvaluator _conditionEvaluator;

        /// <summary>턴 종료 시점에 캐싱된 컨텍스트입니다.</summary>
        private ConditionContext _cachedCtx = new();

        /// <summary>초기화 완료 여부입니다.</summary>
        private bool _isInitialized;

        /// <summary>캠페인 대사 대기 중 클릭 차단 여부입니다.</summary>
        public bool IsWaitingForDialogue { get; private set; }

        /// <summary>현재 진행 중인 시간대입니다. CampaignModeManager가 설정합니다.</summary>
        public TimeOfDay CurrentTimeOfDay { get; set; } = TimeOfDay.Morning;

        // ═══════════════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            Instance = this;
            _progressTracker = new DialogueProgressTracker();
            _conditionEvaluator = new DialogueConditionEvaluator();
        }

        private void Start()
        {
            var turnSM = CampaignGameFlowController.Instance?.GetTurnSM();
            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered += OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished += OnTurnEndDialogueFinished;
            }

            // ★ CampaignModeManager 초기화 완료 이벤트 구독
            // OnCampaignInitialized 발생 시 Initialize(stageId) 자동 호출
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
            else
                Debug.LogWarning("[DialogueTriggerManager] CampaignModeManager를 찾을 수 없습니다. " +
                                 "Initialize()가 호출되지 않으면 대사가 트리거되지 않습니다.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            var turnSM = CampaignGameFlowController.Instance?.GetTurnSM();
            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered -= OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished -= OnTurnEndDialogueFinished;
            }

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnCampaignInitialized -= Initialize;
        }

        // ═══════════════════════════════════════════════════════════════
        // 외부 API — CampaignModeManager에서 호출
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 캠페인 씬 초기화입니다. CampaignModeManager에서 호출합니다.
        /// SO 데이터, 출력 기록, 조각 수집기를 초기화합니다.
        /// </summary>
        public void Initialize(string stageId)
        {
            if (_dialogueData == null)
            {
                Debug.LogError("[DialogueTriggerManager] CampaignDialogueSO가 연결되지 않았습니다.");
                return;
            }

            _progressTracker.Initialize(stageId);
            _fragmentCollector?.Initialize(stageId);
            _isInitialized = true;

            Debug.Log($"[DialogueTriggerManager] 초기화 완료 — {stageId}");
        }

        /// <summary>
        /// 시간대별 대사를 트리거합니다.
        /// CampaignModeManager가 시간대 변경 시 호출합니다.
        /// </summary>
        public void TriggerTimeOfDayDialogue(TimeOfDay timeOfDay)
        {
            if (!_isInitialized) return;
            if (_dialogueData == null) return;

            CurrentTimeOfDay = timeOfDay;
            var entry = _dialogueData.FindByTimeOfDay(timeOfDay);
            if (entry == null || entry.Lines == null || entry.Lines.Count == 0) return;

            Debug.Log($"[DTM] 시간대 대사 트리거 — {timeOfDay}");
            StartCoroutine(PlayTimeOfDayDialogue(entry));
        }

        // ═══════════════════════════════════════════════════════════════
        // 이벤트 핸들러 — 턴 흐름
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 턴 종료 진입 시 호출됩니다.
        /// 사망/기믹 정보를 캐싱해 이후 대사 트리거 시 사용합니다.
        /// </summary>
        private void OnTurnEndEntered(IReadOnlyList<string> roleLog, bool isLastTurn)
        {
            if (!_isInitialized) return;
            CacheConditionContext();
        }

        /// <summary>
        /// 턴 종료 다이얼로그 재생이 완료되면 호출됩니다.
        /// 페이드 아웃 마무리를 기다린 후 캠페인 대사 트리거를 시작합니다.
        /// </summary>
        private void OnTurnEndDialogueFinished()
        {
            if (!_isInitialized) return;
            StartCoroutine(TriggerDialoguesDelayed(_triggerDelay));
        }

        /// <summary>지정한 시간만큼 대기 후 대사 트리거를 실행합니다.</summary>
        private IEnumerator TriggerDialoguesDelayed(float delay)
        {
            IsWaitingForDialogue = true;
            yield return new WaitForSeconds(delay);
            IsWaitingForDialogue = false;

            TriggerDialoguesForAllZones();
        }

        // ═══════════════════════════════════════════════════════════════
        // 컨텍스트 캐싱
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 턴 종료 시점의 사망/기믹 정보를 캐싱합니다.
        ///
        /// 캐싱 항목:
        ///   1. 전체 사망자 ID 집합
        ///   2. 조사 구역(Zone2) 내 사망자 집합
        ///   3. Killer 기믹 — 토니(#5) Zone2 생존 + 사망 발생
        ///   4. Wanderer 기믹 — 새턴(#7) 이동 + 이전 구역 사망
        ///   5. Sacrifice 기믹 — 프리드(#6) 사망 + 같은 구역 생존자 존재
        ///   6. Avenger 기믹 — 살인자(#5) 사망 + 메이(#2) 같은 구역
        ///   7. LoverChain 기믹 — 데우스(#3) + 루이스(#4) 동시 사망
        /// </summary>
        private void CacheConditionContext()
        {
            var ctx = new ConditionContext();
            var gameState = CampaignGameFlowController.Instance?.GameState;

            if (gameState == null)
            {
                _cachedCtx = ctx;
                return;
            }

            // ── 1. 전체 사망자 수집 ────────────────────────────────────
            foreach (int id in gameState.GetAllCharacterIds())
            {
                if (!gameState.IsMarkedForDeath(id)) continue;
                ctx.AllDeadThisTurn.Add(id);
                ctx.TotalDeathCount++;
            }

            // ── 2. 조사 구역(Zone2) 내 사망자 수집 ────────────────────
            const int investigationZoneId = 2;
            foreach (var c in gameState.GetCharactersInZone(investigationZoneId))
                if (ctx.AllDeadThisTurn.Contains(c.CharacterId))
                    ctx.ZoneDeadIds.Add(c.CharacterId);

            // ── 3. Killer 기믹 감지 ────────────────────────────────────
            // 토니(#5)가 Zone2에 생존하고 사망 사건이 발생했을 때
            ctx.KillerActed = ctx.AllDeadThisTurn.Count > 0
                && gameState.GetZone(5) == investigationZoneId
                && !ctx.AllDeadThisTurn.Contains(5);

            // ── 4. Wanderer 기믹 감지 ──────────────────────────────────
            // 새턴(#7)이 이동했고, 이동 전 구역에 사망자가 발생했을 때
            int saturnPrev = gameState.GetPreviousZone(7);
            int saturnCurrent = gameState.GetZone(7);
            if (saturnPrev != saturnCurrent)
            {
                foreach (int deadId in ctx.AllDeadThisTurn)
                {
                    if (gameState.GetPreviousZone(deadId) == saturnPrev)
                    {
                        ctx.WandererActed = true;
                        break;
                    }
                }
            }

            // ── 5. Sacrifice 기믹 감지 ─────────────────────────────────
            // 프리드(#6)가 사망했고, 같은 구역에 생존자가 있을 때
            ctx.SacrificeActed = ctx.AllDeadThisTurn.Contains(6);
            if (ctx.SacrificeActed)
            {
                int friedZone = gameState.GetPreviousZone(6);
                foreach (var c in gameState.GetCharactersInZone(friedZone))
                    if (c.CharacterId != 6 && !ctx.AllDeadThisTurn.Contains(c.CharacterId))
                        ctx.SacrificeTargetIds.Add(c.CharacterId);
            }

            // ── 6. Avenger 기믹 감지 ───────────────────────────────────
            // 살인자(#5)가 사망했고, 메이(#2)가 같은 구역(이전)에 있었을 때
            ctx.AvengerActed = ctx.AllDeadThisTurn.Contains(5)
                && gameState.GetPreviousZone(2) == gameState.GetPreviousZone(5)
                && !ctx.AllDeadThisTurn.Contains(2);

            // ── 7. LoverChain 기믹 감지 ────────────────────────────────
            // 데우스(#3)와 루이스(#4)가 모두 이번 턴에 사망 마킹됐을 때
            // (한쪽이 이전 턴에 이미 죽어있었다면 발동하지 않음 — AllDeadThisTurn에 없음)
            ctx.LoverChainActed = ctx.AllDeadThisTurn.Contains(3)
                                && ctx.AllDeadThisTurn.Contains(4);

            _cachedCtx = ctx;

            Debug.Log($"[DTM] 컨텍스트 캐싱 완료 — " +
                      $"전체사망:[{string.Join(",", ctx.AllDeadThisTurn)}] " +
                      $"구역내사망:[{string.Join(",", ctx.ZoneDeadIds)}] " +
                      $"Killer={ctx.KillerActed} Wanderer={ctx.WandererActed} " +
                      $"Sacrifice={ctx.SacrificeActed} Avenger={ctx.AvengerActed} " +
                      $"LoverChain={ctx.LoverChainActed}");
        }

        // ═══════════════════════════════════════════════════════════════
        // 구역 순회 및 출력
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 대사 트리거 진입점입니다.
        /// _useCharacterBasedZone 값에 따라 두 가지 방식으로 분기합니다.
        ///
        ///   true  — 캐릭터 기반: 앵커 캐릭터(#1)가 있는 Zone 하나만 사용
        ///   false — Zone 기반:   _activeZones에 체크된 모든 Zone 순회
        /// </summary>
        private void TriggerDialoguesForAllZones()
        {
            if (_useCharacterBasedZone)
                TriggerByAnchorCharacter();
            else
                TriggerByActiveZones();
        }

        /// <summary>
        /// [캐릭터 기반] 앵커 캐릭터(_anchorCharacterId)가 있는 Zone 하나만 사용합니다.
        /// 해당 Zone의 캐릭터 조합으로 대사를 선별합니다.
        ///
        /// 기획 의도: 캐릭터 #1(엔비)가 있는 구역을 대화 출력 구역으로 고정합니다.
        /// 앵커 캐릭터가 사망한 경우 대사를 출력하지 않습니다.
        /// </summary>
        private void TriggerByAnchorCharacter()
        {
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null) return;

            // 앵커 캐릭터 생존 여부 확인
            var anchor = gameState.GetCharacter(_anchorCharacterId);
            if (anchor == null || !anchor.IsAlive)
            {
                Debug.Log($"[DTM] 캐릭터 기반 — 앵커 #{_anchorCharacterId} 사망, 대사 없음");
                return;
            }

            int anchorZone = gameState.GetZone(_anchorCharacterId);
            var characterIds = GetCharactersInZone(anchorZone);

            Debug.Log($"[DTM] 캐릭터 기반 — 앵커 #{_anchorCharacterId} Zone{anchorZone}, " +
                      $"캐릭터: [{string.Join(",", characterIds)}]");

            if (characterIds.Count == 0) return;

            var resolved = ResolveDialogueForZone(characterIds);
            if (!resolved.HasValue) return;

            StartCoroutine(PlaySequential(new List<PendingDialogue> { resolved.Value }));
        }

        /// <summary>
        /// [Zone 기반] _activeZones에 체크된 모든 Zone을 0→1→2→3 순서로 순회합니다.
        /// 각 Zone의 캐릭터 조합으로 대사를 선별해 순차 재생합니다.
        /// </summary>
        private void TriggerByActiveZones()
        {
            var pending = new List<PendingDialogue>();

            for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
            {
                if (!IsZoneActive(zoneId)) continue;

                var characterIds = GetCharactersInZone(zoneId);
                Debug.Log($"[DTM] Zone 기반 — Zone{zoneId} 캐릭터: [{string.Join(",", characterIds)}]");
                if (characterIds.Count == 0) continue;

                var resolved = ResolveDialogueForZone(characterIds);
                if (resolved.HasValue)
                    pending.Add(resolved.Value);
            }

            if (pending.Count == 0) return;
            StartCoroutine(PlaySequential(pending));
        }

        /// <summary>구역이 활성 상태인지 확인합니다. Zone 기반 모드에서 사용합니다.</summary>
        private bool IsZoneActive(int zoneId)
            => _activeZones != null
               && zoneId >= 0
               && zoneId < _activeZones.Length
               && _activeZones[zoneId];

        /// <summary>특정 구역에 있는 생존 캐릭터 ID 집합을 반환합니다.</summary>
        private HashSet<int> GetCharactersInZone(int zoneId)
        {
            var result = new HashSet<int>();
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null) return result;

            foreach (var c in gameState.GetCharactersInZone(zoneId))
                result.Add(c.CharacterId);

            return result;
        }

        /// <summary>
        /// 한 구역에서 출력할 대사를 결정합니다.
        /// 우선순위 후보를 순회하며 첫 번째 통과 엔트리를 반환합니다.
        /// 통과 엔트리가 없고 Normal이 모두 출력됐으면 AlreadySeenLines로 폴백합니다.
        /// </summary>
        private PendingDialogue? ResolveDialogueForZone(HashSet<int> characterIds)
        {
            var candidates = FindCandidateEntries(characterIds);

            // 우선순위 순으로 평가
            foreach (var entry in candidates)
            {
                if (_conditionEvaluator.CanPlay(
                        entry, characterIds, _progressTracker, _fragmentCollector, _cachedCtx))
                {
                    return new PendingDialogue
                    {
                        Entry = entry,
                        CharacterIds = characterIds,
                        UseFallback = false,
                    };
                }
            }

            // 출력할 엔트리 없음 → AlreadySeenLines 폴백
            // 단, 해당 조합에 매칭되는 일반 엔트리가 존재했을 때만 폴백
            if (HasAnyMatchingEntry(candidates))
            {
                var fallbackLines = _dialogueData?.AlreadySeenLines;
                if (fallbackLines != null && fallbackLines.Count > 0)
                {
                    return new PendingDialogue
                    {
                        Entry = null,
                        CharacterIds = characterIds,
                        UseFallback = true,
                    };
                }
            }

            return null;
        }

        /// <summary>후보 리스트에 매칭 엔트리가 하나라도 있는지 확인합니다.</summary>
        private bool HasAnyMatchingEntry(List<DialogueEntry> candidates)
            => candidates != null && candidates.Count > 0;

        // ═══════════════════════════════════════════════════════════════
        // 후보 선별 및 우선순위 정렬
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 구역 캐릭터 조합에 매칭되는 후보 엔트리를 우선순위 순으로 반환합니다.
        ///
        /// 우선순위:
        ///   1. Core    (RewardFragmentId 미수집)
        ///   2. Hint
        ///   3. Special
        ///   4. Normal  (Core 수집 완료된 것은 여기로 폴백)
        ///   같은 우선순위 내에서 ParticipantIds 수 정확 일치 우선
        /// </summary>
        private List<DialogueEntry> FindCandidateEntries(HashSet<int> characterIds)
        {
            if (_dialogueData == null) return new List<DialogueEntry>();

            var p1 = new List<DialogueEntry>(); // Core (미수집)
            var p2 = new List<DialogueEntry>(); // Hint
            var p3 = new List<DialogueEntry>(); // Special
            var p4 = new List<DialogueEntry>(); // Normal (Core 수집 완료 폴백 포함)

            foreach (var entry in _dialogueData.Dialogues)
            {
                if (entry == null) continue;

                var participantIds = entry.GetParticipantIds();
                if (!ContainsRequiredParticipants(participantIds, characterIds)) continue;

                switch (entry.Type)
                {
                    case DialogueType.Core:
                        // 미수집은 p1, 수집 완료는 p4 폴백
                        bool collected = !string.IsNullOrEmpty(entry.RewardFragmentId)
                            && (_fragmentCollector?.HasFragment(entry.RewardFragmentId) ?? false);
                        if (collected) p4.Add(entry);
                        else p1.Add(entry);
                        break;

                    case DialogueType.Hint: p2.Add(entry); break;
                    case DialogueType.Special: p3.Add(entry); break;
                    case DialogueType.Normal:
                    default: p4.Add(entry); break;
                }
            }

            SortByExactMatch(p1, characterIds);
            SortByExactMatch(p2, characterIds);
            SortByExactMatch(p3, characterIds);
            SortByExactMatch(p4, characterIds);

            var result = new List<DialogueEntry>(p1.Count + p2.Count + p3.Count + p4.Count);
            result.AddRange(p1);
            result.AddRange(p2);
            result.AddRange(p3);
            result.AddRange(p4);
            return result;
        }

        /// <summary>
        /// ParticipantIds의 모든 ID가 characterIds에 포함되는지 확인합니다.
        /// 단독 대사는 정확히 1명일 때만 true를 반환합니다.
        /// </summary>
        private bool ContainsRequiredParticipants(
            List<int> participantIds, HashSet<int> characterIds)
        {
            if (participantIds == null || participantIds.Count == 0) return false;
            foreach (int id in participantIds)
                if (!characterIds.Contains(id)) return false;

            // 단독 대사는 정확히 1명이어야 함
            if (participantIds.Count == 1)
                return characterIds.Count == 1;

            return true;
        }

        /// <summary>참가자 수가 정확히 일치하는 엔트리를 앞으로 정렬합니다.</summary>
        private void SortByExactMatch(List<DialogueEntry> list, HashSet<int> characterIds)
        {
            list.Sort((a, b) =>
            {
                int countA = a.GetParticipantIds().Count;
                int countB = b.GetParticipantIds().Count;
                int sa = countA == characterIds.Count ? 2 : 1;
                int sb = countB == characterIds.Count ? 2 : 1;
                return sb.CompareTo(sa);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // 대사 재생
        // ═══════════════════════════════════════════════════════════════

        /// <summary>여러 구역의 대사를 순서대로 재생합니다.</summary>
        private IEnumerator PlaySequential(List<PendingDialogue> entries)
        {
            foreach (var pending in entries)
            {
                bool done = false;
                if (pending.UseFallback)
                    PlayFallbackLines(pending.CharacterIds, () => done = true);
                else
                    PlayDialogueEntry(pending.Entry, pending.CharacterIds, () => done = true);

                yield return new WaitUntil(() => done);
            }
        }

        /// <summary>시간대별 대사를 재생합니다.</summary>
        private IEnumerator PlayTimeOfDayDialogue(TimeOfDayDialogueEntry entry)
        {
            if (_dialoguePlayer == null) yield break;

            bool done = false;
            _dialoguePlayer.Play(entry.Lines, onComplete: () => done = true);
            yield return new WaitUntil(() => done);
        }

        /// <summary>
        /// 일반 대사 엔트리를 재생합니다.
        /// 재생 완료 후 출력 기록 마킹, 보상 조각 수집, 알림 출력을 처리합니다.
        /// </summary>
        private void PlayDialogueEntry(
            DialogueEntry entry,
            HashSet<int> characterIds,
            Action onComplete)
        {
            if (_dialoguePlayer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                // 일반 대사만 DialogueId 마킹 (Core는 RewardFragmentId로 관리)
                if (string.IsNullOrEmpty(entry.RewardFragmentId))
                    _progressTracker.MarkPlayed(entry.DialogueId);

                // 보상 조각 수집
                bool fragmentCollected = !string.IsNullOrEmpty(entry.RewardFragmentId)
                    && TryCollectFragment(entry.RewardFragmentId);

                Debug.Log($"[DTM] 대사 완료 — {entry.DialogueId} " +
                          $"[{string.Join(",", characterIds)}]");

                // 보상 알림 출력
                if (fragmentCollected)
                    PlayFragmentNotification(entry.RewardFragmentId, onComplete);
                else
                    onComplete?.Invoke();
            });
        }

        /// <summary>이미 본 대화 대체 라인을 재생합니다.</summary>
        private void PlayFallbackLines(HashSet<int> characterIds, Action onComplete)
        {
            if (_dialoguePlayer == null || _dialogueData == null)
            {
                onComplete?.Invoke();
                return;
            }

            // IReadOnlyList<DialogueLine> → List<DialogueLine> 변환
            // DialoguePlayer.Play()가 List<T>를 요구하므로 변환 필요
            var lines = new List<DialogueLine>(_dialogueData.AlreadySeenLines);
            if (lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            Debug.Log($"[DTM] 이미 본 대화 폴백 — [{string.Join(",", characterIds)}]");
            _dialoguePlayer.Play(lines, onComplete: onComplete);
        }

        /// <summary>보상 조각 획득 알림을 출력합니다.</summary>
        private void PlayFragmentNotification(string fragmentId, Action onComplete)
        {
            int charId = ParseCharacterIdFromFragment(fragmentId);
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(charId);
            string label = !string.IsNullOrEmpty(name) ? $"'{name}'" : $"#{charId}";
            string msg = $"{label}의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다.";

            _dialoguePlayer.PlayNotification(new List<string> { msg }, onComplete);
        }

        // ═══════════════════════════════════════════════════════════════
        // 헬퍼 메서드
        // ═══════════════════════════════════════════════════════════════

        /// <summary>보상 조각 수집을 시도하고 실제로 수집됐는지 반환합니다.</summary>
        private bool TryCollectFragment(string fragmentId)
        {
            if (_fragmentCollector == null) return false;
            if (_fragmentCollector.HasFragment(fragmentId)) return false;
            _fragmentCollector.TryCollectFragment(fragmentId);
            return true;
        }

        /// <summary>
        /// FragmentId(P01_01 형식)에서 캐릭터 ID를 파싱합니다.
        /// 예: "P01_01" → 1 / "P07_05" → 7
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return -1;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return -1;
            return int.TryParse(fragmentId.Substring(1, idx - 1), out int id) ? id : -1;
        }

        // ═══════════════════════════════════════════════════════════════
        // 내부 데이터 구조
        // ═══════════════════════════════════════════════════════════════

        /// <summary>출력 대기 중인 대사 정보입니다.</summary>
        private struct PendingDialogue
    {
        /// <summary>출력할 대사 엔트리. UseFallback=true면 null.</summary>
        public DialogueEntry Entry;

        /// <summary>해당 구역의 캐릭터 ID 집합.</summary>
        public HashSet<int> CharacterIds;

        /// <summary>true면 AlreadySeenLines 대체 출력.</summary>
        public bool UseFallback;
        }

        // ═══════════════════════════════════════════════════════════════
        // 테스트용 (배포 전 제거)
        // ═══════════════════════════════════════════════════════════════

        [ContextMenu("테스트: PlayerPrefs 초기화")]
        private void TestClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[DialogueTriggerManager] PlayerPrefs 전체 초기화 완료");
        }
    }
}