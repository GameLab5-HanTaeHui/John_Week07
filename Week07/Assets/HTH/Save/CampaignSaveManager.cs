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
    /// ─── 저장 데이터 ─────────────────────────────────────────────────────
    ///   collectedFragmentIds  → 수집된 대화 조각 (P01_01 형식)
    ///   playedComboIds        → 재생된 대사 ComboId
    ///   collectedNames        → 수집된 캐릭터 이름
    ///   unlockedConceptCards  → 해금된 컨셉 카드 캐릭터 ID
    ///   unlockedEpilogues     → 해금된 시점 완결문 캐릭터 ID
    ///
    /// ─── 저장 시점 ───────────────────────────────────────────────────────
    ///   FragmentCollector.TryCollectFragment() 완료 시
    ///   DialogueProgressTracker.MarkComboPlayed() 완료 시
    ///   CharacterRecordPanelManager.RegisterCharacterName() 완료 시
    ///   RewardSaveData.SaveConceptCardUnlock() 완료 시
    ///   RewardSaveData.SaveEpilogueUnlock() 완료 시
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   인게임: _CampaignSystem 하위 GameObject
    ///   로비:   CampaignSystem (빈 GameObject)
    ///   두 씬 모두 배치 필요 (DontDestroyOnLoad 사용 안 함)
    ///
    /// ─── 외부 호출 ───────────────────────────────────────────────────────
    ///   CampaignSaveManager.Instance.Load(stageId)
    ///   CampaignSaveManager.Instance.Save(data)
    ///   CampaignSaveManager.Instance.Delete(stageId)
    ///   CampaignSaveManager.Instance.HasSave(stageId)
    ///   CampaignSaveManager.Instance.GetProgressSummary(stageId)
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignSaveManager : MonoBehaviour
    {
        public static CampaignSaveManager Instance { get; private set; }

        private const string FolderName = "HTH";
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
        /// 스테이지 ID로 JSON 저장 파일을 로드합니다.
        /// 파일이 없으면 새 데이터를 생성합니다.
        /// Phase2 진입 시 / 로비 진입 시 호출합니다.
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

                    // null 방어 (JsonUtility가 빈 리스트를 null로 역직렬화하는 경우)
                    CurrentSave.collectedFragmentIds ??= new();
                    CurrentSave.playedComboIds ??= new();
                    CurrentSave.collectedNames ??= new();
                    CurrentSave.unlockedConceptCards ??= new();
                    CurrentSave.unlockedEpilogues ??= new();

                    Debug.Log($"[CampaignSaveManager] 로드 완료 — {stageId}\n" +
                              $"  조각 {CurrentSave.collectedFragmentIds.Count}개\n" +
                              $"  ComboId {CurrentSave.playedComboIds.Count}개\n" +
                              $"  이름 {CurrentSave.collectedNames.Count}개\n" +
                              $"  컨셉카드 {CurrentSave.unlockedConceptCards.Count}개\n" +
                              $"  시점완결문 {CurrentSave.unlockedEpilogues.Count}개");

                    return CurrentSave;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CampaignSaveManager] 로드 실패 — {e.Message}\n" +
                                   "새 데이터로 대체합니다.");
                }
            }

            // 파일 없음 또는 파싱 실패 → 새 데이터 생성
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
        /// 로비에서 이야기 초기화 버튼 클릭 → WarningDialog 확인 시 호출합니다.
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
        /// 저장 파일이 존재하고 진행 데이터가 있는지 확인합니다.
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
        /// 저장 데이터 요약을 반환합니다.
        /// 로비에서 진행 상황 표시에 사용합니다.
        /// 반환: (수집된 조각 수, 총 조각 수, 해금된 시점 완결문 수)
        /// </summary>
        public (int fragmentCount, int totalFragments, int epilogueCount) GetProgressSummary(string stageId)
        {
            if (!HasSave(stageId)) return (0, 35, 0);

            try
            {
                string json = File.ReadAllText(GetFilePath(stageId));
                var data = JsonUtility.FromJson<CampaignSaveData>(json);
                return (
                    data.collectedFragmentIds?.Count ?? 0,
                    35, // 총 35개 ProfileClue
                    data.unlockedEpilogues?.Count ?? 0
                );
            }
            catch
            {
                return (0, 35, 0);
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private string GetFilePath(string stageId)
            => Path.Combine(
                Application.persistentDataPath,
                FolderName,
                $"{FilePrefix}{stageId}.json");

        private void EnsureFolderExists()
        {
            string folder = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}