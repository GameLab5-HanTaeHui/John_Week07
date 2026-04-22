using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인게임 일시정지 패널을 관리합니다.
///
/// 버튼 연결:
///   일시정지 버튼  → Pause()
///   Resume 버튼   → Resume()
///   저장취소+로비  → ExitToLobby()
///   게임 포기     → Forfeit()
/// </summary>
[DisallowMultipleComponent]
public class PauseManager : MonoBehaviour
{
    [Header("일시정지 패널")]
    [SerializeField] private GameObject _pausePanel;

    [Header("씬 이름")]
    [SerializeField] private string _lobbySceneName = "LobbyScene";

    public bool IsPaused { get; private set; }

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused) Resume();
            else          Pause();
        }
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (_pausePanel != null) _pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        if (_pausePanel != null) _pausePanel.SetActive(false);
    }

    /// <summary>인게임 저장을 취소하고 로비로 나갑니다. 현재 진행은 사라지며 이어하기 불가.</summary>
    public void ExitToLobby()
    {
        // ★ [HTH추가] 이탈 로그 (지표 #14)
        var gfc = GameFlowController.Instance;
        GameLogger.Instance?.LogEvent("forfeit", new Dictionary<string, object>
        {
            { "reason",      "exit_to_lobby" },
            { "loop",        gfc != null ? gfc.LoopCount : 0 },
            { "turn",        gfc != null ? gfc.TurnCount : 0 },
            { "day",         gfc?.CurrentDay ?? 0 },
            { "time_of_day", gfc?.CurrentTimeOfDay ?? "" },
        });

        string fileName = GameLogger.Instance?.BuildUploadFileName();
        string stageId = GameLogger.Instance?.CurrentStageId;
        GameLogger.Instance?.StopStageLogging();
        byte[] bytes = GameLogger.Instance?.ExtractCurrentSessionBytes();

        // [HTH추가] 업로드 완료 후 씬 전환
        if (LogUploader.Instance != null)
        {
            LogUploader.Instance.UploadSessionBytes(bytes, fileName, false, stageId,
                onComplete: () =>
                {
                    TurnHistoryRepository.Instance.ClearAll();
                    LeaveToPaused();
                });
        }
        else
        {
            // 기존 코드
            TurnHistoryRepository.Instance.ClearAll();
            LeaveToPaused();
        }
    }

    /// <summary>게임을 포기합니다. 세이브가 삭제되고 로비로 이동합니다.</summary>
    public void Forfeit()
    {
        // ★ [HTH추가] 포기 로그 (지표 #14)
        var gfc = GameFlowController.Instance;
        GameLogger.Instance?.LogEvent("forfeit", new Dictionary<string, object>
        {
            { "reason",      "forfeit" },
            { "loop",        gfc != null ? gfc.LoopCount : 0 },
            { "turn",        gfc != null ? gfc.TurnCount : 0 },
            { "day",         gfc?.CurrentDay ?? 0 },
            { "time_of_day", gfc?.CurrentTimeOfDay ?? "" },
        });

        string fileName = GameLogger.Instance?.BuildUploadFileName();
        string stageId = GameLogger.Instance?.CurrentStageId;
        GameLogger.Instance?.StopStageLogging();

        if (LogUploader.Instance != null)
        {
            LogUploader.Instance.UploadSessionBytes(null, null, false, stageId,
                onComplete: () =>
                {
                    TurnHistoryRepository.Instance.ClearAll();
                    LeaveToPaused();
                });
        }
        else
        {
            TurnHistoryRepository.Instance.ClearAll();
            LeaveToPaused();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void LeaveToPaused()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(_lobbySceneName);
    }
}
