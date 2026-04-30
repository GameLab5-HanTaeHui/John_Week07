using System;
using DG.Tweening;
using UnityEngine;
using HTH.Campaign.Lobby; // 새로 만든 매니저를 위해 네임스페이스 추가

/// <summary>
/// 타이틀 화면용 책 연출 (3D).
/// LobbyPageManager를 자동으로 찾아 애니메이션 시작/종료 시 UI를 최신화합니다.
///
/// [최초 열림] 아무 키 입력 → 슬라이드 + OpenEntries 회전 → LobbyPageManager.ShowInitialPage()
/// [페이지 넘김] TurnPage()     → 현재 챕터 인덱스에 맞는 TurnPageSet 정방향 재생
/// [이전 페이지] TurnPageBack() → 이전 챕터 인덱스에 맞는 TurnPageSet 역방향 재생
///
/// TurnPageSets 배열 인덱스:
///   sets[0] → 챕터 0→1 전환 애니메이션 (역방향: 1→0)
///   sets[1] → 챕터 1→2 전환 애니메이션 (역방향: 2→1)
///   ...
/// 세트가 부족하면 마지막 세트를 재사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class TitleBookAnimator : MonoBehaviour
{
    [Serializable]
    public struct RotationEntry
    {
        [Tooltip("회전시킬 자식 오브젝트")]
        public Transform target;
        [Tooltip("앞으로 넘길 때 목표 Z 회전각")]
        public float rotationZ;
        [Tooltip("뒤로 돌아갈 때 목표 Z 회전각")]
        public float rotationBackZ;
        [Tooltip("Fast=최단경로 / LocalAxisAdd=현재값에서 더하기(방향 강제)")]
        public RotateMode rotateMode;
        public float duration;
        public float delay;
        public Ease ease;
    }

    [Serializable]
    public struct TurnPageSet
    {
        [Tooltip("이 전환에서 재생할 회전 엔트리 목록")]
        public RotationEntry[] entries;
    }

    [Header("슬라이드 설정")]
    [SerializeField] private float _slideDistance = 5f;
    [SerializeField] private float _slideDuration = 0.6f;
    [SerializeField] private Ease _slideEase = Ease.InOutQuart;

    [Header("최초 열림 회전")]
    [SerializeField] private RotationEntry[] _openEntries;

    [Header("페이지 넘김 회전 (챕터 전환 순서대로)")]
    [SerializeField] private TurnPageSet[] _turnPageSets;

    // 바뀐 매니저 선언
    private LobbyPageManager _pageManager;
    private bool _opened;
    private bool _turning;
    private bool _inputReady = true;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // 바뀐 매니저 찾기 (LobbyUI는 안 쓰므로 삭제했습니다)
        _pageManager = FindFirstObjectByType<LobbyPageManager>();
    }

    /// <summary>LobbyDialogueManager가 다이얼로그 완료 후 true로 설정해 책 열기를 허용합니다.</summary>
    public void SetInputReady(bool ready) => _inputReady = ready;

    private void Update()
    {
        if (_opened || !_inputReady || !Input.anyKeyDown) return;
        PlayOpen();
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>다음 페이지 버튼에 연결.</summary>
    public void TurnPage()
    {
        if (_turning) return;

        // 현재 페이지 인덱스 가져오기
        int setIndex = _pageManager != null ? _pageManager.CurrentPageIndex : 0;
        var entries = GetTurnEntries(setIndex);

        _turning = true;
        _pageManager?.OnAnimationStart();

        PlayEntries(entries, () =>
        {
            _turning = false;
            // 바뀐 함수명 적용
            _pageManager?.ShowNextPage();
        });
    }

    /// <summary>이전 페이지 버튼에 연결.</summary>
    public void TurnPageBack()
    {
        if (_turning) return;

        int setIndex = _pageManager != null ? _pageManager.CurrentPageIndex - 1 : 0;

        // 0페이지 미만으로 넘어가는 현상 방지
        if (setIndex < 0) return;

        var entries = GetTurnEntries(setIndex);

        _turning = true;
        _pageManager?.OnAnimationStart();

        PlayEntriesReverse(entries, () =>
        {
            _turning = false;
            // 바뀐 함수명 적용
            _pageManager?.ShowPreviousPage();
        });
    }

    /// <summary>게임 종료.</summary>
    public void GameQuit()
    {
        Application.Quit();
    }


    // ── Private ───────────────────────────────────────────────────────────────

    private RotationEntry[] GetTurnEntries(int setIndex)
    {
        if (_turnPageSets == null || _turnPageSets.Length == 0) return null;
        int clamped = Mathf.Clamp(setIndex, 0, _turnPageSets.Length - 1);
        return _turnPageSets[clamped].entries;
    }

    private void PlayOpen()
    {
        _opened = true;
        _pageManager?.OnAnimationStart();

        var slideTween = transform.DOLocalMoveX(transform.localPosition.x + _slideDistance, _slideDuration)
                                  .SetEase(_slideEase);

        if (_openEntries == null || _openEntries.Length == 0)
        {
            // 바뀐 함수명 적용
            slideTween.OnComplete(() => _pageManager?.ShowInitialPage());
            return;
        }

        // 바뀐 함수명 적용
        PlayEntries(_openEntries, () => _pageManager?.ShowInitialPage());
    }

    private void PlayEntries(RotationEntry[] entries, Action onComplete)
    {
        if (entries == null || entries.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // 전체 소요 시간 = 각 엔트리의 delay + duration 중 최대값
        float maxEndTime = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.target == null) continue;

            float endTime = entry.delay + entry.duration;
            if (endTime > maxEndTime) maxEndTime = endTime;

            Vector3 targetRot = new Vector3(
                entry.target.localEulerAngles.x,
                entry.target.localEulerAngles.y,
                entry.rotationZ);

            entry.target
                 .DOLocalRotate(targetRot, entry.duration, entry.rotateMode)
                 .SetDelay(entry.delay)
                 .SetEase(entry.ease);
        }

        // 모든 트윈이 끝나는 시점에 콜백 호출
        DOVirtual.DelayedCall(maxEndTime, () => onComplete?.Invoke());
    }

    private void PlayEntriesReverse(RotationEntry[] entries, Action onComplete)
    {
        if (entries == null || entries.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        int count = entries.Length;
        float baseDelay = 0f;
        float maxEndTime = 0f;

        for (int i = count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.target == null) continue;

            float delay = baseDelay;
            baseDelay += entry.delay > 0 ? entry.delay : 0.05f;

            float endTime = delay + entry.duration;
            if (endTime > maxEndTime) maxEndTime = endTime;

            Vector3 originRot = new Vector3(
                entry.target.localEulerAngles.x,
                entry.target.localEulerAngles.y,
                entry.rotationBackZ);

            entry.target
                 .DOLocalRotate(originRot, entry.duration, entry.rotateMode)
                 .SetDelay(delay)
                 .SetEase(entry.ease);
        }

        // 모든 트윈이 끝나는 시점에 콜백 호출
        DOVirtual.DelayedCall(maxEndTime, () => onComplete?.Invoke());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_slideDuration <= 0f)
            Debug.LogWarning("[TitleBookAnimator] _slideDuration은 0보다 커야 합니다.");
    }
#endif
}