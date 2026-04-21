/// <summary>
/// 루프를 모두 소진한 뒤 최종 결정 진입을 대기하는 단계입니다.
///
/// 이 상태에서는:
///   - 캐릭터 이동 불가 (GameFlowController에서 LoopState 체크로 입력 차단)
///   - 턴 진행 불가 (HoldToAdvanceTurn이 GameFlowController.ForceEndTurn 체크로 차단)
///   - 씬에 배치된 HoldToEnterFinalDecision 오브젝트를 홀드해야만 FinalDecision으로 진입 가능
///
/// TODO: 대기 연출(UI 안내 메시지 등) 추가 예정
/// </summary>
public class AwaitingFinalDecisionState : IState
{
    private readonly LoopStateMachine _loopSM;

    public AwaitingFinalDecisionState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter()
    {
        // TODO: "최종 결정 버튼을 눌러주세요" 안내 UI 표시
    }

    public void Tick() { }

    public void Exit()
    {
        // TODO: 안내 UI 닫기
    }
}
