using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 게임 실행 시 로비 진입 전 인트로 다이얼로그를 재생합니다.
///
/// 흐름:
///   씬 시작 → DialogueSO의 라인을 타이프라이터로 표시
///   → 모든 라인 완료 후 패널 숨김 → AnyKeyHint 표시
///   → 첫 번째 입력 → OpenHint 표시
///   → 두 번째 입력 → TitleBookAnimator.PlayOpen() → 로비 진입
///
/// Canvas 구조 (씬에서 직접 구성):
///   LobbyDialoguePanel (검은 배경 Panel)  ← _dialoguePanel
///   └── TextDisplay (TMP_Text)            ← _textDisplay
///   AnyKeyHint  (다이얼로그 후 힌트)      ← _anyKeyHint
///   OpenHint    (책 열기 직전 힌트)       ← _openHint
/// </summary>
[DisallowMultipleComponent]
public class LobbyDialogueManager : MonoBehaviour
{
    // ── 씬 간 전달 플래그 ─────────────────────────────────────────────────

    /// <summary>GameFlowController에서 엔딩 스테이지 클리어 시 true로 설정합니다.</summary>
    public static bool PendingEndingDialogue { get; set; }

    [Header("다이얼로그 데이터")]
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private DialogueSO _endingDialogueSO;

    [Header("UI 참조")]
    [SerializeField] private GameObject _dialoguePanel;
    [SerializeField] private TMP_Text   _textDisplay;
    [SerializeField] private GameObject _anyKeyHint;    // 다이얼로그 완료 후 힌트 (선택)
    [SerializeField] private GameObject _openHint;      // 첫 클릭 후 책 열기 직전 힌트 (선택)

    [Header("책 애니메이터")]
    [SerializeField] private TitleBookAnimator _bookAnimator;

    [Header("타이핑 파라미터")]
    [SerializeField] [Range(0.01f, 0.2f)] private float  _charDelay   = 0.04f;
    [SerializeField] [Range(0.05f, 1f)]   private float  _inputDelay  = 0.3f;
    [SerializeField] [Range(0.1f,  1f)]   private float  _cursorBlink = 0.5f;
    [SerializeField]                       private string _cursorChar  = "▮";

    // ── 세션 상태 (앱 실행 중 한 번만 표시) ──────────────────────────────

    private static bool _shownThisSession;

    // ── 런타임 상태 ───────────────────────────────────────────────────────

    private string[] _lines;
    private int      _lineIndex;

    private string _currentFullLine = "";
    private string _currentDisplay  = "";
    private bool   _isTyping;
    private bool   _inputEnabled;
    private bool   _cursorOn = true;

    // 0: 다이얼로그 진행 중
    // 1: 다이얼로그 완료, 첫 번째 클릭 대기 (AnyKeyHint 표시)
    // 2: 첫 클릭 완료, 두 번째 클릭 대기 (OpenHint 표시)
    // 3: 완료
    private int _phase;

    private Coroutine _typewriterCo;
    private Coroutine _cursorCo;

    // ── Unity ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // 다이얼로그가 끝나기 전까지 책 열기 차단
        _bookAnimator?.SetInputReady(false);

