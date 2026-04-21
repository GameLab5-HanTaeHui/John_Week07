using UnityEngine;

/// <summary>
/// 루프 종료 조건: 살인자 사망 OR 루프 누적 사망자가 지정 수 이상일 때 루프를 리셋합니다.
/// </summary>
[CreateAssetMenu(fileName = "Condition_MurdererDeath", menuName = "MafiaGame/LoopCondition/MurdererDeath")]
public class MurdererDeathCondition : LoopConditionConfig
{
    [Tooltip("루프 누적 사망자가 이 수 이상이면 루프를 강제 종료합니다.")]
    [SerializeField] private int _massDeathThreshold = 3;

    public override bool ShouldLoop(GameState gameState)
    {
        bool murdererDead = gameState.GetCharacterByRole(RoleType.Murderer) == null;
        if (murdererDead)
        {
            Debug.Log("[LoopCondition] 루프 종료 — 살인자 사망");
            return true;
        }

        if (gameState.TotalDeathsThisLoop >= _massDeathThreshold)
        {
            Debug.Log($"[LoopCondition] 루프 종료 — 루프 누적 {gameState.TotalDeathsThisLoop}명 사망 (기준: {_massDeathThreshold})");
            return true;
        }

        return false;
    }
}
