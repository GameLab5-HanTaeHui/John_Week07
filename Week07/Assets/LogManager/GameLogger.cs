using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 싱글톤 기반 로그 시스템 — JSON Lines 포맷으로 기록.
///
/// 저장 방식:
///   로컬: 플레이어 1명당 파일 1개에 모든 스테이지 로그를 누적 append
///   경로: 실행파일/log/GameLog_{PlayerUuid}.jsonl
///
///   업로드: 방금 끝난 스테이지 세션 구간만 추출 → Discord 전송
///          (ExtractCurrentSessionBytes() 호출)
///
/// 로깅 범위: 스테이지 씬에서만 동작. 로비·튜토리얼은 기록하지 않음.
///
/// 사용법:
///   // GameFlowController.Start() 에서:
///   GameLogger.Instance.StartStageLogging(stageId);
///
///   // FinalDecisionUI 제출 후 (권장 순서):
///   string fileName = GameLogger.Instance.BuildUploadFileName();
///   string stageId  = GameLogger.Instance.CurrentStageId;
///   GameLogger.Instance.StopStageLogging();                          // session_end 기록
///   byte[] bytes = GameLogger.Instance.ExtractCurrentSessionBytes(); // session_end 포함 추출
///   LogUploader.Instance.UploadSessionBytes(bytes, fileName, isWin, stageId);
/// </summary>
public class GameLogger : MonoBehaviour
{
    public static GameLogger Instance { get; private set; }

    private const string PLAYER_UUID_KEY = "hth_player_uuid";
    private const string ATTEMPT_KEY_PREFIX = "hth_attempt_";

    /// <summary>
    /// 씬에 배치 없이도 자동으로 인스턴스를 생성합니다.
    /// 빌드에서 스크립트 누락 문제를 방지합니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("_GameLogger");
        DontDestroyOnLoad(go);
        go.AddComponent<GameLogger>();
        Debug.Log("[GameLogger] 자동 생성됨");
    }

    private string _logFilePath;
    private string _sessionId;
    private DateTime _sessionStart;
    private string _currentStageId;
    private bool _isLoggingActive;

    // 이번 스테이지 세션이 파일의 몇 바이트 위치에서 시작했는지 기억.
    // 업로드 시 이 위치부터 끝까지를 잘라내서 전송합니다.
    private long _sessionStartFileOffset;

    public string LogFilePath => _logFilePath;
    public string SessionId => _sessionId;
    public DateTime SessionStart => _sessionStart;
    public string PlayerUuid { get; private set; }
    public string CurrentStageId => _currentStageId;
    public bool IsLoggingActive => _isLoggingActive;

    /// <summary>세션 시작 후 경과 시간(초). final_decision_enter 등에서 사용합니다.</summary>
    public int SessionElapsedSec => _isLoggingActive
        ? (int)(System.DateTime.UtcNow - _sessionStart).TotalSeconds
        : 0;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 익명 UUID 복원 또는 신규 생성 (같은 기기 = 같은 UUID = 같은 파일)
        PlayerUuid = PlayerPrefs.GetString(PLAYER_UUID_KEY, string.Empty);
        if (string.IsNullOrEmpty(PlayerUuid))
        {
            PlayerUuid = Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(PLAYER_UUID_KEY, PlayerUuid);
            PlayerPrefs.Save();
        }

        // [HTH추가]
        // 로그 파일 경로는 플레이어 UUID당 고정 (누적 append)
        // 빌드/에디터 모두 호환되는 경로 설정
#if UNITY_EDITOR
        string exeDir = Path.GetDirectoryName(Application.dataPath);
#else
        string exeDir = Path.GetDirectoryName(Application.dataPath);
        // Windows 빌드: Application.dataPath = "MyGame_Data/"
        // GetDirectoryName 하면 실행파일(.exe) 위치
