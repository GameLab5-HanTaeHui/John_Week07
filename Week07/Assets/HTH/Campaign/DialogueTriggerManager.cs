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
    ///   캐릭터 이동 시점이 아닌 턴 종료(날짜 변경) 시점에 모든 구역을 체크합니다.
    ///   플레이어가 원하는 캐릭터들을 배치하고 날짜 변경 버튼을 누르면
    ///   그 시점에 각 구역의 조합을 판별해 대사를 0→1→2→3 순서로 재생합니다.
    ///
    /// ─── 대사 출력 조건 ──────────────────────────────────────────────────
    ///   1. Phase2 활성 상태
    ///   2. 이번 씬 진입 이후 아직 출력하지 않은 조합 (씬 재시작 시 리셋)
    ///   3. 해당 조합의 대화 조각이 아직 미수집
    ///      → 이미 수집 완료된 조합은 영구적으로 스킵
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas 안에 배치하지 않습니다 (UI 컴포넌트가 아님).
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Data         → CampaignDialogueSO 에셋 (대사 데이터)
    ///   Dialogue Player       → _CampaignSystem/DialoguePlayer
    ///   Fragment Collector    → _CampaignSystem/FragmentCollector
    ///   Character Record Book → _CampaignSystem/CharacterRecordBook
    ///   Active Zones [0~3]    → 각 구역의 대사 활성화 여부
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueTriggerManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("이 스테이지의 캠페인 다이얼로그 데이터\n" +
                 "Project → Create → HTH → Campaign → DialogueData로 생성합니다.")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [Tooltip("대사를 화면에 출력하는 컴포넌트\n_CampaignSystem/DialoguePlayer를 연결합니다.")]
        [SerializeField] private DialoguePlayer _dialoguePlayer;

        [Tooltip("대화 조각 수집을 담당하는 컴포넌트\n_CampaignSystem/FragmentCollector를 연결합니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("인물 기록장 컴포넌트. 대사에서 이름이 공개될 때 등록됩니다.\n" +
                 "_CampaignSystem/CharacterRecordBook을 연결합니다.")]
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("단독 대사 (미확정)")]
        [Tooltip("단독 대사 기능 활성화 여부.\n" +
                 "현재 사용 여부가 결정되지 않아 false로 유지합니다.\n" +
                 "true로 설정 시 혼자 있는 캐릭터에게도 대사가 출력됩니다.")]
        [SerializeField] private bool _enableSoloDialogue = false;

        [Header("구역 대사 활성화 (0 → 1 → 2 → 3 순서)")]
        [Tooltip("체크된 구역만 턴 종료 시 대사를 체크합니다.\n" +
                 "인덱스 = ZoneId (0=Zone0, 1=Zone1, 2=Zone2, 3=Zone3)\n" +
                 "false로 설정된 구역은 캐릭터가 모여있어도 대사가 나오지 않습니다.")]
        [SerializeField] private bool[] _activeZones = new bool[GameState.ZoneCount] { true, true, true, true };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // 이번 씬 진입 이후 출력된 조합을 기록합니다. (씬 재시작 시 리셋)
        // PlayerPrefs에 저장하지 않습니다.
        private DialogueProgressTracker _progressTracker;

        // 대사 출력 조건을 판별합니다.
        private DialogueConditionEvaluator _conditionEvaluator;

        // Phase2가 활성화되고 초기화가 완료됐는지 여부입니다.
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

            var gfc = GameFlowController.Instance;
            if (gfc == null) return;

            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                turnSM.OnPlayerActionStarted += OnPlayerActionStarted;
        }

        private void OnDestroy()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;

            var gfc = GameFlowController.Instance;
            if (gfc == null) return;

            var turnSM = gfc.GetTurnSM();
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
                Debug.LogError("[DialogueTriggerManager] CampaignDialogueSO가 연결되지 않았습니다.\n" +
                               "Inspector의 Dialogue Data 필드에 에셋을 연결해주세요.");
                return;
            }

            // 출력 기록 리셋 (씬 진입마다 초기화)
            _progressTracker.Initialize(stageId);

            // 대화 조각 수집 기록 로드 (영구 저장 — 이미 수집한 조각은 유지)
            _fragmentCollector?.Initialize(stageId);

            _isInitialized = true;
            Debug.Log($"[DialogueTriggerManager] Phase2 활성화 — {stageId}");
        }

        /// <summary>
        /// 다음 턴 PlayerAction이 시작될 때 호출됩니다.
        /// 검은 화면 페이드 인이 완전히 끝난 후 발생하므로
        /// 이전 화면의 클릭이 대사 스킵으로 인식되지 않습니다.
        ///
        /// 흐름:
        ///   TurnEnd(검은 화면) 완료
        ///   → AdvanceTurn() → 다음 루프 또는 다음 턴
        ///   → LoopStart → RunningTurn → PlayerAction 진입
        ///   → OnPlayerActionStarted 이벤트 발생 → 여기서 수신
        ///   → 캠페인 대사 시작
        /// </summary>
        private void OnPlayerActionStarted()
        {
            if (!_isInitialized) return;
            if (!CampaignModeManager.IsPhase2Active) return;

            // 이전 턴의 캐릭터 배치를 기준으로 대사를 트리거합니다.
            TriggerDialoguesForAllZones();
        }

        // ── Private — 전체 구역 순회 ─────────────────────────────────────

        /// <summary>
        /// 활성화된 구역을 0 → 1 → 2 → 3 순서로 순회하며
        /// 캐릭터 조합에 맞는 대사를 찾아 순차 재생합니다.
        /// </summary>
        private void TriggerDialoguesForAllZones()
        {
            var pendingEntries = new List<(GroupDialogueEntry entry, HashSet<int> ids)>();

            for (int zoneId = 0; zoneId < GameState.ZoneCount; zoneId++)
            {
                if (_activeZones == null
                    || zoneId >= _activeZones.Length
                    || !_activeZones[zoneId])
                    continue;

                var characterIds = GetCharactersInZone(zoneId);
                if (characterIds.Count < 2) continue;

                var entry = _dialogueData.FindGroupDialogue(characterIds);
                if (entry == null) continue;

                if (!_conditionEvaluator.CanPlay(entry, characterIds, _progressTracker, _fragmentCollector))
                    continue;

                pendingEntries.Add((entry, characterIds));
            }

            if (pendingEntries.Count == 0) return;

            StartCoroutine(PlaySequential(pendingEntries));
        }

        /// <summary>
        /// 여러 구역의 대사를 순서대로 재생합니다.
        /// 한 구역의 대사가 완전히 끝난 후 다음 구역의 대사를 재생합니다.
        /// </summary>
        private IEnumerator PlaySequential(List<(GroupDialogueEntry entry, HashSet<int> ids)> entries)
        {
            foreach (var (entry, characterIds) in entries)
            {
                bool done = false;
                PlayGroupDialogue(entry, characterIds, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
        }

        // ── Private — 조우 판별 ───────────────────────────────────────────

        /// <summary>
        /// 특정 구역 내의 생존 캐릭터 ID 집합을 반환합니다.
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

        /// <summary>
        /// 단독 대사 트리거를 시도합니다. (미확정 기능)
        /// </summary>
        private void TryTriggerSoloDialogue(int characterId)
        {
            var entry = _dialogueData.FindSoloDialogue(characterId);
            if (entry == null) return;

            // 단독 대사는 SoloDialogueEntry 오버로드를 사용합니다.
            if (!_conditionEvaluator.CanPlay(entry, _progressTracker, _fragmentCollector))
                return;

            PlaySoloDialogue(entry);
        }

        // ── Private — 다이얼로그 재생 ─────────────────────────────────────

        /// <summary>
        /// 그룹 대사를 재생합니다.
        /// 완료 후 출력 기록, 조각 수집, 이름 공개를 처리합니다.
        /// </summary>
        private void PlayGroupDialogue(GroupDialogueEntry entry, HashSet<int> characterIds, Action onComplete = null)
        {
            if (_dialoguePlayer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                _progressTracker.MarkGroupPlayed(characterIds);

                if (!string.IsNullOrEmpty(entry.FragmentId))
                    _progressTracker.MarkFragmentPlayed(entry.FragmentId);

                // 조각 수집
                bool fragmentCollected = false;
                if (!string.IsNullOrEmpty(entry.FragmentId))
                    fragmentCollected = TryCollectAndCheck(entry.FragmentId);

                // 이름 공개 처리 및 공개된 이름 목록 수집
                var revealedNames = CollectRevealedNames(entry.Lines);
                RegisterRevealedNames(revealedNames);

                // 획득 알림 메시지 생성
                var notifications = BuildNotifications(entry.FragmentId, fragmentCollected, revealedNames);

                Debug.Log($"[DialogueTriggerManager] 그룹 대사 완료 — {string.Join(",", characterIds)}");

                if (notifications.Count > 0)
                {
                    // 알림 표시 후 onComplete
                    _dialoguePlayer.PlayNotification(notifications, onComplete);
                }
                else
                {
                    onComplete?.Invoke();
                }
            });
        }
        /// <summary>조각 수집을 시도하고 실제로 수집됐는지 반환합니다.</summary>
        private bool TryCollectAndCheck(string fragmentId)
        {
            if (_fragmentCollector == null) return false;
            if (_fragmentCollector.HasFragment(fragmentId)) return false; // 이미 수집됨

            _fragmentCollector.TryCollectFragment(fragmentId);
            return true; // 새로 수집됨
        }

        /// <summary>대사 줄에서 공개될 이름 목록을 수집합니다.</summary>
        private List<(int characterId, string name)> CollectRevealedNames(List<DialogueLine> lines)
        {
            var result = new List<(int, string)>();
            if (lines == null) return result;

            foreach (var line in lines)
            {
                if (line == null) continue;
                if (line.RevealCharacterId < 0) continue;
                if (string.IsNullOrEmpty(line.RevealCharacterName)) continue;

                // 이미 수집된 이름은 제외
                string existing = _characterRecordBook?.GetCollectedName(line.RevealCharacterId);
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

        /// <summary>수집된 이름을 CharacterRecordBook에 등록합니다.</summary>
        private void RegisterRevealedNames(List<(int characterId, string name)> revealedNames)
        {
            foreach (var (id, name) in revealedNames)
                CharacterRecordPanelManager.Instance?.RegisterCharacterName(id, name);
        }

        /// <summary>
        /// 획득 알림 메시지 목록을 생성합니다.
        /// 이름 공개 + 대화 조각 수집 정보를 포함합니다.
        /// </summary>
        private List<string> BuildNotifications(string fragmentId,
                                                 bool fragmentCollected,
                                                 List<(int characterId, string name)> revealedNames)
        {
            var messages = new List<string>();

            // 이름 공개 알림
            foreach (var (id, name) in revealedNames)
                messages.Add($"'{name}'의 이름을 알게 됐습니다.\n인물 기록장에서 확인할 수 있습니다.");

            // 대화 조각 수집 알림
            if (fragmentCollected && !string.IsNullOrEmpty(fragmentId))
            {
                int charId = ParseCharacterIdFromFragment(fragmentId);
                string charLabel = charId >= 0 ? $"#{charId}" : "캐릭터";

                // CharacterRecordBook에서 이름 가져오기 (수집됐으면 실제 이름)
                string charName = _characterRecordBook?.GetCollectedName(charId);
                if (!string.IsNullOrEmpty(charName))
                    charLabel = $"'{charName}'";

                messages.Add($"{charLabel}의 대화 조각을 획득했습니다.\n인물 기록장에서 확인할 수 있습니다.");
            }

            return messages;
        }

        /// <summary>FragmentId에서 캐릭터 ID를 파싱합니다.</summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;
            const string marker = "_char";
            int startIdx = fragmentId.IndexOf(marker, System.StringComparison.Ordinal);
            if (startIdx < 0) return -1;
            startIdx += marker.Length;
            int endIdx = fragmentId.IndexOf('_', startIdx);
            if (endIdx < 0) endIdx = fragmentId.Length;
            string idStr = fragmentId.Substring(startIdx, endIdx - startIdx);
            return int.TryParse(idStr, out int id) ? id : -1;
        }


        /// <summary>단독 대사를 재생합니다.</summary>
        private void PlaySoloDialogue(SoloDialogueEntry entry)
        {
            if (_dialoguePlayer == null) return;

            _dialoguePlayer.Play(entry.Lines, onComplete: () =>
            {
                _progressTracker.MarkSoloPlayed(entry.CharacterId);

                if (!string.IsNullOrEmpty(entry.FragmentId))
                    _fragmentCollector?.TryCollectFragment(entry.FragmentId);

                RevealCharacterNamesFromLines(entry.Lines);

                Debug.Log($"[DialogueTriggerManager] 단독 대사 완료 — ID:{entry.CharacterId}");
            });
        }

        // ── Private — 이름 공개 ───────────────────────────────────────────

        /// <summary>
        /// 대사 줄 목록에서 이름 공개 필드를 확인하고 CharacterRecordBook에 등록합니다.
        /// </summary>
        private void RevealCharacterNamesFromLines(List<DialogueLine> lines)
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

        // ── 테스트용 (배포 전 제거) ───────────────────────────────────────

        [ContextMenu("테스트: Phase2 강제 진입")]
        private void TestEnterPhase2()
        {
            CampaignModeManager.Instance?.OnFirstRunCleared("Stage_1");
        }

        [ContextMenu("테스트: PlayerPrefs 초기화")]
        private void TestClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[DialogueTriggerManager] PlayerPrefs 전체 초기화 완료");
        }
    }
}