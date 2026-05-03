using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 패널 매니저입니다.
    /// 기본모드는 PanelManager(HTH namespace)를 사용하세요.
    ///
    /// ─── 기본모드 PanelManager와의 차이 ──────────────────────────────────
    ///   참조 대상: CampaignGameFlowController (기본: GameFlowController)
    ///   TutorialManager 체크 제거 (캠페인 씬에는 튜토리얼 없음)
    ///   FinalDecision 차단 → WinState / FinalDecision 둘 다 차단
    ///   CharacterRecordPanel 패널 클릭 차단 지원
    ///     캐릭터 기록장이 열려있을 때 바탕화면 클릭으로 닫힘
    ///
    /// ─── 동작 흐름 ────────────────────────────────────────────────────────
    ///   패널 열림 → RegisterPanel(closeAction)
    ///   패널 닫힘 → UnregisterPanel(closeAction)
    ///
    ///   바탕화면 클릭 감지
    ///     → EventSystem 위 클릭 아님
    ///     → FinalDecision / WinState 아님
    ///     → 등록된 모든 패널 닫기 콜백 실행
    ///
    /// ─── 씬 배치 ──────────────────────────────────────────────────────────
    ///   캠페인 씬의 _CampaignSystem 하위 빈 GameObject에 부착합니다.
    ///   Inspector 연결 불필요 — 각 패널이 자동 등록합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignPanelManager : MonoBehaviour
    {
        public static CampaignPanelManager Instance { get; private set; }

        private readonly List<Action> _openPanelCloseActions = new();

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

            // ★ FinalDecision / WinState 상태에서 차단
            var loopState = CampaignGameFlowController.Instance?.CurrentLoopState;
            if (loopState == LoopStateType.FinalDecision ||
                loopState == LoopStateType.WinState) return;

            CloseAllPanels();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 열린 패널의 닫기 콜백을 등록합니다.
        /// CharacterRecordPanelManager.OpenPanel() 등에서 호출합니다.
        /// </summary>
        public void RegisterPanel(Action closeAction)
        {
            if (closeAction == null) return;
            if (!_openPanelCloseActions.Contains(closeAction))
                _openPanelCloseActions.Add(closeAction);
        }

        /// <summary>
        /// 닫힌 패널의 콜백을 해제합니다.
        /// CharacterRecordPanelManager.CloseCurrentPanel() 등에서 호출합니다.
        /// </summary>
        public void UnregisterPanel(Action closeAction)
        {
            _openPanelCloseActions.Remove(closeAction);
        }

        /// <summary>등록된 모든 패널을 닫습니다.</summary>
        public void CloseAllPanels()
        {
            var copy = new List<Action>(_openPanelCloseActions);
            _openPanelCloseActions.Clear();
            foreach (var close in copy)
                close?.Invoke();
        }
    }
}