using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 턴 종료 / 게임 시작 / 게임 종료 시 검은 패널 위에 타이핑 효과로 다이어로그를 재생하는 매니저입니다.
///
/// 게임 종료(승/패) 전용 동작:
///   - 마지막 라인은 _gameEndLastLineDelay 초 이상 대기해야 넘길 수 있습니다.
///   - 모든 라인 완료 후 DialoguePanel을 숨기고 GameFlowController.NotifyGameEndDialogueComplete()를 호출합니다.
///   - 결과 패널 표시는 FinalDecisionUI가 담당합니다.
///
/// Canvas 구조 (씬에 직접 구성):
///   DialoguePanel (검은 배경 Panel) ← _dialoguePanel
///   └── TextRoot (빈 Transform)     ← _textRoot
///       └── [TextPrefab 인스턴스들] (풀로 생성)
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("설정 에셋")]
    [SerializeField] private TurnEndDialogueConfig _config;

    [Header("검은 배경 패널")]
    [SerializeField] private GameObject _dialoguePanel;

    [Header("텍스트 풀 — 프리팹과 부모 Transform")]
    [SerializeField] private TMP_Text  _textPrefab;
    [SerializeField] private Transform _textRoot;

    [Header("타이핑 파라미터")]
    [SerializeField] [Range(0.01f, 0.2f)] private float  _charDelay   = 0.04f;
    [SerializeField] [Range(0.05f, 1f)]   private float  _inputDelay  = 0.3f;
    [SerializeField] [Range(0.1f,  1f)]   private float  _cursorBlink = 0.5f;
    [SerializeField]                       private string _cursorChar  = "▮";

    [Header("게임 종료 — 마지막 라인 최소 대기 시간 (초)")]
    [SerializeField] [Range(0f, 5f)] private float _gameEndLastLineDelay = 3f;

    // ── 런타임 상태 ───────────────────────────────────────────────────────

    private TextPool         _pool;
    private TurnStateMachine _turnSM;

    private readonly List<string> _lines = new List<string>();
    private int           _lineIndex;
    private TMP_Text      _activeText;
    private System.Action _onComplete;

    private string _currentFullLine;
    private string _currentDisplay = "";
    private bool   _isTyping;
    private bool   _inputEnabled;
    private bool   _cursorOn = true;

    private bool _isGameEndDialogue;
    private bool _pendingGameEndIsWin;

    private Coroutine _typewriterCo;
    private Coroutine _cursorCo;

    // ── Unity ──────────────────────────────────────────────────────────────

    private void Start()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null)
        {
            Debug.LogError("[DialogueManager] GameFlowController를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _turnSM = gfc.GetTurnSM();
        if (_turnSM == null)
        {
            Debug.LogError("[DialogueManager] TurnStateMachine을 가져올 수 없습니다.");
            enabled = false;
            return;
        }

        _turnSM.OnTurnEndEntered       += HandleTurnEndEntered;
        gfc.OnGameEndDialogueRequested += HandleGameEndDialogueRequested;

        _pool = new TextPool(_textPrefab, _textRoot);
        _dialoguePanel.SetActive(false);

        // 씬 진입 시점에 게임 시작 다이얼로그 재생.
        // OnGameStarted는 GameFlowController.Start()에서 동기 발생하므로
        // DialogueManager.Start()가 실행될 시점엔 이미 지나간 상태입니다.
        HandleGameStarted();
    }

    private void OnDestroy()
    {
        if (_turnSM != null)
            _turnSM.OnTurnEndEntered -= HandleTurnEndEntered;

        var gfc = GameFlowController.Instance;
        if (gfc != null)
            gfc.OnGameEndDialogueRequested -= HandleGameEndDialogueRequested;
    }

    private void Update()
    {
        if (!_dialoguePanel.activeSelf) return;
        if (Input.GetMouseButtonDown(0))
            OnClick();
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────────────

    private void HandleGameStarted()
    {
        _isGameEndDialogue = false;
        var so = _config != null ? _config.SelectGameStart() : null;
        _lines.Clear();
        if (so != null)
            foreach (var line in so.lines)
                if (!string.IsNullOrEmpty(line))
                    _lines.Add(line);

        if (_lines.Count == 0) return;
        BeginPlay(onComplete: null);
    }

    private void HandleTurnEndEntered(IReadOnlyList<string> eventLog, bool isLoopCondition)
    {
        _isGameEndDialogue = false;
        BuildLines(eventLog, isLoopCondition);
        BeginPlay(() => GameFlowController.Instance.FinishTurnEnd());
    }

    private void HandleGameEndDialogueRequested(bool isWin)
    {
        _isGameEndDialogue   = true;
        _pendingGameEndIsWin = isWin;

        _lines.Clear();
        var so = _config != null ? (isWin ? _config.SelectWin() : _config.SelectLose()) : null;
        if (so != null)
            foreach (var line in so.lines)
                if (!string.IsNullOrEmpty(line))
                    _lines.Add(line);

        if (_lines.Count == 0)
        {
            GameFlowController.Instance.NotifyGameEndDialogueComplete(isWin);
            return;
        }

        BeginPlay(() =>
        {
            _isGameEndDialogue = false;
            GameFlowController.Instance.NotifyGameEndDialogueComplete(_pendingGameEndIsWin);
        });
    }

    private void BeginPlay(System.Action onComplete)
    {
        _onComplete = onComplete;
        _lineIndex  = 0;
        _dialoguePanel.SetActive(true);
        ShowLine();
    }

    // ── 라인 구성 ──────────────────────────────────────────────────────────

    private void BuildLines(IReadOnlyList<string> eventLog, bool isLoopCondition)
    {
        _lines.Clear();

        bool hasDeath = !isLoopCondition && HasDeathEntry(eventLog);
        var so = _config != null ? _config.Select(isLoopCondition, hasDeath) : null;

        if (so != null)
            foreach (var line in so.lines)
                if (!string.IsNullOrEmpty(line))
                    _lines.Add(line);

        if (isLoopCondition)
        {
            _lines.Add("퇴고를 시작합니다.");
            return;
        }

        AppendEventLogLines(eventLog);
    }

    private void AppendEventLogLines(IReadOnlyList<string> eventLog)
    {
        var deadNames = new List<string>();
        if (eventLog != null)
            foreach (var entry in eventLog)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (entry.EndsWith(" 사망"))
                    deadNames.Add(entry.Substring(0, entry.Length - 3));
            }

        _lines.Add(deadNames.Count > 0 ? FormatDeathLine(deadNames) : "사망자 없음.");

        foreach (var entry in eventLog)
            if (!string.IsNullOrEmpty(entry) && !entry.EndsWith(" 사망"))
                _lines.Add(entry);
    }

    private static string FormatDeathLine(List<string> names)
    {
        string joined;
        if (names.Count == 1)
        {
            joined = names[0];
        }
        else if (names.Count == 2)
        {
            string connector = EndsWithConsonant(names[0]) ? "과 " : "와 ";
            joined = names[0] + connector + names[1];
        }
        else
        {
            joined = string.Join(", ", names.GetRange(0, names.Count - 1))
                     + ", " + names[names.Count - 1];
        }

        string subjectParticle = EndsWithConsonant(names[names.Count - 1]) ? "이" : "가";
        return $"{joined}{subjectParticle} 사망했습니다.";
    }

    private static bool EndsWithConsonant(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        char last = s[s.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return false;
        return (last - 0xAC00) % 28 != 0;
    }

    private static bool HasDeathEntry(IReadOnlyList<string> log)
    {
        if (log == null) return false;
        foreach (var e in log)
            if (e.Contains("사망")) return true;
        return false;
    }

    // ── 라인 재생 ─────────────────────────────────────────────────────────

    private void ShowLine()
    {
        if (_lineIndex >= _lines.Count)
        {
            FinishDialogue();
            return;
        }

        ReturnActive();

        _activeText      = _pool.Get();
        _currentFullLine = _lines[_lineIndex];
        _currentDisplay  = "";
        _inputEnabled    = false;
        _isTyping        = true;

        bool isLastLine = _isGameEndDialogue && (_lineIndex == _lines.Count - 1);
        float postDelay = isLastLine ? _gameEndLastLineDelay : 0f;

        StopAllCoroutinesSafe();
        _typewriterCo = StartCoroutine(TypewriterCoroutine(postDelay));
        _cursorCo     = StartCoroutine(CursorBlinkCoroutine());
    }

    private void OnClick()
    {
        if (!_inputEnabled) return;

        if (_isTyping)
        {
            StopCoroutineSafe(ref _typewriterCo);
            _isTyping       = false;
            _currentDisplay = _currentFullLine;
            RefreshDisplay();

            // 게임 종료 마지막 라인은 스킵해도 강제 대기 적용
            bool isLastLine = _isGameEndDialogue && (_lineIndex == _lines.Count - 1);
            if (isLastLine && _gameEndLastLineDelay > 0f)
            {
                _inputEnabled = false;
                _typewriterCo = StartCoroutine(WaitThenEnableInput(_gameEndLastLineDelay));
            }
        }
        else
        {
            _lineIndex++;
            ShowLine();
        }
    }

    private void FinishDialogue()
    {
        StopAllCoroutinesSafe();
        ReturnActive();
        _dialoguePanel.SetActive(false);
        _onComplete?.Invoke();
        _onComplete = null;
    }

    // ── 코루틴 ────────────────────────────────────────────────────────────

    private IEnumerator TypewriterCoroutine(float postDelay = 0f)
    {
        yield return new WaitForSeconds(_inputDelay);
        _inputEnabled = true;

        for (int i = 1; i <= _currentFullLine.Length; i++)
        {
            _currentDisplay = _currentFullLine.Substring(0, i);
            RefreshDisplay();
            yield return new WaitForSeconds(_charDelay);
        }

        _isTyping = false;

        if (postDelay > 0f)
        {
            _inputEnabled = false;
            yield return new WaitForSeconds(postDelay);
            _inputEnabled = true;
        }

        _typewriterCo = null;
    }

    private IEnumerator WaitThenEnableInput(float delay)
    {
        yield return new WaitForSeconds(delay);
        _inputEnabled = true;
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

    private void RefreshDisplay()
    {
        if (_activeText == null) return;
        _activeText.text = _currentDisplay + (_cursorOn ? _cursorChar : " ");
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

    private void ReturnActive()
    {
        if (_activeText == null) return;
        _pool.Return(_activeText);
        _activeText = null;
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
