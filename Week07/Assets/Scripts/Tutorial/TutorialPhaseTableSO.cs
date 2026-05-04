using System;
using System.Collections.Generic;
using UnityEngine;

// ── 열거형 ────────────────────────────────────────────────────────────────

/// <summary>
/// Phase 종류입니다.
/// TutorialManager가 이 타입을 보고 처리 방식을 결정합니다.
/// </summary>
public enum TutorialPhaseType
{
    /// <summary>대화창 표시 → 클릭 시 NextPhase로 자동 진행</summary>
    Dialogue,
    /// <summary>가이드 표시 → 플레이어 조작 대기 (ActionPermission 부여)</summary>
    Action,
    /// <summary>코드에서 자동 처리 (WaitIntro, Tutorial_End 등)</summary>
    System
}

/// <summary>
/// 대화창 화자 식별자입니다.
/// SpeakerSprites 배열 인덱스와 일치합니다.
///
///   [0] None  — 나레이션 (이미지·이름 없음)
///   [1] Envy  — 엔비 이미지 + 이름
///   [2] May   — 메이 이미지 + 이름
///   추가 캐릭터는 이 순서 그대로 확장하세요.
/// </summary>
public enum TutorialSpeaker
{
    None = 0,
    Envy = 1,
    May = 2,
}

// ── Phase 설정 1개 ─────────────────────────────────────────────────────────

[Serializable]
public class TutorialDialogueLine
{
    [Tooltip("이 대화의 화자")]
    public TutorialSpeaker Speaker;

    [Tooltip("출력할 대사")]
    [TextArea(2, 4)]
    public string Text;
}

[Serializable]
public class TutorialPhaseConfig
{
    public TutorialPhase Phase;
    public TutorialPhaseType PhaseType;

    // ── 수정된 대화 묶음 ──────────────────────────────────────────────
    [Header("대화 묶음 (순서대로 출력됨)")]
    [Tooltip("대화가 여러 줄이면 여기에 추가하세요. 대화가 모두 끝난 뒤에 Action이 시작되거나 NextPhase로 넘어갑니다.")]
    public List<TutorialDialogueLine> Dialogues = new List<TutorialDialogueLine>();

    // ── 기존 Action 및 부가 설정 유지 ──────────────────────────────────
    [Header("다음 Phase 연결")]
    public TutorialPhase NextPhase = TutorialPhase.Inactive;

    [Header("Action Phase 전용")]
    public TutorialInputPermission ActionPermission;
    public TutorialPhase NextPhaseOnAction = TutorialPhase.Inactive;

    public bool ShowArrow;
    public Vector2 ArrowPosition;
    public float ArrowRotationZ;
    public float PreDelay;
    public bool EnableRoleDocGroup;
    public bool EnableMemoBookGroup;
}

// ── SO 본체 ────────────────────────────────────────────────────────────────

/// <summary>
/// 튜토리얼 Phase 전체 설정 ScriptableObject입니다.
///
/// ─── Inspector 작성 가이드 ────────────────────────────────────────────────
///   1. SpeakerSprites 배열에 [0]=빈칸, [1]=엔비 이미지, [2]=메이 이미지 순서로 연결
///   2. SpeakerNames 배열에  [0]="", [1]="엔비", [2]="메이" 순서로 입력
///   3. PhaseConfigs 배열에 TutorialPhase enum 순서대로 항목 추가
///
/// ─── PhaseType 작성 규칙 ─────────────────────────────────────────────────
///   Dialogue : 대화창 표시 → 클릭 → NextPhase 자동 진입
///              (DialogueText 또는 GuideData 텍스트)
///   Action   : 가이드 표시 후 플레이어 조작 대기
///              ActionPermission 부여 → Notify* 완료 시 NextPhaseOnAction 진입
///   System   : WaitIntro, Tutorial_End 등 코드 처리 전용
///
/// ─── 생성 ────────────────────────────────────────────────────────────────
///   [우클릭] Create → Tutorial → Phase Table
/// </summary>
[CreateAssetMenu(fileName = "TutorialPhaseTable", menuName = "Tutorial/Phase Table")]
public class TutorialPhaseTableSO : ScriptableObject
{
    [Header("화자 이미지 (TutorialSpeaker 인덱스 순서)")]
    [Tooltip("[0]=None(빈칸), [1]=엔비, [2]=메이 순서로 Sprite를 연결하세요.")]
    public Sprite[] SpeakerSprites;

    [Header("화자 이름 (TutorialSpeaker 인덱스 순서)")]
    [Tooltip("[0]=\"\", [1]=\"엔비\", [2]=\"메이\" 순서로 입력하세요.")]
    public string[] SpeakerNames;

    [Header("Phase 설정 목록 (Phase 순서대로 입력)")]
    public List<TutorialPhaseConfig> PhaseConfigs = new();

    // ── 캐시 ─────────────────────────────────────────────────────────────

    private Dictionary<TutorialPhase, int> _indexCache;

    // ── 조회 API ─────────────────────────────────────────────────────────

    /// <summary>Phase 설정을 반환합니다. 없으면 null.</summary>
    public TutorialPhaseConfig Get(TutorialPhase phase)
    {
        BuildCacheIfNeeded();
        return _indexCache.TryGetValue(phase, out int idx) ? PhaseConfigs[idx] : null;
    }

    /// <summary>
    /// 현재 Phase의 다음 Phase를 반환합니다.
    /// PhaseConfigs 배열 순서 기준으로 다음 항목을 찾습니다.
    /// </summary>
    public TutorialPhase GetNext(TutorialPhase current)
    {
        BuildCacheIfNeeded();
        if (!_indexCache.TryGetValue(current, out int idx)) return TutorialPhase.Inactive;
        int nextIdx = idx + 1;
        return nextIdx < PhaseConfigs.Count ? PhaseConfigs[nextIdx].Phase : TutorialPhase.Inactive;
    }

    /// <summary>화자 Sprite를 반환합니다. None이거나 범위 밖이면 null.</summary>
    public Sprite GetSprite(TutorialSpeaker speaker)
    {
        int idx = (int)speaker;
        if (SpeakerSprites == null || idx < 0 || idx >= SpeakerSprites.Length) return null;
        return SpeakerSprites[idx];
    }

    /// <summary>화자 이름을 반환합니다.</summary>
    public string GetName(TutorialSpeaker speaker)
    {
        int idx = (int)speaker;
        if (SpeakerNames == null || idx < 0 || idx >= SpeakerNames.Length) return "";
        return SpeakerNames[idx] ?? "";
    }

    // ── Private ──────────────────────────────────────────────────────────

    private void BuildCacheIfNeeded()
    {
        if (_indexCache != null) return;
        _indexCache = new Dictionary<TutorialPhase, int>();
        for (int i = 0; i < PhaseConfigs.Count; i++)
            if (PhaseConfigs[i] != null)
                _indexCache[PhaseConfigs[i].Phase] = i;
    }

    private void OnValidate() => _indexCache = null;
}