#endif

        string logDir = Path.Combine(Application.persistentDataPath, "log");
        try
        {
            Directory.CreateDirectory(logDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameLogger] 로그 폴더 생성 실패: {e.Message}");
            return;
        }
        _logFilePath = Path.Combine(logDir, $"GameLog_{PlayerUuid}.jsonl");
        Debug.Log($"[GameLogger] 로그 파일 경로: {_logFilePath}");

        Application.logMessageReceived += HandleUnityLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleUnityLog;
    }

    // ── 스테이지 로깅 제어 ────────────────────────────────────────────────────

    /// <summary>
    /// 스테이지 로깅을 시작합니다. GameFlowController.Start() 에서 호출하세요.
    /// 이미 다른 스테이지가 로깅 중이면 먼저 종료한 뒤 새 스테이지를 시작합니다.
    /// </summary>
    public void StartStageLogging(string stageId)
    {
        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogWarning("[GameLogger] StartStageLogging — stageId가 비어있어 로깅을 시작하지 않습니다.");
            return;
        }

        if (_isLoggingActive)
            StopStageLogging();

        _currentStageId = stageId;
        _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        _sessionStart = DateTime.UtcNow;

        // 이번 스테이지 세션이 시작되는 파일 위치 기록
        _sessionStartFileOffset = File.Exists(_logFilePath)
            ? new FileInfo(_logFilePath).Length
            : 0L;

        _isLoggingActive = true;

        // [HTH추가] 스테이지별 시도 횟수 누적
        string attemptKey = ATTEMPT_KEY_PREFIX + _currentStageId;
        int attemptNumber = PlayerPrefs.GetInt(attemptKey, 0) + 1;
        PlayerPrefs.SetInt(attemptKey, attemptNumber);
        PlayerPrefs.Save();

        LogEvent("session_start", new Dictionary<string, object>
        {
            { "player_uuid",     PlayerUuid                      },
            { "session_id",      _sessionId                      },
            { "stage_id",        _currentStageId                 },
            { "attempt_number",  attemptNumber                   },
            { "unity_version",   Application.unityVersion        },
            { "platform",        Application.platform.ToString() },
            { "system_language", Application.systemLanguage.ToString() },
        });
    }

    /// <summary>
    /// 스테이지 로깅을 종료하고 session_end 이벤트를 기록합니다.
    /// 호출 후 ExtractCurrentSessionBytes()를 부르면 session_end까지 포함한 구간이 추출됩니다.
    ///
    /// 주의: _sessionStartFileOffset은 유지됩니다 (다음 StartStageLogging에서 덮어씀).
    /// 따라서 StopStageLogging → ExtractCurrentSessionBytes → BuildUploadFileName 순서로 사용 가능합니다.
    /// </summary>
    public void StopStageLogging()
    {
        if (!_isLoggingActive) return;

        LogEvent("session_end", new Dictionary<string, object>
        {
            { "duration_sec", (int)(DateTime.UtcNow - _sessionStart).TotalSeconds },
        });

        _isLoggingActive = false;
        // _currentStageId, _sessionId, _sessionStart, _sessionStartFileOffset은
        // 업로드용 메타데이터로 쓰이므로 남겨둠. 다음 StartStageLogging에서 덮어씀.
    }

    /// <summary>
    /// 이번 스테이지 세션이 기록된 구간(session_start부터 파일 끝까지)을 바이트로 추출합니다.
    /// Discord 업로드용입니다. 파일 전체는 로컬에 계속 보존됩니다.
    ///
    /// 권장 호출 순서:
    ///   StopStageLogging() → ExtractCurrentSessionBytes()
    ///   → 이렇게 하면 session_end 이벤트까지 포함된 완전한 세션 구간이 추출됩니다.
    /// </summary>
    public byte[] ExtractCurrentSessionBytes()
    {
        if (!File.Exists(_logFilePath)) return null;

        try
        {
            using (var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = fs.Length - _sessionStartFileOffset;
                if (length <= 0) return null;

                fs.Seek(_sessionStartFileOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                int readTotal = 0;
                while (readTotal < length)
                {
                    int read = fs.Read(buffer, readTotal, (int)(length - readTotal));
                    if (read <= 0) break;
                    readTotal += read;
                }
                return buffer;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] 세션 구간 추출 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Discord 업로드용 파일명을 생성합니다. 규칙: {StageId}_{PlayerUuid}_{SessionId}_{시각}.jsonl
    /// </summary>
    public string BuildUploadFileName()
    {
        string safeStageId = SanitizeFileName(_currentStageId ?? "Unknown");
        return $"{safeStageId}_{PlayerUuid}_{_sessionId}_{_sessionStart:yyyy-MM-dd_HH-mm-ss}.jsonl";
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    public void Log(string message)
    {
        LogEvent("message", new Dictionary<string, object> { { "text", message } });
    }

    /// <summary>이벤트 로그. 로깅 비활성 상태일 때는 무시됩니다.</summary>
    public void LogEvent(string eventName, Dictionary<string, object> fields)
    {
        if (!_isLoggingActive) return;
        if (string.IsNullOrEmpty(eventName)) return;

        var sb = new StringBuilder(256);
        sb.Append('{');
        AppendField(sb, "t", DateTime.UtcNow.ToString("O"));
        sb.Append(',');
        AppendField(sb, "event", eventName);

        // 스테이지 ID는 분석 편의를 위해 모든 이벤트에 자동 부착
        if (!string.IsNullOrEmpty(_currentStageId))
        {
            sb.Append(',');
            AppendField(sb, "stage_id", _currentStageId);
        }

        if (fields != null)
        {
            foreach (var kv in fields)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                // stage_id는 위에서 이미 자동 부착했으므로 중복 방지
                if (kv.Key == "stage_id") continue;
                sb.Append(',');
                AppendField(sb, kv.Key, kv.Value);
            }
        }
        sb.Append('}');

        WriteLine(sb.ToString());
    }

    // ── Unity 콘솔 로그 후킹 (에러만) ─────────────────────────────────────────

    private void HandleUnityLog(string logString, string stackTrace, LogType type)
    {
        if (!_isLoggingActive) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

        var fields = new Dictionary<string, object>
        {
            { "type",    type.ToString() },
            { "message", logString },
        };
        if (type == LogType.Exception || type == LogType.Error)
            fields["stack"] = stackTrace;

        LogEvent("unity_log", fields);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void WriteLine(string jsonLine)
    {
        try
        {
            File.AppendAllText(_logFilePath, jsonLine + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] 파일 기록 실패: {e.Message}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    private static void AppendField(StringBuilder sb, string key, object value)
    {
        sb.Append('"').Append(EscapeJson(key)).Append('"').Append(':');
        AppendValue(sb, value);
    }

    private static void AppendValue(StringBuilder sb, object value)
    {
        switch (value)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int i: sb.Append(i); break;
            case long l: sb.Append(l); break;
            case float f: sb.Append(f.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case string s: sb.Append('"').Append(EscapeJson(s)).Append('"'); break;
            case IEnumerable<int> intArr:
                sb.Append('[');
                bool first = true;
                foreach (var v in intArr)
                {
                    if (!first) sb.Append(',');
                    sb.Append(v);
                    first = false;
                }
                sb.Append(']');
                break;
            default: sb.Append('"').Append(EscapeJson(value.ToString())).Append('"'); break;
        }
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}