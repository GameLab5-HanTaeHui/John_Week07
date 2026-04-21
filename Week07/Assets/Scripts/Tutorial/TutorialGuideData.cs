using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 안내 텍스트를 모아두는 ScriptableObject입니다.
/// 텍스트를 직접 채워넣으세요. 시스템 코드는 건드릴 필요 없습니다.
///
/// 생성: [우클릭] Create → Tutorial → Guide Data
/// </summary>
[CreateAssetMenu(fileName = "TutorialGuideData", menuName = "Tutorial/Guide Data")]
public class TutorialGuideData : ScriptableObject
{
    [Serializable]
    public struct PhaseGuideEntry
    {
        public TutorialPhase Phase;
        [TextArea(2, 6)] public string GuideText;
    }

    [Serializable]
    public struct EventGuideEntry
    {
        public TutorialEventType EventType;
        [TextArea(2, 6)] public string GuideText;
    }

    [Header("순서형 단계 안내 텍스트 (Phase 순서대로 채워주세요)")]
    public PhaseGuideEntry[] PhaseGuides;

    [Header("이벤트형 안내 텍스트 (최초 발생 시 1회 표시)")]
    public EventGuideEntry[] EventGuides;

    public string GetPhaseText(TutorialPhase phase)
    {
        if (PhaseGuides == null) return string.Empty;
        foreach (var entry in PhaseGuides)
            if (entry.Phase == phase) return entry.GuideText;
        return string.Empty;
    }

    public string GetEventText(TutorialEventType eventType)
    {
        if (EventGuides == null) return string.Empty;
        foreach (var entry in EventGuides)
            if (entry.EventType == eventType) return entry.GuideText;
        return string.Empty;
    }
}
