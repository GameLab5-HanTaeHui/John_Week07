using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 스테이지 세션 종료 시 해당 구간의 로그 바이트를 Discord Webhook으로 업로드합니다.
///
/// 설정 방법:
///   1. Discord에서 비공개 서버 생성 후 채널 만들기
///   2. 채널 설정 → 연동 → 웹후크 만들기 → URL 복사
///   3. Inspector의 _webhookUrl에 붙여넣기
///
/// 호출 흐름 (FinalDecisionUI 예시):
///   string fileName = GameLogger.Instance.BuildUploadFileName();
///   string stageId  = GameLogger.Instance.CurrentStageId;
///   GameLogger.Instance.StopStageLogging();                          // session_end 기록
///   byte[] bytes    = GameLogger.Instance.ExtractCurrentSessionBytes();
///   LogUploader.Instance.UploadSessionBytes(bytes, fileName, isWin, stageId);
///
/// 로컬 파일은 GameLogger에서 계속 누적 관리되므로, 업로드 실패해도 데이터는 보존됩니다.
/// </summary>
[DisallowMultipleComponent]
public class LogUploader : MonoBehaviour
{
    public static LogUploader Instance { get; private set; }

    [Header("Discord Webhook")]
    [Tooltip("Discord 채널 Webhook URL. 빌드 시 하드코딩되므로 비공개 교육용으로만 사용.")]
    [SerializeField] private string _webhookUrl = "https://discord.com/api/webhooks/1496334476031033375/dxKk2qUqkV-b7tNwyjXYcjshXwQWm60g18VTWvj3CJGC5NT7eAaMudxVwHoTMnpnbjYF";

    [Tooltip("false면 업로드 시도하지 않습니다. 개발 중에만 사용.")]
    [SerializeField] private bool _uploadEnabled = true;

    // Inspector 대신 코드에서 URL을 설정할 때 사용합니다.
    // 빌드 후 씬 참조가 끊어지는 경우를 대비한 폴백입니다.
    private const string FALLBACK_WEBHOOK_URL = "https://discord.com/api/webhooks/1496334476031033375/dxKk2qUqkV-b7tNwyjXYcjshXwQWm60g18VTWvj3CJGC5NT7eAaMudxVwHoTMnpnbjYF"; // ← 여기에 Webhook URL 붙여넣기

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("_LogUploader");
        DontDestroyOnLoad(go);
        go.AddComponent<LogUploader>();
        Debug.Log("[LogUploader] 자동 생성됨");
    }

    [Header("타임아웃")]
    [SerializeField][Range(5, 60)] private int _timeoutSec = 20;

    private bool _uploadInProgress;
    private Action _onUploadComplete;

/// <summary>현재 업로드 진행 중 여부. GameFlowController에서 씬 전환 대기에 사용합니다.</summary>
    public bool IsUploadInProgress => _uploadInProgress;

/// <summary>업로드 완료 시 한 번 호출될 콜백을 등록합니다.</summary>
    public void SetOnUploadComplete(Action callback)
    {
        _onUploadComplete = callback;
    }

