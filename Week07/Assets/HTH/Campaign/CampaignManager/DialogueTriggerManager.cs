using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬의 대화 조각 트리거 및 출력을 관리하는 매니저입니다.
    ///
    /// ─── 턴 구조 ─────────────────────────────────────────────────────────
    ///   아침(Turn 0) / 점심(Turn 1) / 저녁(Turn 2) — 3턴 = 1루프
    ///
    /// ─── 매 턴 종료 흐름 (아침/점심/저녁 공통) ───────────────────────────
    ///   OnTurnEndEntered   → 게임 상태 스냅샷 저장
    ///   OnTurnEndDialogueFinished
    ///     → GetAvailableClues() 로 조건 충족 조각 탐색
    ///     → 있으면: 대사 출력 → 조각 지급 → FinishTurnEnd()
    ///     → 없으면: FinishTurnEnd() 즉시
    ///
    /// ─── 강제퇴고 흐름 (엔비 사망, isLoopCondition = true) ───────────────
    ///   OnTurnEndEntered   → 스냅샷 저장
    ///   OnTurnEndDialogueFinished
    ///     → GetForcedExitClues() 로 P05_01/P05_04 탐색
    ///     → 있으면: 특수 대사 출력 → FinishTurnEnd()
    ///                → OnLoopReset 수신 → 조각 지급
    ///     → 없으면: FinishTurnEnd() 즉시
    ///
    /// ─── OnLoopReset 처리 ────────────────────────────────────────────────
    ///   강제퇴고 후 _pendingForcedClues 지급만 처리
    ///   일반 대사는 OnLoopReset 후 출력 안 함 (매 턴 종료 시 즉시 처리)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Fragment Data       → FragmentDataSO
    ///   Dialogue Player     → DialoguePlayer
    ///   Fragment Collector  → FragmentCollector
    ///   Anchor Character Id → 앵커 캐릭터 ID (기본 1 = 엔비)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public class DialogueTriggerManager : MonoBehaviour
    {
        public static DialogueTriggerManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private FragmentDataSO _fragmentData;

        [Tooltip("캐릭터 이름 조회용 ProfileDataSO입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("컴포넌트 참조")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("앵커 캐릭터")]
        [Tooltip("이 캐릭터가 있는 Zone을 대사 출력 구역으로 사용합니다. 기본 1 = 엔비")]
        [SerializeField] private int _anchorCharacterId = 1;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private DialogueProgressTracker _progressTracker;
        private bool _isInitialized;

        /// <summary>true = 엔비 사망으로 인한 강제퇴고</summary>
        private bool _isForcedExit;

        /// <summary>TurnEnd 진입 시점 게임 상태 스냅샷</summary>
        private GameStateSnapshot _snapshot;

        /// <summary>강제퇴고 후 OnLoopReset에서 지급할 조각 목록</summary>
        private List<FragmentEntry> _pendingForcedClues;

        public bool IsWaitingForDialogue { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _progressTracker = new DialogueProgressTracker();

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnCampaignInitialized += Initialize;
        }

        private void Start()
        {
            var gfc = CampaignGameFlowController.Instance;
            var turnSM = gfc?.GetTurnSM();

            // GFC AnchorCharacterId 동기화
            if (gfc != null && gfc.AnchorCharacterId != _anchorCharacterId)
            {
                _anchorCharacterId = gfc.AnchorCharacterId;
                Debug.Log($"[DTM] 앵커 캐릭터 GFC 동기화 — #{_anchorCharacterId}");
            }

            if (turnSM != null)
            {
                turnSM.OnTurnEndEntered += OnTurnEndEntered;
                turnSM.OnTurnEndDialogueFinished += OnTurnEndDialogueFinished;
                Debug.Log($"[DTM] Start — TurnSM 구독 완료, 앵커 #{_anchorCharacterId}");
            }
            else
                Debug.LogWarning("[DTM] Start — TurnSM 없음");

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

        // ── 외부 API ─────────────────────────────────────────────────────

        public void Initialize(string stageId)
        {
            _progressTracker.Initialize(stageId);
            _fragmentCollector?.Initialize(stageId);
            _isInitialized = true;
            Debug.Log($"[DTM] 초기화 완료 — {stageId}");
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────

        private void OnTurnEndEntered(IReadOnlyList<string> roleLog, bool isLoopCondition)
        {
            if (!_isInitialized) return;
            _isForcedExit = isLoopCondition; // true = 엔비 사망(강제퇴고)
            _snapshot = CaptureGameState();

            Debug.Log($"[DTM] TurnEnd 진입 — 강제퇴고:{_isForcedExit}");
        }

        private void OnTurnEndDialogueFinished()
        {
            if (!_isInitialized) return;

            if (_isForcedExit)
            {
                HandleForcedExit();
                return;
            }

            HandleNormalTurnEnd();
        }

        // ── 강제퇴고 처리 (엔비 사망) ─────────────────────────────────────

        private void HandleForcedExit()
        {
            var forcedClues = _fragmentCollector?.GetForcedExitClues(
                _snapshot.AnchorZoneId,
                _snapshot.LivingInAnchorZone,
                _snapshot.DeadCharacters);

            if (forcedClues != null && forcedClues.Count > 0)
            {
                Debug.Log($"[DTM] 강제퇴고 — 특수 대화 {forcedClues.Count}개 출력");
                StartCoroutine(PlayForcedExitCoroutine(forcedClues));
            }
            else
            {
                Debug.Log("[DTM] 강제퇴고 — 특수 대화 없음, FinishTurnEnd");
                CampaignGameFlowController.Instance?.FinishTurnEnd();
            }
        }

        private IEnumerator PlayForcedExitCoroutine(List<FragmentEntry> clues)
        {
            IsWaitingForDialogue = true;

            foreach (var clue in clues)
            {
                if (clue.Lines == null || clue.Lines.Count == 0) continue;
                bool done = false;
                _dialoguePlayer.Play(clue.Lines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }

            IsWaitingForDialogue = false;

            // 조각 지급은 퇴고(루프 리셋) 후로 예약
            _pendingForcedClues = clues;
            CampaignGameFlowController.Instance?.FinishTurnEnd();
        }

        // ── 일반 턴 종료 처리 (아침/점심/저녁 공통) ──────────────────────

        private void HandleNormalTurnEnd()
        {
            var available = _fragmentCollector?.GetAvailableClues(
                _snapshot.AnchorZoneId,
                _snapshot.LivingInAnchorZone,
                _snapshot.DeadCharacters);

            Debug.Log($"[DTM] 일반 턴 종료 — Zone{_snapshot.AnchorZoneId} " +
                      $"캐릭터:[{string.Join(",", _snapshot.LivingInAnchorZone)}] " +
                      $"사망:[{string.Join(",", _snapshot.DeadCharacters)}] " +
                      $"후보:{available?.Count ?? 0}개");

            // 조건 충족 + 미재생 조각 1개 선택
            FragmentEntry target = null;
            if (available != null)
            {
                foreach (var entry in available)
                {
                    if (_progressTracker.HasPlayed(entry.ProfileClueId)) continue;
                    if (entry.Lines == null || entry.Lines.Count == 0)
                    {
                        Debug.LogWarning($"[DTM] Lines 없음 — {entry.ProfileClueId}");
                        continue;
                    }
                    target = entry;
                    break; // 한 턴 1개
                }
            }

            if (target == null)
            {
                Debug.Log("[DTM] 출력할 조각 없음 — FinishTurnEnd");
                CampaignGameFlowController.Instance?.FinishTurnEnd();
                return;
            }

            Debug.Log($"[DTM] 조각 대사 출력 — {target.ProfileClueId}");
            StartCoroutine(PlayNormalTurnDialogue(target));
        }

        private IEnumerator PlayNormalTurnDialogue(FragmentEntry entry)
        {
            IsWaitingForDialogue = true;

            bool done = false;
            _dialoguePlayer.Play(entry.Lines, onComplete: () => done = true);
            yield return new WaitUntil(() => done);

            IsWaitingForDialogue = false;

            // 조각 지급 + 재생 기록
            _progressTracker.MarkPlayed(entry.ProfileClueId);

            bool collected = !(_fragmentCollector?.HasFragment(entry.ProfileClueId) ?? true);
            if (collected)
            {
                // 주 조각 지급 (SimultaneousIds도 내부에서 자동 지급)
                _fragmentCollector.TryCollectFragment(entry.ProfileClueId);

                // 획득된 모든 조각 알림 목록 구성 (주 조각 + 동시획득 조각)
                var notifyIds = new List<string> { entry.ProfileClueId };
                if (entry.SimultaneousIds != null)
                    foreach (var simId in entry.SimultaneousIds)
                        if (!string.IsNullOrEmpty(simId)) notifyIds.Add(simId);

                // 조각별 알림을 1개씩 순서대로 표시
                foreach (var fragmentId in notifyIds)
                {
                    int charId = ParseCharId(fragmentId);
                    string name = GetCharacterName(charId);
                    Color color = GetPersonalColor(charId);

                    // 알림 메시지 (이름에 퍼스널컬러 적용)
                    string colorHex = ColorUtility.ToHtmlStringRGB(color);
                    string msg = $"<color=#{colorHex}>{name}</color>의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다.";

                    bool notifyDone = false;
                    _dialoguePlayer.PlayNotification(
                        new List<string> { msg },
                        onComplete: () => notifyDone = true);
                    yield return new WaitUntil(() => notifyDone);
                }
            }

            Debug.Log($"[DTM] 대사 완료 — {entry.ProfileClueId}, FinishTurnEnd");
            CampaignGameFlowController.Instance?.FinishTurnEnd();
        }

        // ── OnLoopReset — 강제퇴고 후 조각 지급만 처리 ────────────────────

        private void OnLoopReset()
        {
            if (_pendingForcedClues == null || _pendingForcedClues.Count == 0)
            {
                _pendingForcedClues = null;
                return;
            }

            foreach (var clue in _pendingForcedClues)
            {
                Debug.Log($"[DTM] 강제퇴고 후 조각 지급 — {clue.ProfileClueId}");
                _fragmentCollector?.TryCollectFragment(clue.ProfileClueId);
            }
            _pendingForcedClues = null;
        }

        // ── 게임 상태 스냅샷 ──────────────────────────────────────────────

        private GameStateSnapshot CaptureGameState()
        {
            var snap = new GameStateSnapshot();
            var gameState = CampaignGameFlowController.Instance?.GameState;
            if (gameState == null) return snap;

            // 앵커(엔비) Zone 기준
            var anchor = gameState.GetCharacter(_anchorCharacterId);
            if (anchor == null || !anchor.IsAlive)
            {
                Debug.Log($"[DTM] 앵커 #{_anchorCharacterId} 사망 — 스냅샷 비어있음");
                return snap;
            }

            snap.AnchorZoneId = gameState.GetZone(_anchorCharacterId);

            foreach (var ch in gameState.GetCharactersInZone(snap.AnchorZoneId))
                if (ch.IsAlive)
                    snap.LivingInAnchorZone.Add(ch.CharacterId);

            // 이번 턴 사망 마크 기준
            foreach (int id in gameState.GetAllCharacterIds())
                if (gameState.IsMarkedForDeath(id))
                    snap.DeadCharacters.Add(id);

            Debug.Log($"[DTM] 스냅샷 — Zone{snap.AnchorZoneId} " +
                      $"생존:[{string.Join(",", snap.LivingInAnchorZone)}] " +
                      $"사망마크:[{string.Join(",", snap.DeadCharacters)}]");

            return snap;
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        /// <summary>ProfileDataSO에서 캐릭터 이름을 반환합니다. 없으면 #ID.</summary>
        private string GetCharacterName(int characterId)
        {
            var profile = _profileData?.FindProfile(characterId);
            return !string.IsNullOrEmpty(profile?.CharacterFullName)
                ? profile.CharacterFullName
                : $"#{characterId}";
        }

        /// <summary>캐릭터 ID의 퍼스널 컬러를 반환합니다.</summary>
        private static Color GetPersonalColor(int characterId)
        {
            var colors = new Color[]
            {
                Color.white,
                new Color(0xC8/255f, 0xA8/255f, 0x88/255f), // #1 엔비
                new Color(0x48/255f, 0x78/255f, 0x48/255f), // #2 메이
                new Color(0x58/255f, 0x58/255f, 0x88/255f), // #3 데우스
                new Color(0xD8/255f, 0xD8/255f, 0xE8/255f), // #4 루이스
                new Color(0xE8/255f, 0xD8/255f, 0x98/255f), // #5 토니
                new Color(0xE8/255f, 0x88/255f, 0x68/255f), // #6 프리드
                new Color(0x98/255f, 0x88/255f, 0x68/255f), // #7 새턴
            };
            if (characterId < 0 || characterId >= colors.Length) return Color.white;
            return colors[characterId];
        }

        private static int ParseCharId(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return -1;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return -1;
            return int.TryParse(fragmentId.Substring(1, idx - 1), out int id) ? id : -1;
        }

        // ── 내부 구조 ─────────────────────────────────────────────────────

        private class GameStateSnapshot
        {
            public int AnchorZoneId = -1;
            public List<int> LivingInAnchorZone = new();
            public HashSet<int> DeadCharacters = new();
        }

        // ── 테스트 ────────────────────────────────────────────────────────

        [ContextMenu("테스트: PlayerPrefs 초기화")]
        private void TestClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[DTM] PlayerPrefs 초기화 완료");
        }
    }
}