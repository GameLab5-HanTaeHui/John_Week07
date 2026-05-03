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
    /// ─── 보상 내용 ───────────────────────────────────────────────────────
    ///   isTutorialCleared = true
    ///   collectedFragmentIds에 "P02_01" 추가 (튜토리얼에서 메이와 첫 대화 조각)
    ///
    /// ─── 초기화 보존 ─────────────────────────────────────────────────────
    ///   CampaignSaveManager.Delete() 호출 시 이 두 값은 보존됩니다.
    /// </summary>
    public static class TutorialSaveHelper
    {
        private const string StageId = "CampaignMode";
        private const string TutorialEnvy01 = "P01_01";
        private const string TutorialEnvy02 = "P01_02";
        private const string TutorialEnvy03 = "P01_03";
        private const string TutorialEnvy04 = "P01_04";
        private const string TutorialEnvy05 = "P01_05";
        private const string TutorialClueId = "P02_01";

        /// <summary>
        /// 튜토리얼 완료 보상을 CampaignSaveData에 기록하고 저장합니다.
        /// TutorialManager의 Dialog_EnvyOutro5 완료 시 호출하세요.
        /// </summary>
        public static void GrantTutorialReward()
        {
            var mgr = CampaignSaveManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[TutorialSaveHelper] CampaignSaveManager 없음 — 보상 저장 실패");
                return;
            }

            // CurrentSave가 없으면 로드
            var saveData = mgr.CurrentSave ?? mgr.Load(StageId);

            // 튜토리얼 클리어 기록
            saveData.isTutorialCleared = true;

            // 엔비 조각 지급

            if (!saveData.collectedFragmentIds.Contains(TutorialEnvy01))
            {
                saveData.collectedFragmentIds.Add(TutorialEnvy01);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialEnvy01}");
            }
            if (!saveData.collectedFragmentIds.Contains(TutorialEnvy02))
            {
                saveData.collectedFragmentIds.Add(TutorialEnvy02);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialEnvy02}");
            }
            if (!saveData.collectedFragmentIds.Contains(TutorialEnvy03))
            {
                saveData.collectedFragmentIds.Add(TutorialEnvy03);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialEnvy03}");
            }
            if (!saveData.collectedFragmentIds.Contains(TutorialEnvy04))
            {
                saveData.collectedFragmentIds.Add(TutorialEnvy04);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialEnvy04}");
            }
            if (!saveData.collectedFragmentIds.Contains(TutorialEnvy05))
            {
                saveData.collectedFragmentIds.Add(TutorialEnvy05);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialEnvy05}");
            }

            // P02_01 조각 지급 (중복 방지)
            if (!saveData.collectedFragmentIds.Contains(TutorialClueId))
            {
                saveData.collectedFragmentIds.Add(TutorialClueId);
                Debug.Log($"[TutorialSaveHelper] 튜토리얼 조각 지급 — {TutorialClueId}");
            }

            mgr.Save(saveData);
            Debug.Log("[TutorialSaveHelper] 튜토리얼 완료 보상 저장 완료");
        }
    }
}