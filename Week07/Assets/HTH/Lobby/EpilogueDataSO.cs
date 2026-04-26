using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터별 시점 완결문 데이터 ScriptableObject입니다.
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → EpilogueData
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Epilogues 배열에 CharacterId와 내용을 직접 입력합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EpilogueDataSO",
                     menuName = "HTH/Campaign/EpilogueData")]
    public class EpilogueDataSO : ScriptableObject
    {
        [SerializeField] private List<EpilogueEntry> _epilogues = new();

        /// <summary>
        /// CharacterId에 해당하는 시점 완결문을 반환합니다.
        /// 없으면 빈 문자열을 반환합니다.
        /// </summary>
        public string GetEpilogue(int characterId)
        {
            foreach (var entry in _epilogues)
                if (entry.characterId == characterId)
                    return entry.content;
            return string.Empty;
        }
    }

    [Serializable]
    public class EpilogueEntry
    {
        [Tooltip("캐릭터 ID입니다. (1~7)")]
        public int characterId;

        [Tooltip("시점 완결문 내용입니다.")]
        [TextArea(5, 20)]
        public string content;
    }
}