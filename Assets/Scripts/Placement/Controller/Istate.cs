public interface IState
{
    void Enter();   // Called once when the state becomes active
    void Tick();    // Called every frame
    void Exit();    // Called once when leaving the state
}

