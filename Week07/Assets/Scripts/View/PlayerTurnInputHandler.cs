using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 클릭/드래그로 캐릭터를 구역에 배치합니다.
///
/// 클릭  : 캐릭터 선택 → 같은 캐릭터 재클릭 → 대기 확정
/// 드래그 : 캐릭터를 잡고 Zone 위에 놓으면 이동 확정 (GameState + 시각 동시 반영)
///          Zone 밖에서 놓으면 원래 위치로 복귀 (취소)
///
/// Inspector 필수 연결:
///   MainCamera         → 레이캐스트용 카메라
///   ZoneLayerMask      → ZonePoint 레이어 마스크
///   CharacterLayerMask → 캐릭터 레이어 마스크 (미설정 시 전체 대상)
///   ZoneLayout         → 구역 위치 조회
/// </summary>
public class PlayerTurnInputHandler : MonoBehaviour
{
    [SerializeField] private Camera     _mainCamera;
    [SerializeField] private LayerMask  _zoneLayerMask;
    [SerializeField] private LayerMask  _characterLayerMask;
    [SerializeField] private ZoneLayout _zoneLayout;

    // 드래그 판정 픽셀 임계값
    private const float DragThreshold = 8f;

    private PlayerActionState              _playerAction;
    private Dictionary<int, CharacterView> _characterViews;

    // 시각적 구역 추적 (GameState 커밋 전 pending 상태 포함)
    private Dictionary<int, int> _assignedZones = new();

