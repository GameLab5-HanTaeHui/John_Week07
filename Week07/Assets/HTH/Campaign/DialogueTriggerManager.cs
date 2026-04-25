using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 조우를 감지하고 다이얼로그를 선택해 DialoguePlayer에 전달합니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   CampaignModeManager.OnPhase2Entered 수신 → 활성화
    ///   PlayerActionState.OnActionConfirmed 이벤트 수신
    ///   → OnCharacterMoved(movedCharacterId, zoneId) 호출
    ///   → 해당 구역의 캐릭터 목록 수집
    ///   → 2명 이상: 그룹 대사 체크
    ///   → 1명: 단독 대사 체크 (미확정 기능 — 활성화 여부는 추후 결정)
    ///   → DialogueConditionEvaluator로 출력 가능 여부 확인
    ///   → CampaignDialogueSO에서 대사 검색
    ///   → DialoguePlayer.Play() 호출
    ///   → 재생 완료 후 ProgressTracker 기록 + FragmentCollector 수집 요청
    ///
    /// ─── 활성화 조건 ─────────────────────────────────────────────────────
    ///   CampaignModeManager.IsPhase2Active == true 일 때만 동작
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   DialogueData    → 이 스테이지의 CampaignDialogueSO
    ///   DialoguePlayer  → UI 출력 담당
    ///   EnableSoloDialogue → 단독 대사 기능 활성화 여부 (미확정)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueTriggerManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("이 스테이지의 캠페인 다이얼로그 데이터")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("단독 대사 (미확정)")]
        [Tooltip("단독 대사 기능 활성화 여부. 사용 여부 미확정 — 기능만 구현됨.")]
        [SerializeField] private bool _enableSoloDialogue = false;

        [Header("구역 대사 활성화 (0 → 1 → 2 → 3 순서)")]
        [Tooltip("체크된 구역만 턴 종료 시 대사 체크. 인덱스 = ZoneId.")]
        [SerializeField] private bool[] _activeZones = new bool[GameState.ZoneCount] { true, true, true, true };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private DialogueProgressTracker _progressTracker;
        private DialogueConditionEvaluator _conditionEvaluator;
        private PlayerActionState _playerAction;

        private bool _isInitialized;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _progressTracker = new DialogueProgressTracker();
            _conditionEvaluator = new DialogueConditionEvaluator();
        }

        private void Start()
        {
            // Phase2 진입 이벤트 구독
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;

            // GameFlowController.OnLoopReset 구독
            // → 루프마다 PlayerActionState가 갱신될 수 있으므로 재구독
            var gfc = GameFlowController.Instance;
            if (gfc == null) return;

            // [캠패인모드] 턴 종료 시점에 다이얼로그 트리거
            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                turnSM.OnTurnEndEntered += OnTurnEndEntered;

            // 최초 구독 시도
            SubscribePlayerActionEvents();
        }

        private void OnDestroy()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;

            var gfc = GameFlowController.Instance;
            if (gfc == null) return;

            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                turnSM.OnTurnEndEntered -= OnTurnEndEntered;

                UnsubscribePlayerActionEvents();
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            // 다이얼로그 데이터 스테이지 ID 검증
            if (_dialogueData == null)
            {
                Debug.LogError("[DialogueTriggerManager] CampaignDialogueSO가 연결되지 않았습니다.");
                return;
            }

            _progressTracker.Initialize(stageId);
            _fragmentCollector?.Initialize(stageId);
            _isInitialized = true;

            Debug.Log($"[DialogueTriggerManager] Phase2 활성화 — {stageId}");
        }
        /// <summary>
        /// 턴 종료 시 호출됩니다.
        /// 모든 구역을 순회하며 캐릭터 조합에 맞는 대사를 트리거합니다.
        /// </summary>
        private void OnTurnEndEntered(System.Collections.Generic.IReadOnlyList<string> _, bool __)
        {
            if (!_isInitialized) return;
            if (!CampaignModeManager.IsPhase2Active) return;

            TriggerDialoguesForAllZones();
        }
        /// <summary>
        /// PlayerActionState.OnActionConfirmed 이벤트 수신 시 호출됩니다.
        /// (characterId, targetZoneId)
        /// </summary>
        private void OnActionConfirmed(int characterId, int targetZoneId)
        {
            if (!_isInitialized) return;
            if (!CampaignModeManager.IsPhase2Active) return;
            if (_dialoguePlayer != null && _dialoguePlayer.IsPlaying) return;

            OnCharacterMoved(characterId, targetZoneId);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 이동 완료 시 호출됩니다.
        /// 해당 구역의 캐릭터 조합을 판별해 다이얼로그를 트리거합니다.
        /// </summary>
        public void OnCharacterMoved(int movedCharacterId, int zoneId)
        {
            if (!_isInitialized || !CampaignModeManager.IsPhase2Active) return;

            var characterIds = GetCharactersInZone(zoneId);

            if (characterIds.Count >= 2)
            {
                // 2명 이상: 그룹 대사 체크
                TryTriggerGroupDialogue(characterIds);
            }
            else if (characterIds.Count == 1 && _enableSoloDialogue)
            {
                // 1명: 단독 대사 체크 (미확정 기능)
                TryTriggerSoloDialogue(movedCharacterId);
            }
        }
        // ── Private — 전체 구역 순회 ─────────────────────────────────────────

        /// <summary>
        /// 활성화된 구역을 0 → 1 → 2 → 3 순서로 순회하며
        /// 캐릭터 조합에 맞는 대사를 순차 재생합니다.
        /// Inspector의 _activeZones에서 체크된 구역만 체크합니다.
        /// </summary>
        private void TriggerDialoguesForAllZones()
        {
            var pendingEntries = new System.Collections.Generic.List
                <(GroupDialogueEntry entry, System.Collections.Generic.HashSet<int> ids)>();

            // 0 → 1 → 2 → 3 순서로 순회
            for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
            {
                // Inspector에서 비활성화된 구역은 스킵
                if (_activeZones == null
                    || zoneId >= _activeZones.Length
                    || !_activeZones[zoneId])
                {
                    Debug.Log($"[DialogueTriggerManager] 구역 {zoneId} 비활성화 — 스킵");
                    continue;
                }

                var characterIds = GetCharactersInZone(zoneId);
                if (characterIds.Count < 2) continue;

                var entry = _dialogueData.FindGroupDialogue(characterIds);
                if (entry == null) continue;

                if (!_conditionEvaluator.CanPlay(entry, characterIds,
                                                  _progressTracker, _fragmentCollector))
                    continue;

                pendingEntries.Add((entry, characterIds));
            }

            if (pendingEntries.Count == 0) return;

            StartCoroutine(PlaySequential(pendingEntries));
        }

        /// <summary>
        /// 여러 구역의 대사를 순서대로 재생합니다.
        /// </summary>
        private System.Collections.IEnumerator PlaySequential(System.Collections.Generic.List
                <(GroupDialogueEntry entry, System.Collections.Generic.HashSet<int> ids)> entries)
        {
            foreach (var (entry, characterIds) in entries)
            {
                bool done = false;
                PlayGroupDialogue(entry, characterIds, onComplete: () => done = true);
                yield return new UnityEngine.WaitUntil(() => done);
            }
        }

        // ── Private — 조우 판별 ───────────────────────────────────────────

        /// <summary>
        /// 구역 내 생존 캐릭터 ID 집합을 수집합니다.
        /// GameState를 통해 현재 구역의 캐릭터 목록을 가져옵니다.
        /// </summary>
        private HashSet<int> GetCharactersInZone(int zoneId)
        {
            var result = new HashSet<int>();
            var gameState = GameFlowController.Instance?.GameState;
            if (gameState == null) return result;

            var characters = gameState.GetCharactersInZone(zoneId);
            foreach (var c in characters)
                result.Add(c.CharacterId);

            return result;
        }

        /// <summary>그룹 대사 트리거를 시도합니다.</summary>
        private void TryTriggerGroupDialogue(HashSet<int> characterIds)
        {
            var entry = _dialogueData.FindGroupDialogue(characterIds);
            if (entry == null) return;

            if (!_conditionEvaluator.CanPlay(entry, characterIds, _progressTracker, _fragmentCollector))
                return;

            PlayGroupDialogue(entry, characterIds);
        }

        /// <summary>단독 대사 트리거를 시도합니다. (미확정 기능)</summary>
        private void TryTriggerSoloDialogue(int characterId)
        {
            var entry = _dialogueData.FindSoloDialogue(characterId);
            if (entry == null) return;

            if (!_conditionEvaluator.CanPlay(entry, _progressTracker, _fragmentCollector))
                return;

            PlaySoloDialogue(entry);
        }

        // ── Private — 다이얼로그 재생 ─────────────────────────────────────

        private void PlayGroupDialogue(GroupDialogueEntry entry, HashSet<int> characterIds, System.Action onComplete = null)
        {
            if (_dialoguePlayer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _dialoguePlayer.Play(
                entry.Lines,
                onComplete: () =>
                {
                    _progressTracker.MarkGroupPlayed(characterIds);

                    // 대화 조각 수집
                    if (!string.IsNullOrEmpty(entry.FragmentId))
                        _fragmentCollector?.TryCollectFragment(entry.FragmentId);

                    // 이름 공개 처리
                    RevealCharacterNamesFromLines(entry.Lines);

                    Debug.Log($"[DialogueTriggerManager] 그룹 대사 완료 — {string.Join(",", characterIds)}");
                    onComplete?.Invoke();
                });
        }

        private void PlaySoloDialogue(SoloDialogueEntry entry)
        {
            if (_dialoguePlayer == null) return;

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                _progressTracker.MarkSoloPlayed(entry.CharacterId);

                if (!string.IsNullOrEmpty(entry.FragmentId))
                    _fragmentCollector?.TryCollectFragment(entry.FragmentId);

                // 이름 공개 처리
                RevealCharacterNamesFromLines(entry.Lines);

                Debug.Log($"[DialogueTriggerManager] 단독 대사 완료 — ID:{entry.CharacterId}");
            });
        }

        // ── Private — 이벤트 구독 ─────────────────────────────────────────

        /// <summary>
        /// 대사 줄 목록에서 이름 공개 필드를 확인하고
        /// CharacterRecordBook에 이름을 등록합니다.
        /// DialogueLine.RevealCharacterId >= 0이고
        /// RevealCharacterName이 비어있지 않은 경우에만 동작합니다.
        /// </summary>
        private void RevealCharacterNamesFromLines(
            System.Collections.Generic.List<DialogueLine> lines)
        {
            if (lines == null || _characterRecordBook == null) return;

            foreach (var line in lines)
            {
                if (line == null) continue;
                if (line.RevealCharacterId < 0) continue;
                if (string.IsNullOrEmpty(line.RevealCharacterName)) continue;

                _characterRecordBook.RegisterCharacterName(
                    line.RevealCharacterId,
                    line.RevealCharacterName);
            }
        }

        /// <summary>
        /// PlayerActionState 이벤트를 구독합니다.
        /// GameFlowController.Start() 이후에 호출해야 합니다.
        /// </summary>
        private void SubscribePlayerActionEvents()
        {
            var gfc = GameFlowController.Instance;
            if (gfc == null) return;

            var playerAction = gfc.GetPlayerActionState();
            if (playerAction == null)
            {
                Debug.LogWarning("[DialogueTriggerManager] PlayerActionState를 찾을 수 없습니다.");
                return;
            }

            // 중복 구독 방지
            if (_playerAction == playerAction) return;

            UnsubscribePlayerActionEvents();
            _playerAction = playerAction;
            _playerAction.OnActionConfirmed += OnActionConfirmed;

            Debug.Log("[DialogueTriggerManager] PlayerActionState 구독 완료");
        }

        /// <summary>
        /// 루프 리셋 시 PlayerActionState 재구독합니다.
        /// PlayerActionState 인스턴스가 루프마다 유지되므로 실질적 재구독은 최초 1회입니다.
        /// </summary>
        private void ResubscribePlayerActionEvents()
        {
            SubscribePlayerActionEvents();
        }

        private void UnsubscribePlayerActionEvents()
        {
            if (_playerAction != null)
            {
                _playerAction.OnActionConfirmed -= OnActionConfirmed;
                _playerAction = null;
            }
        }
        // 테스트용 — 배포 전 제거
        [ContextMenu("테스트: Phase2 강제 진입")]
        private void TestEnterPhase2()
        {
            CampaignModeManager.Instance?.OnFirstRunCleared("Stage_1");
        }
    }
}