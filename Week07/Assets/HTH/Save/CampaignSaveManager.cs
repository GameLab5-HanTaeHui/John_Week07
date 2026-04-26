using System;
using System.IO;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 진행 데이터를 JSON 파일로 저장/로드/삭제하는 싱글톤입니다.
    ///
    /// ─── 저장 파일 경로 ──────────────────────────────────────────────────
    ///   {Application.persistentDataPath}/HTH/campaign_save_{stageId}.json
    ///
    /// ─── 저장 시점 ───────────────────────────────────────────────────────
    ///   FragmentCollector.TryCollectFragment() 완료 시
    ///   DialogueProgressTracker.MarkComboPlayed() 완료 시
    ///   CharacterRecordPanelManager.RegisterCharacterName() 완료 시
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   DontDestroyOnLoad는 사용하지 않습니다.
    ///   씬마다 존재하며 Initialize(stageId)로 초기화합니다.
    ///
    /// ─── 외부 호출 ───────────────────────────────────────────────────────
    ///   CampaignSaveManager.Instance.Save(data)
    ///   CampaignSaveManager.Instance.Load(stageId)
    ///   CampaignSaveManager.Instance.Delete(stageId)
    ///   CampaignSaveManager.Instance.HasSave(stageId)
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignSaveManager : MonoBehaviour
    {
        public static CampaignSaveManager Instance { get; private set; }

        /// <summary>저장 폴더명입니다.</summary>
        private const string FolderName = "HTH";

        /// <summary>저장 파일명 접두사입니다.</summary>
        private const string FilePrefix = "campaign_save_";

        /// <summary>현재 로드된 저장 데이터입니다.</summary>
        public CampaignSaveData CurrentSave { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 ID로 저장 파일을 로드합니다.
        /// 파일이 없으면 새 데이터를 생성합니다.
        /// Phase2 진입 시 호출합니다.
        /// </summary>
        public CampaignSaveData Load(string stageId)
        {
            string path = GetFilePath(stageId);

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    CurrentSave = JsonUtility.FromJson<CampaignSaveData>(json);
                    Debug.Log($"[CampaignSaveManager] 저장 데이터 로드 완료 — {stageId}\n" +
                              $"조각 {CurrentSave.collectedFragmentIds.Count}개 / " +
                              $"ComboId {CurrentSave.playedComboIds.Count}개 / " +
                              $"이름 {CurrentSave.collectedNames.Count}개");
                    return CurrentSave;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CampaignSaveManager] 로드 실패 — {e.Message}");
                }
            }

            // 파일 없음 → 새 데이터 생성
            CurrentSave = new CampaignSaveData { stageId = stageId };
            Debug.Log($"[CampaignSaveManager] 새 저장 데이터 생성 — {stageId}");
            return CurrentSave;
        }

        /// <summary>
        /// 현재 저장 데이터를 JSON 파일로 저장합니다.
        /// </summary>
        public void Save(CampaignSaveData data)
        {
            if (data == null) return;

            try
            {
                EnsureFolderExists();

                data.savedAt = DateTime.Now.ToString("o");
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string path = GetFilePath(data.stageId);

                File.WriteAllText(path, json);
                Debug.Log($"[CampaignSaveManager] 저장 완료 — {data.stageId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignSaveManager] 저장 실패 — {e.Message}");
            }
        }

        /// <summary>
        /// 저장 파일을 삭제합니다.
        /// 로비에서 진행 초기화 버튼 클릭 시 호출합니다.
        /// </summary>
        public void Delete(string stageId)
        {
            string path = GetFilePath(stageId);

            if (File.Exists(path))
            {
                File.Delete(path);
                CurrentSave = null;
                Debug.Log($"[CampaignSaveManager] 저장 데이터 삭제 완료 — {stageId}");
            }
            else
            {
                Debug.LogWarning($"[CampaignSaveManager] 삭제할 파일 없음 — {stageId}");
            }
        }

        /// <summary>
        /// 저장 파일이 존재하는지 확인합니다.
        /// 로비에서 이어하기/새로하기 버튼 표시 여부 결정에 사용합니다.
        /// </summary>
        public bool HasSave(string stageId)
        {
            string path = GetFilePath(stageId);
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<CampaignSaveData>(json);
                return data != null && data.HasProgress;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 저장 데이터의 요약 정보를 반환합니다.
        /// 로비에서 진행 상황 표시에 사용합니다.
        /// </summary>
        public (int fragmentCount, int totalFragments) GetProgressSummary(string stageId)
        {
            if (!HasSave(stageId)) return (0, 35);

            try
            {
                string json = File.ReadAllText(GetFilePath(stageId));
                var data = JsonUtility.FromJson<CampaignSaveData>(json);
                return (data.collectedFragmentIds.Count, 35); // 총 35개 ProfileClue
            }
            catch
            {
                return (0, 35);
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private string GetFilePath(string stageId)
            => Path.Combine(Application.persistentDataPath, FolderName, $"{FilePrefix}{stageId}.json");

        private void EnsureFolderExists()
        {
            string folder = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}