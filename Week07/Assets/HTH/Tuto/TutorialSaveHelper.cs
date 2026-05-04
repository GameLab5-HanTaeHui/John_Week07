using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// TutorialManager 완료 시 캠페인 저장 데이터에
    /// 튜토리얼 보상을 기록하는 유틸리티입니다.
    ///
    /// ─── 호출 위치 ───────────────────────────────────────────────────────
    ///   TutorialManager.HandleGuideAdvanced()
    ///   case TutorialPhase.Dialog_EnvyOutro5 블록 안에서 호출합니다.
    ///
    /// ─── 지급 조각 ───────────────────────────────────────────────────────
    ///   P01_01 ~ P01_05 : 엔비(플레이어블) 조각 5개 — 처음부터 공개
    ///   P02_01          : 메이와의 첫 대화 조각 (튜토리얼 보상)
    ///
    /// ─── 초기화 보존 ─────────────────────────────────────────────────────
    ///   CampaignSaveManager.Delete() 호출 시
    ///   isTutorialCleared + 위 6개 조각은 보존됩니다.
    /// </summary>
    public static class TutorialSaveHelper
    {
        private const string StageId = "CampaignMode";

        /// <summary>튜토리얼 완료 시 일괄 지급되는 조각 목록입니다.</summary>
        private static readonly string[] RewardFragmentIds =
        {
            "P01_01", // 엔비 조각 1
            "P01_02", // 엔비 조각 2
            "P01_03", // 엔비 조각 3
            "P01_04", // 엔비 조각 4
            "P01_05", // 엔비 조각 5
            "P02_01", // 메이 첫 대화 (튜토리얼 보상)
        };
        private static readonly string[] RewardDialogueIds =
        {
            "P02_01",
        };


        /// <summary>
        /// 튜토리얼 완료 보상을 CampaignSaveData에 기록하고 저장합니다.
        /// TutorialManager의 Dialog_EnvyOutro5 완료 시 호출하세요.
        ///
        /// ★ 튜토리얼 씬에는 CampaignSaveManager가 없을 수 있으므로
        ///   GetOrCreate()로 자동 생성 후 저장합니다.
        /// </summary>
        public static void GrantTutorialReward()
        {
            var mgr = CampaignSaveManager.GetOrCreate();
            if (mgr == null)
            {
                Debug.LogError("[TutorialSaveHelper] CampaignSaveManager 생성 실패");
                return;
            }

            var saveData = mgr.CurrentSave ?? mgr.Load(StageId);

            saveData.isTutorialCleared = true;

            foreach (var fragmentId in RewardFragmentIds)
            {
                if (!saveData.collectedFragmentIds.Contains(fragmentId))
                {
                    saveData.collectedFragmentIds.Add(fragmentId);
                    Debug.Log($"[TutorialSaveHelper] 조각 지급 — {fragmentId}");
                }
            }
            foreach (var id in RewardDialogueIds)
            {
                if (!saveData.playedDialogueIds.Contains(id))
                    saveData.playedDialogueIds.Add(id);
            }

            mgr.Save(saveData);
            Debug.Log($"[TutorialSaveHelper] 튜토리얼 완료 보상 저장 완료 — " +
                      $"{Application.persistentDataPath}/HTH/campaign_save_{StageId}.json");
        }
    }
}