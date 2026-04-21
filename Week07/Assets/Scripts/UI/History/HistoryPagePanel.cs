using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public struct CharacterTokenPrefabs
{
    [Tooltip("이 캐릭터의 토큰 프리팹 (MovedIcon·StaticIcon·DeadMark 포함)")]
    public HistoryCharacterToken Prefab;
}

/// <summary>
/// 특정 루프·턴의 히스토리를 표시하는 패널입니다.
/// HistoryPageController가 카드 덱 방식으로 관리하며,
/// 헤더 탭을 클릭하면 OnHeaderClicked 이벤트로 컨트롤러에 알립니다.
///
/// ─── 동작 원칙 ───────────────────────────────────────────────────────────
///   Activate()  : 최초 1회만 TurnRecord를 수신하고 헤더를 갱신합니다.
///   Expand()    : RefreshContent()로 내용을 최신화한 뒤 지정 Y 위치로 슬라이드합니다.
///   Collapse()  : 내용 갱신 없이 지정 Y 위치(헤더만 보이는 위치)로 슬라이드합니다.
///
/// ─── 권장 프리팹 구조 (Canvas / VerticalLayoutGroup 불필요) ──────────────
///   HistoryPagePanel          (이 컴포넌트 + RectTransform, 패널 전체 높이)
///   ├── Content               (콘텐츠 영역 — 패널 상단부)
///   │   ├── TokenContainer    ← _tokenContainer
///   │   ├── [Before] Anchor_0 ← _beforeAnchors[0]  ZoneId=0
///   │   ├── [Before] Anchor_1 ← _beforeAnchors[1]  ZoneId=1
///   │   ├── [Before] Anchor_2 ← _beforeAnchors[2]  ZoneId=2
///   │   ├── [Before] Anchor_3 ← _beforeAnchors[3]  ZoneId=3
///   │   ├── [After]  Anchor_0 ← _afterAnchors[0]   ZoneId=0
///   │   ├── [After]  Anchor_1 ← _afterAnchors[1]   ZoneId=1
///   │   ├── [After]  Anchor_2 ← _afterAnchors[2]   ZoneId=2
///   │   └── [After]  Anchor_3 ← _afterAnchors[3]   ZoneId=3
///   └── Header                (헤더 탭 — 패널 하단부, 접혔을 때도 항상 노출)
///       ├── HeaderLabel       ← _headerLabel        "L1  T2"
///       ├── LoopConditionIcon ← _loopConditionIcon  특수 루프 표시
///       └── SelectButton      ← _selectButton
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class HistoryPagePanel : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("헤더 참조")]
    [SerializeField] private TMP_Text  _headerLabel;
    [SerializeField] private GameObject _loopConditionIcon;
    [SerializeField] private Button    _selectButton;

    [Header("구역 앵커 — Before (이동 전, 4개)")]
    [SerializeField] private HistoryZoneAnchor[] _beforeAnchors = new HistoryZoneAnchor[4];

    [Header("구역 앵커 — After (이동 후, 4개)")]
    [SerializeField] private HistoryZoneAnchor[] _afterAnchors = new HistoryZoneAnchor[4];

    [Header("토큰 설정")]
    [SerializeField] private RectTransform _tokenContainer;
    [Tooltip("캐릭터 0~6 순서로 설정 (CharacterId 기준).\n각 항목에 이동 프리팹(Moved)과 정지 프리팹(Static)을 연결하세요.")]
    [SerializeField] private CharacterTokenPrefabs[] _characterTokenPrefabs = new CharacterTokenPrefabs[7];
    [Tooltip("같은 구역 내 토큰 간 수평 간격 (px)")]
    [SerializeField] private float _tokenSpacing = 30f;
    [Tooltip("이 X 너비(px)를 초과하면 토큰을 두 줄로 표시합니다. 0이면 한 줄 고정.")]
    [SerializeField] private float _maxRowWidth  = 0f;

    [Header("DOTween 설정")]
    [SerializeField] private float _animDuration = 0.35f;
    [SerializeField] private Ease  _expandEase   = Ease.OutCubic;
    [SerializeField] private Ease  _collapseEase = Ease.InCubic;

    [Header("버튼 호버 설정")]
    [Tooltip("호버 시 버튼이 위로 올라가는 거리 (px)")]
    [SerializeField] private float _hoverOffsetY  = 8f;
    [SerializeField] private float _hoverDuration = 0.15f;
    [SerializeField] private Ease  _hoverEase     = Ease.OutCubic;

    [Header("드래그 설정")]
    [Tooltip("이 거리(px) 이상 아래로 드래그하면 패널이 내려갑니다.")]
    [SerializeField] private float _collapseDistanceThreshold = 100f;
    [Tooltip("이 속도(px/s) 이상으로 아래로 스와이프하면 즉시 내려갑니다.")]
    [SerializeField] private float _collapseVelocityThreshold = 600f;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────────────────

    public int  LoopIndex   { get; private set; }
    public int  TurnIndex   { get; private set; }
    public bool IsActivated { get; private set; }
    public bool IsExpanded  { get; private set; }

    /// <summary>헤더 탭 버튼 클릭 시 발생합니다.</summary>
    public event Action<HistoryPagePanel> OnHeaderClicked;

    /// <summary>드래그 또는 외부 요청으로 패널이 스스로 내려갈 때 발생합니다.</summary>
    public event Action<HistoryPagePanel> OnCollapseRequested;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private TurnRecord        _record;
    private RectTransform     _rect;
    private Tweener           _currentTween;

    /// <summary>씬에 배치된 원래 anchoredPosition.y</summary>
    private float _originY;
    /// <summary>Expand() 호출 시 저장되는 펼친 상태 Y — 드래그 snap-back 기준점</summary>
    private float _expandedY;
    /// <summary>컨트롤러에서 전달받은 펼침 오프셋 — 버튼 드래그 상한 계산에 사용</summary>
    private float _expandOffset;
    /// <summary>드래그 중 계산한 Y 속도 (px/s, 아래 방향이 음수)</summary>
    private float _dragVelocityY;

    /// <summary>버튼 RectTransform 및 호버 원점 Y</summary>
    private RectTransform _selectButtonRect;
    private float         _buttonOriginY;

    // 버튼 드래그 상태
    private float _buttonDragVelocityY;
    private Tweener       _hoverTween;

    private readonly List<HistoryCharacterToken> _activeTokens = new List<HistoryCharacterToken>();

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rect    = GetComponent<RectTransform>();
        _originY = _rect.anchoredPosition.y;
        _selectButton.onClick.AddListener(HandleSelectClicked);

        _selectButtonRect = _selectButton.GetComponent<RectTransform>();
        _buttonOriginY    = _selectButtonRect.anchoredPosition.y;
        RegisterHoverEvents();
    }

    // ── 초기화 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// HistoryPageController가 Awake에서 한 번 호출합니다.
    /// </summary>
    public void Init(int loopIndex, int turnIndex, float expandOffset)
    {
        LoopIndex     = loopIndex;
        TurnIndex     = turnIndex;
        _expandOffset = expandOffset;
        IsActivated   = false;

        SetHeaderLabel(null);
        SetLoopConditionIcon(false);
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 해당 TurnRecord를 수신하여 패널을 활성화합니다.
    /// 최초 1회만 유효하며 이후 재호출은 무시됩니다.
    /// </summary>
    public void Activate(TurnRecord record)
    {
        if (IsActivated) return;
        IsActivated = true;
        _record     = record;
        SetHeaderLabel(record);
    }

    /// <summary>
    /// 콘텐츠를 최신화한 뒤 오리진 위치에서 expandOffset만큼 위로 슬라이드합니다.
    /// </summary>
    public void Expand(float expandOffset, bool instant = false)
    {
        IsExpanded = true;
        _expandedY = _originY + expandOffset;
        _selectButton.interactable = false;
        // 호버 상태로 올라가 있을 수 있으므로 버튼을 원점으로 복귀
        _hoverTween?.Kill();
        _selectButtonRect.anchoredPosition = new Vector2(
            _selectButtonRect.anchoredPosition.x, _buttonOriginY);
        RefreshContent();
        AnimateTo(_expandedY, instant, isExpanding: true);
    }

    /// <summary>
    /// 오리진 위치로 돌아옵니다. 콘텐츠는 갱신하지 않습니다.
    /// </summary>
    public void Collapse(bool instant = false)
    {
        IsExpanded = false;
        _selectButton.interactable = true;
        AnimateTo(_originY, instant, isExpanding: false);
    }

    /// <summary>
    /// 최초 활성화 시 오리진 아래에서 슬라이드업하여 자연스럽게 등장합니다.
    /// SetActive(true) 직후 호출하세요.
    /// </summary>
    /// <param name="belowOffset">오리진에서 시작 위치까지의 거리 (양수 = 아래)</param>
    public void SpawnIn(float belowOffset)
    {
        SnapTo(_originY - belowOffset);
        AnimateTo(_originY, instant: false, isExpanding: true);
    }

    /// <summary>
    /// 애니메이션 없이 즉시 지정 위치로 이동합니다.
    /// </summary>
    public void SnapTo(float targetY)
    {
        _currentTween?.Kill();
        var pos = _rect.anchoredPosition;
        _rect.anchoredPosition = new Vector2(pos.x, targetY);
    }

    // ── 드래그 핸들러 ─────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsExpanded) return;
        _currentTween?.Kill();
        _dragVelocityY = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsExpanded) return;

        // 속도 갱신 (화면 공간 px/s, 아래 방향 음수)
        if (Time.deltaTime > 0f)
            _dragVelocityY = eventData.delta.y / Time.deltaTime;

        // 오직 아래 방향만 허용 — 펼친 위치 위로는 올라가지 않음
        float newY = Mathf.Min(_rect.anchoredPosition.y + eventData.delta.y, _expandedY);
        _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, newY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsExpanded) return;

        float draggedDown  = _expandedY - _rect.anchoredPosition.y;
        bool  pastDistance = draggedDown  >  _collapseDistanceThreshold;
        bool  fastSwipe    = _dragVelocityY < -_collapseVelocityThreshold;

        if (pastDistance || fastSwipe)
        {
            Collapse();
            OnCollapseRequested?.Invoke(this);
        }
        else
        {
            // 기준치 미달 → 펼친 위치로 복귀
            AnimateTo(_expandedY, instant: false, isExpanding: true);
        }
    }

    // ── 콘텐츠 갱신 ──────────────────────────────────────────────────────────

    private void RefreshContent()
    {
        ClearTokens();
        if (_record == null) return;

        SetLoopConditionIcon(_record.IsLoopConditionTurn);

        if (_record.IsLoopConditionTurn)
            RefreshMovementOnly(_record);
        else
            RefreshFull(_record);
    }

    private void RefreshFull(TurnRecord record)
    {
        var aliveAtStart = BuildAliveSet(record.BeforeAction);
        var diedThisTurn = BuildDeathSet(record.Deaths);

        PlaceTokens(record.BeforeAction, aliveAtStart, diedThisTurn,
                    record, _beforeAnchors, isAfterAction: false);
        PlaceTokens(record.AfterAction,  aliveAtStart, diedThisTurn,
                    record, _afterAnchors,  isAfterAction: true);
    }

    private void RefreshMovementOnly(TurnRecord record)
    {
        var aliveAtStart = BuildAliveSet(record.BeforeAction);
        PlaceMovementTokens(record, record.BeforeAction, _beforeAnchors, aliveAtStart);
        PlaceMovementTokens(record, record.AfterAction,  _afterAnchors,  aliveAtStart);
    }

    private void PlaceTokens(
        List<CharacterPositionSnapshot> snapshots,
        HashSet<int>                    aliveAtStart,
        HashSet<int>                    diedThisTurn,
        TurnRecord                      record,
        HistoryZoneAnchor[]             anchors,
        bool                            isAfterAction)
    {
        var byZone = GroupByZone(snapshots);
        foreach (var kv in byZone)
        {
            Vector2 center = GetAnchorLocalPos(anchors, kv.Key);
            for (int i = 0; i < kv.Value.Count; i++)
            {
                int  id             = kv.Value[i].CharacterId;
                bool alreadyDead    = !aliveAtStart.Contains(id);
                bool moved          = !alreadyDead && HasMoved(record, id);
                bool diedThisTurnId = isAfterAction && !alreadyDead && diedThisTurn.Contains(id);

                SpawnToken(id, moved, diedThisTurnId, alreadyDead, center, i, kv.Value.Count);
            }
        }
    }

    private void PlaceMovementTokens(
        TurnRecord                      record,
        List<CharacterPositionSnapshot> snapshots,
        HistoryZoneAnchor[]             anchors,
        HashSet<int>                    aliveAtStart)
    {
        var byZone = GroupByZone(snapshots);
        foreach (var kv in byZone)
        {
            Vector2 center = GetAnchorLocalPos(anchors, kv.Key);
            for (int i = 0; i < kv.Value.Count; i++)
            {
                int  id          = kv.Value[i].CharacterId;
                bool alreadyDead = !aliveAtStart.Contains(id);
                bool moved       = !alreadyDead && HasMoved(record, id);

                SpawnToken(id, moved, diedThisTurn: false, alreadyDead, center, i, kv.Value.Count);
            }
        }
    }

    private void SpawnToken(int characterId, bool moved, bool diedThisTurn, bool alreadyDead,
                            Vector2 centerPos, int slotIndex, int totalInZone)
    {
        int idx   = Mathf.Clamp(characterId, 0, _characterTokenPrefabs.Length - 1);
        var entry = _characterTokenPrefabs[idx];
        if (entry.Prefab == null) return;

        var token = Instantiate(entry.Prefab, _tokenContainer);
        token.Setup(moved, diedThisTurn, alreadyDead);
        token.Rect.anchoredPosition = centerPos + SlotOffset(slotIndex, totalInZone);
        _activeTokens.Add(token);
    }

    private void ClearTokens()
    {
        foreach (var token in _activeTokens)
            if (token != null) Destroy(token.gameObject);
        _activeTokens.Clear();
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────────

    private void AnimateTo(float targetY, bool instant, bool isExpanding)
    {
        _currentTween?.Kill();
        if (instant)
        {
            var pos = _rect.anchoredPosition;
            _rect.anchoredPosition = new Vector2(pos.x, targetY);
            return;
        }
        _currentTween = _rect.DOAnchorPosY(targetY, _animDuration)
                             .SetEase(isExpanding ? _expandEase : _collapseEase);
    }

    // ── UI 헬퍼 ───────────────────────────────────────────────────────────────

    private void SetHeaderLabel(TurnRecord record)
    {
        if (_headerLabel == null) return;
        _headerLabel.text = record != null
            ? $"L{record.LoopIndex + 1}  T{record.TurnIndex + 1}"
            : "---";
    }

    private void SetLoopConditionIcon(bool active)
    {
        if (_loopConditionIcon != null)
            _loopConditionIcon.SetActive(active);
    }

    private void HandleSelectClicked()
    {
        OnHeaderClicked?.Invoke(this);
    }

    // ── 버튼 호버 ─────────────────────────────────────────────────────────────

    private void RegisterHoverEvents()
    {
        var trigger = _selectButton.gameObject.AddComponent<EventTrigger>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => OnButtonHoverEnter());
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => OnButtonHoverExit());
        trigger.triggers.Add(exitEntry);

        var beginDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDragEntry.callback.AddListener(e => OnButtonBeginDrag((PointerEventData)e));
        trigger.triggers.Add(beginDragEntry);

        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(e => OnButtonDrag((PointerEventData)e));
        trigger.triggers.Add(dragEntry);

        var endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEntry.callback.AddListener(e => OnButtonEndDrag((PointerEventData)e));
        trigger.triggers.Add(endDragEntry);
    }

    private void OnButtonBeginDrag(PointerEventData eventData)
    {
        if (IsExpanded) return;
        _currentTween?.Kill();
        _buttonDragVelocityY = 0f;
    }

    private void OnButtonDrag(PointerEventData eventData)
    {
        if (IsExpanded) return;
        if (Time.deltaTime > 0f)
            _buttonDragVelocityY = eventData.delta.y / Time.deltaTime;

        // 위 방향만 허용 — 오리진 아래로는 내려가지 않음, 펼침 위치 위로도 올라가지 않음
        float targetExpandedY = _originY + _expandOffset;
        float newY = Mathf.Clamp(_rect.anchoredPosition.y + eventData.delta.y, _originY, targetExpandedY);
        _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, newY);
    }

    private void OnButtonEndDrag(PointerEventData eventData)
    {
        if (IsExpanded) return;
        float targetExpandedY = _originY + _expandOffset;
        float draggedUp   = _rect.anchoredPosition.y - _originY;
        bool  pastDistance = draggedUp > _collapseDistanceThreshold;
        bool  fastSwipe    = _buttonDragVelocityY > _collapseVelocityThreshold;

        if (pastDistance || fastSwipe)
            OnHeaderClicked?.Invoke(this);   // 컨트롤러가 Expand() 호출
        else
            AnimateTo(_originY, instant: false, isExpanding: false); // 원위치 복귀
    }

    private void OnButtonHoverEnter()
    {
        if (IsExpanded) return;
        _hoverTween?.Kill();
        _hoverTween = _selectButtonRect
            .DOAnchorPosY(_buttonOriginY + _hoverOffsetY, _hoverDuration)
            .SetEase(_hoverEase);
    }

    private void OnButtonHoverExit()
    {
        if (IsExpanded) return;
        _hoverTween?.Kill();
        _hoverTween = _selectButtonRect
            .DOAnchorPosY(_buttonOriginY, _hoverDuration)
            .SetEase(_hoverEase);
    }

    // ── 위치 헬퍼 ─────────────────────────────────────────────────────────────

    private Vector2 GetAnchorLocalPos(HistoryZoneAnchor[] anchors, int zoneId)
    {
        foreach (var anchor in anchors)
        {
            if (anchor == null || anchor.ZoneId != zoneId) continue;
            Vector3 local = _tokenContainer.InverseTransformPoint(anchor.transform.position);
            return new Vector2(local.x, local.y);
        }
        Debug.LogWarning($"[HistoryPagePanel] L{LoopIndex}T{TurnIndex}: ZoneId {zoneId} 앵커 없음");
        return Vector2.zero;
    }

    private Vector2 SlotOffset(int slotIndex, int total)
    {
        // _maxRowWidth가 0보다 크고 한 줄에 다 들어가지 않으면 두 줄로 표시
        if (_maxRowWidth > 0f && _tokenSpacing > 0f)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt(_maxRowWidth / _tokenSpacing));
            if (total > perRow)
            {
                int   row        = slotIndex / perRow;
                int   col        = slotIndex % perRow;
                int   countInRow = (row == 0) ? perRow : total - perRow;
                float x          = (col - (countInRow - 1) * 0.5f) * _tokenSpacing;
                float y          = (row == 0 ? 1f : -1f) * _tokenSpacing * 0.5f;
                return new Vector2(x, y);
            }
        }

        return new Vector2((slotIndex - (total - 1) * 0.5f) * _tokenSpacing, 0f);
    }

    // ── 데이터 헬퍼 (정적) ───────────────────────────────────────────────────

    private static HashSet<int> BuildAliveSet(List<CharacterPositionSnapshot> snapshots)
    {
        var set = new HashSet<int>();
        foreach (var s in snapshots)
            if (s.IsAlive) set.Add(s.CharacterId);
        return set;
    }

    private static HashSet<int> BuildDeathSet(List<TurnDeathRecord> deaths)
    {
        var set = new HashSet<int>();
        foreach (var d in deaths) set.Add(d.CharacterId);
        return set;
    }

    private static Dictionary<int, List<CharacterPositionSnapshot>> GroupByZone(
        List<CharacterPositionSnapshot> snapshots)
    {
        var result = new Dictionary<int, List<CharacterPositionSnapshot>>();
        foreach (var s in snapshots)
        {
            if (!result.TryGetValue(s.ZoneId, out var list))
                result[s.ZoneId] = list = new List<CharacterPositionSnapshot>();
            list.Add(s);
        }
        return result;
    }

    private static bool HasMoved(TurnRecord record, int characterId)
    {
        int before = -1, after = -1;
        foreach (var s in record.BeforeAction)
            if (s.CharacterId == characterId) { before = s.ZoneId; break; }
        foreach (var s in record.AfterAction)
            if (s.CharacterId == characterId) { after  = s.ZoneId; break; }
        return before != -1 && after != -1 && before != after;
    }

    // ── 정리 ──────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        _currentTween?.Kill();
        _hoverTween?.Kill();
        if (_selectButton != null)
            _selectButton.onClick.RemoveListener(HandleSelectClicked);
    }
}
