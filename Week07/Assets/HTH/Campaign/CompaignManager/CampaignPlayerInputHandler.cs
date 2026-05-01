using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 플레이어 입력 핸들러입니다.
    /// 기본모드 PlayerTurnInputHandler와 씬을 완전히 분리합니다.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────────
    ///   참조 대상: CampaignGameFlowController (기본: GameFlowController)
    ///   DialogueTriggerManager.IsWaitingForDialogue 차단 로직 포함
    ///
    /// ─── 캐릭터 위치 재배치 규칙 ─────────────────────────────────────────────
    ///   위치 재배치(ReplaceTo/SnapToPosition)는 아래 두 경우에만 수행합니다.
    ///   1. HandleActionConfirmed — 플레이어가 직접 드래그해서 Zone에 드롭
    ///   2. HandleLoopReset      — 퇴고/강제퇴고 시 슬롯 맵 재초기화
    ///   SyncAssignedZonesFromGameState는 _assignedZones 값만 동기화하며
    ///   위치 이동 또는 슬롯 맵 초기화를 수행하지 않습니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────────
    ///   MainCamera         → 레이캐스트용 카메라
    ///   ZoneLayerMask      → ZonePoint 레이어 마스크
    ///   CharacterLayerMask → 캐릭터 레이어 마스크
    ///   ZoneLayout         → 구역 위치 조회
    /// </summary>
    public class CampaignPlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _zoneLayerMask;
        [SerializeField] private LayerMask _characterLayerMask;
        [SerializeField] private CampaignZoneLayout _zoneLayout;

        [Header("구역 연출")]
        [Tooltip("Zone 0~3의 SpriteRenderer 배열입니다.\n" +
                 "인덱스 = ZoneId (Element 0 = Zone0, Element 3 = Zone3)\n" +
                 "앵커 캐릭터가 드롭된 Zone이 활성색으로 바뀝니다.")]
        [SerializeField] private SpriteRenderer[] _zoneRenderers = new SpriteRenderer[4];

        [Tooltip("앵커 캐릭터가 있는 Zone의 활성 색상입니다.")]
        [SerializeField] private Color _activeZoneColor = Color.green;

        [Tooltip("비활성 Zone의 기본 색상입니다.")]
        [SerializeField] private Color _defaultZoneColor = Color.white;

        private const float DragThreshold = 8f;

        [Header("캐릭터 클릭 쿨타임")]
        [Tooltip("캐릭터 클릭 후 다음 클릭까지 대기 시간(초)")]
        [SerializeField] private float _clickCooldown = 0.7f;

        [Header("앵커 캐릭터")]
        [Tooltip("true이면 앵커 캐릭터 이동 시 Zone 색상을 갱신합니다.\n" +
                 "false이면 Zone 색상 변경 없음.")]
        [SerializeField] private bool _anchorCharacterActive = false;

        [Tooltip("Zone 색상 갱신 기준이 되는 앵커 캐릭터 ID입니다.\n" +
                 "DialogueTriggerManager의 _anchorCharacterId와 동일하게 설정하세요.\n" +
                 "기본값 1 = 엔비")]
        [SerializeField] private int _anchorCharacterId = 1;

        private readonly Dictionary<int, float> _lastClickTimePerCharacter = new();

        private PlayerActionState _playerAction;
        private Dictionary<int, CharacterView> _characterViews;
        private Dictionary<int, int> _assignedZones = new();

        // 드래그 상태
        private bool _isPressing;
        private Vector2 _pressStartScreenPos;
        private bool _isDragging;
        private int _draggingId = -1;
        private CharacterView _draggingView;
        private CharacterPickupAnimator _draggingAnimator;
        private Vector3 _dragOriginalPos;
        private Quaternion _dragOriginalRot;
        private Plane _groundPlane;
        private ZonePoint _hoveredZone;
        private Vector3 _dropWorldPos;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null)
            {
                Debug.LogError("[CampaignPlayerInputHandler] CampaignGameFlowController를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // ★ GFC의 AnchorCharacterId로 동기화 — Inspector 값과 다를 때만 갱신
            if (gfc.AnchorCharacterId != _anchorCharacterId)
            {
                _anchorCharacterId = gfc.AnchorCharacterId;
                Debug.Log($"[CampaignPlayerInputHandler] 앵커 캐릭터 GFC 동기화 — #{_anchorCharacterId}");
            }

            _playerAction = gfc.GetPlayerActionState();
            _characterViews = new Dictionary<int, CharacterView>(gfc.CharacterViews);

            var gs = gfc.GameState;
            foreach (var charId in _characterViews.Keys)
                _assignedZones[charId] = gs?.GetZone(charId) ?? 0;

            if (_playerAction != null)
            {
                _playerAction.OnCharacterSelected += HandleCharacterSelected;
                _playerAction.OnActionConfirmed += HandleActionConfirmed;
            }

            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                // ★ _assignedZones 값 동기화만 — 위치 이동 없음
                turnSM.OnPlayerActionStarted += SyncAssignedZonesFromGameState;

            gfc.OnLoopReset += HandleLoopReset;

            Debug.Log($"[CampaignPlayerInputHandler] 초기화 완료 — 캐릭터 {_characterViews.Count}개, 앵커 #{_anchorCharacterId}");
        }

        private void OnDestroy()
        {
            if (_playerAction != null)
            {
                _playerAction.OnCharacterSelected -= HandleCharacterSelected;
                _playerAction.OnActionConfirmed -= HandleActionConfirmed;
            }

            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null) return;

            gfc.OnLoopReset -= HandleLoopReset;
            var turnSM = gfc.GetTurnSM();
            if (turnSM != null)
                turnSM.OnPlayerActionStarted -= SyncAssignedZonesFromGameState;
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            var loopState = CampaignGameFlowController.Instance?.CurrentLoopState;
            if (loopState == LoopStateType.FinalDecision ||
                loopState == LoopStateType.AwaitingFinalDecision) return;

            // 캠페인 대사 대기 중 입력 차단
            if (DialogueTriggerManager.Instance != null &&
                DialogueTriggerManager.Instance.IsWaitingForDialogue) return;

            if (Mouse.current.leftButton.wasPressedThisFrame) OnPress();
            if (Mouse.current.leftButton.isPressed) OnHold();
            if (Mouse.current.leftButton.wasReleasedThisFrame) OnRelease();
        }

        // ── 입력 단계 ────────────────────────────────────────────────────────

        private void OnPress()
        {
            _pressStartScreenPos = Mouse.current.position.ReadValue();
            _isPressing = false;
            _isDragging = false;

            var view = RaycastCharacter();
            if (view == null) return;

            int charId = view.CharacterId;
            if (_lastClickTimePerCharacter.TryGetValue(charId, out float lastTime)
                && Time.time - lastTime < _clickCooldown)
            {
                Debug.Log($"[CampaignPlayerInputHandler] 클릭 쿨타임 중 — ID:{charId}");
                return;
            }
            _lastClickTimePerCharacter[charId] = Time.time;

            _isPressing = true;
            _draggingId = view.CharacterId;
            _draggingView = view;
            _dragOriginalPos = view.transform.position;

            var anim = view.GetComponent<CharacterPickupAnimator>();
            if (anim != null)
                _dragOriginalPos.y = anim.GroundY;

            _dragOriginalRot = view.transform.rotation;
            _groundPlane = new Plane(Vector3.up, view.transform.position);
            _draggingAnimator = anim;

            if (_draggingAnimator != null)
                _draggingAnimator.PickUp(view.transform.rotation);

            Debug.Log($"[CampaignPlayerInputHandler] 프레스 — {(view.Data != null ? view.Data.CharacterName : "?")} (ID:{_draggingId})");
        }

        private void OnHold()
        {
            if (!_isPressing) return;

            Vector2 currentPos = Mouse.current.position.ReadValue();
            float moved = Vector2.Distance(currentPos, _pressStartScreenPos);

            if (!_isDragging && moved > DragThreshold)
            {
                _isDragging = true;
                CampaignGameFlowController.Instance?.BeginDragSelect(_draggingId);
            }

            if (_isDragging && _draggingView != null)
            {
                var ray = _mainCamera.ScreenPointToRay(currentPos);
                if (_groundPlane.Raycast(ray, out float dist))
                {
                    var groundPos = ray.GetPoint(dist);
                    var offset = _draggingAnimator != null ? _draggingAnimator.PickupOffset : Vector3.zero;
                    float liftY = _dragOriginalPos.y
                                    + (_draggingAnimator != null ? _draggingAnimator.LiftHeight : 0f)
                                    + offset.y;
                    var holdPos = new Vector3(groundPos.x + offset.x, liftY, groundPos.z + offset.z);

                    var prevPos = _draggingView.transform.position;
                    _draggingView.transform.position = holdPos;

                    if (_draggingAnimator != null)
                    {
                        var worldVelocity = (holdPos - prevPos) / Time.deltaTime;
                        _draggingAnimator.SetDragVelocity(worldVelocity);
                    }
                }

                var zoneRay = _mainCamera.ScreenPointToRay(currentPos);
                ZonePoint newZone = null;
                if (Physics.Raycast(zoneRay, out var zoneHit, Mathf.Infinity, _zoneLayerMask))
                    zoneHit.collider.TryGetComponent(out newZone);

                if (newZone != _hoveredZone)
                {
                    _hoveredZone?.SetDropIndicator(false);
                    _hoveredZone = newZone;
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
                var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _zoneLayerMask)
                    && hit.collider.TryGetComponent(out ZonePoint zonePoint))
                {
                    _dropWorldPos = hit.point;
                    CampaignGameFlowController.Instance?.NotifyZoneClicked(zonePoint.ZoneId);
                }
                else
                {
                    if (_draggingView != null)
                    {
                        if (_draggingAnimator != null)
                            _draggingAnimator.LandAt(_dragOriginalPos, _dragOriginalRot);
                        else
                            _draggingView.SnapToPosition(_dragOriginalPos);
                    }
                    CampaignGameFlowController.Instance?.BeginDragSelect(-1);
                }
            }
            else
            {
                var view = RaycastCharacter();
                if (view != null)
                    CampaignGameFlowController.Instance?.NotifyCharacterClicked(view.CharacterId);

                if (_draggingAnimator != null)
                    _draggingAnimator.LandAt(_dragOriginalPos, _dragOriginalRot);
            }

            _isPressing = false;
            _isDragging = false;
            _draggingId = -1;
            _draggingView = null;
            _draggingAnimator = null;
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────────────

        /// <summary>
        /// 퇴고/강제퇴고 시 호출됩니다.
        /// 슬롯 맵을 GameState 기준으로 재초기화하고 캐릭터를 원래 위치로 되돌립니다.
        /// 위치 재배치가 허용되는 두 경우 중 하나입니다.
        /// </summary>
        private void HandleLoopReset()
        {
            var gs = CampaignGameFlowController.Instance?.GameState;
            foreach (var charId in _characterViews.Keys)
                _assignedZones[charId] = gs != null ? gs.GetZone(charId) : 0;

            if (_zoneLayout != null)
                _zoneLayout.InitSlots(_assignedZones);

            _lastClickTimePerCharacter.Clear();
        }

        /// <summary>
        /// 매 턴 시작(OnPlayerActionStarted) 시 호출됩니다.
        /// ★ _assignedZones 값만 동기화합니다.
        ///    InitSlots, ReplaceTo, SnapToPosition을 호출하지 않습니다.
        ///    위치 재배치는 HandleActionConfirmed와 HandleLoopReset에서만 수행합니다.
        /// </summary>
        private void SyncAssignedZonesFromGameState()
        {
            var gs = CampaignGameFlowController.Instance?.GameState;
            if (gs == null) return;

            foreach (var charId in _characterViews.Keys)
                _assignedZones[charId] = gs.GetZone(charId);
        }

        private void HandleCharacterSelected(int characterId)
        {
            foreach (var kv in _characterViews)
                kv.Value.SetSelected(kv.Key == characterId);
        }

        /// <summary>
        /// 플레이어가 드래그로 캐릭터를 Zone에 드롭 확정 시 호출됩니다.
        /// 위치 재배치가 허용되는 두 경우 중 하나입니다.
        /// </summary>
        private void HandleActionConfirmed(int characterId, int targetZoneId)
        {
            if (!_characterViews.TryGetValue(characterId, out var view)) return;

            // 앵커 캐릭터 이동 확정 시 Zone 색상 갱신
            if (_anchorCharacterActive && characterId == _anchorCharacterId && targetZoneId >= 0)
                RefreshAnchorZoneColor(targetZoneId);

            if (targetZoneId >= 0)
            {
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

        // ── 구역 색상 갱신 ───────────────────────────────────────────────────

        private void RefreshAnchorZoneColor(int anchorZone)
        {
            if (_zoneRenderers == null) return;
            for (int i = 0; i < _zoneRenderers.Length; i++)
            {
                if (_zoneRenderers[i] == null) continue;
                _zoneRenderers[i].color = (i == anchorZone)
                    ? _activeZoneColor
                    : _defaultZoneColor;
            }
            Debug.Log($"[CampaignPlayerInputHandler] Zone 색상 갱신 — 앵커 Zone{anchorZone} 활성");
        }

        private void ResyncZones(params int[] zoneIds)
        {
            if (_zoneLayout == null) return;

            var affected = new HashSet<int>(zoneIds);
            var subset = new Dictionary<int, int>();
            foreach (var kv in _assignedZones)
                if (affected.Contains(kv.Value))
                    subset[kv.Key] = kv.Value;

            var positions = _zoneLayout.ComputeSlotPositions(subset);
            var rotations = _zoneLayout.ComputeSlotRotations(subset);

            foreach (var kv in positions)
            {
                if (!_characterViews.TryGetValue(kv.Key, out var v)) continue;
                var rot = rotations.TryGetValue(kv.Key, out var r) ? r : Quaternion.identity;
                var anim = v.GetComponent<CharacterPickupAnimator>();

                if (kv.Key == _draggingId && anim != null)
                    anim.LandAt(kv.Value, rot);
                else if (anim != null)
                    anim.ReplaceTo(kv.Value, rot);
                else
                {
                    v.SnapToPosition(kv.Value);
                    v.SnapToRotation(rot);
                }
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        private CharacterView RaycastCharacter()
        {
            if (_mainCamera == null) return null;

            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            bool hasHit = _characterLayerMask.value != 0
                ? Physics.Raycast(ray, out var hit, Mathf.Infinity, _characterLayerMask)
                : Physics.Raycast(ray, out hit, Mathf.Infinity);

            if (!hasHit) return null;

            hit.collider.TryGetComponent(out CharacterView view);
            if (view == null)
                view = hit.collider.GetComponentInParent<CharacterView>();
            if (view == null) return null;

            var gs = CampaignGameFlowController.Instance?.GameState;
            if (gs != null)
            {
                var status = gs.GetCharacter(view.CharacterId);
                if (status != null && !status.IsAlive) return null;
            }

            return view;
        }
    }
}