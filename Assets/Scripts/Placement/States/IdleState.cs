public class IdleState : IState
{
    public void Enter()
    {
        // Show nothing, wait for mode selection
    }

    public void Tick()
    {
        // Idle logic (rarely needed)
    }

    public void Exit()
    {
        // Cleanup if needed
    }
}
