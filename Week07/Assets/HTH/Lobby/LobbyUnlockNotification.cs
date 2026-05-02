using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비 진입 시 튜토리얼/캠페인 클리어 상태를 JSON 데이터에서 확인하고,
    /// 해금된 콘텐츠(캠페인 모드, 도감)에 대한 알림 팝업을 띄웁니다.
    /// (앱 실행 기준 세션당 1회만 표시됩니다)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyUnlockNotification : MonoBehaviour
    {
        [Header("UI 요소 (알림창)")]
        [SerializeField] private GameObject _notificationPanel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("로비 메뉴 버튼 (해금 제어용)")]
        [Tooltip("튜토리얼 클리어 시 활성화될 캠페인 모드 버튼")]
        [SerializeField] private Button _campaignButton;

        [Tooltip("캠페인 클리어 시 활성화될 도감(시점 완결문) 버튼")]
        [SerializeField] private Button _collectionButton;

        [Header("데이터 및 설정")]
        [SerializeField] private RewardSaveData _rewardSaveData;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _autoCloseDelay = 3.0f;

        // 내부 상태
        private Coroutine _autoCloseCoroutine;

        // PlayerPrefs 대신 작성자님의 원본 코드 방식(static 메모리 유지) 적용
        private static bool _shownTutorialPopupThisSession = false;
        private static bool _shownCollectionPopupThisSession = false;

        private void Awake()
        {
            if (_canvasGroup == null && _notificationPanel != null)
            {
                _canvasGroup = _notificationPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _notificationPanel.AddComponent<CanvasGroup>();
            }

            if (_notificationPanel != null)
                _notificationPanel.SetActive(false);

            _closeButton?.onClick.AddListener(HideNotification);

            // 시작할 때 JSON 데이터 기반으로 메뉴 버튼 활성화/비활성화 처리
            InitializeMenuButtons();
        }

        private void Start()
        {
            StartCoroutine(CheckAndShowUnlockNotificationRoutine());
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(HideNotification);

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
            }
        }

        /// <summary>
        /// JSON 데이터 기반으로 로비 메뉴 버튼들의 잠금을 해제합니다.
        /// </summary>
        private void InitializeMenuButtons()
        {
            // 1. 캠페인 버튼 활성화 (튜토리얼 클리어 여부 확인)
            bool isTutorialCleared = _rewardSaveData != null && _rewardSaveData.IsTutorialCleared();
            if (_campaignButton != null) _campaignButton.interactable = isTutorialCleared;

            // 2. 도감 버튼 활성화 (도감 1개 이상 해금 여부 확인)
            bool isCampaignCleared = GetUnlockedEpilogueCount() > 0;
            if (_collectionButton != null) _collectionButton.interactable = isCampaignCleared;
        }

        private IEnumerator CheckAndShowUnlockNotificationRoutine()
        {
            yield return null; // 데이터 로드 대기

            bool isTutorialCleared = _rewardSaveData != null && _rewardSaveData.IsTutorialCleared();
            int unlockedCount = GetUnlockedEpilogueCount();

            // ── 1. 튜토리얼 클리어 -> 캠페인 해금 알림 ──
            if (isTutorialCleared && !_shownTutorialPopupThisSession)
            {
                _shownTutorialPopupThisSession = true;
                ShowNotification("튜토리얼 클리어!\n<color=#FFD700>[캠페인 모드]</color>가 해금되었습니다.");
                yield break; // 알림창이 겹치지 않도록 여기서 중단
            }

            // ── 2. 캠페인 클리어 -> 도감 해금 알림 ──
            if (unlockedCount > 0 && !_shownCollectionPopupThisSession)
            {
                _shownCollectionPopupThisSession = true;
                string message = unlockedCount == 1
                    ? "캠페인 클리어!\n<color=#FFD700>도감</color>에 시점 완결문이 해금되었습니다."
                    : $"새로운 시점 완결문 해금!\n현재 총 <color=#FFD700>{unlockedCount}</color>개의 도감이 열렸습니다.";

                ShowNotification(message);
            }
        }

        private int GetUnlockedEpilogueCount()
        {
            if (_rewardSaveData == null) return 0;

            int count = 0;
            for (int i = 1; i <= 7; i++)
            {
                // JSON에 기록된 시점 완결문(Epilogue) 해금 개수 산출
                if (_rewardSaveData.IsEpilogueUnlocked(i)) count++;
            }
            return count;
        }

        private void ShowNotification(string message)
        {
            if (_messageText != null)
                _messageText.text = message;

            if (_notificationPanel != null)
                _notificationPanel.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration);
            }

            if (_autoCloseDelay > 0f)
            {
                if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
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
            {
                _notificationPanel?.SetActive(false);
            }
        }
    }
}