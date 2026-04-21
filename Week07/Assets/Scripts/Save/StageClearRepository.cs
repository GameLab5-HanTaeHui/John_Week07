using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 기록을 JSON 파일로 영속화하는 저장소입니다.
///
/// 저장 경로: Application.persistentDataPath/stage_clear.json
///
/// 사용 예:
///   StageClearRepository.Instance.HasCleared("Stage_1")  → 클리어 여부 확인
///   StageClearRepository.Instance.RecordClear("Stage_1") → 클리어 기록 저장
/// </summary>
public class StageClearRepository
{
    // ── Singleton ────────────────────────────────────────────────────────────

    public static StageClearRepository Instance { get; } = new StageClearRepository();

    private StageClearRepository()
    {
        Load();
    }

    // ── 저장 경로 ────────────────────────────────────────────────────────────

    private static string SavePath
        => Path.Combine(Application.persistentDataPath, "stage_clear.json");

    // ── 데이터 ───────────────────────────────────────────────────────────────

    [Serializable]
    private class SaveData
    {
        public List<string> clearedStageIds = new List<string>();
    }

    private SaveData _data = new SaveData();

    // ── 공개 API ─────────────────────────────────────────────────────────────

    /// <summary>해당 stageId가 클리어된 기록이 있는지 확인합니다.</summary>
    public bool HasCleared(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return false;
        return _data.clearedStageIds.Contains(stageId);
    }

    /// <summary>stageId 클리어 기록을 저장합니다. 중복 저장은 무시됩니다.</summary>
    public void RecordClear(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;
        if (_data.clearedStageIds.Contains(stageId)) return;

        _data.clearedStageIds.Add(stageId);
        Save();
    }

    // ── 내부 저장/로드 ───────────────────────────────────────────────────────

    private void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(_data, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageClearRepository] 저장 실패: {e.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            _data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageClearRepository] 로드 실패: {e.Message}");
            _data = new SaveData();
        }
    }
}
