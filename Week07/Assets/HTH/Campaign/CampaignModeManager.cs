using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전체 상태를 관리하는 싱글톤입니다.
    ///
    /// ─── 1회차 → 2회차 전환 흐름 ────────────────────────────────────────
    ///   GameFlowController.HandleGameEnded(isWin=true)
    ///   → OnFirstRunCleared("Stage_1")
    ///   → EnterPhase2SameScene()
    ///   → Phase2TransitionCoroutine()
    ///       1. 검은 화면 FadeIn
    ///       2. _DiaText에 전환 문구 FadeIn 표시
    ///       3. DialoguePlayer 클릭 대기 또는 2초 자동 대기
    ///       4. _DiaText FadeOut
    ///       5. Phase2 상태 설정
    ///       6. 검은 화면 FadeOut
    ///       7. OnPhase2Entered 이벤트 발생
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dia Text                  → 검은 화면 위 TMP_Text (필수)
    ///   Phase2 Transition Lines   → 표시 문구
    ///   Transition Black Panel    → 검은 화면 패널 (CanvasGroup 필요)
    ///   Transition Fade Duration  → 페이드 시간 (초)
    ///   Phase2 Transition Player  → DialoguePlayer (선택, 없으면 2초 대기)
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignModeManager : SingletonMonobehaviour<CampaignModeManager>
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("씬 전환 방식")]
        [Tooltip("false = A방식: 같은 씬에서 컨텐츠 교체 (현재 사용)\n" +
                 "true  = B방식: 로비를 거쳐서 2회차 씬으로 진입 (추후 구현)")]
        [SerializeField] private bool _useSceneTransition = false;

        [Tooltip("로비 씬 이름입니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("엔딩")]
        [Tooltip("엔딩 씬 이름입니다. 비워두면 로비로 이동합니다.")]
        [SerializeField] private string _endingSceneName = "";

        [Header("Phase2 스테이지 ID 접미사")]
        [Tooltip("예: 'Stage_1' + '_Phase2' = 'Stage_1_Phase2'")]
        [SerializeField] private string _phase2Suffix = "_Phase2";

        [Header("Phase2 전환 연출")]
        [Tooltip("검은 화면 위에 표시할 TMP_Text입니다. (필수)")]
        [SerializeField] private TextMeshProUGUI _DiaText;

        [Tooltip("검은 화면 위에 표시할 전환 문구입니다.")]
        [SerializeField] private string _phase2TransitionLines = "이대로 이야기를 끝낼 순 없어...";

        [Tooltip("검은 화면 패널입니다. CanvasGroup 컴포넌트 필요.")]
        [SerializeField] private GameObject _transitionBlackPanel;

        [Tooltip("검은 화면 / 문구 페이드 시간 (초)")]
        [SerializeField] private float _transitionFadeDuration = 0.5f;

        [Tooltip("전환 다이얼로그를 재생할 DialoguePlayer입니다. (선택)\n" +
                 "연결 시 클릭으로 진행, 미연결 시 2초 자동 대기합니다.")]
        [SerializeField] private DialoguePlayer _phase2TransitionPlayer;

        [Tooltip("전환 화자 ID입니다. (0 = 내레이션)")]
        [SerializeField] private int _transitionSpeakerId = 0;

        [Header("Phase2 역할 배정")]
        [Tooltip("Phase2에서 사용할 StageRoleConfig 에셋입니다." +
                 "기본모드 역할을 제거하고 캠페인 모드 역할로 재배정합니다.")]
        [SerializeField] private StageRoleConfig _phase2RoleConfig;

        [Header("Zone 설정")]
        [SerializeField] private SpriteRenderer _zone;
        [SerializeField] private TextMeshPro _zoneText;

        // ── 상태 ─────────────────────────────────────────────────────────

        public CampaignPhase CurrentPhase { get; private set; } = CampaignPhase.None;
        public string CurrentPhase2StageId { get; private set; }

        public static bool IsPhase2Active
            => Instance != null && Instance.CurrentPhase == CampaignPhase.Phase2;

        // ── 이벤트 ───────────────────────────────────────────────────────

        public event Action<string> OnPhase2Entered;
        public event Action OnPhase2Exited;
        public event Action OnCampaignEnding;

        // ── 외부 API ─────────────────────────────────────────────────────

        public void OnFirstRunCleared(string phase1StageId)
        {
            if (string.IsNullOrEmpty(phase1StageId))
            {
                Debug.LogWarning("[CampaignModeManager] OnFirstRunCleared — StageId가 비어있습니다.");
                return;
            }

            string phase2StageId = phase1StageId + _phase2Suffix;
            Debug.Log($"[CampaignModeManager] 1회차 클리어 — {phase1StageId} → {phase2StageId}");

            if (_useSceneTransition) EnterPhase2ViaLobby(phase2StageId);
            else EnterPhase2SameScene(phase2StageId);
        }

        public void EnterPhase2FromLobby(string phase2StageId)
        {
            if (string.IsNullOrEmpty(phase2StageId)) return;

            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            Debug.Log($"[CampaignModeManager] Phase2 진입 (로비 경유) — {phase2StageId}");
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        // ── Private — Phase2 진입 ─────────────────────────────────────────

        private void EnterPhase2SameScene(string phase2StageId)
        {
            StartCoroutine(Phase2TransitionCoroutine(phase2StageId));
        }

        /// <summary>
        /// Phase2 전환 연출 코루틴입니다.
        ///
        /// 순서
        ///   1. 검은 화면 FadeIn
        ///   2. _DiaText에 전환 문구 FadeIn 표시
        ///   3. DialoguePlayer가 있으면 클릭 대기, 없으면 2초 자동 대기
        ///   4. _DiaText FadeOut
        ///   5. Phase2 상태 설정 + Zone 변경
        ///   6. 검은 화면 FadeOut → 화면 밝아짐
        ///   7. OnPhase2Entered 이벤트 발생
        /// </summary>
        private IEnumerator Phase2TransitionCoroutine(string phase2StageId)
        {
            // ── 1. 검은 화면 FadeIn ───────────────────────────────────────
            CanvasGroup cg = null;
            if (_transitionBlackPanel != null)
            {
                _transitionBlackPanel.SetActive(true);
                cg = _transitionBlackPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = _transitionBlackPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                yield return cg.DOFade(1f, _transitionFadeDuration)
                               .SetEase(Ease.OutQuad)
                               .WaitForCompletion();
            }

            // ── 2. 전환 문구 FadeIn ───────────────────────────────────────
            if (_DiaText != null)
            {
                _DiaText.text = _phase2TransitionLines;
                _DiaText.alpha = 0f;
                _DiaText.gameObject.SetActive(true);
                yield return _DiaText.DOFade(1f, _transitionFadeDuration)
                                     .SetEase(Ease.OutQuad)
                                     .WaitForCompletion();
            }

            // ── 3. 클릭 대기 / 자동 대기 ─────────────────────────────────
            if (_phase2TransitionPlayer != null
                && !string.IsNullOrWhiteSpace(_phase2TransitionLines))
            {
                var lines = new System.Collections.Generic.List<DialogueLine>
                {
                    new DialogueLine
                    {
                        SpeakerId           = _transitionSpeakerId,
                        Text                = _phase2TransitionLines.Trim(),
                        RevealCharacterId   = -1,
                        RevealCharacterName = string.Empty,
                    }
                };
                bool done = false;
                _phase2TransitionPlayer.Play(lines, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }

            // ── 4. 전환 문구 FadeOut ──────────────────────────────────────
            if (_DiaText != null)
            {
                yield return _DiaText.DOFade(0f, _transitionFadeDuration)
                                     .SetEase(Ease.InQuad)
                                     .WaitForCompletion();
                _DiaText.gameObject.SetActive(false);
            }

            // ── 5. Phase2 상태 설정 ───────────────────────────────────────
            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            GameLogger.Instance?.StartStageLogging(phase2StageId);

            // Phase2 역할 재배정 (기본모드 역할 → 캠페인 모드 역할)
            if (_phase2RoleConfig != null)
                GameFlowController.Instance?.ReassignRolesForPhase2(_phase2RoleConfig);
            else
                Debug.LogWarning("[CampaignModeManager] Phase2 Role Config 미연결 — 역할 재배정 건너뜀");

            if (_zone != null) _zone.color = Color.green;
            if (_zoneText != null) { _zoneText.color = Color.green; _zoneText.text = "조사 지정 구역"; }

            SubscribeFragmentCollectorEvents();

            Debug.Log($"[CampaignModeManager] Phase2 진입 (같은 씬) — {phase2StageId}");

            // ── 6. 검은 화면 FadeOut ──────────────────────────────────────
            if (cg != null)
            {
                yield return cg.DOFade(0f, _transitionFadeDuration)
                               .SetEase(Ease.InQuad)
                               .WaitForCompletion();
                _transitionBlackPanel.SetActive(false);
            }

            // ── 7. OnPhase2Entered 이벤트 발생 ────────────────────────────
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        private void EnterPhase2ViaLobby(string phase2StageId)
        {
            NewGameConfig.PendingPhase2StageId = phase2StageId;
            Debug.Log($"[CampaignModeManager] Phase2 진입 (로비 경유) — {phase2StageId}");
            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── Private — 엔딩 처리 ───────────────────────────────────────────

        private void SubscribeFragmentCollectorEvents()
        {
            var collector = FindObjectOfType<FragmentCollector>();
            if (collector == null)
            {
                Debug.LogWarning("[CampaignModeManager] FragmentCollector를 찾을 수 없습니다.");
                return;
            }
            collector.OnAllCharactersCompleted += OnAllCharactersCompleted;
            Debug.Log("[CampaignModeManager] FragmentCollector 엔딩 이벤트 구독 완료");
        }

        private void OnAllCharactersCompleted()
        {
            Debug.Log("[CampaignModeManager] 캠페인 엔딩 조건 달성 — 모든 캐릭터 기록 완수");
            OnCampaignEnding?.Invoke();

            string targetScene = string.IsNullOrEmpty(_endingSceneName)
                ? _lobbySceneName
                : _endingSceneName;

            Debug.Log($"[CampaignModeManager] 씬 전환 → {targetScene}");
            StartCoroutine(EndingTransitionCoroutine(targetScene));
        }

        private IEnumerator EndingTransitionCoroutine(string sceneName)
        {
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene(sceneName);
        }
    }
}