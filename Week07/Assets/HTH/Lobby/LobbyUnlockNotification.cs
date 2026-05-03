using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비 진입 시 캠페인 저장 데이터를 로드해
    /// 메뉴 버튼 표시/숨김 및 알림 팝업을 처리합니다.
    ///
    /// ─── 버튼 표시 조건 (SetActive) ──────────────────────────────────────
    ///   캠페인 버튼  : isTutorialCleared = true
    ///   초기화 버튼  : isTutorialCleared = true
    ///                  (튜토리얼 보상 P01_01~05 / P02_01 / isTutorialCleared 는 초기화 제외)
    ///   다음장 버튼  : unlockedEpilogues 1개 이상
    ///
    /// ─── 첫 실행 (JSON 없음) ─────────────────────────────────────────────
    ///   3개 버튼 모두 SetActive(false) 유지
    ///
    /// ─── 알림 팝업 ───────────────────────────────────────────────────────
    ///   세션당 1회만 표시 (static 플래그)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyUnlockNotification : MonoBehaviour
    {
        private const string StageId = "CampaignMode";

        [Header("UI 요소 (알림창)")]
        [SerializeField] private GameObject _notificationPanel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("로비 메뉴 버튼")]
        [Tooltip("isTutorialCleared = true 시 표시되는 캠페인 버튼")]
        [SerializeField] private GameObject _campaignButton;

        [Tooltip("isTutorialCleared = true 시 표시되는 초기화 버튼\n" +
                 "★ 초기화 시 튜토리얼 보상(P01_01~05, P02_01, isTutorialCleared)은 보존됩니다.")]
        [SerializeField] private GameObject _resetButton;

        [Tooltip("unlockedEpilogues 1개 이상 시 표시되는 다음장 버튼")]
        [SerializeField] private GameObject _nextChapterButton;

        [Header("설정")]
        [SerializeField] private float _fadeDuration = 0.5f;

        // 세션당 1회 표시 플래그
        private static bool _shownTutorialPopupThisSession = false;
        private static bool _shownCollectionPopupThisSession = false;

        private Coroutine _autoCloseCoroutine;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            // ★ 기본값 = 숨김 — JSON 로드 전까지 전부 비표시
            _campaignButton?.SetActive(false);
            _resetButton?.SetActive(false);
            _nextChapterButton?.SetActive(false);

            if (_canvasGroup == null && _notificationPanel != null)
            {
                _canvasGroup = _notificationPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _notificationPanel.AddComponent<CanvasGroup>();
            }

            if (_notificationPanel != null) _notificationPanel.SetActive(false);

            _closeButton?.onClick.AddListener(HideNotification);
        }

        private void Start()
        {
            StartCoroutine(LoadAndInitialize());
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(HideNotification);
            _canvasGroup?.DOKill();
        }

        // ── 초기화 ───────────────────────────────────────────────────────

        private IEnumerator LoadAndInitialize()
        {
            // 1프레임 대기 — CampaignSaveManager Start() 완료 보장
            yield return null;

            var mgr = CampaignSaveManager.GetOrCreate();
            var saveData = mgr.CurrentSave ?? mgr.Load(StageId);

            bool isTutorialCleared = saveData?.isTutorialCleared ?? false;
            int epilogueCount = saveData?.unlockedEpilogues?.Count ?? 0;

            // 버튼 표시/숨김
            _campaignButton?.SetActive(isTutorialCleared);
            _resetButton?.SetActive(isTutorialCleared);
            _nextChapterButton?.SetActive(epilogueCount > 0);

            Debug.Log($"[LobbyUnlockNotification] 로드 완료 — " +
                      $"튜토리얼:{isTutorialCleared}, 에필로그:{epilogueCount}개");

            // 알림 팝업 (세션당 1회)
            yield return ShowUnlockNotification(isTutorialCleared, epilogueCount);
        }

        // ── 알림 팝업 ────────────────────────────────────────────────────

        private IEnumerator ShowUnlockNotification(bool isTutorialCleared, int epilogueCount)
        {
            if (isTutorialCleared && !_shownTutorialPopupThisSession)
            {
                _shownTutorialPopupThisSession = true;
                ShowNotification("튜토리얼 클리어!\n<color=#FFD700>[캠페인 모드]</color>가 해금되었습니다.");
                yield break;
            }

            if (epilogueCount > 0 && !_shownCollectionPopupThisSession)
            {
                _shownCollectionPopupThisSession = true;
                string msg = epilogueCount == 1
                    ? "캠페인 클리어!\n<color=#FFD700>다음장</color>이 해금되었습니다."
                    : $"새로운 시점 완결문 해금!\n현재 총 <color=#FFD700>{epilogueCount}</color>개가 열렸습니다.";
                ShowNotification(msg);
            }
        }

        // ── 팝업 표시/숨김 ───────────────────────────────────────────────

        private void ShowNotification(string message)
        {
            if (_messageText != null) _messageText.text = message;
            if (_notificationPanel != null) _notificationPanel.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration);
            }
        }

        private void HideNotification()
        {
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.DOFade(0f, _fadeDuration)
                    .OnComplete(() => _notificationPanel?.SetActive(false));
            }
            else
                _notificationPanel?.SetActive(false);
        }
    }
}