using System.Collections.Generic;
using HTH.Campaign;
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
            else Pause();
        }
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (_pausePanel != null) _pausePanel.SetActive(true);

        // ★ 튜토리얼/캠페인 공통 — pause_open 로그
        var stageId = GameLogger.Instance?.CurrentStageId ?? "";
        if (stageId == "Tutorial")
        {
            GameLogger.Instance?.LogEvent("tutorial_pause_open", new Dictionary<string, object>
            {
                { "elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
            });
        }
        else
        {
            var gfc = CampaignGameFlowController.Instance;
            GameLogger.Instance?.LogEvent("pause_open", new Dictionary<string, object>
            {
                { "loop",        gfc?.LoopCount ?? 0 },
                { "turn",        gfc?.TurnCount ?? 0 },
                { "elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
            });
        }
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
        var stageId = GameLogger.Instance?.CurrentStageId ?? "";
        if (stageId == "Tutorial")
        {
            // 튜토리얼 포기
            GameLogger.Instance?.LogEvent("tutorial_forfeit", new Dictionary<string, object>
            {
                { "elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
                { "last_step",   "unknown" }, // TutorialManager에서 직접 호출 시 덮어씀
            });
        }
        else
        {
            // 캠페인 중도 포기 (저장 취소 + 로비)
            var gfc = CampaignGameFlowController.Instance;
            GameLogger.Instance?.LogEvent("campaign_forfeit", new Dictionary<string, object>
            {
                { "loop",        gfc?.LoopCount ?? 0 },
                { "turn",        gfc?.TurnCount ?? 0 },
                { "elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
            });
        }

        string fileName = GameLogger.Instance?.BuildUploadFileName();
        GameLogger.Instance?.StopStageLogging();
        byte[] bytes = GameLogger.Instance?.ExtractCurrentSessionBytes();

        if (LogUploader.Instance != null)
        {
            LogUploader.Instance.UploadSessionBytes(bytes, fileName, false, stageId,
                onComplete: () =>
                {
                    TurnHistoryRepository.Instance?.ClearAll();
                    LeaveToPaused();
                });
        }
        else
        {
            TurnHistoryRepository.Instance?.ClearAll();
            LeaveToPaused();
        }
    }

    /// <summary>게임을 포기합니다. 세이브가 삭제되고 로비로 이동합니다.</summary>
    public void Forfeit()
    {
        var stageId = GameLogger.Instance?.CurrentStageId ?? "";
        var gfc = CampaignGameFlowController.Instance;
        GameLogger.Instance?.LogEvent("campaign_forfeit", new Dictionary<string, object>
        {
            { "loop",        gfc?.LoopCount ?? 0 },
            { "turn",        gfc?.TurnCount ?? 0 },
            { "elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
        });

        string fileName = GameLogger.Instance?.BuildUploadFileName();
        GameLogger.Instance?.StopStageLogging();

        if (LogUploader.Instance != null)
        {
            LogUploader.Instance.UploadSessionBytes(null, null, false, stageId,
                onComplete: () =>
                {
                    TurnHistoryRepository.Instance?.ClearAll();
                    LeaveToPaused();
                });
        }
        else
        {
            TurnHistoryRepository.Instance?.ClearAll();
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