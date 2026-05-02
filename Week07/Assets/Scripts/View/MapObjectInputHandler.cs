using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// 맵 오브젝트 클릭을 레이캐스트로 감지해 ClickScaleBounce에 위임합니다.
///
/// Inspector 필수 연결:
///   MainCamera       → 레이캐스트용 카메라
///   MapObjectMask    → ClickScaleBounce가 붙은 오브젝트의 레이어 마스크
/// </summary>
public class MapObjectInputHandler : MonoBehaviour
{
    [SerializeField] private Camera    _mainCamera;
    [SerializeField] private LayerMask _mapObjectMask;

    private MapObjectHoverActivator  _hoveredActivator;
    private HoldToAdvanceTurn        _heldTurnButton;
    private HoldToEnterFinalDecision _heldFinalButton;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Mouse.current == null) return;

        var loopState = GameFlowController.Instance?.CurrentLoopState;
        if (loopState == LoopStateType.FinalDecision) return;

        // AwaitingFinalDecision 상태에서는 HoldToEnterFinalDecision 입력만 허용
        if (loopState == LoopStateType.AwaitingFinalDecision)
        {
            HandleFinalDecisionOnlyInput();
            return;
        }

        // [튜토리얼 방어선] 튜토리얼 중일 때는 전용 방어 로직을 따르고, 
        // 일반 게임 중일 때만 기존의 LoopStateType 방어 로직을 따릅니다.
        if (TutorialManager.IsActive)
        {
            if (UpdateTutorialDefense()) return;
        }
        else
        {
            if (loopState == LoopStateType.FinalDecision) return;

            if (loopState == LoopStateType.AwaitingFinalDecision)
            {
                HandleFinalDecisionOnlyInput();
                return;
            }
        }

        UpdateHover();
        HandleInput();
    }

    /// <summary>
    /// 튜토리얼 전용 입력 방어 로직입니다.
    /// 월드 상호작용 권한이 하나라도 있는지 체크합니다.
    /// </summary>
    private bool UpdateTutorialDefense()
    {
        // 월드 조작과 관련된 권한이 하나라도 있는지 확인합니다.
        bool isWorldInputAllowed =
            TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.CharacterMove) ||
            TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.AdvanceTurn) ||
            TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.FinalDecisionEnter) ||
            TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.FinalCharacterSelect);

        // 권한이 아예 없는 상태(대화 중 등)라면 모든 월드 상호작용을 차단합니다.
        if (!isWorldInputAllowed)
        {
            if (_hoveredActivator != null)
            {
                _hoveredActivator.OnHoverExit();
                _hoveredActivator = null;
            }
            return true; // 입력을 차단함
        }

        return false; // 입력을 허용함
    }

    private void HandleFinalDecisionOnlyInput()
    {
        var mouse = Mouse.current;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            RaycastHit(out var hit);
            if (hit.collider == null) return;

            var finalBtn = hit.collider.TryGetComponent(out HoldToEnterFinalDecision fd) ? fd
                         : hit.collider.GetComponentInParent<HoldToEnterFinalDecision>();
            if (finalBtn != null)
            {
                _heldFinalButton = finalBtn;
                _heldFinalButton.BeginHold();
            }
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
            var bounce = RaycastHit(out var hit);

            if (hit.collider != null)
            {
                // HoldToEnterFinalDecision 우선 감지
                var finalBtn = hit.collider.TryGetComponent(out HoldToEnterFinalDecision fd) ? fd
                             : hit.collider.GetComponentInParent<HoldToEnterFinalDecision>();
                if (finalBtn != null)
                {
                    _heldFinalButton = finalBtn;
                    _heldFinalButton.BeginHold();
                    return;
                }

                // HoldToAdvanceTurn 감지
                var turnBtn = hit.collider.TryGetComponent(out HoldToAdvanceTurn h) ? h
                            : hit.collider.GetComponentInParent<HoldToAdvanceTurn>();
                if (turnBtn != null)
                {
                    _heldTurnButton = turnBtn;
                    _heldTurnButton.BeginHold();
                    return;
                }
            }

            bounce?.PlayBounce();
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

    // ── 호버 ─────────────────────────────────────────────────────────────────

    private void UpdateHover()
    {
        RaycastHit(out var hit);

        MapObjectHoverActivator next = null;
        if (hit.collider != null && !hit.collider.TryGetComponent<RaycastBlocker>(out _))
        {
            hit.collider.TryGetComponent(out next);
            if (next == null)
                next = hit.collider.GetComponentInParent<MapObjectHoverActivator>();
        }

        if (next == _hoveredActivator) return;

        _hoveredActivator?.OnHoverExit();
        _hoveredActivator = next;
        _hoveredActivator?.OnHoverEnter();
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private ClickScaleBounce RaycastHit(out RaycastHit hit)
    {
        hit = default;
        if (_mainCamera == null) return null;

        var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        bool hasHit = _mapObjectMask.value != 0
            ? Physics.Raycast(ray, out hit, Mathf.Infinity, _mapObjectMask)
            : Physics.Raycast(ray, out hit, Mathf.Infinity);

        if (!hasHit) return null;
        if (hit.collider.TryGetComponent<RaycastBlocker>(out _)) return null;

        if (hit.collider.TryGetComponent(out ClickScaleBounce bounce)) return bounce;
        return hit.collider.GetComponentInParent<ClickScaleBounce>();
    }
}
