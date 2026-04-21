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
        TurnHistoryRepository.Instance.ClearAll();
        LeaveToPaused();
    }

    /// <summary>게임을 포기합니다. 세이브가 삭제되고 로비로 이동합니다.</summary>
    public void Forfeit()
    {
        TurnHistoryRepository.Instance.ClearAll();
        LeaveToPaused();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void LeaveToPaused()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(_lobbySceneName);
    }
}
