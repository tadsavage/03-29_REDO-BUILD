using UnityEngine;
using System.Collections;

public class BuildingDestructionEffect : MonoBehaviour
{
    private Vector3 _originalPos;
    private float _timer;
    
    private float _duration;
    private float _sinkAmount;
    private float _vibrationAmount;
    private float _vibrationSpeed;

    private bool _isComplete;

    // Fired once the sink animation finishes and the object is deactivated. Used to delay
    // revealing whatever is underneath (e.g. the yard tile) until the dying object is actually
    // gone, instead of both being visible/overlapping for the duration of the animation.
    public System.Action OnComplete;

    public void Initialize(float duration, float sinkAmount, float vibrationAmount, float vibrationSpeed)
    {
        _duration = duration;
        _sinkAmount = sinkAmount;
        _vibrationAmount = vibrationAmount;
        _vibrationSpeed = vibrationSpeed;
        
        _originalPos = transform.position;
        _timer = 0f;
        _isComplete = false;

        // Play dust FX at start
        if (FXPool.Instance != null)
        {
            FXPool.Instance.Play("dust", transform.position);
        }
    }

    private void Update()
    {
        if (_isComplete) return;

        // UNSCALED: the delete that triggered this effect already refunded money and removed the
        // object from the grid synchronously in DeleteCommand.Execute(), regardless of game speed.
        // Using scaled Time.deltaTime/Time.time here meant that while the game was Paused (0x, the
        // TopBar speed control's own pause button — see TopBarUI.SetSpeed), the object was already
        // logically deleted but sat there sinking forever, since a paused Time.timeScale never lets
        // the animation actually finish and call SetActive(false) — it looked like the delete had
        // silently failed even though the player had already been charged/refunded correctly.
        _timer += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(_timer / _duration);

        // Sink effect
        Vector3 sinkOffset = Vector3.down * (_sinkAmount * normalizedTime);

        // Vibration effect (Sims 3 style)
        // Using sine waves with different frequencies for X and Z to make it look jittery
        float jitterX = Mathf.Sin(Time.unscaledTime * _vibrationSpeed) * _vibrationAmount;
        float jitterZ = Mathf.Cos(Time.unscaledTime * _vibrationSpeed * 1.1f) * _vibrationAmount;
        
        // Decay vibration as it sinks? Or keep it constant. The request said "vibrate a little".
        // Let's keep it constant but slightly fade it out at the very end.
        float fade = 1.0f - Mathf.Pow(normalizedTime, 4); 
        Vector3 vibrationOffset = new Vector3(jitterX, 0, jitterZ) * fade;

        transform.position = _originalPos + sinkOffset + vibrationOffset;

        if (_timer >= _duration)
        {
            Complete();
        }
    }

    private void Complete()
    {
        _isComplete = true;
        gameObject.SetActive(false);
        // The component will be destroyed when the object is re-enabled/undone or just cleaned up

        var callback = OnComplete;
        OnComplete = null;
        callback?.Invoke();
    }

    public void Abort()
    {
        _isComplete = true;
        transform.position = _originalPos;
        OnComplete = null; // the delete is being undone — whatever it was about to reveal must not fire
        Destroy(this);
    }
}
