using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 조우를 감지하고 다이얼로그를 선택해 DialoguePlayer에 전달합니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   캠페인 2회차(Phase2)에서 "누가 누구와 같은 구역에 있는가"를 판단해
    ///   해당 조합의 대사를 찾아 DialoguePlayer에게 재생을 요청합니다.
    ///   대사 재생이 완료되면 대화 조각 수집과 이름 공개도 처리합니다.
    ///
    /// ─── 트리거 시점 ─────────────────────────────────────────────────────
    ///   턴 종료(날짜 변경) 시점에 모든 구역을 체크합니다.
    ///   플레이어가 원하는 캐릭터들을 배치하고 날짜 변경 버튼을 누르면
    ///   각 구역의 조합을 판별해 대사를 0→1→2→3 순서로 재생합니다.
    ///
    /// ─── 대사 출력 조건 ──────────────────────────────────────────────────
    ///   1. Phase2 활성 상태
    ///   2. 이번 씬 진입 이후 아직 출력하지 않은 조합 (씬 재시작 시 리셋)
    ///   3. 해당 조합의 대화 조각이 아직 미수집
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Data      → CampaignDialogueSO 에셋
    ///   Dialogue Player    → _CampaignSystem/DialoguePlayer
    ///   Fragment Collector → _CampaignSystem/FragmentCollector
    ///   Active Zones [0~3] → 각 구역의 대사 활성화 여부
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueTriggerManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("이 스테이지의 캠페인 다이얼로그 데이터입니다.")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [Tooltip("대사를 화면에 출력하는 컴포넌트입니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Tooltip("대화 조각 수집을 담당하는 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Header("구역 대사 활성화 (0 → 1 → 2 → 3 순서)")]
        [Tooltip("체크된 구역만 턴 종료 시 대사를 체크합니다.\n" +
                 "인덱스 = ZoneId (0=Zone0, 1=Zone1, 2=Zone2, 3=Zone3)")]
        [SerializeField] private bool[] _activeZones = new bool[GameState.ZoneCount] { true, true, true, true };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        /// <summary>이번 씬 진입 이후 출력된 조합을 기록합니다. (씬 재시작 시 리셋)</summary>
        private DialogueProgressTracker _progressTracker;

        /// <summary>대사 출력 조건을 판별합니다.</summary>
        private DialogueConditionEvaluator _conditionEvaluator;

        /// <summary>Phase2가 활성화되고 초기화가 완료됐는지 여부입니다.</summary>
        private bool _isInitialized;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _progressTracker = new DialogueProgressTracker();
            _conditionEvaluator = new DialogueConditionEvaluator();
        }

        private void Start()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;

            var turnSM = GameFlowController.Instance?.GetTurnSM();
            if (turnSM != null)
                turnSM.OnPlayerActionStarted += OnPlayerActionStarted;
        }

        private void OnDestroy()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;

            var turnSM = GameFlowController.Instance?.GetTurnSM();
            if (turnSM != null)
                turnSM.OnPlayerActionStarted -= OnPlayerActionStarted;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────

        /// <summary>
        /// Phase2 진입 시 호출됩니다.
        /// ProgressTracker는 메모리만 초기화합니다 (PlayerPrefs 저장 없음).
        /// FragmentCollector는 이전 수집 기록을 PlayerPrefs에서 로드합니다.
        /// </summary>
        private void OnPhase2Entered(string stageId)
        {
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
        /// 다음 턴 PlayerAction이 시작될 때 호출됩니다.
        /// 이전 턴의 캐릭터 배치를 기준으로 대사를 트리거합니다.
        /// </summary>
        private void OnPlayerActionStarted()
        {
            if (!_isInitialized) return;
            if (!CampaignModeManager.IsPhase2Active) return;

            TriggerDialoguesForAllZones();
        }

        // ── Private — 전체 구역 순회 ─────────────────────────────────────

        /// <summary>
        /// 활성화된 구역을 0→1→2→3 순서로 순회하며
        /// 캐릭터 조합에 맞는 대사를 찾아 순차 재생합니다.
        /// </summary>
        private void TriggerDialoguesForAllZones()
        {
            var pendingEntries = new List<(GroupDialogueEntry entry, HashSet<int> ids)>();
            var ctx = BuildConditionContext();

            for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
            {
                if (_activeZones == null
                    || zoneId >= _activeZones.Length
                    || !_activeZones[zoneId])
                    continue;

                var characterIds = GetCharactersInZone(zoneId);
                Debug.Log($"[DTM] Zone{zoneId} 캐릭터: [{string.Join(",", characterIds)}]");
                if (characterIds.Count == 0) continue;

                var candidates = FindCandidateEntries(characterIds);
                foreach (var entry in candidates)
                {
                    if (!_conditionEvaluator.CanPlay(
                            entry, characterIds, _progressTracker, _fragmentCollector, ctx))
                        continue;

                    pendingEntries.Add((entry, characterIds));
                    break; // 구역당 1개 대사
                }
            }

            if (pendingEntries.Count == 0) return;

            StartCoroutine(PlaySequential(pendingEntries));
        }

        /// <summary>현재 턴의 ConditionContext를 생성합니다.</summary>
        private ConditionContext BuildConditionContext()
        {
            var gameState = GameFlowController.Instance?.GameState;
            var deadThisTurn = new HashSet<int>();
            int totalDeathCount = 0;

            if (gameState != null)
            {
                foreach (int id in gameState.GetAllCharacterIds())
                {
                    if (gameState.GetCharacter(id) == null) continue;
                    if (!gameState.IsMarkedForDeath(id)) continue;

                    deadThisTurn.Add(id);
                    totalDeathCount++;
                }
            }

            // Phase2 역할 기믹은 캐릭터 ID로 판별
            // 새턴(#7) = 배회자 / 프리드(#6) = 희생양
            bool wandererKill = false;
            bool sacrifice = false;

            if (gameState != null)
            {
                int saturnPrev = gameState.GetPreviousZone(7);
                int saturnCurrent = gameState.GetZone(7);

                if (saturnPrev != saturnCurrent)
                {
                    foreach (int deadId in deadThisTurn)
                    {
                        if (gameState.GetZone(deadId) == saturnPrev)
                        {
                            wandererKill = true;
                            break;
                        }
                    }
                }

                sacrifice = gameState.IsMarkedForDeath(6);
            }

            return new ConditionContext
            {
                AllDeadThisTurn = deadThisTurn,
                TotalDeathCount = totalDeathCount,
                WandererKillOccurred = wandererKill,
                SacrificeOccurred = sacrifice,
            };
        }

        /// <summary>
        /// 구역 캐릭터 조합에 맞는 후보를 전부 반환합니다.
        /// 우선순위 정렬은 여기서 하고, 상황 조건 필터링은 CanPlay()에서 처리합니다.
        ///
        /// 우선순위
        ///   1. 프로파일 핵심문장 (fragmentId 있음, 미수집)
        ///   2. 사망 반응 계열
        ///   3. 생존 조합 대사 / 2인 대화 / 3인 대화 (일반 대화, 정확한 조합 우선)
        ///   4. 개인 독백
        ///   5. 프로파일 유도대사
        /// </summary>
        private List<GroupDialogueEntry> FindCandidateEntries(HashSet<int> characterIds)
        {
            if (_dialogueData == null) return new List<GroupDialogueEntry>();

            var priority1 = new List<GroupDialogueEntry>(); // 프로파일 핵심문장
            var priority2 = new List<GroupDialogueEntry>(); // 사망 반응 계열
            var priority3 = new List<GroupDialogueEntry>(); // 일반 대화
            var priority4 = new List<GroupDialogueEntry>(); // 개인 독백
            var priority5 = new List<GroupDialogueEntry>(); // 프로파일 유도대사

            foreach (var entry in _dialogueData.GroupDialogues)
            {
                if (entry == null) continue;
                if (!MatchesAnyComboKey(entry.ComboKey, characterIds)) continue;

                switch (entry.SituationType)
                {
                    case "프로파일 핵심문장":
                        if (!string.IsNullOrEmpty(entry.FragmentId) &&
                            !(_fragmentCollector?.HasFragment(entry.FragmentId) ?? false))
                            priority1.Add(entry);
                        break;

                    case "사망 반응":
                    case "사망 반응 / 연인 연쇄":
                    case "사망 반응 / 배회자":
                    case "사망 반응 / 살인자":
                    case "사망 반응 / 복수자":
                    case "사망 반응 / 희생양":
                        priority2.Add(entry);
                        break;

                    case "개인 독백":
                        priority4.Add(entry);
                        break;

                    case "프로파일 유도대사":
                        priority5.Add(entry);
                        break;

                    default: // 생존 조합 대사, 2인 대화, 3인 대화, 전체 파티 대화
                        priority3.Add(entry);
                        break;
                }
            }

            // 정확한 조합 우선 정렬 (participantIds.Count == characterIds.Count)
            SortByMatchScore(priority1, characterIds);
            SortByMatchScore(priority2, characterIds);
            SortByMatchScore(priority3, characterIds);

            // 우선순위 순서대로 합쳐서 반환
            // TriggerDialoguesForAllZones에서 CanPlay()로 상황 조건 필터링 후 첫 번째 사용
            var result = new List<GroupDialogueEntry>();
            result.AddRange(priority1);
            result.AddRange(priority2);
            result.AddRange(priority3);
            result.AddRange(priority4);
            result.AddRange(priority5);
            return result;
        }

        private void SortByMatchScore(List<GroupDialogueEntry> list, HashSet<int> characterIds)
        {
            list.Sort((a, b) =>
            {
                int scoreA = a.ParticipantIds.Count == characterIds.Count ? 2 : 1;
                int scoreB = b.ParticipantIds.Count == characterIds.Count ? 2 : 1;
                return scoreB.CompareTo(scoreA);
            });
        }

        private bool MatchesAnyComboKey(string comboKey, HashSet<int> characterIds)
        {
            if (string.IsNullOrEmpty(comboKey)) return false;

            string[] orKeys = comboKey.Split(
                new[] { " 또는 " }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string key in orKeys)
                if (MatchesComboKey(key.Trim(), characterIds)) return true;

            return false;
        }

        private bool MatchesComboKey(string comboKey, HashSet<int> characterIds)
        {
            if (string.IsNullOrEmpty(comboKey)) return false;

            bool hasAny = comboKey.Contains("ANY");
            var parts = comboKey.Split('|');
            var requiredIds = new List<int>();

            foreach (string part in parts)
            {
                string p = part.Trim().Replace("#", "");
                if (p == "ANY") continue;
                if (int.TryParse(p, out int id))
                    requiredIds.Add(id);
            }

            foreach (int rid in requiredIds)
                if (!characterIds.Contains(rid)) return false;

            if (!hasAny && requiredIds.Count == 1)
                return characterIds.Count == 1;

            return true;
        }

        /// <summary>
        /// 여러 구역의 대사를 순서대로 재생합니다.
        /// </summary>
        private IEnumerator PlaySequential(
            List<(GroupDialogueEntry entry, HashSet<int> ids)> entries)
        {
            foreach (var (entry, characterIds) in entries)
            {
                bool done = false;
                PlayGroupDialogue(entry, characterIds, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
        }

        // ── Private — 조우 판별 ───────────────────────────────────────────

        /// <summary>특정 구역 내의 생존 캐릭터 ID 집합을 반환합니다.</summary>
        private HashSet<int> GetCharactersInZone(int zoneId)
        {
            var result = new HashSet<int>();
            var gameState = GameFlowController.Instance?.GameState;
            if (gameState == null) return result;

            foreach (var c in gameState.GetCharactersInZone(zoneId))
                result.Add(c.CharacterId);

            return result;
        }

        // ── Private — 다이얼로그 재생 ─────────────────────────────────────

        /// <summary>
        /// 그룹 대사를 재생합니다.
        /// 완료 후 출력 기록, 조각 수집, 이름 공개를 처리합니다.
        /// </summary>
        private void PlayGroupDialogue(
            GroupDialogueEntry entry,
            HashSet<int> characterIds,
            Action onComplete = null)
        {
            if (_dialoguePlayer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                _progressTracker.MarkComboPlayed(entry.ComboId);

                // 조각 수집
                bool fragmentCollected = !string.IsNullOrEmpty(entry.FragmentId)
                    && TryCollectAndCheck(entry.FragmentId);

                // 이름 공개 수집 및 등록
                var revealedNames = CollectRevealedNames(entry.Lines);
                RegisterRevealedNames(revealedNames);

                // 획득 알림
                var notifications = BuildNotifications(
                    entry.FragmentId, fragmentCollected, revealedNames);

                Debug.Log($"[DialogueTriggerManager] 그룹 대사 완료 — " +
                          $"{string.Join(",", characterIds)}");

                if (notifications.Count > 0)
                    _dialoguePlayer.PlayNotification(notifications, onComplete);
                else
                    onComplete?.Invoke();
            });
        }

        /// <summary>조각 수집을 시도하고 실제로 수집됐는지 반환합니다.</summary>
        private bool TryCollectAndCheck(string fragmentId)
        {
            if (_fragmentCollector == null) return false;
            if (_fragmentCollector.HasFragment(fragmentId)) return false;

            _fragmentCollector.TryCollectFragment(fragmentId);
            return true;
        }

        /// <summary>대사 줄에서 공개될 이름 목록을 수집합니다.</summary>
        private List<(int characterId, string name)> CollectRevealedNames(
            List<DialogueLine> lines)
        {
            var result = new List<(int, string)>();
            if (lines == null) return result;

            foreach (var line in lines)
            {
                if (line == null) continue;
                if (line.RevealCharacterId < 0) continue;
                if (string.IsNullOrEmpty(line.RevealCharacterName)) continue;

                // 이미 수집된 이름 제외
                string existing = CharacterRecordPanelManager.Instance?
                    .GetCollectedName(line.RevealCharacterId);
                if (!string.IsNullOrEmpty(existing)) continue;

                // 중복 방지
                bool alreadyInList = false;
                foreach (var r in result)
                    if (r.Item1 == line.RevealCharacterId) { alreadyInList = true; break; }

                if (!alreadyInList)
                    result.Add((line.RevealCharacterId, line.RevealCharacterName));
            }

            return result;
        }

        /// <summary>수집된 이름을 CharacterRecordPanelManager에 등록합니다.</summary>
        private void RegisterRevealedNames(List<(int characterId, string name)> revealedNames)
        {
            foreach (var (id, name) in revealedNames)
                CharacterRecordPanelManager.Instance?.RegisterCharacterName(id, name);
        }

        /// <summary>획득 알림 메시지 목록을 생성합니다.</summary>
        private List<string> BuildNotifications(
            string fragmentId,
            bool fragmentCollected,
            List<(int characterId, string name)> revealedNames)
        {
            var messages = new List<string>();

            foreach (var (id, name) in revealedNames)
                messages.Add($"'{name}'의 이름을 알게 됐습니다.\n인물 기록장에서 확인할 수 있습니다.");

            if (fragmentCollected && !string.IsNullOrEmpty(fragmentId))
            {
                int charId = ParseCharacterIdFromFragment(fragmentId);
                string charLabel = charId >= 0 ? $"#{charId}" : "캐릭터";
                string charName = CharacterRecordPanelManager.Instance?.GetCollectedName(charId);

                if (!string.IsNullOrEmpty(charName))
                    charLabel = $"'{charName}'";

                messages.Add($"{charLabel}의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다.");
            }

            return messages;
        }

        /// <summary>
        /// P01_01 형식의 FragmentId에서 캐릭터 ID를 파싱합니다.
        /// 예: "P01_01" → 1 / "P07_05" → 7
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;

            if (fragmentId.Length >= 3 && fragmentId[0] == 'P')
            {
                int underscoreIdx = fragmentId.IndexOf('_');
                if (underscoreIdx > 1)
                {
                    string charPart = fragmentId.Substring(1, underscoreIdx - 1);
                    if (int.TryParse(charPart, out int charId))
                        return charId;
                }
            }

            return -1;
        }

        // ── 테스트용 (배포 전 제거) ───────────────────────────────────────

        [ContextMenu("테스트: Phase2 강제 진입")]
        private void TestEnterPhase2()
            => CampaignModeManager.Instance?.OnFirstRunCleared("Stage_1");

        [ContextMenu("테스트: PlayerPrefs 초기화")]
        private void TestClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[DialogueTriggerManager] PlayerPrefs 전체 초기화 완료");
        }
    }
}