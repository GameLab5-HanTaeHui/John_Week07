using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬의 다이얼로그 트리거 및 출력을 관리하는 매니저입니다.
    ///
    /// ─── 일반퇴고 트리거 흐름 ────────────────────────────────────────────
    ///   1. TurnEnd 진입 → CacheConditionContext (현재 캐릭터 조합 캐싱)
    ///   2. FireTurnEndDialogueFinished
    ///      → 현재 캐릭터 조합 기준 대사 후보 선조회 → _pendingDialogues 저장
    ///      → FinishTurnEnd() 호출 (→ LoopReset → 페이드 중 캐릭터 재배치)
    ///   3. OnLoopReset 수신 (재배치 완료)
    ///      → _triggerDelay 대기 후 저장된 대사 출력
    ///
    /// ─── 강제퇴고 트리거 흐름 ────────────────────────────────────────────
    ///   1. TurnEnd 진입 → CacheConditionContext
    ///   2. FireTurnEndDialogueFinished
    ///      → FinishTurnEnd() 즉시 (Zone 대사 없음)
    ///
    /// ─── 우선순위 ────────────────────────────────────────────────────────
    ///   1. Core    — 핵심 대화 (RewardFragmentId 미수집)
    ///   2. Hint    — 힌트 대화
    ///   3. Special — 특수 대화
    ///   4. Normal  — 일반 대화 (이미 출력됐으면 AlreadySeenLines 폴백)
    ///   같은 우선순위 내에서 ParticipantIds 수 정확 일치 우선
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Data            → CampaignDialogueSO 에셋
    ///   Dialogue Player          → DialoguePlayer 컴포넌트
    ///   Fragment Collector       → FragmentCollector 컴포넌트
    ///   Use Character Based Zone → true=앵커 기반 / false=Zone 순회
    ///   Anchor Character Id      → 앵커 캐릭터 ID (기본 1 = 엔비)
    ///   Active Zones             → Zone 순회 모드에서 활성화할 Zone
    ///   Trigger Delay            → 루프 리셋 완료 후 대사 트리거까지 대기 시간
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public class DialogueTriggerManager : MonoBehaviour
    {
        public static DialogueTriggerManager Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════
        // Inspector
        // ═══════════════════════════════════════════════════════════════

        [Header("데이터")]
        [Tooltip("이 캠페인 씬의 다이얼로그 데이터입니다.")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [Tooltip("대사를 화면에 출력하는 컴포넌트입니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Tooltip("대화 조각 수집을 담당하는 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("구역 판별 방식")]
        [Tooltip("true  — 앵커 캐릭터(#1)가 있는 Zone 하나만 대사 출력 구역으로 사용\n" +
                 "false — _activeZones에 체크된 모든 Zone을 순회")]
        [SerializeField] private bool _useCharacterBasedZone = true;

        [Tooltip("앵커 캐릭터 ID (기본 1 = 엔비)\n이 캐릭터가 있는 Zone이 대사 출력 구역이 됩니다.")]
        [SerializeField] private int _anchorCharacterId = 1;

        [Tooltip("Zone 순회 모드에서 활성화할 Zone\n인덱스 = ZoneId (0~3)")]
        [SerializeField]
        private bool[] _activeZones =
            new bool[GameState.ZoneCount] { true, true, true, true };

        [Header("타이밍")]
        [Tooltip("루프 리셋 완료 후 대사 트리거까지 대기 시간(초)")]
        [SerializeField] private float _triggerDelay = 1f;

        // ═══════════════════════════════════════════════════════════════
        // 내부 상태
        // ═══════════════════════════════════════════════════════════════

        private DialogueProgressTracker _progressTracker;
        private DialogueConditionEvaluator _conditionEvaluator;
        private ConditionContext _cachedCtx = new();
        private bool _isInitialized;

        /// <summary>
        /// true  = 강제퇴고 (주인공 사망) — Zone 대사 출력 차단
        /// false = 일반퇴고 (저녁 턴 종료) — Zone 대사 출력 허용
        /// </summary>
        private bool _isForcedLoop;

        /// <summary>
        /// 일반퇴고 시 FinishTurnEnd 전에 선조회한 대사 목록입니다.
        /// OnLoopReset 수신 후 재생합니다.
        /// </summary>
        private List<PendingDialogue> _pendingDialogues;

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

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
        }

        private void Start()
        {
            var gfc = CampaignGameFlowController.Instance;
            var turnSM = gfc?.GetTurnSM();

            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered += OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished += OnTurnEndDialogueFinished;
                Debug.Log("[DTM] Start — TurnSM 이벤트 구독 완료");
            }
            else
                Debug.LogWarning("[DTM] Start — CampaignGameFlowController 또는 TurnSM 없음");

            if (gfc != null)
                gfc.OnLoopReset += OnLoopReset;

            if (CampaignModeManager.Instance != null && !_isInitialized)
            {
                CampaignModeManager.Instance.OnCampaignInitialized -= Initialize;
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            var gfc = CampaignGameFlowController.Instance;
            var turnSM = gfc?.GetTurnSM();

            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered -= OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished -= OnTurnEndDialogueFinished;
            }

            if (gfc != null)
                gfc.OnLoopReset -= OnLoopReset;

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnCampaignInitialized -= Initialize;
        }

        // ═══════════════════════════════════════════════════════════════
        // 외부 API
        // ═══════════════════════════════════════════════════════════════

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

        private void OnTurnEndEntered(IReadOnlyList<string> roleLog, bool isLastTurn)
        {
            Debug.Log($"[DTM] OnTurnEndEntered — isInitialized:{_isInitialized}, 강제퇴고:{isLastTurn}");
            if (!_isInitialized) return;

            _isForcedLoop = isLastTurn;
            CacheConditionContext();
        }

        private void OnTurnEndDialogueFinished()
        {
            Debug.Log($"[DTM] OnTurnEndDialogueFinished — 강제퇴고:{_isForcedLoop}");
            if (!_isInitialized) return;

            if (_isForcedLoop)
            {
                // 강제퇴고 — Zone 대사 없이 즉시 진행
                Debug.Log("[DTM] 강제퇴고 — Zone 대사 생략, FinishTurnEnd 호출");
                CampaignGameFlowController.Instance?.FinishTurnEnd();
                return;
            }

            // ★ 페이드 전, 현재 캐릭터 조합 기준으로 대사 후보 선조회
            _pendingDialogues = PreResolvePendingDialogues();

            if (_pendingDialogues.Count == 0)
            {
                // 출력할 대사 없음 — 즉시 진행
                Debug.Log("[DTM] 일반퇴고 — 출력할 대사 없음, FinishTurnEnd 호출");
                _pendingDialogues = null;
                CampaignGameFlowController.Instance?.FinishTurnEnd();
                return;
            }

            // ★ 대사가 있으면 FinishTurnEnd 호출 후 OnLoopReset에서 재생
            Debug.Log($"[DTM] 일반퇴고 — {_pendingDialogues.Count}개 선조회 완료, FinishTurnEnd 호출");
            CampaignGameFlowController.Instance?.FinishTurnEnd();
        }

        /// <summary>
        /// 루프 리셋 완료(캐릭터 재배치 완료) 후 호출됩니다.
        /// 페이드 화면이 끝나기를 기다린 뒤 선조회된 대사를 출력합니다.
        /// </summary>
        private void OnLoopReset()
        {
            if (_pendingDialogues == null || _pendingDialogues.Count == 0)
            {
                _pendingDialogues = null;
                return;
            }

            Debug.Log("[DTM] OnLoopReset — 페이드 후 대사 출력 대기");
            var dialoguesToPlay = _pendingDialogues;
            _pendingDialogues = null;

            StartCoroutine(PlayAfterLoopReset(dialoguesToPlay));
        }

        private IEnumerator PlayAfterLoopReset(List<PendingDialogue> dialogues)
        {
            IsWaitingForDialogue = true;
            yield return new WaitForSeconds(_triggerDelay);
            IsWaitingForDialogue = false;

            Debug.Log("[DTM] 대기 완료 → 선조회 대사 출력 시작");

            bool done = false;
            StartCoroutine(PlaySequentialWithComplete(dialogues, () => done = true));
            yield return new WaitUntil(() => done);

            Debug.Log("[DTM] Zone 대사 완료");
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

            foreach (int id in gameState.GetAllCharacterIds())
            {
                if (!gameState.IsMarkedForDeath(id)) continue;
                ctx.AllDeadThisTurn.Add(id);
                ctx.TotalDeathCount++;
            }

            int investigationZoneId = GetInvestigationZoneId(gameState);
            foreach (var c in gameState.GetCharactersInZone(investigationZoneId))
                if (ctx.AllDeadThisTurn.Contains(c.CharacterId))
                    ctx.ZoneDeadIds.Add(c.CharacterId);

            ctx.KillerActed = ctx.AllDeadThisTurn.Count > 0
                && gameState.GetZone(5) == investigationZoneId
                && !ctx.AllDeadThisTurn.Contains(5);

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

            ctx.SacrificeActed = ctx.AllDeadThisTurn.Contains(6);
            if (ctx.SacrificeActed)
            {
                int friedZone = gameState.GetPreviousZone(6);
                foreach (var c in gameState.GetCharactersInZone(friedZone))
                    if (c.CharacterId != 6 && !ctx.AllDeadThisTurn.Contains(c.CharacterId))
                        ctx.SacrificeTargetIds.Add(c.CharacterId);
            }

            ctx.AvengerActed = ctx.AllDeadThisTurn.Contains(5)
                && gameState.GetPreviousZone(2) == gameState.GetPreviousZone(5)
                && !ctx.AllDeadThisTurn.Contains(2);

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

        private int GetInvestigationZoneId(IGameState gameState)
        {
            var anchor = gameState.GetCharacter(_anchorCharacterId);
            if (anchor == null || !anchor.IsAlive) return 0;
            return gameState.GetZone(_anchorCharacterId);
        }

        // ═══════════════════════════════════════════════════════════════
        // 대사 선조회
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// FinishTurnEnd 호출 전, 현재 캐릭터 조합(루프 리셋 전)을 기준으로 대사 후보를 수집합니다.
        /// </summary>
        private List<PendingDialogue> PreResolvePendingDialogues()
        {
            var result = new List<PendingDialogue>();
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null) return result;

            if (_useCharacterBasedZone)
            {
                var anchor = gameState.GetCharacter(_anchorCharacterId);
                if (anchor == null || !anchor.IsAlive)
                {
                    Debug.Log($"[DTM] 선조회 — 앵커 #{_anchorCharacterId} 사망, 대사 없음");
                    return result;
                }

                int anchorZone = gameState.GetZone(_anchorCharacterId);
                var characterIds = GetLivingCharactersInZone(anchorZone, gameState);

                Debug.Log($"[DTM] 선조회 — Zone{anchorZone} 캐릭터:[{string.Join(",", characterIds)}]");

                if (characterIds.Count > 0)
                {
                    var resolved = ResolveDialogueForZone(characterIds);
                    if (resolved.HasValue) result.Add(resolved.Value);
                }
            }
            else
            {
                for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
                {
                    if (!IsZoneActive(zoneId)) continue;
                    var characterIds = GetLivingCharactersInZone(zoneId, gameState);

                    Debug.Log($"[DTM] 선조회 — Zone{zoneId} 캐릭터:[{string.Join(",", characterIds)}]");

                    if (characterIds.Count == 0) continue;
                    var resolved = ResolveDialogueForZone(characterIds);
                    if (resolved.HasValue) result.Add(resolved.Value);
                }
            }

            Debug.Log($"[DTM] 선조회 완료 — {result.Count}개 대사 대기");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // 구역 유틸
        // ═══════════════════════════════════════════════════════════════

        private bool IsZoneActive(int zoneId)
            => _activeZones != null
               && zoneId >= 0
               && zoneId < _activeZones.Length
               && _activeZones[zoneId];

        private HashSet<int> GetLivingCharactersInZone(int zoneId, IGameState gameState)
        {
            var result = new HashSet<int>();
            foreach (var ch in gameState.GetCharactersInZone(zoneId))
                if (ch.IsAlive)
                    result.Add(ch.CharacterId);
            return result;
        }

        private PendingDialogue? ResolveDialogueForZone(HashSet<int> characterIds)
        {
            var candidates = FindCandidateEntries(characterIds);

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

            if (candidates != null && candidates.Count > 0)
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

        // ═══════════════════════════════════════════════════════════════
        // 후보 선별 및 우선순위 정렬
        // ═══════════════════════════════════════════════════════════════

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
            result.AddRange(p1);
            result.AddRange(p2);
            result.AddRange(p3);
            result.AddRange(p4);
            return result;
        }

        private bool ContainsRequiredParticipants(List<int> participantIds, HashSet<int> characterIds)
        {
            if (participantIds == null || participantIds.Count == 0) return false;
            if (characterIds == null || characterIds.Count == 0) return false;
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

        private IEnumerator PlaySequentialWithComplete(List<PendingDialogue> entries,
                                                       System.Action onComplete)
        {
            yield return StartCoroutine(PlaySequential(entries));
            onComplete?.Invoke();
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

            _dialoguePlayer.Play(entry.Lines, dialogueType: entry.Type, onComplete: () =>
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

            Debug.Log($"[DTM] 이미 본 대화 폴백 — [{string.Join(",", characterIds)}]");
            _dialoguePlayer.Play(lines, onComplete: onComplete);
        }

        private void PlayFragmentNotification(string fragmentId, Action onComplete)
        {
            int charId = ParseCharacterIdFromFragment(fragmentId);
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(charId);
            string label = !string.IsNullOrEmpty(name) ? $"'{name}'" : $"#{charId}";
            string msg = $"{label}의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다.";
            _dialoguePlayer.PlayNotification(new List<string> { msg }, onComplete);
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
        // 내부 데이터 구조
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
            Debug.Log("[DialogueTriggerManager] PlayerPrefs 전체 초기화 완료");
        }
    }
}