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
    ///   로비 씬의 Empty GameObject에 한 번만 배치합니다.
    ///   DontDestroyOnLoad로 씬 전환 후에도 유지됩니다.
    ///   인게임 씬에 별도 배치 불필요.
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
            DontDestroyOnLoad(gameObject);
        }
        // ★ 추가 — 씬에 없을 때 자동 생성용 static 접근자
        public static CampaignSaveManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("CampaignSaveManager");
            var mgr = go.AddComponent<CampaignSaveManager>();
            DontDestroyOnLoad(go);
            return mgr;
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
                    CurrentSave.playedDialogueIds ??= new();
                    CurrentSave.collectedNames ??= new();
                    CurrentSave.unlockedConceptCards ??= new();
                    CurrentSave.unlockedEpilogues ??= new();
                    CurrentSave.finalTalkRecords ??= new();

                    Debug.Log($"[CampaignSaveManager] 로드 완료 — {stageId}\n" +
                              $"  조각 {CurrentSave.collectedFragmentIds.Count}개\n" +
                              $"  ComboId {CurrentSave.playedDialogueIds.Count}개\n" +
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
                // gameVersion은 SaveDataVersionManager가 설정한 값을 유지
                // 비어있으면 현재 Application.version으로 채움
                if (string.IsNullOrEmpty(data.gameVersion))
                    data.gameVersion = Application.version;
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
        /// ★ 튜토리얼 클리어 기록(isTutorialCleared)과
        ///   튜토리얼 보상 조각(P02_01)은 초기화 후에도 보존합니다.
        /// </summary>
        public void Delete(string stageId)
        {
            string path = GetFilePath(stageId);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CampaignSaveManager] 삭제할 파일 없음 — {stageId}");
                return;
            }

            // 삭제 전 보존할 필드 캡처
            bool savedTutorialCleared = false;
            bool savedP02_01 = false;

            try
            {
                string json = File.ReadAllText(path);
                var oldData = JsonUtility.FromJson<CampaignSaveData>(json);
                if (oldData != null)
                {
                    savedTutorialCleared = oldData.isTutorialCleared;
                    savedP02_01 = oldData.collectedFragmentIds?.Contains("P02_01") ?? false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CampaignSaveManager] 기존 데이터 캡처 실패 — {e.Message}");
            }

            File.Delete(path);
            Debug.Log($"[CampaignSaveManager] 저장 데이터 삭제 완료 — {stageId}");

            // 보존 데이터가 있으면 새 파일로 즉시 기록
            if (savedTutorialCleared || savedP02_01)
            {
                var preserved = new CampaignSaveData { stageId = stageId };
                preserved.isTutorialCleared = savedTutorialCleared;
                if (savedP02_01)
                    preserved.collectedFragmentIds.Add("P02_01");

                CurrentSave = preserved;
                Save(preserved);
                Debug.Log($"[CampaignSaveManager] 보존 데이터 유지 — " +
                          $"튜토리얼:{savedTutorialCleared}, P02_01:{savedP02_01}");
            }
            else
            {
                CurrentSave = null;
            }
        }

        /// <summary>인메모리 CurrentSave를 null로 초기화합니다.</summary>
        public void ClearCurrentSave()
        {
            CurrentSave = null;
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