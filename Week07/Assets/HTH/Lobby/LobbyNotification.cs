using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비 진입 시 도감 해금 알림을 표시합니다.
    ///
    /// ─── 표시 조건 ───────────────────────────────────────────────────────
    ///   캠페인에서 새로 해금된 시점 완결문이 있을 때만 표시합니다.
    ///   한 번 표시한 알림은 다시 표시하지 않습니다. (세션 기준)
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   NotificationPanel (기본 비활성)
    ///     MessageText (TMP_Text) → "도감이 해금되었습니다." 등
    ///     CloseButton (Button)   → 닫기 버튼
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Notification Panel → 알림 패널 GameObject
    ///   Message Text       → TMP_Text
    ///   Close Button       → 닫기 버튼
    ///   Fade Duration      → 페이드 인/아웃 시간
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyNotification : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Tooltip("알림 패널 GameObject입니다.")]
        [SerializeField] private GameObject _notificationPanel;

        [Tooltip("알림 메시지를 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _messageText;

        [Tooltip("닫기 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button _closeButton;

        [Header("데이터")]
        [Tooltip("캠페인 보상 해금 기록 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Tooltip("페이드 인/아웃 시간입니다.")]
        [SerializeField] private float _fadeDuration = 0.5f;

        [Tooltip("알림 표시 후 자동으로 닫히는 시간입니다. (0이면 자동 닫기 없음)")]
        [SerializeField] private float _autoCloseDelay = 0f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private static bool _shownThisSession;
        private CanvasGroup _canvasGroup;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = _notificationPanel?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _notificationPanel != null)
                _canvasGroup = _notificationPanel.AddComponent<CanvasGroup>();

            if (_notificationPanel != null)
                _notificationPanel.SetActive(false);

            _closeButton?.onClick.AddListener(Hide);
        }

        private void Start()
        {
            CheckAndShow();
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>세션 플래그를 초기화합니다. (테스트용)</summary>
        public static void ResetSession() => _shownThisSession = false;

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 도감 해금 알림 표시 조건을 확인하고 표시합니다.
        /// </summary>
        private void CheckAndShow()
        {
            if (_shownThisSession) return;

            // 해금된 에필로그 수 확인
            int unlockedCount = 0;
            if (_rewardSaveData == null) return;
            for (int i = 1; i <= 7; i++)
                if (_rewardSaveData.IsEpilogueUnlocked(i)) unlockedCount++;

            if (unlockedCount <= 0) return;

            _shownThisSession = true;

            string message = unlockedCount == 1
                ? "도감이 해금되었습니다.\n캐릭터의 시점 완결문을 확인해보세요."
                : $"도감에 {unlockedCount}개의 시점 완결문이 해금되었습니다.\n확인해보세요.";

            Show(message);
        }

        private void Show(string message)
        {
            if (_messageText != null)
                _messageText.text = message;

            if (_notificationPanel != null)
                _notificationPanel.SetActive(true);

            // 페이드 인
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration);
            }

            // 자동 닫기
            if (_autoCloseDelay > 0f)
                StartCoroutine(AutoCloseCoroutine());
        }

        private void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(0f, _fadeDuration)
                    .OnComplete(() => _notificationPanel?.SetActive(false));
            }
            else
            {
                _notificationPanel?.SetActive(false);
            }
        }

        private IEnumerator AutoCloseCoroutine()
        {
            yield return new WaitForSeconds(_autoCloseDelay);
            Hide();
        }
    }
}