    // 드래그 상태
    private bool                    _isPressing;
    private Vector2                 _pressStartScreenPos;
    private bool                    _isDragging;
    private int                     _draggingId = -1;
    private CharacterView           _draggingView;
    private CharacterPickupAnimator _draggingAnimator;
    private Vector3                 _dragOriginalPos;
    private Quaternion              _dragOriginalRot;
    private Plane                   _groundPlane;
    private ZonePoint               _hoveredZone;
    private Vector3                 _dropWorldPos;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null)
        {
            Debug.LogError("[PlayerTurnInputHandler] GameFlowController를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _playerAction   = gfc.GetPlayerActionState();
        _characterViews = new Dictionary<int, CharacterView>(gfc.CharacterViews);

        // 초기 구역 스냅샷 (GameState 기준)
        var gs = gfc.GameState;
        foreach (var charId in _characterViews.Keys)
            _assignedZones[charId] = gs?.GetZone(charId) ?? 0;

        if (_playerAction != null)
        {
            _playerAction.OnCharacterSelected += HandleCharacterSelected;
            _playerAction.OnActionConfirmed   += HandleActionConfirmed;
        }

        // 파도 구역 등 턴 시작 시 구역이 변경될 수 있으므로 OnPlayerActionStarted 때 _assignedZones 재동기화
        var turnSM = gfc.GetTurnSM();
        if (turnSM != null)
            turnSM.OnPlayerActionStarted += SyncAssignedZonesFromGameState;

        gfc.OnLoopReset += HandleLoopReset;

        Debug.Log($"[PlayerTurnInputHandler] 초기화 완료 — 캐릭터 {_characterViews.Count}개");
    }

    private void OnDestroy()
    {
        if (_playerAction != null)
        {
            _playerAction.OnCharacterSelected -= HandleCharacterSelected;
            _playerAction.OnActionConfirmed   -= HandleActionConfirmed;
        }

        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnLoopReset -= HandleLoopReset;
            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                turnSM.OnPlayerActionStarted -= SyncAssignedZonesFromGameState;
        }
    }

    private void Update()
    {
        if (Mouse.current == null) return;
        var loopState = GameFlowController.Instance?.CurrentLoopState;
        if (loopState == LoopStateType.FinalDecision ||
            loopState == LoopStateType.AwaitingFinalDecision) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)  OnPress();
        if (Mouse.current.leftButton.isPressed)            OnHold();
        if (Mouse.current.leftButton.wasReleasedThisFrame) OnRelease();
    }

    // ── 입력 단계 ────────────────────────────────────────────────────────────

    private void OnPress()
    {
        _pressStartScreenPos = Mouse.current.position.ReadValue();
        _isPressing          = false;
        _isDragging          = false;

        var view = RaycastCharacter();
        if (view == null) return;

        _isPressing      = true;
        _draggingId      = view.CharacterId;
        _draggingView    = view;
        _dragOriginalPos = view.transform.position;
        _dragOriginalRot = view.transform.rotation;
        _groundPlane     = new Plane(Vector3.up, view.transform.position);

        // 누르는 순간 보드 말 집기 애니메이션 시작
        _draggingAnimator = view.GetComponent<CharacterPickupAnimator>();

        if (TutorialManager.IsActive && !TutorialManager.Instance.IsCharacterDragAllowed(_draggingId))
        {
            _isPressing = false; _draggingId = -1; _draggingView = null; _draggingAnimator = null;
            return;
        }

        if (_draggingAnimator != null)
            _draggingAnimator.PickUp(view.transform.rotation);

        Debug.Log($"[PlayerTurnInputHandler] 프레스 — {(view.Data != null ? view.Data.CharacterName : "?")} (ID:{_draggingId})");
    }

    private void OnHold()
    {
        if (!_isPressing) return;

        Vector2 currentPos = Mouse.current.position.ReadValue();
        float   moved      = Vector2.Distance(currentPos, _pressStartScreenPos);

        // 드래그 시작 판정
        if (!_isDragging && moved > DragThreshold)
        {
            _isDragging = true;
            Debug.Log($"[PlayerTurnInputHandler] 드래그 시작 — ID:{_draggingId}");
            var gfc = GameFlowController.Instance;
            if (gfc == null)
            {
                Debug.LogError("[PlayerTurnInputHandler] OnHold: gfc가 null — BeginDragSelect 호출 불가");
            }
            else
            {
                Debug.Log($"[PlayerTurnInputHandler] BeginDragSelect({_draggingId}) 호출");
                gfc.BeginDragSelect(_draggingId);
            }
        }

        // 드래그 중: 들린 높이를 유지하며 XZ를 마우스 위치로 이동
        if (_isDragging && _draggingView != null)
        {
            var ray = _mainCamera.ScreenPointToRay(currentPos);
            if (_groundPlane.Raycast(ray, out float dist))
            {
                var groundPos = ray.GetPoint(dist);

                // 들린 높이 + 피벗 오프셋 적용
                var   offset  = _draggingAnimator != null ? _draggingAnimator.PickupOffset : Vector3.zero;
                float liftY   = _dragOriginalPos.y
                                + (_draggingAnimator != null ? _draggingAnimator.LiftHeight : 0f)
                                + offset.y;
                var holdPos = new Vector3(groundPos.x + offset.x, liftY, groundPos.z + offset.z);

                // 속도 계산 → 기울기 업데이트
                if (_draggingAnimator != null)
                {
                    var velocity = new Vector3(
                        holdPos.x - _draggingView.transform.position.x,
                        0f,
                        holdPos.z - _draggingView.transform.position.z) / Time.deltaTime;
                    _draggingAnimator.SetDragVelocity(velocity);
                }

                // XZ·Y 세팅; 리프트 tween 중엔 DOTween LateUpdate가 Y를 덮어씀
                _draggingView.transform.position = holdPos;
            }

            // 드래그 중 Zone 위에 있으면 해당 존의 인디케이터만 표시
            ZonePoint newHovered = null;
            if (Physics.Raycast(_mainCamera.ScreenPointToRay(currentPos),
                out var zoneHit, Mathf.Infinity, _zoneLayerMask))
                zoneHit.collider.TryGetComponent(out newHovered);

            if (newHovered != _hoveredZone)
            {
                _hoveredZone?.SetDropIndicator(false);
                _hoveredZone = newHovered;
                _hoveredZone?.SetDropIndicator(true);
            }
        }
    }

    private void OnRelease()
    {
        if (!_isPressing) return;

        _hoveredZone?.SetDropIndicator(false);
        _hoveredZone = null;

        if (_isDragging)
        {
            // 드래그 종료 — Zone 위에 놓였는지 확인
            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _zoneLayerMask)
                && hit.collider.TryGetComponent(out ZonePoint zonePoint))
            {
                if (TutorialManager.IsActive && !TutorialManager.Instance.IsZoneDropAllowed(_draggingId, zonePoint.ZoneId))
                {
                    // 튜토리얼 드롭 차단 → 원위치 복귀
                    if (_draggingAnimator != null) _draggingAnimator.LandAt(_dragOriginalPos, _dragOriginalRot);
                    else if (_draggingView != null) _draggingView.SnapToPosition(_dragOriginalPos);
                    GameFlowController.Instance?.BeginDragSelect(-1);
                }
                else
                {
                    _dropWorldPos = hit.point;
                    Debug.Log($"[PlayerTurnInputHandler] 드롭 → Zone {zonePoint.ZoneId}");
                    var gfc = GameFlowController.Instance;
                    if (gfc != null) gfc.NotifyZoneClicked(zonePoint.ZoneId);
                    // HandleActionConfirmed에서 SnapToPosition 처리
                }
            }
            else
            {
                // Zone 밖 → 원위치 착지 복귀
                Debug.Log("[PlayerTurnInputHandler] 드롭 취소 — 원위치 복귀");
                if (_draggingView != null)
                {
                    if (_draggingAnimator != null)
                        _draggingAnimator.LandAt(_dragOriginalPos, _dragOriginalRot);
                    else
                        _draggingView.SnapToPosition(_dragOriginalPos);
                }

                // BeginDragSelect(-1) 로 선택 해제 (NotifyCharacterClicked 사용 시 재클릭=대기가 발동되는 버그 방지)
                var gfc = GameFlowController.Instance;
                if (gfc != null) gfc.BeginDragSelect(-1);
            }
        }
        else
        {
            // 클릭 — 선택/대기 로직 + 들렸던 말 원위치 착지
            var view = RaycastCharacter();
            if (view != null)
            {
                Debug.Log($"[PlayerTurnInputHandler] 클릭 — ID:{view.CharacterId}");
                var gfc = GameFlowController.Instance;
                if (gfc != null) gfc.NotifyCharacterClicked(view.CharacterId);
            }

            if (_draggingAnimator != null)
                _draggingAnimator.LandAt(_dragOriginalPos, _dragOriginalRot);
        }

        _isPressing       = false;
        _isDragging       = false;
        _draggingId       = -1;
        _draggingView     = null;
        _draggingAnimator = null;
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────────

    private void HandleLoopReset()
    {
        var gfc = GameFlowController.Instance;
        var gs  = gfc != null ? gfc.GameState : null;
        foreach (var charId in _characterViews.Keys)
            _assignedZones[charId] = gs != null ? gs.GetZone(charId) : 0;

        if (_zoneLayout != null)
            _zoneLayout.InitSlots(_assignedZones);
    }

    /// <summary>
    /// 턴 시작 시 파도 구역 등 외부 효과로 변경된 CurrentZone을 _assignedZones, ZoneLayout._slotMap,
    /// 그리고 CharacterView 트랜스폼까지 반영합니다.
    /// </summary>
    private void SyncAssignedZonesFromGameState()
    {
        var gs = GameFlowController.Instance?.GameState;
        if (gs == null) return;

        foreach (var charId in _characterViews.Keys)
            _assignedZones[charId] = gs.GetZone(charId);

        if (_zoneLayout == null) return;

        _zoneLayout.InitSlots(_assignedZones);

        var positions = _zoneLayout.ComputeSlotPositions(_assignedZones);
        var rotations = _zoneLayout.ComputeSlotRotations(_assignedZones);
        foreach (var kv in _characterViews)
        {
            if (!positions.TryGetValue(kv.Key, out var pos)) continue;
            var rot  = rotations.TryGetValue(kv.Key, out var r) ? r : kv.Value.transform.rotation;
            var anim = kv.Value.GetComponent<CharacterPickupAnimator>();
            if (anim != null) anim.ReplaceTo(pos, rot);
            else              kv.Value.SnapToPosition(pos);
        }
    }

    private void HandleCharacterSelected(int characterId)
    {
        foreach (var kv in _characterViews)
            kv.Value.SetSelected(kv.Key == characterId);

        if (characterId >= 0 && _characterViews.TryGetValue(characterId, out var view))
            Debug.Log($"[선택] {(view.Data != null ? view.Data.CharacterName : "?")} (ID:{characterId})");
    }

    private void HandleActionConfirmed(int characterId, int targetZoneId)
    {
        if (!_characterViews.TryGetValue(characterId, out var view)) return;

        if (targetZoneId >= 0)
        {
            // 이동 확정 → _assignedZones 갱신 후 영향받는 두 존 재계산
            int prevZone = _assignedZones.TryGetValue(characterId, out var z) ? z : targetZoneId;
            _assignedZones[characterId] = targetZoneId;

            if (_zoneLayout != null)
            {
                _zoneLayout.MoveToZone(characterId, prevZone, targetZoneId, _dropWorldPos);
                ResyncZones(prevZone, targetZoneId);
            }
        }

        view.RefreshView();
    }

    /// <summary>지정된 존들에 속한 캐릭터 전체의 슬롯 위치·회전을 재계산하고 애니메이션으로 이동합니다.</summary>
    private void ResyncZones(params int[] zoneIds)
    {
        if (_zoneLayout == null) return;

        var affected = new HashSet<int>(zoneIds);

        var subset = new Dictionary<int, int>();
        foreach (var kv in _assignedZones)
        {
            if (affected.Contains(kv.Value))
                subset[kv.Key] = kv.Value;
        }

        var positions = _zoneLayout.ComputeSlotPositions(subset);
        var rotations = _zoneLayout.ComputeSlotRotations(subset);

        foreach (var kv in positions)
        {
            if (!_characterViews.TryGetValue(kv.Key, out var v)) continue;
            var rot  = rotations.TryGetValue(kv.Key, out var r) ? r : Quaternion.identity;
            var anim = v.GetComponent<CharacterPickupAnimator>();

            if (kv.Key == _draggingId && anim != null)
                anim.LandAt(kv.Value, rot);          // 방금 드래그한 말 → 착지 애니메이션
            else if (anim != null)
                anim.ReplaceTo(kv.Value, rot);        // 밀려난 말 → 부드러운 재배치
            else
            {
                v.SnapToPosition(kv.Value);
                v.SnapToRotation(rot);
            }
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private CharacterView RaycastCharacter()
    {
        if (_mainCamera == null) return null;

        var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;
        bool hasHit = _characterLayerMask.value != 0
            ? Physics.Raycast(ray, out hit, Mathf.Infinity, _characterLayerMask)
            : Physics.Raycast(ray, out hit, Mathf.Infinity);

        if (!hasHit) return null;

        hit.collider.TryGetComponent(out CharacterView view);
        if (view == null)
            view = hit.collider.GetComponentInParent<CharacterView>();

        if (view == null) return null;

        // 사망 캐릭터는 클릭/드래그 대상에서 제외
        var gs = GameFlowController.Instance != null ? GameFlowController.Instance.GameState : null;
        if (gs != null)
        {
            var status = gs.GetCharacter(view.CharacterId);
            if (status != null && !status.IsAlive)
            {
                Debug.Log($"[PlayerTurnInputHandler] 사망 캐릭터 클릭 무시 — ID:{view.CharacterId}");
                return null;
            }
        }

        return view;
    }
}
