using UnityEngine;

public class DestroyDebugger : MonoBehaviour
{
    private void OnDestroy()
    {
        Debug.LogWarning($"DestroyDebugger.OnDestroy() - {gameObject.name} is being destroyed!");
    }
}
