using UnityEngine;

/// <summary>
/// 로비 챕터 전환을 관리합니다.
/// TitleBookAnimator가 Awake에서 자동으로 찾아 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public class LobbyUIManager : MonoBehaviour
{
    [Header("챕터 UI 목록 (인덱스 순서대로)")]
    [SerializeField] private GameObject[] _chapters;

    private int _currentChapterIndex = -1;

    /// <summary>현재 표시 중인 챕터 인덱스.</summary>
    public int CurrentChapterIndex => _currentChapterIndex;

    /// <summary>다음 챕터로 넘길 수 있으면 true.</summary>
    public bool CanGoNext => _chapters != null && _currentChapterIndex < _chapters.Length - 1;

    /// <summary>이전 챕터로 돌아갈 수 있으면 true.</summary>
    public bool CanGoBack => _currentChapterIndex > 0;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        HideAll();
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>애니메이션 시작 시 호출. 현재 챕터를 숨깁니다.</summary>
    public void OnAnimationStart()
    {
        HideAll();
    }

    /// <summary>최초 열림 완료 시 호출. chapter[0]을 표시합니다.</summary>
    public void Show()
    {
        ShowChapter(0);
    }

    /// <summary>페이지 넘김 완료 시 호출. 다음 챕터를 순차적으로 표시합니다.</summary>
    public void ShowNextChapter()
    {
        ShowChapter(_currentChapterIndex + 1);
    }

    /// <summary>이전 페이지 완료 시 호출. 이전 챕터로 돌아갑니다.</summary>
    public void ShowPreviousChapter()
    {
        ShowChapter(_currentChapterIndex - 1);
    }

    public void ShowChapter(int index)
    {
        if (_chapters == null || _chapters.Length == 0) return;
        _currentChapterIndex = Mathf.Clamp(index, 0, _chapters.Length - 1);
        for (int i = 0; i < _chapters.Length; i++)
        {
            if (_chapters[i] == null) continue;
            _chapters[i].SetActive(i == _currentChapterIndex);
        }
    }

    public void HideAll()
    {
        foreach (var chapter in _chapters)
            if (chapter != null) chapter.SetActive(false);
    }
}
