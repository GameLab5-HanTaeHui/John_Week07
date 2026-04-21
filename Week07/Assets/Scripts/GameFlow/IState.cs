/// <summary>
/// 모든 상태(State)가 구현해야 하는 인터페이스입니다.
/// Enter : 상태 진입 시 1회 호출
/// Tick  : 매 프레임(또는 스텝) 호출
/// Exit  : 상태 탈출 시 1회 호출
/// </summary>
public interface IState
{
    void Enter();
    void Tick();
    void Exit();
}