private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // [HTH추가] 
        // Inspector URL이 비어있으면 코드 상수 폴백 사용
        if (string.IsNullOrEmpty(_webhookUrl) && !string.IsNullOrEmpty(FALLBACK_WEBHOOK_URL))
        {
            _webhookUrl = FALLBACK_WEBHOOK_URL;
            Debug.Log("[LogUploader] 폴백 Webhook URL 적용됨");
        }

        Debug.Log($"[LogUploader] 초기화 완료 — Webhook URL {(string.IsNullOrEmpty(_webhookUrl) ? "미설정 ⚠️" : "설정됨 ✅")}");
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 이번 스테이지 세션 바이트를 Discord로 업로드합니다.
    /// GameLogger.ExtractCurrentSessionBytes() 결과를 인자로 넘기세요.
    /// </summary>
    public void UploadSessionBytes(byte[] sessionBytes, string fileName, bool isWin, string stageId, Action onComplete = null)
    {
        if (!_uploadEnabled)
        {
            Debug.Log("[LogUploader] 업로드 비활성화 — 스킵");
            onComplete?.Invoke();
            return;
        }
        if (string.IsNullOrEmpty(_webhookUrl))
        {
            Debug.LogWarning("[LogUploader] Webhook URL 비어있음 — 스킵");
            onComplete?.Invoke();
            return;
        }
        if (_uploadInProgress)
        {
            Debug.LogWarning("[LogUploader] 이미 업로드 중 — 완료 후 재시도");
            SetOnUploadComplete(() => UploadSessionBytes(sessionBytes, fileName, isWin, stageId, onComplete));
            return;
        }

        // 전체 누적 파일을 읽어서 업로드
        var logger = GameLogger.Instance;
        if (logger == null || !System.IO.File.Exists(logger.LogFilePath))
        {
            Debug.LogWarning("[LogUploader] 로그 파일 없음");
            onComplete?.Invoke();
            return;
        }

        byte[] fullFileBytes;
        try
        {
            fullFileBytes = System.IO.File.ReadAllBytes(logger.LogFilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LogUploader] 파일 읽기 실패: {e.Message}");
            onComplete?.Invoke();
            return;
        }

        // 파일명: {StageId}_{UUID}.jsonl — 스테이지별로 구분
        string uploadFileName = $"{logger.PlayerUuid}.jsonl";

        StartCoroutine(UploadCoroutine(fullFileBytes, uploadFileName, isWin, stageId, onComplete));
    }

    // ── 코루틴 ───────────────────────────────────────────────────────────────

    private IEnumerator UploadCoroutine(byte[] bytes, string fileName, bool isWin, string stageId, Action onComplete)
    {
        Debug.Log("[LogUploader] 데이터를 전송 합니다");
        _uploadInProgress = true;

        string summary = BuildSummary(isWin, stageId, bytes.Length);

        var form = new WWWForm();
        form.AddField("content", summary);
        form.AddBinaryData("file", bytes, fileName, "text/plain");

        using (UnityWebRequest req = UnityWebRequest.Post(_webhookUrl, form))
        {
            req.timeout = _timeoutSec;
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool success = req.result == UnityWebRequest.Result.Success;
#else
            bool success = !req.isHttpError && !req.isNetworkError;
#endif

            if (success)
                Debug.Log($"[LogUploader] 업로드 성공 ({bytes.Length} bytes)");
            else
            {
                Debug.LogWarning($"[LogUploader] 업로드 실패: {req.error} / code={req.responseCode} / result={req.result}");
                Debug.LogWarning($"[LogUploader] URL={_webhookUrl.Substring(0, Mathf.Min(40, _webhookUrl.Length))}...");
            }
        }

        _uploadInProgress = false;
        // 등록된 씬 전환 콜백 실행
        var sceneCallback = _onUploadComplete;
        _onUploadComplete = null;
        sceneCallback?.Invoke();

        // 제출 직후 등록한 onComplete 콜백 실행
        onComplete?.Invoke();
    }

    // ── 요약 메시지 ───────────────────────────────────────────────────────────

    private static string BuildSummary(bool isWin, string stageId, int byteSize)
    {
        var logger = GameLogger.Instance;
        var sb = new StringBuilder();

        // ── 제목 ──────────────────────────────────────────────────────────────
        sb.AppendLine($"**[Week07] {stageId} · {(isWin ? "✅ 승리" : "❌ 패배")}**");
        sb.AppendLine("──────────────────────────");

        // ── 플레이어 정보 ──────────────────────────────────────────────────────
        if (logger != null)
        {
            sb.AppendLine($"👤  플레이어  `{logger.PlayerUuid}`");
            sb.AppendLine($"🔑  세션      `{logger.SessionId}`");
        }

        // ── 날짜 ──────────────────────────────────────────────────────────────
        sb.AppendLine($"📅  날짜      `{System.DateTime.Now:yyyy-MM-dd  HH:mm:ss}`");

        // ── 클리어된 스테이지 ─────────────────────────────────────────────────
        var cleared = new System.Text.StringBuilder();
        var repo = StageClearRepository.Instance;
        // StageRoleConfig의 stageId 목록을 직접 나열 (현재 프로젝트 기준)
        string[] allStageIds = { "Stage_1", "Stage_2", "Epilogue_1", "Epilogue_2" };
        foreach (var id in allStageIds)
        {
            if (repo != null && repo.HasCleared(id))
                cleared.Append($"`{id}` ");
        }
        string clearedText = cleared.Length > 0 ? cleared.ToString().Trim() : "없음";
        sb.AppendLine($"🏆  클리어    {clearedText}");

        // ── 플레이 시간 ────────────────────────────────────────────────────────
        if (logger != null)
        {
            var duration = System.DateTime.UtcNow - logger.SessionStart;
            sb.AppendLine($"⏱  플레이    {(int)duration.TotalMinutes}분 {duration.Seconds}초");
        }

        // ── 파일 크기 ──────────────────────────────────────────────────────────
        sb.AppendLine("──────────────────────────");
        sb.Append($"📁  전체 누적 로그 {byteSize / 1024f:F1} KB");

        return sb.ToString();
    }
}