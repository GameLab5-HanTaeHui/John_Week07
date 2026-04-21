using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 튜토리얼 진행 여부(IsStarted)와 클리어 여부(IsCleared)를 별도 파일로 영속화합니다.
/// 일반 게임 세이브(TurnHistoryRepository)와 독립된 파일을 사용합니다.
///
/// 저장 경로: Application.persistentDataPath/tutorial_progress.json
/// </summary>
public class TutorialProgressRepository
{
    public static TutorialProgressRepository Instance { get; } = new TutorialProgressRepository();
    private TutorialProgressRepository() { }

    private static string SavePath
        => Path.Combine(Application.persistentDataPath, "tutorial_progress.json");

    public bool IsStarted { get; private set; }
    public bool IsCleared { get; private set; }

    // ── 쓰기 API ─────────────────────────────────────────────────────────────

    /// <summary>튜토리얼 시작 시 호출합니다. 이미 시작된 경우 덮어쓰지 않습니다.</summary>
    public void MarkStarted()
    {
        if (IsStarted) return;
        IsStarted = true;
        Save();
    }

    /// <summary>튜토리얼 클리어(승리) 시 호출합니다.</summary>
    public void MarkCleared()
    {
        IsCleared = true;
        Save();
    }

    // ── I/O ──────────────────────────────────────────────────────────────────

    public bool TryLoad()
    {
        if (!File.Exists(SavePath)) return false;
        try
        {
            var data  = JsonUtility.FromJson<TutorialSaveData>(File.ReadAllText(SavePath));
            IsStarted = data?.IsStarted ?? false;
            IsCleared = data?.IsCleared ?? false;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TutorialProgressRepository] 로드 실패: {e.Message}");
            return false;
        }
    }

    private void Save()
    {
        try
        {
            var data = new TutorialSaveData { IsStarted = IsStarted, IsCleared = IsCleared };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[TutorialProgressRepository] 저장 실패: {e.Message}");
        }
    }
}
