/// <summary>
/// 플레이어가 7개 캐릭터의 역할을 추리해 제출하는 단계입니다.
/// 중간 선언(PlayerAction 단계) 또는 5루프 종료 시 진입합니다.
/// 제출이 완료되면 SubmitDeduction()을 통해 GameEnd로 전환합니다.
/// </summary>
public class DeductionPhaseState : IState
{
    private readonly LoopStateMachine _loopSM;

    public DeductionPhaseState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter()
    {
        // TODO: 7개 캐릭터 역할 추리 UI 표시
    }

    public void Tick() { }

    public void Exit()
    {
        // TODO: 추리 UI 닫기
    }

    /// <summary>플레이어가 추리를 제출했을 때 UI에서 호출합니다.</summary>
    public void SubmitDeduction()
    {
        // TODO: 플레이어 추리 결과와 실제 역할 비교 (정답 판정)
        _loopSM.EnterGameEnd(true);
    }
}
