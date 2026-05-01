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
        [Tooltip("대화 시 출력될 캐릭터 이미지 (없으면 비워두세요)")]
        public Sprite SpeakerSprite; // 캐릭터 이미지를 위한 변수 추가
        [TextArea(2, 6)] public string GuideText;
    }

    [Serializable]
    public struct EventGuideEntry
    {
        public TutorialEventType EventType;
        [Tooltip("이벤트 안내 시 출력될 캐릭터 이미지 (없으면 비워두세요)")]
        public Sprite SpeakerSprite; // 이벤트에도 동일하게 추가
        [TextArea(2, 6)] public string GuideText;
    }

    [Header("순서형 단계 안내 텍스트 (Phase 순서대로 채워주세요)")]
    public PhaseGuideEntry[] PhaseGuides;

    [Header("이벤트형 안내 텍스트 (최초 발생 시 1회 표시)")]
    public EventGuideEntry[] EventGuides;

    /// <summary>
    /// Phase에 해당하는 텍스트와 스프라이트를 반환합니다.
    /// </summary>
    public bool TryGetPhaseGuide(TutorialPhase phase, out string text, out Sprite sprite)
    {
        text = string.Empty;
        sprite = null;

        if (PhaseGuides == null) return false;

        foreach (var entry in PhaseGuides)
        {
            if (entry.Phase == phase)
            {
                text = entry.GuideText;
                sprite = entry.SpeakerSprite;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// EventType에 해당하는 텍스트와 스프라이트를 반환합니다.
    /// </summary>
    public bool TryGetEventGuide(TutorialEventType eventType, out string text, out Sprite sprite)
    {
        text = string.Empty;
        sprite = null;

        if (EventGuides == null) return false;

        foreach (var entry in EventGuides)
        {
            if (entry.EventType == eventType)
            {
                text = entry.GuideText;
                sprite = entry.SpeakerSprite;
                return true;
            }
        }
        return false;
    }
}
