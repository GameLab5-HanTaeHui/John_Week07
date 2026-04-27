using System;
using System.IO;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 게임 버전 불일치 시 저장 데이터를 초기화합니다.
    ///
    /// ─── PlayerPrefs 미사용 ──────────────────────────────────────────────
    ///   버전 정보를 별도 version.json 파일에 저장합니다.
    ///   CampaignSaveData.gameVersion 비교 방식이 아닌
    ///   독립적인 파일로 관리하므로 Execution Order에 무관합니다.
    ///
    ///   저장 경로:
    ///   {Application.persistentDataPath}/HTH/version.json
    ///
    /// ─── 동작 순서 ───────────────────────────────────────────────────────
    ///   1. version.json 에서 저장된 버전 읽기
    ///   2. _currentVersion과 비교
    ///   3. 불일치 → 저장 파일 + 보상 데이터 전체 삭제
    ///   4. version.json에 현재 버전 기록
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   로비 씬 Empty 오브젝트에 CampaignSaveManager와 함께 배치합니다.
    ///   Script Execution Order 설정 불필요.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Current Version  → 현재 빌드 버전 (예: "0.1.3")
    ///   Stage Id         → 초기화할 스테이지 ID (예: "Stage_1_Phase2")
    ///   Reward Save Data → RewardSaveData 에셋
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveDataVersionManager : MonoBehaviour
    {
        private const string FolderName = "HTH";
        private const string VersionFile = "version.json";

        [Tooltip("현재 빌드 버전입니다.\n이 값이 바뀌면 저장 데이터가 자동 초기화됩니다.")]
        [SerializeField] private string _currentVersion = "0.1.3";

        [Tooltip("보상 저장 데이터 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            // 씬 전환 후에도 유지
            DontDestroyOnLoad(gameObject);

            string savedVersion = LoadSavedVersion();

            if (savedVersion == _currentVersion)
            {
                Debug.Log($"[SaveDataVersionManager] 버전 일치 ({_currentVersion}) — 기존 데이터 유지");
                return;
            }

            Debug.Log($"[SaveDataVersionManager] 버전 변경 감지 " +
                      $"({(string.IsNullOrEmpty(savedVersion) ? "없음" : savedVersion)} " +
                      $"→ {_currentVersion}) — 저장 데이터 초기화");

            ClearAllSaveData();
            SaveCurrentVersion();
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>version.json에서 저장된 버전 문자열을 읽습니다.</summary>
        private string LoadSavedVersion()
        {
            string path = GetVersionFilePath();
            if (!File.Exists(path)) return string.Empty;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<VersionData>(json);
                return data?.version ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveDataVersionManager] version.json 읽기 실패: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>현재 버전을 version.json에 저장합니다.</summary>
        private void SaveCurrentVersion()
        {
            try
            {
                EnsureFolderExists();
                var data = new VersionData { version = _currentVersion };
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(GetVersionFilePath(), json);
                Debug.Log($"[SaveDataVersionManager] version.json 저장 완료 — {_currentVersion}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataVersionManager] version.json 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 게임 전체 저장 데이터를 삭제합니다.
        ///
        /// 삭제 대상
        ///   1. stage_clear.json          (StageClearRepository)
        ///   2. tutorial_progress.json    (TutorialProgressRepository)
        ///   3. HTH/campaign_save_*.json 전부  (CampaignSaveManager)
        ///   4. RewardSaveData 인메모리 초기화 (에필로그/컨셉카드 해금)
        /// </summary>
        private void ClearAllSaveData()
        {
            // 1. 스테이지 클리어 기록 삭제
            DeleteFileIfExists(Path.Combine(
                Application.persistentDataPath, "stage_clear.json"));

            // 2. 튜토리얼 진행 기록 삭제
            DeleteFileIfExists(Path.Combine(
                Application.persistentDataPath, "tutorial_progress.json"));

            // 3. 캠페인 진행 데이터 전체 삭제 (HTH 폴더 내 campaign_save_*.json 전부)
            DeleteAllCampaignSaveFiles();
            CampaignSaveManager.Instance?.ClearCurrentSave();

            // 4. RewardSaveData 인메모리 초기화 (에필로그/컨셉카드 해금)
            _rewardSaveData?.Clear();

            Debug.Log("[SaveDataVersionManager] 전체 저장 데이터 초기화 완료");
        }

        /// <summary>HTH 폴더 안의 campaign_save_*.json 파일을 전부 삭제합니다.</summary>
        private void DeleteAllCampaignSaveFiles()
        {
            string folder = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(folder)) return;

            string[] files = Directory.GetFiles(folder, "campaign_save_*.json");
            foreach (string file in files)
            {
                File.Delete(file);
                Debug.Log($"[SaveDataVersionManager] 삭제 완료 — {file}");
            }
        }

        private void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
            Debug.Log($"[SaveDataVersionManager] 삭제 완료 — {path}");
        }

        private string GetVersionFilePath()
            => Path.Combine(Application.persistentDataPath, FolderName, VersionFile);

        private void EnsureFolderExists()
        {
            string folder = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        // ── 테스트 ───────────────────────────────────────────────────────

        [ContextMenu("Force Clear Save Data")]
        private void ForceClear()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SaveDataVersionManager] 플레이 모드에서만 실행 가능합니다.");
                return;
            }
            ClearAllSaveData();
            SaveCurrentVersion();
            Debug.Log("[SaveDataVersionManager] 강제 초기화 완료");
        }

        [ContextMenu("Print Saved Version")]
        private void PrintSavedVersion()
        {
            string v = LoadSavedVersion();
            Debug.Log($"[SaveDataVersionManager] 저장된 버전: '{(string.IsNullOrEmpty(v) ? "없음" : v)}'" +
                      $" / 현재 버전: '{_currentVersion}'");
        }

        // ── 직렬화 클래스 ─────────────────────────────────────────────────

        [Serializable]
        private class VersionData
        {
            public string version;
        }
    }
}