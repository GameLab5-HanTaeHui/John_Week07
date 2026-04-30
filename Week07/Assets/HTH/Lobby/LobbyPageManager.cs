using System;
using UnityEngine;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비의 책 페이지(챕터) UI 패널들의 활성/비활성 상태를 관리합니다.
    /// TitleBookAnimator의 애니메이션 이벤트와 연동되어 작동합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPageManager : MonoBehaviour
    {
        [Header("페이지 UI 목록")]
        [Tooltip("인덱스 0: 첫 페이지(시작/이어하기), 인덱스 1: 2페이지(도감) 등 순서대로 배치")]
        [SerializeField] private GameObject[] _pages;

        private int _currentPageIndex = -1;

        /// <summary>현재 표시 중인 페이지 인덱스</summary>
        public int CurrentPageIndex => _currentPageIndex;

        /// <summary>페이지가 전환될 때 발생하는 이벤트 (인덱스 전달)</summary>
        public event Action<int> OnPageChanged;

        private void Awake()
        {
            HideAll();
        }

        // ── 책 애니메이터(TitleBookAnimator)에서 호출할 API ──

        /// <summary>애니메이션 시작 시 호출. 전환 중 어색함을 막기 위해 모든 페이지를 숨깁니다.</summary>
        public void OnAnimationStart()
        {
            HideAll();
        }

        /// <summary>최초 열림 완료 시 호출. 첫 페이지(0)를 표시합니다.</summary>
        public void ShowInitialPage()
        {
            ShowPage(0);
        }

        /// <summary>페이지 넘김 완료 시 호출. 다음 페이지를 표시합니다.</summary>
        public void ShowNextPage()
        {
            ShowPage(_currentPageIndex + 1);
        }

        /// <summary>이전 페이지 완료 시 호출. 이전 페이지를 표시합니다.</summary>
        public void ShowPreviousPage()
        {
            ShowPage(_currentPageIndex - 1);
        }

        // ── 내부 로직 ──

        public void ShowPage(int index)
        {
            if (_pages == null || _pages.Length == 0) return;

            _currentPageIndex = Mathf.Clamp(index, 0, _pages.Length - 1);

            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] == null) continue;

                bool isTargetPage = (i == _currentPageIndex);
                _pages[i].SetActive(isTargetPage);
            }

            // 페이지 전환 완료 이벤트 브로드캐스팅
            OnPageChanged?.Invoke(_currentPageIndex);
        }

        public void HideAll()
        {
            foreach (var page in _pages)
            {
                if (page != null) page.SetActive(false);
            }
        }
    }
}