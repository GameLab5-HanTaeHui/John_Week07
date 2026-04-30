using UnityEngine;
using UnityEngine.InputSystem;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 맵 오브젝트 입력 핸들러입니다.
    /// 기본모드는 MapObjectInputHandler를 사용하세요.
    ///
    /// ─── 클릭 차단 구조 ──────────────────────────────────────────────────────
    ///   Raycast 히트 후 처리 우선순위:
    ///     1. RaycastBlocker 컴포넌트 → 즉시 차단, 이후 처리 없음
    ///     2. CampaignHoldToEnterFinalDecision → BeginHold() 호출
    ///     3. CampaignHoldToAdvanceTurn        → BeginHold() 호출
    ///     4. ClickScaleBounce                 → PlayBounce() 호출
    ///     5. 그 외 오브젝트 → 아무것도 안 함 (클릭 흡수, 뒤로 전파 없음)
    ///
    ///   FillObject가 Raycast를 가로채는 경우:
    ///     방법 A: FillObject에 RaycastBlocker 컴포넌트 추가
    ///     방법 B: FillObject의 Collider 제거
    ///     방법 C: FillObject 레이어를 _mapObjectMask 밖으로 변경
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────────
    ///   Main Camera      → 레이캐스트용 카메라
    ///   Map Object Mask  → 감지할 레이어 마스크
    ///   Debug Raycast    → true 시 클릭마다 히트 정보 로그 출력
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignMapObjectInputHandler : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _mapObjectMask;

        [Header("디버그")]
        [Tooltip("true 시 클릭할 때마다 Raycast 히트 정보를 로그로 출력합니다.")]
        [SerializeField] private bool _debugRaycast = true;

        private MapObjectHoverActivator _hoveredActivator;
        private CampaignHoldToAdvanceTurn _heldTurnButton;
        private CampaignHoldToEnterFinalDecision _heldFinalButton;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (Mouse.current == null) return;

            var loopState = CampaignGameFlowController.Instance?.CurrentLoopState;
            if (loopState == LoopStateType.FinalDecision) return;

            if (loopState == LoopStateType.AwaitingFinalDecision)
            {
                HandleFinalDecisionOnlyInput();
                return;
            }

            UpdateHover();
            HandleInput();
        }

        // ── 입력 처리 ────────────────────────────────────────────────────────

        private void HandleFinalDecisionOnlyInput()
        {
            var mouse = Mouse.current;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!TryGetValidHit(out var hit)) return;

                var finalBtn = hit.collider.TryGetComponent(out CampaignHoldToEnterFinalDecision fd) ? fd
                             : hit.collider.GetComponentInParent<CampaignHoldToEnterFinalDecision>();
                if (finalBtn == null) return;

                _heldFinalButton = finalBtn;
                _heldFinalButton.BeginHold();
            }

            if (mouse.leftButton.wasReleasedThisFrame && _heldFinalButton != null)
            {
                _heldFinalButton.EndHold();
                _heldFinalButton = null;
            }
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // 유효한 히트 없으면 (RaycastBlocker 포함) 전부 무시
                if (!TryGetValidHit(out var hit))
                {
                    if (_debugRaycast)
                        Debug.Log("[CampaignMapInput] 히트 없음 또는 RaycastBlocker — 클릭 무시");
                    return;
                }

                if (_debugRaycast)
                    Debug.Log($"[CampaignMapInput] 히트 — {hit.collider.gameObject.name} " +
                              $"[{LayerMask.LayerToName(hit.collider.gameObject.layer)}]");

                // 1순위: CampaignHoldToEnterFinalDecision
                var finalBtn = hit.collider.TryGetComponent(out CampaignHoldToEnterFinalDecision fd) ? fd
                             : hit.collider.GetComponentInParent<CampaignHoldToEnterFinalDecision>();
                if (finalBtn != null)
                {
                    _heldFinalButton = finalBtn;
                    _heldFinalButton.BeginHold();
                    if (_debugRaycast)
                        Debug.Log($"[CampaignMapInput] ★ CampaignHoldToEnterFinalDecision — {finalBtn.gameObject.name}");
                    return;
                }

                // 2순위: CampaignHoldToAdvanceTurn
                var turnBtn = hit.collider.TryGetComponent(out CampaignHoldToAdvanceTurn h) ? h
                            : hit.collider.GetComponentInParent<CampaignHoldToAdvanceTurn>();
                if (turnBtn != null)
                {
                    _heldTurnButton = turnBtn;
                    _heldTurnButton.BeginHold();
                    if (_debugRaycast)
                        Debug.Log($"[CampaignMapInput] ★ CampaignHoldToAdvanceTurn — {turnBtn.gameObject.name}");
                    return;
                }

                // 3순위: ClickScaleBounce
                if (hit.collider.TryGetComponent(out ClickScaleBounce bounce) ||
                    (bounce = hit.collider.GetComponentInParent<ClickScaleBounce>()) != null)
                {
                    bounce.PlayBounce();
                    if (_debugRaycast)
                        Debug.Log($"[CampaignMapInput] ClickScaleBounce — {bounce.gameObject.name}");
                    return;
                }

                // ★ 그 외 오브젝트 — 클릭 흡수, 뒤로 전파하지 않음
                if (_debugRaycast)
                    Debug.Log($"[CampaignMapInput] 감지됐지만 처리 없음 (클릭 흡수) — {hit.collider.gameObject.name} " 
                        + $"FillObject라면: RaycastBlocker 추가 또는 Collider 제거 권장");
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (_heldFinalButton != null)
                {
                    _heldFinalButton.EndHold();
                    _heldFinalButton = null;
                }
                if (_heldTurnButton != null)
                {
                    _heldTurnButton.EndHold();
                    _heldTurnButton = null;
                }
            }
        }

        // ── 호버 ─────────────────────────────────────────────────────────────

        private void UpdateHover()
        {
            if (!TryGetValidHit(out var hit))
            {
                if (_hoveredActivator != null)
                {
                    _hoveredActivator.OnHoverExit();
                    _hoveredActivator = null;
                }
                return;
            }

            MapObjectHoverActivator next = null;
            hit.collider.TryGetComponent(out next);
            if (next == null)
                next = hit.collider.GetComponentInParent<MapObjectHoverActivator>();

            if (next == _hoveredActivator) return;

            _hoveredActivator?.OnHoverExit();
            _hoveredActivator = next;
            _hoveredActivator?.OnHoverEnter();
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Raycast 후 유효한 히트를 반환합니다.
        ///
        /// 차단 조건:
        ///   1. Canvas UI 위 클릭 → EventSystem.IsPointerOverGameObject()로 감지 → 3D Raycast 스킵
        ///      FillObject(Image 등 Canvas UI)가 클릭을 가로채는 문제를 해결합니다.
        ///   2. 마스크에 히트 없음 → false 반환
        ///   3. RaycastBlocker 컴포넌트 감지 → false 반환
        /// </summary>
        private bool TryGetValidHit(out RaycastHit hit)
        {
            hit = default;
            if (_mainCamera == null) return false;

            // ★ Canvas UI(FillObject 등) 위 클릭이면 3D Raycast 스킵
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return false;

            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            bool hasHit = _mapObjectMask.value != 0
                ? Physics.Raycast(ray, out hit, Mathf.Infinity, _mapObjectMask)
                : Physics.Raycast(ray, out hit, Mathf.Infinity);

            if (!hasHit) return false;

            // RaycastBlocker → 즉시 차단
            if (hit.collider.TryGetComponent<RaycastBlocker>(out _)) return false;

            return true;
        }
    }
}