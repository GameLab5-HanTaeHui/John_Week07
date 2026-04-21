using System;

/// <summary>튜토리얼 진행 상태를 디스크에 저장하는 데이터 구조입니다.</summary>
[Serializable]
public class TutorialSaveData
{
    /// <summary>튜토리얼을 한 번이라도 시작했으면 true.</summary>
    public bool IsStarted;
    /// <summary>튜토리얼을 클리어(게임 승리)했으면 true.</summary>
    public bool IsCleared;
}
