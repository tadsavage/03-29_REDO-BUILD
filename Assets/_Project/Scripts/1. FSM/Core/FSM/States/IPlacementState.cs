using UnityEngine;
public interface IPlacementState
{
    void OnEnter();
    void OnExit();
    void Tick();
    bool IsPlacementState { get; }
}

