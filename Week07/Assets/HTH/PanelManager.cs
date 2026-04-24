using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HTH
{
    /// <summary>
    /// 씬 내 열린 패널을 등록/해제하고 바탕화면 클릭 시 닫아주는 싱글톤입니다.
    ///
    /// ─── 동작 흐름 ────────────────────────────────────────────────────────────
    ///   DrawerPanel.Show()      → PanelManager.RegisterPanel(closeAction)
    ///   DrawerPanel.Hide()      → PanelManager.UnregisterPanel(closeAction)
    ///   HistoryPagePanel 열림   → PanelManager.RegisterPanel(closeAction)
    ///   HistoryPagePanel 닫힘   → PanelManager.UnregisterPanel(closeAction)
    ///
    ///   바탕화면 클릭 감지
    ///       → EventSystem 위 클릭이 아님 (UI 위 클릭 아님)
    ///       → FinalDecision 상태가 아님
    ///       → 등록된 모든 패널 닫기 콜백 실행
    ///
    /// ─── FinalDecision 예외 ───────────────────────────────────────────────────
    ///   FinalDecision 상태에서는 바탕화면 클릭으로 패널이 닫히지 않습니다.
    ///   버튼 클릭으로만 닫힙니다.
    ///
    /// ─── 씬 배치 ──────────────────────────────────────────────────────────────
    ///   스테이지 씬의 빈 GameObject에 이 컴포넌트를 부착합니다.
    ///   Inspector 연결 불필요 — DrawerPanel/HistoryPageController가 자동 등록합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelManager : MonoBehaviour
    {
        public static PanelManager Instance { get; private set; }

        // 열린 패널의 닫기 콜백 목록
        private readonly System.Collections.Generic.List<Action> _openPanelCloseActions = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        private void Update()
        {
            if (_openPanelCloseActions.Count == 0) return;
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            // UI 위 클릭이면 무시
            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            // FinalDecision 상태에서 차단
            if (GameFlowController.Instance?.CurrentLoopState == LoopStateType.FinalDecision)
                return;

            // 튜토리얼 씬 자체이거나 튜토리얼 진행 중이면 차단
            if (TutorialManager.IsActive) return;

            CloseAllPanels();
        }

        // ── 공개 API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 열린 패널의 닫기 콜백을 등록합니다.
        /// DrawerPanel.Show() / HistoryPageController.ExpandPanel() 에서 호출합니다.
        /// </summary>
        public void RegisterPanel(Action closeAction)
        {
            if (closeAction == null) return;
            if (!_openPanelCloseActions.Contains(closeAction))
                _openPanelCloseActions.Add(closeAction);
        }

        /// <summary>
        /// 닫힌 패널의 콜백을 해제합니다.
        /// DrawerPanel.Hide() / HistoryPageController.CollapseExpanded() 에서 호출합니다.
        /// </summary>
        public void UnregisterPanel(Action closeAction)
        {
            _openPanelCloseActions.Remove(closeAction);
        }

        /// <summary>
        /// 등록된 모든 패널을 닫습니다.
        /// </summary>
        public void CloseAllPanels()
        {
            // 복사본으로 순회 (닫기 콜백 안에서 UnregisterPanel이 호출되므로)
            var copy = new System.Collections.Generic.List<Action>(_openPanelCloseActions);
            _openPanelCloseActions.Clear();
            foreach (var close in copy)
                close?.Invoke();
        }
    }
}