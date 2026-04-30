using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비 진입 시 도감(시점 완결문)이 새로 해금되었을 때 띄워주는 알림 팝업입니다.
    /// 앱 실행 후(세션 당) 1회만 표시됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyUnlockNotification : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private GameObject _notificationPanel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("데이터 및 설정")]
        [SerializeField] private RewardSaveData _rewardSaveData;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _autoCloseDelay = 3.0f; // 0보다 크면 자동 닫기 작동

        // 내부 상태
        private static bool _shownThisSession = false; // 세션 당 1회 표시 보장
        private Coroutine _autoCloseCoroutine;

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
        }

        private void Start()
        {
            CheckAndShowUnlockNotification();
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(HideNotification);

            // 오브젝트 파괴 시 진행 중인 트윈(애니메이션) 안전하게 강제 종료
            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
            }
        }

        /// <summary>
        /// 테스트 등을 위해 세션 플래그를 초기화합니다.
        /// </summary>
        public static void ResetSession() => _shownThisSession = false;

        /// <summary>
        /// 해금 조건을 검사하고 알림을 띄울지 결정합니다.
        /// </summary>
        private void CheckAndShowUnlockNotification()
        {
            // 이미 이번 세션에 보여줬다면 무시[cite: 6]
            if (_shownThisSession) return;

            int unlockedCount = GetUnlockedEpilogueCount();
            if (unlockedCount <= 0) return;

            // 표시 확정
            _shownThisSession = true;

            string message = unlockedCount == 1
                ? "도감이 해금되었습니다.\n캐릭터의 시점 완결문을 확인해보세요."
                : $"도감에 {unlockedCount}개의 시점 완결문이 해금되었습니다.\n확인해보세요.";

            ShowNotification(message);
        }

        private int GetUnlockedEpilogueCount()
        {
            if (_rewardSaveData == null) return 0;

            int count = 0;
            for (int i = 1; i <= 7; i++)
            {
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
                // 이전 트윈이 있다면 취소하고 페이드 인 시작
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration);
            }

            // 자동 닫기 코루틴 시작
            if (_autoCloseDelay > 0f)
            {
                if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
            }
        }

        private void HideNotification()
        {
            // 닫기 버튼을 직접 누른 경우 자동 닫기 코루틴 중단
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

        private IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(_autoCloseDelay);
            HideNotification();
        }
    }
}