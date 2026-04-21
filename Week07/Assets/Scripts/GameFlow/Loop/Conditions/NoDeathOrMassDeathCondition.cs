using UnityEngine;

/// <summary>
/// 루프 종료 조건: 이번 턴 사망자 없음 OR 이번 턴 지정 수 이상 사망 시 루프 리셋. (Stage 2 기믹)
/// </summary>
[CreateAssetMenu(fileName = "Condition_NoDeathOrMassDeath", menuName = "MafiaGame/LoopCondition/NoDeathOrMassDeath")]
public class NoDeathOrMassDeathCondition : LoopConditionConfig
{
    [Tooltip("이번 턴에 이 수 이상 사망 시 루프를 강제 종료합니다.")]
    [SerializeField] private int _massDeathThreshold = 3;

    public override bool ShouldLoop(GameState gameState)
    {
        int deaths = gameState.DeathsThisTurn;

        if (deaths == 0)
        {
            Debug.Log("[LoopCondition] 루프 종료 — 이번 턴 사망자 없음");
            return true;
        }

        if (deaths >= _massDeathThreshold)
        {
            Debug.Log($"[LoopCondition] 루프 종료 — 이번 턴 {deaths}명 이상 사망 (기준: {_massDeathThreshold})");
            return true;
        }

        return false;
    }
}
