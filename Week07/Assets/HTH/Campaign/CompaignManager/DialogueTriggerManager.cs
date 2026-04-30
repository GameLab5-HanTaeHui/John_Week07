using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬의 다이얼로그 트리거 및 출력을 관리하는 매니저입니다.
    ///
    /// ─── 구역 판별 방식 ──────────────────────────────────────────────────
    ///   _useCharacterBasedZone = true  (기본값):
    ///     앵커 캐릭터(#1)가 있는 Zone이 대사 출력 구역이자 조사 구역(InZone)입니다.
    ///     _activeZones 무관.
    ///
    ///   _useCharacterBasedZone = false:
    ///     _activeZones에 체크된 모든 Zone을 0→3 순서로 순회합니다.
    ///     조사 구역은 앵커 캐릭터 Zone으로 자동 판별합니다.
    ///
    /// ─── 트리거 흐름 ─────────────────────────────────────────────────────
    ///   OnTurnEndEntered → CacheConditionContext()
    ///   OnTurnEndDialogueFinished → TriggerDialoguesDelayed()
    ///     → TriggerDialoguesForAllZones() → PlaySequential()
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Data       → CampaignDialogueSO 에셋
    ///   Dialogue Player     → DialoguePlayer 컴포넌트
    ///   Fragment Collector  → FragmentCollector 컴포넌트
    ///   Use Character Based Zone → true=앵커 기반 / false=Zone 순회
    ///   Anchor Character Id → 앵커 캐릭터 ID (기본 1 = 엔비)
    ///   Active Zones        → Zone 순회 모드에서 활성화할 Zone (인덱스=ZoneId)
    ///   Trigger Delay       → 턴종료 대사 완료 후 대기 시간
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)] // CampaignModeManager(-10)보다 늦게 실행
    public class DialogueTriggerManager : MonoBehaviour
    {
        public static DialogueTriggerManager Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════
        // Inspector
        // ═══════════════════════════════════════════════════════════════

        [Header("데이터")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("구역 판별 방식")]
        [Tooltip("true  — 앵커 캐릭터(#1)가 있는 Zone 하나만 대사 출력 구역으로 사용\n" +
                 "false — _activeZones에 체크된 모든 Zone을 순회")]
        [SerializeField] private bool _useCharacterBasedZone = true;

        [Tooltip("앵커 캐릭터 ID (기본 1 = 엔비)\n" +
                 "이 캐릭터가 있는 Zone이 대사 출력 구역이 됩니다.")]
        [SerializeField] private int _anchorCharacterId = 1;

        [Tooltip("Zone 순회 모드(_useCharacterBasedZone=false)에서 활성화할 Zone\n" +
                 "인덱스 = ZoneId (0=Zone0 ~ 3=Zone3)")]
        [SerializeField]
        private bool[] _activeZones =
            new bool[GameState.ZoneCount] { true, true, true, true };

        [Header("타이밍")]
        [Tooltip("턴 종료 대사 완료 후 캠페인 대사 트리거까지 대기 시간(초)")]
        [SerializeField] private float _triggerDelay = 1f;



        // ═══════════════════════════════════════════════════════════════
        // 내부 상태
        // ═══════════════════════════════════════════════════════════════

        private DialogueProgressTracker _progressTracker;
        private DialogueConditionEvaluator _conditionEvaluator;
        private ConditionContext _cachedCtx = new();
        private bool _isInitialized;

        /// <summary>
        /// true = 강제퇴고 (주인공 사망) — 대사 출력 차단
        /// false = 일반퇴고 (저녁 턴 종료) — 대사 출력 허용
        /// </summary>
        private bool _isForcedLoop;

        public bool IsWaitingForDialogue { get; private set; }
        public TimeOfDay CurrentTimeOfDay { get; set; } = TimeOfDay.Morning;

        // ═══════════════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ═══════════════════════════════════════════════════════════════

        private void Awake()
        {
            Instance = this;
            _progressTracker = new DialogueProgressTracker();
            _conditionEvaluator = new DialogueConditionEvaluator();

            // CampaignModeManager가 Awake에서 이미 생성됐을 경우 바로 구독
            if (CampaignModeManager.Instance != null)
            {
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
                Debug.Log("[DTM] Awake — OnCampaignInitialized 구독 완료");
            }
            else
            {
                Debug.LogWarning("[DTM] Awake — CampaignModeManager.Instance 없음. Start()에서 재시도");
            }
        }

        private void Start()
        {
            // TurnSM 이벤트 구독
            var turnSM = CampaignGameFlowController.Instance?.GetTurnSM();
            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered += OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished += OnTurnEndDialogueFinished;
                Debug.Log("[DTM] Start — TurnSM 이벤트 구독 완료");
            }
            else
            {
                Debug.LogWarning("[DTM] Start — CampaignGameFlowController 또는 TurnSM 없음");
            }

            // Awake에서 구독 실패한 경우 폴백
            if (CampaignModeManager.Instance != null && !_isInitialized)
            {
                CampaignModeManager.Instance.OnCampaignInitialized -= Initialize;
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
                Debug.Log("[DTM] Start — OnCampaignInitialized 구독 재시도");
            }
            else if (CampaignModeManager.Instance == null)
            {
                Debug.LogError("[DTM] Start — CampaignModeManager 없음. Initialize()가 호출되지 않습니다.");
            }
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
        // 외부 API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>CampaignModeManager.OnCampaignInitialized 이벤트에서 호출됩니다.</summary>
        public void Initialize(string stageId)
        {
            Debug.Log($"[DTM] Initialize 호출 — stageId:{stageId}");

            if (_dialogueData == null)
            {
                Debug.LogError("[DTM] CampaignDialogueSO가 연결되지 않았습니다. Inspector를 확인하세요.");
                return;
            }
            if (_dialoguePlayer == null)
                Debug.LogWarning("[DTM] DialoguePlayer가 연결되지 않았습니다. 대사가 출력되지 않습니다.");

            _progressTracker.Initialize(stageId);
            _fragmentCollector?.Initialize(stageId);
            _isInitialized = true;

            Debug.Log($"[DTM] 초기화 완료 — stageId:{stageId}, " +
                      $"Dialogues:{_dialogueData.Dialogues.Count}개, " +
                      $"모드:{(_useCharacterBasedZone ? $"앵커기반(#{_anchorCharacterId})" : "Zone순회")}");
        }

        /// <summary>시간대별 대사를 트리거합니다.</summary>
        public void TriggerTimeOfDayDialogue(TimeOfDay timeOfDay)
        {
            if (!_isInitialized) return;
            if (_dialogueData == null) return;

            CurrentTimeOfDay = timeOfDay;
            var entry = _dialogueData.FindByTimeOfDay(timeOfDay);
            if (entry == null || entry.Lines == null || entry.Lines.Count == 0) return;

            Debug.Log($"[DTM] 시간대 대사 — {timeOfDay}");
            StartCoroutine(PlayTimeOfDayDialogue(entry));
        }

        // ═══════════════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ═══════════════════════════════════════════════════════════════

        private void OnTurnEndEntered(IReadOnlyList<string> roleLog, bool isLastTurn)
        {
            Debug.Log($"[DTM] OnTurnEndEntered — isInitialized:{_isInitialized}");
            if (!_isInitialized) return;

            CacheConditionContext();
        }

        private void OnTurnEndDialogueFinished()
        {
            Debug.Log($"[DTM] OnTurnEndDialogueFinished — isInitialized:{_isInitialized}, 강제퇴고:{_isForcedLoop}");
            if (!_isInitialized) return;

            // ★ 강제퇴고(주인공 사망) 시 Zone 대사 출력 차단
            // 일반퇴고(저녁 턴 종료) 시만 대사 출력
            if (_isForcedLoop)
            {
                Debug.Log("[DTM] 강제퇴고 — Zone 대사 출력 생략");
                return;
            }

            StartCoroutine(TriggerDialoguesDelayed(_triggerDelay));
        }

        private IEnumerator TriggerDialoguesDelayed(float delay)
        {
            IsWaitingForDialogue = true;
            yield return new WaitForSeconds(delay);
            IsWaitingForDialogue = false;
            Debug.Log("[DTM] 대기 완료 → 대사 트리거 시작");
            TriggerDialoguesForAllZones();
        }

        // ═══════════════════════════════════════════════════════════════
        // 컨텍스트 캐싱
        // ═══════════════════════════════════════════════════════════════

        private void CacheConditionContext()
        {
            var ctx = new ConditionContext();
            var gameState = CampaignGameFlowController.Instance?.GameState;

            if (gameState == null)
            {
                Debug.LogWarning("[DTM] CacheConditionContext — GameState 없음");
                _cachedCtx = ctx;
                return;
            }

            // 1. 전체 사망자
            foreach (int id in gameState.GetAllCharacterIds())
            {
                if (!gameState.IsMarkedForDeath(id)) continue;
                ctx.AllDeadThisTurn.Add(id);
                ctx.TotalDeathCount++;
            }

            // 2. 조사 구역(앵커 캐릭터 Zone) 내 사망자
            int investigationZoneId = GetInvestigationZoneId(gameState);
            foreach (var c in gameState.GetCharactersInZone(investigationZoneId))
                if (ctx.AllDeadThisTurn.Contains(c.CharacterId))
                    ctx.ZoneDeadIds.Add(c.CharacterId);

            // 3. Killer — 토니(#5)가 조사 구역에 생존 + 사망 발생
            ctx.KillerActed = ctx.AllDeadThisTurn.Count > 0
                && gameState.GetZone(5) == investigationZoneId
                && !ctx.AllDeadThisTurn.Contains(5);

            // 4. Wanderer — 새턴(#7) 이동 + 이전 구역 사망
            int saturnPrev = gameState.GetPreviousZone(7);
            int saturnCurrent = gameState.GetZone(7);
            if (saturnPrev != saturnCurrent)
                foreach (int deadId in ctx.AllDeadThisTurn)
                    if (gameState.GetPreviousZone(deadId) == saturnPrev)
                    { ctx.WandererActed = true; break; }

            // 5. Sacrifice — 프리드(#6) 사망 + 같은 구역 생존자
            ctx.SacrificeActed = ctx.AllDeadThisTurn.Contains(6);
            if (ctx.SacrificeActed)
            {
                int friedZone = gameState.GetPreviousZone(6);
                foreach (var c in gameState.GetCharactersInZone(friedZone))
                    if (c.CharacterId != 6 && !ctx.AllDeadThisTurn.Contains(c.CharacterId))
                        ctx.SacrificeTargetIds.Add(c.CharacterId);
            }

            // 6. Avenger — 토니(#5) 사망 + 메이(#2) 같은 구역
            ctx.AvengerActed = ctx.AllDeadThisTurn.Contains(5)
                && gameState.GetPreviousZone(2) == gameState.GetPreviousZone(5)
                && !ctx.AllDeadThisTurn.Contains(2);

            // 7. LoverChain — 데우스(#3) + 루이스(#4) 동시 사망
            ctx.LoverChainActed = ctx.AllDeadThisTurn.Contains(3)
                               && ctx.AllDeadThisTurn.Contains(4);

            _cachedCtx = ctx;
            Debug.Log($"[DTM] 컨텍스트 캐싱 — InZone:{investigationZoneId} " +
                      $"전체사망:[{string.Join(",", ctx.AllDeadThisTurn)}] " +
                      $"구역내사망:[{string.Join(",", ctx.ZoneDeadIds)}] " +
                      $"Killer={ctx.KillerActed} Wanderer={ctx.WandererActed} " +
                      $"Sacrifice={ctx.SacrificeActed} Avenger={ctx.AvengerActed} " +
                      $"LoverChain={ctx.LoverChainActed}");
        }

        /// <summary>
        /// 조사 구역(InZone) ID를 반환합니다.
        /// 앵커 캐릭터가 생존 중이면 그 Zone, 사망했으면 0을 반환합니다.
        /// </summary>
        private int GetInvestigationZoneId(IGameState gameState)
        {
            var anchor = gameState.GetCharacter(_anchorCharacterId);
            if (anchor == null || !anchor.IsAlive) return 0;
            return gameState.GetZone(_anchorCharacterId);
        }

        // ═══════════════════════════════════════════════════════════════
        // 구역 순회 및 출력
        // ═══════════════════════════════════════════════════════════════

        private void TriggerDialoguesForAllZones()
        {
            Debug.Log($"[DTM] TriggerDialoguesForAllZones — 모드:{(_useCharacterBasedZone ? "앵커기반" : "Zone순회")}");
            if (_useCharacterBasedZone)
                TriggerByAnchorCharacter();
            else
                TriggerByActiveZones();
        }

        /// <summary>
        /// [앵커 기반] 앵커 캐릭터가 있는 Zone의 캐릭터 조합으로 대사를 선별합니다.
        /// 앵커 캐릭터가 사망했으면 대사 없음.
        /// </summary>
        private void TriggerByAnchorCharacter()
        {
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null)
            {
                Debug.LogWarning("[DTM] TriggerByAnchorCharacter — GameState 없음");
                return;
            }

            var anchor = gameState.GetCharacter(_anchorCharacterId);
            if (anchor == null || !anchor.IsAlive)
            {
                Debug.Log($"[DTM] 앵커 #{_anchorCharacterId} 사망 — 대사 없음");
                return;
            }

            int anchorZone = gameState.GetZone(_anchorCharacterId);
            var characterIds = GetLivingCharactersInZone(anchorZone, gameState);

            Debug.Log($"[DTM] 앵커 #{_anchorCharacterId} Zone{anchorZone} — " +
                      $"구역 캐릭터:[{string.Join(",", characterIds)}] ({characterIds.Count}명)");

            if (characterIds.Count == 0)
            {
                Debug.LogWarning($"[DTM] Zone{anchorZone} 생존 캐릭터 없음");
                return;
            }

            var resolved = ResolveDialogueForZone(characterIds);
            if (!resolved.HasValue)
            {
                Debug.Log($"[DTM] Zone{anchorZone} — 출력할 대사 없음");
                return;
            }

            StartCoroutine(PlaySequential(new List<PendingDialogue> { resolved.Value }));
        }



        /// <summary>[Zone 순회] _activeZones 체크된 모든 Zone 순회.</summary>
        private void TriggerByActiveZones()
        {
            var pending = new List<PendingDialogue>();
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null) return;

            for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
            {
                if (!IsZoneActive(zoneId)) continue;

                var characterIds = GetLivingCharactersInZone(zoneId, gameState);
                Debug.Log($"[DTM] Zone{zoneId} 캐릭터:[{string.Join(",", characterIds)}]");
                if (characterIds.Count == 0) continue;

                var resolved = ResolveDialogueForZone(characterIds);
                if (resolved.HasValue)
                    pending.Add(resolved.Value);
            }

            if (pending.Count == 0) { Debug.Log("[DTM] Zone 순회 — 출력할 대사 없음"); return; }
            StartCoroutine(PlaySequential(pending));
        }

        private bool IsZoneActive(int zoneId)
            => _activeZones != null
            && zoneId >= 0
            && zoneId < _activeZones.Length
            && _activeZones[zoneId];

        /// <summary>특정 구역의 생존 캐릭터 ID 집합을 반환합니다.</summary>
        private HashSet<int> GetLivingCharactersInZone(int zoneId, IGameState gameState)
        {
            var result = new HashSet<int>();
            foreach (var c in gameState.GetCharactersInZone(zoneId))
                if (c.IsAlive)
                    result.Add(c.CharacterId);
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // 후보 선별
        // ═══════════════════════════════════════════════════════════════

        private PendingDialogue? ResolveDialogueForZone(HashSet<int> characterIds)
        {
            var candidates = FindCandidateEntries(characterIds);
            Debug.Log($"[DTM] ResolveDialogue — 조합:[{string.Join(",", characterIds)}] 후보:{candidates.Count}개");

            foreach (var entry in candidates)
            {
                bool canPlay = _conditionEvaluator.CanPlay(
                    entry, characterIds, _progressTracker, _fragmentCollector, _cachedCtx);
                Debug.Log($"[DTM]   {entry.DialogueId} CanPlay:{canPlay}");
                if (canPlay)
                    return new PendingDialogue { Entry = entry, CharacterIds = characterIds, UseFallback = false };
            }

            if (HasAnyMatchingEntry(candidates))
            {
                var fallback = _dialogueData?.AlreadySeenLines;
                if (fallback != null && fallback.Count > 0)
                {
                    Debug.Log("[DTM] AlreadySeenLines 폴백");
                    return new PendingDialogue { Entry = null, CharacterIds = characterIds, UseFallback = true };
                }
            }

            return null;
        }

        private bool HasAnyMatchingEntry(List<DialogueEntry> candidates)
            => candidates != null && candidates.Count > 0;

        private List<DialogueEntry> FindCandidateEntries(HashSet<int> characterIds)
        {
            if (_dialogueData == null) return new List<DialogueEntry>();

            var p1 = new List<DialogueEntry>();
            var p2 = new List<DialogueEntry>();
            var p3 = new List<DialogueEntry>();
            var p4 = new List<DialogueEntry>();

            foreach (var entry in _dialogueData.Dialogues)
            {
                if (entry == null) continue;
                var participantIds = entry.GetParticipantIds();
                if (!ContainsRequiredParticipants(participantIds, characterIds)) continue;

                switch (entry.Type)
                {
                    case DialogueType.Core:
                        // ★ 수집 완료된 Core는 후보에서 완전 제외
                        // p4(Normal 폴백)에도 넣지 않음 → 해당 조합 내 다른 대사를 찾지 않음
                        bool collected = !string.IsNullOrEmpty(entry.RewardFragmentId)
                            && (_fragmentCollector?.HasFragment(entry.RewardFragmentId) ?? false);
                        if (!collected) p1.Add(entry);
                        break;
                    case DialogueType.Hint: p2.Add(entry); break;
                    case DialogueType.Special: p3.Add(entry); break;
                    default: p4.Add(entry); break;
                }
            }

            SortByExactMatch(p1, characterIds);
            SortByExactMatch(p2, characterIds);
            SortByExactMatch(p3, characterIds);
            SortByExactMatch(p4, characterIds);

            var result = new List<DialogueEntry>(p1.Count + p2.Count + p3.Count + p4.Count);
            result.AddRange(p1); result.AddRange(p2);
            result.AddRange(p3); result.AddRange(p4);
            return result;
        }

        /// <summary>
        /// ParticipantIds와 Zone 캐릭터 조합이 완전히 일치하는지 확인합니다.
        ///
        /// ★ 완전 일치 — Zone 캐릭터 수와 참가자 수가 같아야 합니다.
        ///   participantIds=[1,3,4], zoneIds={1,3,4} → true  (정확히 일치)
        ///   participantIds=[3,4],   zoneIds={1,3,4} → false (Zone에 1이 더 있음)
        ///   participantIds=[1,3,4], zoneIds={1,3}   → false (참가자에 4가 없음)
        ///
        /// 이 규칙으로 Zone 내 캐릭터 조합이 다를 때 다른 대사를 찾는 문제를 방지합니다.
        /// </summary>
        private bool ContainsRequiredParticipants(List<int> participantIds, HashSet<int> characterIds)
        {
            if (participantIds == null || participantIds.Count == 0) return false;
            if (characterIds == null || characterIds.Count == 0) return false;

            // ★ 수가 다르면 즉시 false — 조합이 달라지는 근본 원인 차단
            if (participantIds.Count != characterIds.Count) return false;

            foreach (int id in participantIds)
                if (!characterIds.Contains(id)) return false;

            return true;
        }

        private void SortByExactMatch(List<DialogueEntry> list, HashSet<int> characterIds)
        {
            list.Sort((a, b) =>
            {
                int sa = a.GetParticipantIds().Count == characterIds.Count ? 2 : 1;
                int sb = b.GetParticipantIds().Count == characterIds.Count ? 2 : 1;
                return sb.CompareTo(sa);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // 대사 재생
        // ═══════════════════════════════════════════════════════════════

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

        private IEnumerator PlayTimeOfDayDialogue(TimeOfDayDialogueEntry entry)
        {
            if (_dialoguePlayer == null) yield break;
            bool done = false;
            _dialoguePlayer.Play(entry.Lines, onComplete: () => done = true);
            yield return new WaitUntil(() => done);
        }

        private void PlayDialogueEntry(DialogueEntry entry, HashSet<int> characterIds, Action onComplete)
        {
            if (_dialoguePlayer == null) { onComplete?.Invoke(); return; }

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                if (string.IsNullOrEmpty(entry.RewardFragmentId))
                    _progressTracker.MarkPlayed(entry.DialogueId);

                bool fragmentCollected = !string.IsNullOrEmpty(entry.RewardFragmentId)
                    && TryCollectFragment(entry.RewardFragmentId);

                Debug.Log($"[DTM] 대사 완료 — {entry.DialogueId} [{string.Join(",", characterIds)}]");

                if (fragmentCollected)
                    PlayFragmentNotification(entry.RewardFragmentId, onComplete);
                else
                    onComplete?.Invoke();
            });
        }

        private void PlayFallbackLines(HashSet<int> characterIds, Action onComplete)
        {
            if (_dialoguePlayer == null || _dialogueData == null) { onComplete?.Invoke(); return; }

            var lines = new List<DialogueLine>(_dialogueData.AlreadySeenLines);
            if (lines.Count == 0) { onComplete?.Invoke(); return; }

            Debug.Log($"[DTM] 폴백 재생 — [{string.Join(",", characterIds)}]");
            _dialoguePlayer.Play(lines, onComplete: onComplete);
        }

        private void PlayFragmentNotification(string fragmentId, Action onComplete)
        {
            int charId = ParseCharacterIdFromFragment(fragmentId);
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(charId);
            string label = !string.IsNullOrEmpty(name) ? $"'{name}'" : $"#{charId}";
            _dialoguePlayer.PlayNotification(
                new List<string> { $"{label}의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다." },
                onComplete);
        }

        // ═══════════════════════════════════════════════════════════════
        // 헬퍼
        // ═══════════════════════════════════════════════════════════════

        private bool TryCollectFragment(string fragmentId)
        {
            if (_fragmentCollector == null) return false;
            if (_fragmentCollector.HasFragment(fragmentId)) return false;
            _fragmentCollector.TryCollectFragment(fragmentId);
            return true;
        }

        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return -1;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return -1;
            return int.TryParse(fragmentId.Substring(1, idx - 1), out int id) ? id : -1;
        }

        // ═══════════════════════════════════════════════════════════════
        // 내부 구조체
        // ═══════════════════════════════════════════════════════════════

        private struct PendingDialogue
        {
            public DialogueEntry Entry;
            public HashSet<int> CharacterIds;
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
            Debug.Log("[DTM] PlayerPrefs 전체 초기화 완료");
        }

        [ContextMenu("테스트: 강제 대사 트리거")]
        private void TestForceTrigger()
        {
            if (!_isInitialized) { Debug.LogError("[DTM] 초기화 안 됨"); return; }
            Debug.Log("[DTM] 강제 대사 트리거");
            TriggerDialoguesForAllZones();
        }
    }
}