        if (_anyKeyHint != null) _anyKeyHint.SetActive(false);
        if (_openHint   != null) _openHint.SetActive(false);
    }

    private void Start()
    {
        // 엔딩 다이얼로그 우선 처리 (세션 표시 여부와 무관하게 항상 재생)
        if (PendingEndingDialogue)
        {
            PendingEndingDialogue = false;
            _shownThisSession = true; // 이후 인트로 다이얼로그 재표시 방지

            bool hasEnding = _endingDialogueSO != null
                          && _endingDialogueSO.lines != null
                          && _endingDialogueSO.lines.Length > 0;

            if (hasEnding)
            {
                _lines = _endingDialogueSO.lines;
                _dialoguePanel?.SetActive(true);
                ShowLine();
                return;
            }

            _dialoguePanel?.SetActive(false);
            FinishDialogue();
            return;
        }

        bool hasLines = _dialogueSO != null
                     && _dialogueSO.lines != null
                     && _dialogueSO.lines.Length > 0;

        if (!hasLines || _shownThisSession)
        {
            _dialoguePanel?.SetActive(false);
            FinishDialogue();
            return;
        }

        _shownThisSession = true;
        _lines = _dialogueSO.lines;
        _dialoguePanel?.SetActive(true);
        ShowLine();
    }

    private void Update()
    {
        bool anyInput = Input.GetMouseButtonDown(0) || Input.anyKeyDown;

        if (_phase == 1)
        {
            if (anyInput) OnFirstClick();
            return;
        }

        if (_phase == 2)
        {
            if (anyInput) OpenBook();
            return;
        }

        if (_phase != 0)                    return;
        if (!_dialoguePanel.activeSelf)     return;
        if (!_inputEnabled)                 return;
        if (anyInput) OnInput();
    }

    // ── 입력 처리 ─────────────────────────────────────────────────────────

    private void OnInput()
    {
        if (_isTyping)
        {
            // 타이핑 즉시 완성
            StopCoroutineSafe(ref _typewriterCo);
            _isTyping       = false;
            _currentDisplay = _currentFullLine;
            RefreshDisplay();
        }
        else
        {
            _lineIndex++;
            if (_lineIndex >= _lines.Length)
                FinishDialogue();
            else
                ShowLine();
        }
    }

    // ── 라인 재생 ─────────────────────────────────────────────────────────

    private void ShowLine()
    {
        _currentFullLine = _lines[_lineIndex];
        _currentDisplay  = "";
        _inputEnabled    = false;
        _isTyping        = true;

        StopAllCoroutinesSafe();
        _typewriterCo = StartCoroutine(TypewriterCoroutine());
        _cursorCo     = StartCoroutine(CursorBlinkCoroutine());
    }

    private void FinishDialogue()
    {
        StopAllCoroutinesSafe();
        _dialoguePanel?.SetActive(false);

        if (_anyKeyHint != null) _anyKeyHint.SetActive(true);
        _phase = 1;
    }

    // 첫 번째 클릭: AnyKeyHint → OpenHint 전환
    private void OnFirstClick()
    {
        if (_anyKeyHint != null) _anyKeyHint.SetActive(false);
        if (_openHint   != null) _openHint.SetActive(true);
        _phase = 2;
    }

    // 두 번째 클릭: 책 열기
    private void OpenBook()
    {
        _phase = 3;
        if (_openHint != null) _openHint.SetActive(false);
        _bookAnimator?.SetInputReady(true);
    }

    // ── 코루틴 ────────────────────────────────────────────────────────────

    private IEnumerator TypewriterCoroutine()
    {
        yield return new WaitForSeconds(_inputDelay);
        _inputEnabled = true;

        for (int i = 1; i <= _currentFullLine.Length; i++)
        {
            _currentDisplay = _currentFullLine.Substring(0, i);
            RefreshDisplay();
            yield return new WaitForSeconds(_charDelay);
        }

        _isTyping     = false;
        _typewriterCo = null;
    }

    private IEnumerator CursorBlinkCoroutine()
    {
        while (true)
        {
            _cursorOn = !_cursorOn;
            RefreshDisplay();
            yield return new WaitForSeconds(_cursorBlink);
        }
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

    private void RefreshDisplay()
    {
        if (_textDisplay == null) return;
        string shown = _isTyping ? _currentDisplay : _currentFullLine;
        _textDisplay.text = shown + (_cursorOn ? _cursorChar : " ");
    }

    private void StopAllCoroutinesSafe()
    {
        StopCoroutineSafe(ref _typewriterCo);
        StopCoroutineSafe(ref _cursorCo);
    }

    private void StopCoroutineSafe(ref Coroutine co)
    {
        if (co == null) return;
        StopCoroutine(co);
        co = null;
    }
}
