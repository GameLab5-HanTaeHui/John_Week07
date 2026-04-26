using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 미수집 대화 조각 힌트 데이터 ScriptableObject입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   각 캐릭터별 미수집 조각 힌트 텍스트를 저장합니다.
    ///   CharacterRecordBook에서 힌트 목록을 표시할 때 사용합니다.
    ///
    /// ─── 힌트 예시 ───────────────────────────────────────────────────────
    ///   #1: "새턴과의 철학적 충돌"
    ///   #1: "약자를 대신하려는 반응"
    ///   #1: "리더십이 드러나는 상황"
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → FragmentHintData
    /// </summary>
    [CreateAssetMenu(fileName = "FragmentHintDataSO",
                     menuName = "HTH/Campaign/FragmentHintData")]
    public class FragmentHintDataSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("캐릭터별 힌트 목록")]
        [SerializeField] private List<CharacterHintData> _characterHints = new();

        public string StageId => _stageId;

        /// <summary>캐릭터 ID로 힌트 목록을 반환합니다.</summary>
        public List<string> GetHints(int characterId)
        {
            foreach (var data in _characterHints)
                if (data != null && data.CharacterId == characterId)
                    return data.Hints ?? new List<string>();
            return new List<string>();
        }
    }

    /// <summary>캐릭터 1명의 힌트 데이터입니다.</summary>
    [System.Serializable]
    public class CharacterHintData
    {
        [Tooltip("캐릭터 ID (#1~#7)")]
        public int CharacterId;

        [Tooltip("미수집 조각 힌트 목록\n예: 새턴과의 철학적 충돌")]
        public List<string> Hints = new();
    }
}