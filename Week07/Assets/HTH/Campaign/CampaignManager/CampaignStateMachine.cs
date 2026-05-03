namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 상태머신 베이스 클래스입니다.
    /// CampaignLoopStateMachine의 부모 클래스로 사용합니다.
    /// 기본모드는 StateMachine을 사용하세요.
    /// </summary>
    public abstract class CampaignStateMachine
    {
        private IState _current;

        /// <summary>현재 상태를 종료하고 다음 상태로 전환합니다.</summary>
        protected void ChangeState(IState next)
        {
            _current?.Exit();
            _current = next;
            _current?.Enter();
        }

        /// <summary>현재 상태의 Tick을 실행합니다. CampaignGameFlowController.Update()에서 호출합니다.</summary>
        public void Tick()
        {
            _current?.Tick();
        }
    }
}